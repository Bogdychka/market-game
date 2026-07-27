using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Market.DebugTools.Editor
{
    /// <summary>Pure validation helpers shared by checks and EditMode tests.</summary>
    public static class ProjectHealthRules
    {
        private static readonly Regex LowerSnakeCase =
            new("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        public static bool IsMissingStableId(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        public static bool IsLowerSnakeCase(string value)
        {
            return !string.IsNullOrEmpty(value) && LowerSnakeCase.IsMatch(value);
        }

        public static bool IsProjectAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.Replace('\\', '/').StartsWith("Assets/_Project/", StringComparison.Ordinal);
        }

        public static bool IsNonNegative(float value)
        {
            return value >= 0f;
        }

        public static bool HasNonAscii(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < value.Length; i++)
                if (value[i] > 127)
                    return true;

            return false;
        }

        public static bool HasNullReference<T>(IReadOnlyList<T> values) where T : class
        {
            if (values == null)
                return true;

            for (int i = 0; i < values.Count; i++)
                if (values[i] == null)
                    return true;

            return false;
        }

        public static bool SerializedComponentHasSetting(
            string yaml,
            string component,
            string setting,
            string value)
        {
            if (string.IsNullOrEmpty(yaml))
                return false;

            string componentPattern = $"(?ms)^{Regex.Escape(component)}:\\r?\\n(?:(?!^--- !u!).)*";
            string settingPattern = $"(?m)^\\s+{Regex.Escape(setting)}:\\s+{Regex.Escape(value)}\\s*$";
            MatchCollection components = Regex.Matches(yaml, componentPattern, RegexOptions.CultureInvariant);
            for (int i = 0; i < components.Count; i++)
                if (Regex.IsMatch(components[i].Value, settingPattern, RegexOptions.CultureInvariant))
                    return true;

            return false;
        }

        public static bool SerializedComponentFloatBelow(
            string yaml,
            string component,
            string setting,
            float minimum)
        {
            if (string.IsNullOrEmpty(yaml))
                return false;

            string componentPattern = $"(?ms)^{Regex.Escape(component)}:\\r?\\n(?:(?!^--- !u!).)*";
            string settingPattern = $"(?m)^\\s+{Regex.Escape(setting)}:\\s+(?<value>-?[0-9.]+)\\s*$";
            MatchCollection components = Regex.Matches(yaml, componentPattern, RegexOptions.CultureInvariant);
            for (int i = 0; i < components.Count; i++)
            {
                Match value = Regex.Match(components[i].Value, settingPattern, RegexOptions.CultureInvariant);
                if (value.Success
                    && float.TryParse(value.Groups["value"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float parsed)
                    && parsed < minimum)
                    return true;
            }

            return false;
        }

        public static HashSet<string> FindDuplicateKeys(IEnumerable<string> keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);

            foreach (string key in keys)
                if (!string.IsNullOrEmpty(key) && !seen.Add(key))
                    duplicates.Add(key);

            return duplicates;
        }
    }
}
