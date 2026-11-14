using System;
using Autodesk.Navisworks.Api.Automation;

namespace BIMTools.NwcCollector.Worker
{
    /// <summary>
    /// One headless Navisworks instance driven through the Automation API.
    /// All Navisworks calls in the worker go through this class, so an API difference in your
    /// Navisworks 2026 SDK only needs to be fixed here.
    ///
    /// VERIFY against the installed 2026 Automation assembly (written from memory, not compiled):
    ///   - class Autodesk.Navisworks.Api.Automation.NavisworksApplication with a public parameterless constructor
    ///   - methods OpenFile(string), AppendFile(string), SaveFile(string)
    ///   - IDisposable.Dispose() closes the instance
    /// </summary>
    internal sealed class AutomationSession : IDisposable
    {
        private NavisworksApplication _app;

        public AutomationSession()
        {
            _app = new NavisworksApplication();
        }

        public void OpenFile(string path)
        {
            _app.OpenFile(path);
        }

        public void AppendFile(string path)
        {
            _app.AppendFile(path);
        }

        public void SaveFile(string path)
        {
            _app.SaveFile(path);
        }

        public void Dispose()
        {
            var app = _app;
            _app = null;
            if (app != null) app.Dispose();
        }
    }
}
