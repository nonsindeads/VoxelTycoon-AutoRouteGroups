using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoRouteGroups
{
    internal static class StopAbbreviator
    {
        private static readonly HashSet<string> PlacePrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ALT", "BAD", "GREAT", "GROSS", "GROß", "KLEIN", "LITTLE", "NEU", "NEW", "OLD", "SAINT", "SANKT", "ST"
        };

        public static Dictionary<int, string> Build(
            IEnumerable<KeyValuePair<int, string>> stops,
            int minimumLength = 2,
            int maximumLength = 4)
        {
            minimumLength = Math.Max(2, Math.Min(4, minimumLength));
            maximumLength = Math.Max(minimumLength, Math.Min(4, maximumLength));

            List<StopEntry> entries = stops
                .Select(pair => new StopEntry(pair.Key, pair.Value))
                .OrderBy(entry => entry.NormalizedName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Id)
                .ToList();

            Dictionary<int, string> result = new Dictionary<int, string>();
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<StopEntry> unresolved = entries;

            for (int length = minimumLength; length <= maximumLength && unresolved.Count > 0; length++)
            {
                Dictionary<string, List<StopEntry>> candidateGroups = unresolved
                    .GroupBy(entry => CreateCandidate(entry.Name, length), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

                List<StopEntry> next = new List<StopEntry>();
                foreach (StopEntry entry in unresolved)
                {
                    string candidate = CreateCandidate(entry.Name, length);
                    if (candidateGroups[candidate].Count == 1 && !used.Contains(candidate))
                    {
                        result[entry.Id] = candidate;
                        used.Add(candidate);
                    }
                    else
                    {
                        next.Add(entry);
                    }
                }

                unresolved = next;
            }

            // Exact duplicates and names whose first four meaningful characters are
            // still identical receive a deterministic four-character fallback.
            foreach (StopEntry entry in unresolved)
            {
                string stem = CreateCandidate(entry.Name, maximumLength).PadRight(3, 'X').Substring(0, 3);
                int counter = 0;
                string candidate;
                do
                {
                    candidate = stem + ToBase36(counter++, 1);
                }
                while (used.Contains(candidate) && counter < 36);

                if (used.Contains(candidate))
                {
                    string shortStem = stem.Substring(0, 2);
                    counter = 0;
                    do
                    {
                        candidate = shortStem + ToBase36(counter++, 2);
                    }
                    while (used.Contains(candidate));
                }

                result[entry.Id] = candidate;
                used.Add(candidate);
            }

            return result;
        }

        internal static string CreateCandidate(string name, int length)
        {
            length = Math.Max(2, Math.Min(4, length));
            List<string> words = SplitWords(name);
            if (words.Count == 0)
            {
                return "XX";
            }

            if (words.Count == 1)
            {
                return TakeAndPad(words[0], length);
            }

            string candidate;
            if (PlacePrefixes.Contains(words[0]))
            {
                candidate = BuildPrefixedCandidate(words, length);
            }
            else
            {
                candidate = BuildMultiWordCandidate(words, length);
            }

            return TakeAndPad(candidate, length);
        }

        private static string BuildPrefixedCandidate(List<string> words, int length)
        {
            StringBuilder result = new StringBuilder(length);
            result.Append(words[0][0]);

            int available = length - 1;
            int trailingInitials = Math.Min(Math.Max(0, words.Count - 2), Math.Max(0, available - 1));
            int mainWordCharacters = Math.Max(1, available - trailingInitials);
            result.Append(words[1].Substring(0, Math.Min(mainWordCharacters, words[1].Length)));

            for (int i = 2; i < words.Count && result.Length < length; i++)
            {
                result.Append(words[i][0]);
            }

            AppendUnusedCharacters(result, words.Skip(1), length);
            return result.ToString();
        }

        private static string BuildMultiWordCandidate(List<string> words, int length)
        {
            StringBuilder result = new StringBuilder(length);

            if (words.Count == 2)
            {
                result.Append(words[0][0]);
                result.Append(words[1].Substring(0, Math.Min(length - 1, words[1].Length)));
                AppendUnusedCharacters(result, words, length);
                return result.ToString();
            }

            int trailingInitials = Math.Min(words.Count - 1, length - 1);
            int firstWordCharacters = Math.Max(1, length - trailingInitials);
            result.Append(words[0].Substring(0, Math.Min(firstWordCharacters, words[0].Length)));

            for (int i = 1; i < words.Count && result.Length < length; i++)
            {
                result.Append(words[i][0]);
            }

            AppendUnusedCharacters(result, words, length);
            return result.ToString();
        }

        private static void AppendUnusedCharacters(StringBuilder result, IEnumerable<string> words, int length)
        {
            foreach (string word in words)
            {
                for (int i = 1; i < word.Length && result.Length < length; i++)
                {
                    result.Append(word[i]);
                }
            }
        }

        private static List<string> SplitWords(string value)
        {
            List<string> words = new List<string>();
            StringBuilder current = new StringBuilder();

            foreach (char character in (value ?? string.Empty).ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    current.Append(character);
                }
                else if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
            }

            if (current.Length > 0)
            {
                words.Add(current.ToString());
            }

            return words;
        }

        private static string TakeAndPad(string value, int length)
        {
            string compact = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (compact.Length >= length)
            {
                return compact.Substring(0, length);
            }

            return compact.PadRight(Math.Max(2, compact.Length), 'X');
        }

        private static string ToBase36(int value, int width)
        {
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            char[] result = Enumerable.Repeat('0', width).ToArray();
            for (int i = width - 1; i >= 0; i--)
            {
                result[i] = alphabet[value % alphabet.Length];
                value /= alphabet.Length;
            }

            return new string(result);
        }

        private sealed class StopEntry
        {
            public int Id { get; }
            public string Name { get; }
            public string NormalizedName { get; }

            public StopEntry(int id, string name)
            {
                Id = id;
                Name = name ?? string.Empty;
                NormalizedName = string.Join("", SplitWords(Name));
            }
        }
    }
}
