using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BIMTools.NwcCollector.Core.Contracts;

namespace BIMTools.NwcCollector.Launcher
{
    internal sealed class PresetEntry
    {
        public string Name;
        public string Description;
        public string Path;

        /// <summary>"" built-in, "(user) " personal profile, "(shared) " company profile on a network share.</summary>
        public string Origin;
    }

    /// <summary>
    /// Where profiles and settings live:
    ///   built-in : &lt;plug-in folder&gt;\Profiles          (shipped with the tool)
    ///   shared   : folder named by the environment variable BIMTOOLS_NWC_PROFILES (a company network share)
    ///   user     : %AppData%\BIMTools\NwcFolderCollector\Profiles
    /// </summary>
    internal static class ProfileStore
    {
        public const string SharedFolderVariable = "BIMTOOLS_NWC_PROFILES";

        private static string AssemblyDir
        {
            get { return System.IO.Path.GetDirectoryName(typeof(ProfileStore).Assembly.Location); }
        }

        public static string BuiltInDir { get { return System.IO.Path.Combine(AssemblyDir, "Profiles"); } }
        public static string LanguagesDir { get { return System.IO.Path.Combine(AssemblyDir, "Languages"); } }

        public static string UserRoot
        {
            get
            {
                return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMTools", "NwcFolderCollector");
            }
        }

        public static string UserDir { get { return System.IO.Path.Combine(UserRoot, "Profiles"); } }
        private static string SettingsPath { get { return System.IO.Path.Combine(UserRoot, "settings.ini"); } }

        public static List<PresetEntry> List()
        {
            var list = new List<PresetEntry>();
            AddFrom(list, BuiltInDir, "");
            string shared = Environment.GetEnvironmentVariable(SharedFolderVariable);
            if (!string.IsNullOrWhiteSpace(shared)) AddFrom(list, shared, "(shared) ");
            AddFrom(list, UserDir, "(user) ");
            return list;
        }

        private static void AddFrom(List<PresetEntry> list, string dir, string origin)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var p = ProfileSerializer.Read(file);
                        list.Add(new PresetEntry { Name = p.Name, Description = p.Description, Path = file, Origin = origin });
                    }
                    catch (Exception ex) when (ex is IOException || ex is System.Runtime.Serialization.SerializationException || ex is InvalidOperationException)
                    {
                        // an unreadable profile file is simply not listed
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // an unreachable share is not an error for the user
            }
        }

        public static string SaveUser(CollectorProfile profile)
        {
            Directory.CreateDirectory(UserDir);
            string name = PathUtil.SafeFileName(profile.Name);
            string path = System.IO.Path.Combine(UserDir, name + ".json");
            ProfileSerializer.Write(profile, path);
            return path;
        }

        public static Dictionary<string, string> LoadSettings()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(SettingsPath)) return map;
                foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            return map;
        }

        public static void SaveSettings(Dictionary<string, string> map)
        {
            try
            {
                Directory.CreateDirectory(UserRoot);
                File.WriteAllLines(SettingsPath, map.Select(kv => kv.Key + "=" + kv.Value), new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }
    }
}
