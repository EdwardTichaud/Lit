using System.Globalization;
using System.Text;
using UnityEngine;

namespace Lit.Story
{
    // The existing world snapshot owns persistence and resets this value between saves/sessions.
    public static class StorySequenceNames
    {
        public const string MuninKey = "story.munin.name";
        public const string DefaultMuninName = "Munin";
        public const int MaximumNameLength = 24;

        public static string NormalizeName(string value)
        {
            var result = new StringBuilder();
            foreach (char c in value ?? string.Empty)
            {
                // Plain text names; never allow rich-text tags or nested dialogue tokens.
                if (char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark ||
                    c == ' ' || c == '-' || c == '\'' || c == '\u2019') result.Append(c);
            }
            string clean = result.ToString().Trim().Normalize(NormalizationForm.FormC);
            int[] elements = StringInfo.ParseCombiningCharacters(clean);
            if (elements.Length > MaximumNameLength) clean = clean.Substring(0, elements[MaximumNameLength]).TrimEnd();
            return string.IsNullOrWhiteSpace(clean) ? DefaultMuninName : clean;
        }

        public static string GetMuninName(WorldRulesStateManager rules)
        {
            return rules != null && rules.TryGetString(MuninKey, out string name)
                ? NormalizeName(name) : DefaultMuninName;
        }

        public static void SetMuninName(WorldRulesStateManager rules, string name)
        {
            if (rules == null) throw new System.ArgumentNullException(nameof(rules));
            rules.SetString(MuninKey, NormalizeName(name));
        }

        public static string Resolve(string text, WorldRulesStateManager rules)
        {
            return (text ?? string.Empty).Replace("[Munin]", GetMuninName(rules));
        }

        public static string Resolve(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("[Munin]")) return text ?? string.Empty;
            return Resolve(text, Object.FindAnyObjectByType<WorldRulesStateManager>());
        }
    }
}
