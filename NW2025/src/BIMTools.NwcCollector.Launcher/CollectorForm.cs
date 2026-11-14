using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BIMTools.NwcCollector.Core.Contracts;
using BIMTools.NwcCollector.Core.Localization;
using BIMTools.NwcCollector.Core.Planning;
using BIMTools.NwcCollector.Core.Protocol;
using BIMTools.NwcCollector.Core.Scanning;

namespace BIMTools.NwcCollector.Launcher
{
    /// <summary>Main window: profile, scenario, filters, order/output, preview, run.</summary>
    internal sealed class CollectorForm : Form
    {
        private sealed class EnumItem
        {
            public string Value;
            public string Key;
            public override string ToString() { return Loc.T(Key); }
        }

        private sealed class LangItem
        {
            public string Code;
            public string Name;
            public override string ToString() { return Name; }
        }

        private sealed class PresetItem
        {
            public PresetEntry Entry;
            public override string ToString() { return Entry.Origin + Entry.Name; }
        }

        private readonly List<KeyValuePair<Control, string>> _tr = new List<KeyValuePair<Control, string>>();
        private readonly Dictionary<string, string> _settings;
        private readonly string[] _columnKeys = { "col.name", "col.folders", "col.files", "col.local", "col.notLocal", "col.status" };

        // header
        private ComboBox _preset, _lang;
        private Button _btnLoad, _btnSave;
        private Label _presetDesc;
        private TabControl _tabs;

        // source tab
        private TextBox _root, _output, _names, _pattern, _delims, _fileRegex, _singleName, _unmatched, _rootGroup;
        private ComboBox _mode;
        private Label _modeHelp;
        private NumericUpDown _depth, _tokenIndex, _maxDepth;
        private CheckBox _rootFiles, _rootOnly;
        private Button _browseRoot, _browseOutput;
        private TableLayoutPanel _pFolder, _pDepth, _pToken, _pRegex, _pSingle, _pUnmatched;
        private FlowLayoutPanel _rootRow;

        // filters tab
        private TextBox _ext, _inc, _exc, _excFolders, _excFolderRx, _revRegex;
        private NumericUpDown _minSize;
        private DateTimePicker _modAfter;
        private CheckBox _latest;

        // order and output tab
        private ComboBox _fileOrder, _groupOrder, _outLocation, _format, _policy;
        private TextBox _priority, _template;
        private NumericUpDown _maxFiles;
        private CheckBox _upToDate, _csv, _openFirst, _openFolder;

        // run area
        private Button _scan, _start, _cancel;
        private ListView _groups;
        private ProgressBar _bar;
        private Label _status;
        private TextBox _log;

        private PlanResult _plan;
        private CancellationTokenSource _scanCts;
        private WorkerClient _worker;
        private bool _cancelRequestedOnce;
        private bool _loadingProfile;
        private string _profileName = "Custom";
        private string _lastOutputDir;
        private readonly Dictionary<string, ListViewItem> _itemsByGroup = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _outputToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public CollectorForm()
        {
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1040, 900);
            MinimumSize = new Size(820, 640);

            _settings = ProfileStore.LoadSettings();
            string setting;
            Loc.Load(Loc.Resolve(_settings.TryGetValue("language", out setting) ? setting : "auto", ProfileStore.LanguagesDir), ProfileStore.LanguagesDir);

            BuildUi();
            LoadPresets();
            LoadLanguages();
            ApplyLanguage();                      // fills the scenario / order / output lists first
            ApplyProfile(new CollectorProfile()); // then the defaults can select their values
            RestoreSettings();
            WireEvents();
        }

        // ================================================================== UI construction

        private T Tr<T>(T control, string key) where T : Control
        {
            _tr.Add(new KeyValuePair<Control, string>(control, key));
            return control;
        }

