using System;
using System.Text.RegularExpressions;

namespace BIMTools.NwcCollector.Core.Planning
{
    public static class RegexUtil
    {
        /// <summary>Returns null for an empty pattern; throws ArgumentException with a readable message for a bad one.</summary>
        public static Regex Make(string pattern, string label)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return null;
            try
            {
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid regular expression in '" + label + "': " + ex.Message);
            }
        }
    }
}
