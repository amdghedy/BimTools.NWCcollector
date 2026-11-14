using System.IO;

namespace BIMTools.NwcCollector.Core.Contracts
{
    public static class PathUtil
    {
        /// <summary>Makes a folder name safe to use as a file name (folder names are already valid, this is a guard).</summary>
        public static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
