using System;
using System.IO;
using System.Reflection;

namespace BIMTools.NwcCollector.Worker
{
    /// <summary>
    /// The worker is a stand-alone exe, so it must be able to find the Navisworks assemblies in the install folder.
    /// Call Install before any method that touches Autodesk.Navisworks types is JIT-compiled.
    /// </summary>
    internal static class NavisworksLocator
    {
        public const string DefaultDir = @"C:\Program Files\Autodesk\Navisworks Manage 2026";

        public static string Resolve(string argDir)
        {
            string dir = argDir;
            if (string.IsNullOrWhiteSpace(dir)) dir = Environment.GetEnvironmentVariable("NAVISWORKS_DIR");
            if (string.IsNullOrWhiteSpace(dir)) dir = DefaultDir;
            return dir;
        }

        public static void Install(string argDir)
        {
            string dir = Resolve(argDir);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException("Navisworks install folder not found: " + dir +
                    ". Pass --nwdir or set the NAVISWORKS_DIR environment variable.");

            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + path);

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                string simple = new AssemblyName(e.Name).Name;
                string candidate = Path.Combine(dir, simple + ".dll");
                return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
            };
        }
    }
}