        private TableLayoutPanel NewTable()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 0, Padding = new Padding(6) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return t;
        }

        private void AddRow(TableLayoutPanel t, string labelKey, Control field, Control extra)
        {
            int row = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = Tr(new Label { AutoSize = true, MaximumSize = new Size(282, 0), Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 6, 0) }, labelKey);
            t.Controls.Add(label, 0, row);
            if (field is TextBox || field is ComboBox) field.Dock = DockStyle.Fill;
            else field.Anchor = AnchorStyles.Left;
            t.Controls.Add(field, 1, row);
            if (extra != null) t.Controls.Add(extra, 2, row);
        }

        private void AddWide(TableLayoutPanel t, Control control)
        {
            int row = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Anchor = AnchorStyles.Left;
            t.Controls.Add(control, 0, row);
            t.SetColumnSpan(control, 3);
        }

        private static NumericUpDown Num(decimal min, decimal max, decimal value)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 90 };
        }

        private static ComboBox Combo()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 420 };
        }

        private void BuildUi()
        {
            // ---- header: profile + language
            _preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, DropDownWidth = 520 };
            _lang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            _btnLoad = Tr(new Button { AutoSize = true }, "btn.loadFile");
            _btnSave = Tr(new Button { AutoSize = true }, "btn.saveAs");
            _presetDesc = new Label { AutoSize = true, MaximumSize = new Size(960, 0), ForeColor = SystemColors.GrayText, Padding = new Padding(8, 0, 8, 4) };

            var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6, 6, 6, 0), WrapContents = true };
            header.Controls.Add(Tr(new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, "lbl.preset"));
            header.Controls.Add(_preset);
            header.Controls.Add(_btnLoad);
            header.Controls.Add(_btnSave);
            header.Controls.Add(Tr(new Label { AutoSize = true, Padding = new Padding(24, 6, 0, 0) }, "lbl.language"));
            header.Controls.Add(_lang);

            // ---- source tab
            _root = new TextBox();
            _output = new TextBox();
            _browseRoot = Tr(new Button { AutoSize = true }, "btn.browse");
            _browseOutput = Tr(new Button { AutoSize = true }, "btn.browse");
            _mode = Combo();
            _modeHelp = new Label { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = SystemColors.GrayText, Padding = new Padding(0, 0, 0, 6) };

            var source = NewTable();
            AddRow(source, "lbl.root", _root, _browseRoot);
            AddRow(source, "lbl.output", _output, _browseOutput);
            AddRow(source, "lbl.mode", _mode, null);
            AddWide(source, _modeHelp);

            _names = new TextBox();
            _pattern = new TextBox();
            _pFolder = NewTable();
            AddRow(_pFolder, "lbl.folderNames", _names, null);
            AddRow(_pFolder, "lbl.folderPattern", _pattern, null);
            AddWide(_pFolder, Tr(new Label { AutoSize = true, ForeColor = SystemColors.GrayText }, "hint.folderNames"));

            _depth = Num(1, 30, 1);
            _pDepth = NewTable();
            AddRow(_pDepth, "lbl.depth", _depth, null);

            _delims = new TextBox();
            _tokenIndex = Num(-30, 30, 1);
            _pToken = NewTable();
            AddRow(_pToken, "lbl.delims", _delims, null);
            AddRow(_pToken, "lbl.tokenIndex", _tokenIndex, null);

            _fileRegex = new TextBox();
            _pRegex = NewTable();
            AddRow(_pRegex, "lbl.fileRegex", _fileRegex, null);
            AddWide(_pRegex, Tr(new Label { AutoSize = true, ForeColor = SystemColors.GrayText }, "hint.fileRegex"));

            _singleName = new TextBox();
            _pSingle = NewTable();
            AddRow(_pSingle, "lbl.singleName", _singleName, null);

            _unmatched = new TextBox();
            _pUnmatched = NewTable();
            AddRow(_pUnmatched, "lbl.unmatched", _unmatched, null);

            foreach (var p in new[] { _pFolder, _pDepth, _pToken, _pRegex, _pSingle, _pUnmatched })
            {
                p.Dock = DockStyle.None;
                p.Visible = false;
                AddWide(source, p);
                p.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }

            _rootFiles = Tr(new CheckBox { AutoSize = true }, "chk.rootFiles");
            _rootGroup = new TextBox { Width = 140 };
            _rootRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            _rootRow.Controls.Add(_rootFiles);
            _rootRow.Controls.Add(Tr(new Label { AutoSize = true, Padding = new Padding(8, 5, 4, 0) }, "lbl.rootGroup"));
            _rootRow.Controls.Add(_rootGroup);
            AddWide(source, _rootRow);

            _rootOnly = Tr(new CheckBox { AutoSize = true }, "chk.rootOnly");
            _maxDepth = Num(0, 64, 0);
            var depthRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            depthRow.Controls.Add(_rootOnly);
            depthRow.Controls.Add(Tr(new Label { AutoSize = true, Padding = new Padding(16, 5, 4, 0) }, "lbl.maxDepth"));
            depthRow.Controls.Add(_maxDepth);
            AddWide(source, depthRow);

            // ---- filters tab
            _ext = new TextBox();
            _inc = new TextBox();
            _exc = new TextBox();
            _excFolders = new TextBox();
            _excFolderRx = new TextBox();
            _minSize = Num(0, 100000000, 0);
            _modAfter = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 150 };
            _latest = Tr(new CheckBox { AutoSize = true }, "chk.latest");
            _revRegex = new TextBox();

            var filters = NewTable();
            AddRow(filters, "lbl.ext", _ext, null);
            AddRow(filters, "lbl.inc", _inc, null);
            AddRow(filters, "lbl.exc", _exc, null);
            AddRow(filters, "lbl.excFolders", _excFolders, null);
            AddRow(filters, "lbl.excFolderRx", _excFolderRx, null);
            AddRow(filters, "lbl.minSize", _minSize, null);
            AddRow(filters, "lbl.modAfter", _modAfter, null);
            AddWide(filters, _latest);
            AddRow(filters, "lbl.revRegex", _revRegex, null);

            // ---- order and output tab
            _fileOrder = Combo();
            _priority = new TextBox();
            _groupOrder = Combo();
            _outLocation = Combo();
            _template = new TextBox();
            _format = Combo();
            _policy = Combo();
            _maxFiles = Num(0, 100000, 0);
            _upToDate = Tr(new CheckBox { AutoSize = true }, "chk.upToDate");
            _csv = Tr(new CheckBox { AutoSize = true }, "chk.csv");
            _openFirst = Tr(new CheckBox { AutoSize = true }, "chk.openFirst");
            _openFolder = Tr(new CheckBox { AutoSize = true, Checked = true }, "chk.openFolder");

            var output = NewTable();
            AddRow(output, "lbl.fileOrder", _fileOrder, null);
            AddRow(output, "lbl.priority", _priority, null);
            AddRow(output, "lbl.groupOrder", _groupOrder, null);
            AddRow(output, "lbl.outLocation", _outLocation, null);
            AddRow(output, "lbl.template", _template, null);
            AddWide(output, Tr(new Label { AutoSize = true, ForeColor = SystemColors.GrayText }, "hint.template"));
            AddRow(output, "lbl.format", _format, null);
            AddRow(output, "lbl.policy", _policy, null);
            AddRow(output, "lbl.maxFiles", _maxFiles, null);
            AddWide(output, _upToDate);
            AddWide(output, _csv);
            AddWide(output, _openFirst);
            AddWide(output, _openFolder);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            foreach (var pair in new[] { new KeyValuePair<string, TableLayoutPanel>("tab.source", source), new KeyValuePair<string, TableLayoutPanel>("tab.filters", filters), new KeyValuePair<string, TableLayoutPanel>("tab.output", output) })
            {
                var page = Tr(new TabPage { AutoScroll = true }, pair.Key);
                pair.Value.Dock = DockStyle.Top;
                page.Controls.Add(pair.Value);
                _tabs.TabPages.Add(page);
            }

            // ---- run area
            _scan = Tr(new Button { AutoSize = true }, "btn.scan");
            _start = Tr(new Button { AutoSize = true, Enabled = false }, "btn.start");
            _cancel = Tr(new Button { AutoSize = true, Enabled = false }, "btn.cancel");
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6, 2, 6, 2) };
            buttons.Controls.Add(_scan);
            buttons.Controls.Add(_start);
            buttons.Controls.Add(_cancel);

            _groups = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, GridLines = true };
            int[] widths = { 200, 70, 70, 70, 80, 380 };
            for (int i = 0; i < _columnKeys.Length; i++) _groups.Columns.Add(_columnKeys[i], widths[i]);

            _bar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1, Height = 18 };
            _status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
            var progress = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2, Padding = new Padding(8, 4, 8, 4) };
            progress.Controls.Add(_bar, 0, 0);
            progress.Controls.Add(_status, 0, 1);

            _log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f) };

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // header
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // profile description
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 330f));      // tabs
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // buttons
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));        // groups
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // progress
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));        // log
            main.Controls.Add(header, 0, 0);
            main.Controls.Add(_presetDesc, 0, 1);
            main.Controls.Add(_tabs, 0, 2);
            main.Controls.Add(buttons, 0, 3);
            main.Controls.Add(_groups, 0, 4);
            main.Controls.Add(progress, 0, 5);
            main.Controls.Add(_log, 0, 6);
            Controls.Add(main);
        }

        private void WireEvents()
        {
            _browseRoot.Click += (s, e) => Browse(_root);
            _browseOutput.Click += (s, e) => Browse(_output);
            _scan.Click += OnScan;
            _start.Click += OnStart;
            _cancel.Click += OnCancel;
            _mode.SelectedIndexChanged += (s, e) => UpdateModeVisibility();
            _preset.SelectedIndexChanged += OnPresetChanged;
            _btnLoad.Click += OnLoadProfileFile;
            _btnSave.Click += OnSaveProfile;
            _lang.SelectedIndexChanged += OnLanguageChanged;
            _groups.DoubleClick += OnGroupDoubleClick;
            FormClosing += OnFormClosing;
        }

        private void Browse(TextBox target)
        {
            using (var dlg = new FolderBrowserDialog { ShowNewFolderButton = true })
            {
                if (!string.IsNullOrWhiteSpace(target.Text) && Directory.Exists(target.Text)) dlg.SelectedPath = target.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) target.Text = dlg.SelectedPath;
            }
        }

        // ================================================================== language

        private void LoadLanguages()
        {
            _lang.Items.Clear();
            _lang.Items.Add(new LangItem { Code = "auto", Name = Loc.T("lang.auto") });
            _lang.Items.Add(new LangItem { Code = "en", Name = "English" });
            foreach (var kv in Loc.Discover(ProfileStore.LanguagesDir))
                _lang.Items.Add(new LangItem { Code = kv.Key, Name = kv.Value });

            string saved;
            if (!_settings.TryGetValue("language", out saved)) saved = "auto";
            int index = 0;
            for (int i = 0; i < _lang.Items.Count; i++)
                if (((LangItem)_lang.Items[i]).Code.Equals(saved, StringComparison.OrdinalIgnoreCase)) index = i;
            _lang.SelectedIndex = index;
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            var item = _lang.SelectedItem as LangItem;
            if (item == null || _loadingProfile) return;
            _settings["language"] = item.Code;
            Loc.Load(Loc.Resolve(item.Code, ProfileStore.LanguagesDir), ProfileStore.LanguagesDir);
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            Text = Loc.T("app.title");
            foreach (var kv in _tr) kv.Key.Text = Loc.T(kv.Value);
            for (int i = 0; i < _columnKeys.Length; i++) _groups.Columns[i].Text = Loc.T(_columnKeys[i]);

            if (_lang.Items.Count > 0) ((LangItem)_lang.Items[0]).Name = Loc.T("lang.auto");

            string mode = EnumValue(_mode), fo = EnumValue(_fileOrder), go = EnumValue(_groupOrder), ol = EnumValue(_outLocation), fm = EnumValue(_format), po = EnumValue(_policy);
            FillEnum(_mode, "mode.", Enum.GetNames(typeof(GroupingMode)), mode ?? GroupingMode.FolderName.ToString());
            FillEnum(_fileOrder, "order.", Enum.GetNames(typeof(FileOrder)), fo ?? FileOrder.PathAscending.ToString());
            FillEnum(_groupOrder, "gorder.", Enum.GetNames(typeof(GroupOrder)), go ?? GroupOrder.NameAscending.ToString());
            FillEnum(_outLocation, "loc.", Enum.GetNames(typeof(OutputLocation)), ol ?? OutputLocation.SingleFolder.ToString());
            FillEnum(_format, "fmt.", Enum.GetNames(typeof(OutputFormat)), fm ?? OutputFormat.Nwd.ToString());
            FillEnum(_policy, "policy.", Enum.GetNames(typeof(ExistingFilePolicy)), po ?? ExistingFilePolicy.Ask.ToString());

            bool rtl = Loc.IsRtl;
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = rtl;
            _tabs.RightToLeftLayout = rtl;

            UpdateModeVisibility();
            if (_plan == null && _worker == null) _status.Text = Loc.T("msg.ready");
        }

        private static void FillEnum(ComboBox box, string prefix, string[] values, string selected)
        {
            box.BeginUpdate();
            box.Items.Clear();
            int index = 0;
            for (int i = 0; i < values.Length; i++)
            {
                box.Items.Add(new EnumItem { Value = values[i], Key = prefix + values[i] });
                if (values[i].Equals(selected, StringComparison.OrdinalIgnoreCase)) index = i;
            }
            box.SelectedIndex = index;
            box.EndUpdate();
        }

        private static string EnumValue(ComboBox box)
        {
            var item = box.SelectedItem as EnumItem;
            return item == null ? null : item.Value;
        }

        private static void SelectEnum(ComboBox box, string value)
        {
            for (int i = 0; i < box.Items.Count; i++)
                if (((EnumItem)box.Items[i]).Value.Equals(value, StringComparison.OrdinalIgnoreCase)) { box.SelectedIndex = i; return; }
            if (box.Items.Count > 0) box.SelectedIndex = 0;
        }

        private void UpdateModeVisibility()
        {
            var mode = EnumUtil.Parse(EnumValue(_mode), GroupingMode.FolderName);
            _pFolder.Visible = mode == GroupingMode.FolderName;
            _pDepth.Visible = mode == GroupingMode.FolderAtDepth;
            _pToken.Visible = mode == GroupingMode.FileNameToken;
            _pRegex.Visible = mode == GroupingMode.FileNameRegex;
            _pSingle.Visible = mode == GroupingMode.SingleOutput;
            _pUnmatched.Visible = mode == GroupingMode.FileNameToken || mode == GroupingMode.FileNameRegex;
            _rootRow.Visible = mode == GroupingMode.FolderName || mode == GroupingMode.EachFolder || mode == GroupingMode.FolderAtDepth;
            _modeHelp.Text = Loc.T("mode.desc." + mode);
        }

        // ================================================================== profile <-> controls

        private static List<string> ParseList(string text)
        {
            return (text ?? "").Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        private CollectorProfile ReadProfile()
        {
            var p = new CollectorProfile();
            p.Name = _profileName;
            p.Description = _presetDesc.Text;
            p.Mode = EnumValue(_mode) ?? GroupingMode.FolderName.ToString();
            p.FolderNames = ParseList(_names.Text);
            p.FolderNamePattern = _pattern.Text.Trim();
            p.Depth = (int)_depth.Value;
            p.TokenDelimiters = _delims.Text;
            p.TokenIndex = (int)_tokenIndex.Value;
            p.FileNameRegex = _fileRegex.Text.Trim();
            p.UnmatchedGroupName = _unmatched.Text.Trim();
            p.SingleOutputName = _singleName.Text.Trim();
            p.IncludeRootFiles = _rootFiles.Checked;
            p.RootFilesGroupName = _rootGroup.Text.Trim();
            p.RootFolderOnly = _rootOnly.Checked;
            p.MaxScanDepth = (int)_maxDepth.Value;

            p.Extensions = ParseList(_ext.Text);
            p.IncludeFileRegex = _inc.Text.Trim();
            p.ExcludeFileRegex = _exc.Text.Trim();
            p.ExcludeFolderNames = ParseList(_excFolders.Text);
            p.ExcludeFolderRegex = _excFolderRx.Text.Trim();
            p.MinSizeKb = (long)_minSize.Value;
            p.ModifiedAfter = _modAfter.Checked ? _modAfter.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
            p.LatestRevisionOnly = _latest.Checked;
            p.RevisionRegex = _revRegex.Text.Trim();

            p.FileOrder = EnumValue(_fileOrder) ?? FileOrder.PathAscending.ToString();
            p.PriorityWords = ParseList(_priority.Text);
            p.GroupOrder = EnumValue(_groupOrder) ?? GroupOrder.NameAscending.ToString();

            p.OutputLocation = EnumValue(_outLocation) ?? OutputLocation.SingleFolder.ToString();
            p.OutputNameTemplate = _template.Text.Trim();
            p.OutputFormat = EnumValue(_format) ?? OutputFormat.Nwd.ToString();
            p.ExistingFilePolicy = EnumValue(_policy) ?? ExistingFilePolicy.Ask.ToString();
            p.SkipUpToDate = _upToDate.Checked;
            p.MaxFilesPerOutput = (int)_maxFiles.Value;
            p.WriteCsv = _csv.Checked;
            p.OpenFirstFile = _openFirst.Checked;
            p.Normalize();
            return p;
        }

        private static void SetNum(NumericUpDown n, decimal value)
        {
            n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, value));
        }

        private void ApplyProfile(CollectorProfile p)
        {
            _loadingProfile = true;
            try
            {
                p.Normalize();
                _profileName = p.Name;
                _presetDesc.Text = p.Description ?? "";

                SelectEnum(_mode, p.Mode);
                _names.Text = string.Join(", ", p.FolderNames);
                _pattern.Text = p.FolderNamePattern;
                SetNum(_depth, p.Depth);
                _delims.Text = p.TokenDelimiters;
                SetNum(_tokenIndex, p.TokenIndex);
                _fileRegex.Text = p.FileNameRegex;
                _unmatched.Text = p.UnmatchedGroupName;
                _singleName.Text = p.SingleOutputName;
                _rootFiles.Checked = p.IncludeRootFiles;
                _rootGroup.Text = p.RootFilesGroupName;
                _rootOnly.Checked = p.RootFolderOnly;
                SetNum(_maxDepth, p.MaxScanDepth);

                _ext.Text = string.Join(", ", p.Extensions);
                _inc.Text = p.IncludeFileRegex;
                _exc.Text = p.ExcludeFileRegex;
                _excFolders.Text = string.Join(", ", p.ExcludeFolderNames);
                _excFolderRx.Text = p.ExcludeFolderRegex;
                SetNum(_minSize, p.MinSizeKb);
                DateTime d;
                if (DateTime.TryParse(p.ModifiedAfter, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) { _modAfter.Value = d; _modAfter.Checked = true; }
                else _modAfter.Checked = false;
                _latest.Checked = p.LatestRevisionOnly;
                _revRegex.Text = p.RevisionRegex;

                SelectEnum(_fileOrder, p.FileOrder);
                _priority.Text = string.Join(", ", p.PriorityWords);
                SelectEnum(_groupOrder, p.GroupOrder);
                SelectEnum(_outLocation, p.OutputLocation);
                _template.Text = p.OutputNameTemplate;
                SelectEnum(_format, p.OutputFormat);
                SelectEnum(_policy, p.ExistingFilePolicy);
                _upToDate.Checked = p.SkipUpToDate;
                SetNum(_maxFiles, p.MaxFilesPerOutput);
                _csv.Checked = p.WriteCsv;
                _openFirst.Checked = p.OpenFirstFile;
            }
            finally
            {
                _loadingProfile = false;
            }
            UpdateModeVisibility();
        }

        // ================================================================== presets

        private void LoadPresets()
        {
            _preset.Items.Clear();
            foreach (var entry in ProfileStore.List())
                _preset.Items.Add(new PresetItem { Entry = entry });
        }

        private void OnPresetChanged(object sender, EventArgs e)
        {
            var item = _preset.SelectedItem as PresetItem;
            if (item == null || _loadingProfile) return;
            try { ApplyProfile(ProfileSerializer.Read(item.Entry.Path)); }
            catch (Exception ex) { MessageBox.Show(this, Loc.F("msg.profileInvalid", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void OnLoadProfileFile(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "Profile (*.json)|*.json", CheckFileExists = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { ApplyProfile(ProfileSerializer.Read(dlg.FileName)); }
                catch (Exception ex) { MessageBox.Show(this, Loc.F("msg.profileInvalid", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void OnSaveProfile(object sender, EventArgs e)
        {
            string name = PromptForm.Show(this, Loc.T("msg.saveProfileTitle"), Loc.T("msg.profileName"), _profileName);
            if (name == null) return;

            _profileName = name;
            var profile = ReadProfile();
            string path = ProfileStore.SaveUser(profile);
            AppendLog(Loc.F("msg.profileSaved", path));

            LoadPresets();
            for (int i = 0; i < _preset.Items.Count; i++)
            {
                if (string.Equals(((PresetItem)_preset.Items[i]).Entry.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    _loadingProfile = true;
                    _preset.SelectedIndex = i;
                    _loadingProfile = false;
                    break;
                }
            }
        }

        private void RestoreSettings()
        {
            string v;
            if (_settings.TryGetValue("root", out v)) _root.Text = v;
            if (_settings.TryGetValue("output", out v)) _output.Text = v;
            if (_settings.TryGetValue("preset", out v))
            {
                for (int i = 0; i < _preset.Items.Count; i++)
                {
                    var entry = ((PresetItem)_preset.Items[i]).Entry;
                    if (entry.Name.Equals(v, StringComparison.OrdinalIgnoreCase))
                    {
                        try { ApplyProfile(ProfileSerializer.Read(entry.Path)); }
                        catch (Exception) { }
                        _loadingProfile = true;
                        _preset.SelectedIndex = i;
                        _loadingProfile = false;
                        break;
                    }
                }
            }
        }

        // ================================================================== scan

        private async void OnScan(object sender, EventArgs e)
        {
            var profile = ReadProfile();
            string root = _root.Text.Trim();

            SetBusy(true);
            _groups.Items.Clear();
            _itemsByGroup.Clear();
            _plan = null;
            AppendLog("Scanning " + root + " ...");
            _scanCts = new CancellationTokenSource();

            try
            {
                var progress = new Progress<string>(p => _status.Text = "Scanning: " + p); // created on the UI thread
                var token = _scanCts.Token;
                var plan = await Task.Run(() => new GroupPlanner().Plan(profile, root, token, progress), token);
                _plan = plan;
                ShowPlan(plan);
            }
            catch (OperationCanceledException)
            {
                AppendLog(Loc.T("msg.scanCancelled"));
                _status.Text = Loc.T("msg.scanCancelled");
            }
            catch (ArgumentException ex)
            {
                AppendLog(ex.Message);
                MessageBox.Show(this, ex.Message, Loc.T("msg.cannotScan"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                AppendLog("Scan failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, Loc.T("msg.cannotScan"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_scanCts != null) _scanCts.Dispose();
                _scanCts = null;
                SetBusy(false);
            }
        }

        private void ShowPlan(PlanResult plan)
        {
            foreach (var g in plan.Groups)
            {
                var item = new ListViewItem(g.Name) { Checked = g.TotalCount > 0, Tag = g };
                item.SubItems.Add(g.SourceFolders.Count.ToString(CultureInfo.CurrentCulture));
                item.SubItems.Add(g.TotalCount.ToString(CultureInfo.CurrentCulture));
                item.SubItems.Add(g.LocalCount.ToString(CultureInfo.CurrentCulture));
                item.SubItems.Add(g.NotLocalCount.ToString(CultureInfo.CurrentCulture));
                item.SubItems.Add(g.NotLocalCount > 0 ? "Ready (files that are not local will be skipped)" : "Ready");
                _groups.Items.Add(item);
                _itemsByGroup[g.Name] = item;

                AppendLog(g.Name + ": " + g.SourceFolders.Count + " folder(s), " + g.TotalCount + " file(s), " + g.NotLocalCount + " not local");
                foreach (var f in g.Files.Where(x => !x.IsLocal))
                    AppendLog("    not local: " + f.Path + " - " + f.UnavailableReason);
            }

            foreach (var err in plan.Errors) AppendLog("Scan warning: " + err);
            int shown = 0;
            foreach (var w in plan.Warnings)
            {
                if (shown++ >= 50) { AppendLog("... " + (plan.Warnings.Count - 50) + " more warning(s)"); break; }
                AppendLog("Note: " + w);
            }

            _status.Text = Loc.F("msg.scanDone", plan.Groups.Count, plan.Groups.Sum(g => g.TotalCount), plan.FoldersVisited);
            AppendLog(_status.Text);
        }

        /// <summary>Double-click a group to list its files in the planned append order.</summary>
        private void OnGroupDoubleClick(object sender, EventArgs e)
        {
            if (_groups.SelectedItems.Count == 0) return;
            var g = _groups.SelectedItems[0].Tag as FolderGroup;
            if (g == null) return;
            AppendLog("== " + g.Name + " - append order ==");
            int n = 1;
            foreach (var f in g.Files) AppendLog("  " + (n++).ToString("000", CultureInfo.InvariantCulture) + "  " + f.Path);
        }

        // ================================================================== run

        private async void OnStart(object sender, EventArgs e)
        {
            if (_plan == null) { MessageBox.Show(this, Loc.T("msg.scanFirst"), Text); return; }

            string outDir = _output.Text.Trim();
            if (string.IsNullOrWhiteSpace(outDir)) { MessageBox.Show(this, Loc.T("msg.selectOutput"), Text); return; }

            var selected = _groups.CheckedItems.Cast<ListViewItem>().Select(i => (FolderGroup)i.Tag).Where(g => g.TotalCount > 0).ToList();
            if (selected.Count == 0) { MessageBox.Show(this, Loc.T("msg.noGroups"), Text); return; }

            try { Directory.CreateDirectory(outDir); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            var profile = ReadProfile();
            JobSpec job = OutputPlanner.BuildJob(profile, selected, _root.Text.Trim(), outDir);
            if (job.Groups.Count == 0) { MessageBox.Show(this, Loc.T("msg.noGroups"), Text); return; }

            // Never overwrite without explicit confirmation.
            var policy = profile.ExistingFilePolicyValue;
            var existing = job.Groups.Where(g => File.Exists(g.OutputPath)).Select(g => g.OutputPath).ToList();
            if (existing.Count > 0 && policy == ExistingFilePolicy.Ask)
            {
                var answer = MessageBox.Show(this, Loc.F("msg.existsAsk", string.Join("\n", existing.Take(15))), Loc.T("msg.existsTitle"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (answer == DialogResult.Cancel) return;
                policy = answer == DialogResult.Yes ? ExistingFilePolicy.Overwrite : ExistingFilePolicy.Skip;
            }
            else if (existing.Count > 0 && policy == ExistingFilePolicy.Overwrite)
            {
                var answer = MessageBox.Show(this, Loc.F("msg.existsConfirm", string.Join("\n", existing.Take(15))), Loc.T("msg.existsTitle"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (answer != DialogResult.OK) return;
            }
            else if (policy == ExistingFilePolicy.Ask)
            {
                policy = ExistingFilePolicy.Skip; // nothing exists yet, so nothing to decide
            }
            job.ExistingFilePolicy = policy.ToString();

            string workerExe = Path.Combine(Path.GetDirectoryName(typeof(CollectorForm).Assembly.Location), "Worker", "BIMTools.NwcCollector.Worker.exe");
            if (!File.Exists(workerExe))
            {
                MessageBox.Show(this, Loc.T("msg.workerMissing") + "\n" + workerExe, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // The running Navisworks (Roamer.exe) lives in the install folder the worker must load the API from.
            string nwDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

            _outputToGroup.Clear();
            int totalFiles = 0;
            foreach (var g in job.Groups)
            {
                _outputToGroup[g.Name] = g.GroupKey;
                totalFiles += g.Files.Count;
                SetItemStatus(g.GroupKey, "Queued");
            }

            _lastOutputDir = outDir;
            _bar.Maximum = Math.Max(1, totalFiles);
            _bar.Value = 0;
            _cancelRequestedOnce = false;
            SetBusy(true);
            AppendLog("Starting worker: " + job.Groups.Count + " output file(s), " + totalFiles + " source file(s). Output: " + outDir);

            _worker = new WorkerClient();
            try
            {
                int exit = await _worker.RunAsync(job, workerExe, nwDir, line =>
                {
                    if (IsDisposed) return;
                    try { BeginInvoke(new Action(() => HandleWorkerLine(line))); } catch (InvalidOperationException) { }
                });

                string text = exit == 0 ? Loc.T("msg.finishedOk")
                            : exit == 3 ? Loc.T("msg.cancelled")
                            : exit == 1 ? Loc.T("msg.finishedProblems")
                            : Loc.F("msg.failedExit", exit);
                _status.Text = text;
                AppendLog(text);

                if (_openFolder.Checked && (exit == 0 || exit == 1) && Directory.Exists(_lastOutputDir))
                {
                    try { Process.Start("explorer.exe", "\"" + _lastOutputDir + "\""); }
                    catch (Exception) { }
                }
            }
            catch (Exception ex)
            {
                AppendLog("Worker could not run: " + ex.Message);
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _worker = null;
                SetBusy(false);
            }
        }

        private string GroupOf(string outputName)
        {
            string group;
            return _outputToGroup.TryGetValue(outputName, out group) ? group : outputName;
        }

        private void HandleWorkerLine(string line)
        {
            string type;
            string[] f;
            if (!WorkerProtocol.TryParse(line, out type, out f)) { AppendLog(line); return; }

            switch (type)
            {
                case WorkerProtocol.Log:
                    AppendLog(f.Length > 0 ? f[0] : string.Empty);
                    break;

                case WorkerProtocol.GroupStart:
                    SetItemStatus(GroupOf(f[0]), "Running... (" + f[0] + ")");
                    _status.Text = "Building " + f[0];
                    AppendLog("== " + f[0] + " (" + f[1] + " file(s)) ==");
                    break;

                case WorkerProtocol.File:
                    // name, index, total, state, path, reason
                    if (_bar.Value < _bar.Maximum) _bar.Value++;
                    SetItemStatus(GroupOf(f[0]), f[0] + ": " + f[1] + "/" + f[2]);
                    AppendLog("   [" + f[3] + "] " + f[4] + (string.IsNullOrEmpty(f[5]) ? "" : " - " + f[5]));
                    break;

                case WorkerProtocol.GroupDone:
                    // name, status, expected, appended, skipped, failed, output
                    SetItemStatus(GroupOf(f[0]), f[0] + ": " + f[1] + " (expected " + f[2] + ", appended " + f[3] + ", skipped " + f[4] + ", failed " + f[5] + ")");
                    AppendLog("<< " + f[0] + ": " + f[1] + " -> " + f[6]);
                    break;

                case WorkerProtocol.JobDone:
                    AppendLog("Job done: " + f[0] + " of " + f[1] + " output file(s) fully OK.");
                    break;
            }
        }

        private void OnCancel(object sender, EventArgs e)
        {
            if (_scanCts != null) { _scanCts.Cancel(); return; }

            if (_worker != null && _worker.IsRunning)
            {
                if (!_cancelRequestedOnce)
                {
                    _cancelRequestedOnce = true;
                    _worker.RequestCancel();
                    AppendLog("Cancel requested: the worker stops after the current file. Click Cancel again to stop it immediately.");
                    _status.Text = Loc.T("msg.cancelling");
                }
                else
                {
                    _worker.Kill();
                    AppendLog("Worker was stopped immediately. A partial *.partial.nwd file may remain in the output folder.");
                }
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_worker != null && _worker.IsRunning)
            {
                var r = MessageBox.Show(this, Loc.T("msg.closeRunning"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
                _worker.Kill();
            }
            if (_scanCts != null) _scanCts.Cancel();

            _settings["root"] = _root.Text.Trim();
            _settings["output"] = _output.Text.Trim();
            _settings["preset"] = _profileName;
            ProfileStore.SaveSettings(_settings);
        }

        // ================================================================== helpers

        private void SetBusy(bool busy)
        {
            _scan.Enabled = !busy;
            _start.Enabled = !busy && _plan != null && _plan.Groups.Count > 0;
            _cancel.Enabled = busy;
            _tabs.Enabled = !busy;
            _preset.Enabled = _lang.Enabled = _btnLoad.Enabled = _btnSave.Enabled = !busy;
        }

        private void SetItemStatus(string group, string text)
        {
            ListViewItem item;
            if (_itemsByGroup.TryGetValue(group, out item)) item.SubItems[5].Text = text;
        }

        private void AppendLog(string text)
        {
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + text + Environment.NewLine);
        }
    }
}
