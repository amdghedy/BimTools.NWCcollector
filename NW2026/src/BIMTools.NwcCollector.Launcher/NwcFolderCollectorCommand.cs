using Autodesk.Navisworks.Api.Plugins;

namespace BIMTools.NwcCollector.Launcher
{
    /// <summary>
    /// Ribbon command "NWC Folder Collector" on the BIM TOOLS tab.
    ///
    /// VERIFY against the Navisworks 2026 SDK samples (written from memory, not compiled):
    /// the PluginAttribute / RibbonLayoutAttribute / RibbonTabAttribute / CommandAttribute shapes, the 4-letter
    /// developer id "BIMT", and where the ribbon XAML must be deployed (en-US folder beside the DLL).
    /// </summary>
    [Plugin("BIMTools.NwcFolderCollector", "BIMT",
        DisplayName = "NWC Folder Collector",
        ToolTip = "Collect NWC files from every folder with the same name and build one NWD per name.")]
    [RibbonLayout("BimToolsRibbon.xaml")]
    [RibbonTab("ID_BimTools_Tab", DisplayName = "BIM TOOLS")]
    [Command("ID_NwcFolderCollector_Button",
        DisplayName = "NWC Folder Collector",
        ToolTip = "Collect NWC files from repeated folder names into one NWD per name.")]
    public class NwcFolderCollectorCommand : AddInPlugin
    {
        private static CollectorForm _form;

        public override int Execute(params string[] parameters)
        {
            if (_form == null || _form.IsDisposed)
            {
                _form = new CollectorForm();
                _form.Show();
            }
            else
            {
                if (_form.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                    _form.WindowState = System.Windows.Forms.FormWindowState.Normal;
                _form.Activate();
            }
            return 0;
        }
    }
}
