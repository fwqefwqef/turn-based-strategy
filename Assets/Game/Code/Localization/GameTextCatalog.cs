using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;

namespace Windy.Srpg.Game.Localization
{
    public static class GameTextCatalog
    {
        private const string DefaultLanguageCode = "en";
        private const string CsvFileName = "game_text.csv";
        private static readonly string[] CsvSearchDirectories =
        {
            Path.Combine(Application.dataPath, "Game", "Data"),
            Application.streamingAssetsPath,
            Path.Combine(Application.dataPath, "StreamingAssets")
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Entries = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static bool isLoaded;
        private static string currentLanguageCode;

        public static string CurrentLanguageCode
        {
            get => string.IsNullOrWhiteSpace(currentLanguageCode) ? DetectDefaultLanguageCode() : currentLanguageCode;
            set => currentLanguageCode = NormalizeLanguageCode(value);
        }

        public static string Get(string key, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            EnsureLoaded();
            if (!Entries.TryGetValue(key, out Dictionary<string, string> localizedValues))
            {
                return fallback ?? key;
            }

            string activeLanguage = NormalizeLanguageCode(CurrentLanguageCode);
            if (TryGetLocalizedValue(localizedValues, activeLanguage, out string localizedValue))
            {
                return localizedValue;
            }

            return TryGetLocalizedValue(localizedValues, DefaultLanguageCode, out string defaultValue)
                ? defaultValue
                : fallback ?? key;
        }

        public static string Format(string key, string fallback, params object[] args)
        {
            string template = Get(key, fallback);
            return args == null || args.Length == 0
                ? template
                : string.Format(CultureInfo.InvariantCulture, template, args);
        }

        public static string ResolveOverride(string overrideText, string key, string fallback)
        {
            return string.IsNullOrWhiteSpace(overrideText) ? Get(key, fallback) : overrideText;
        }

        public static string ResolveSceneText(TMP_Text text, string key, string fallback)
        {
            return text != null && !string.IsNullOrWhiteSpace(text.text)
                ? text.text
                : Get(key, fallback);
        }

        private static void EnsureLoaded()
        {
            if (isLoaded)
            {
                return;
            }

            isLoaded = true;
            Entries.Clear();

            string csvPath = ResolveCsvPath();
            if (!File.Exists(csvPath))
            {
                Debug.LogWarning($"GameTextCatalog: Missing CSV at '{csvPath}'.");
                return;
            }

            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length == 0)
            {
                return;
            }

            List<string> headers = ParseCsvLine(lines[0]);
            if (headers.Count == 0)
            {
                return;
            }

            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                List<string> values = ParseCsvLine(line);
                if (values.Count == 0)
                {
                    continue;
                }

                string key = values[0]?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                Dictionary<string, string> localizedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int columnIndex = 1; columnIndex < headers.Count && columnIndex < values.Count; columnIndex++)
                {
                    string header = NormalizeLanguageCode(headers[columnIndex]);
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        continue;
                    }

                    localizedValues[header] = values[columnIndex];
                }

                Entries[key] = localizedValues;
            }
        }

        private static bool TryGetLocalizedValue(IReadOnlyDictionary<string, string> localizedValues, string languageCode, out string value)
        {
            value = null;
            if (localizedValues == null || string.IsNullOrWhiteSpace(languageCode))
            {
                return false;
            }

            if (!localizedValues.TryGetValue(languageCode, out value) || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return true;
        }

        private static string DetectDefaultLanguageCode()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.Korean => "ko",
                SystemLanguage.Japanese => "ja",
                _ => DefaultLanguageCode
            };
        }

        private static string NormalizeLanguageCode(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> values = new List<string>();
            if (line == null)
            {
                return values;
            }

            bool inQuotes = false;
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];
                if (character == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (character == ',' && !inQuotes)
                {
                    values.Add(builder.ToString());
                    builder.Length = 0;
                    continue;
                }

                builder.Append(character);
            }

            values.Add(builder.ToString());
            return values;
        }

        private static string ResolveCsvPath()
        {
            foreach (string directory in CsvSearchDirectories)
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory, CsvFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(CsvSearchDirectories[0], CsvFileName);
        }
    }
}

