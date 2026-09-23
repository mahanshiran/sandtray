using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Sandplay.Core
{
    public static partial class Localization
    {
        public static readonly Language[] SupportedLanguages = { Language.English, Language.Chinese, Language.Spanish,
            Language.PortugueseBrazil, Language.French, Language.German, Language.Japanese, Language.Czech };
        public static readonly string[] NativeLanguageNames = { "English", "简体中文", "Español", "Português (Brasil)", "Français", "Deutsch", "日本語", "Čeština" };
        private static readonly string[] Codes = { "en", "zh", "es", "pt-BR", "fr", "de", "ja", "cs" };
        private static readonly string[] Cultures = { "en-US", "zh-CN", "es-ES", "pt-BR", "fr-FR", "de-DE", "ja-JP", "cs-CZ" };
        public static string LanguageCode => Codes[(int)Current];
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Cultures[(int)Current]);
        private static readonly Dictionary<Language, Dictionary<string,string>> Translations = new Dictionary<Language, Dictionary<string,string>>();

        public static Language FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: case SystemLanguage.ChineseTraditional: return Language.Chinese;
                case SystemLanguage.Spanish: return Language.Spanish;
                case SystemLanguage.Portuguese: return Language.PortugueseBrazil;
                case SystemLanguage.French: return Language.French;
                case SystemLanguage.German: return Language.German;
                case SystemLanguage.Japanese: return Language.Japanese;
                case SystemLanguage.Czech: return Language.Czech;
                default: return Language.English;
            }
        }

        private static Dictionary<string,string> Table(Language language)
        {
            if (language == Language.English) return _en;
            if (language == Language.Chinese) return _zh;
            if (Translations.TryGetValue(language, out var table)) return table;
            if (language == Language.Czech)
            {
                table = new Dictionary<string, string>();
                var czech = Resources.Load<TextAsset>("Localization/Czech");
                if (czech != null)
                    foreach (var raw in czech.text.Split('\n'))
                    {
                        var line = raw.TrimEnd('\r');
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                        var cells = line.Split('|');
                        if (cells.Length != 2) { Debug.LogError("Invalid Czech localization row: " + cells[0]); continue; }
                        table[cells[0]] = cells[1].Replace("\\n", "\n");
                    }
                Translations[language] = table;
                return table;
            }
            // UTF-8, pipe-separated columns: key | es | pt-BR | fr | de | ja.
            // Newlines inside a value are escaped; placeholders retain their C# format.
            var resource = Resources.Load<TextAsset>("Localization/AdditionalLanguages");
            for (int i=2;i<=6;i++) Translations[SupportedLanguages[i]] = new Dictionary<string,string>();
            if (resource != null)
                foreach (var raw in resource.text.Split('\n'))
                {
                    string line=raw.TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var cells=line.Split('|');
                    if (cells.Length != 6) { Debug.LogError("Invalid localization row: " + cells[0]); continue; }
                    for(int i=1;i<6;i++) Translations[SupportedLanguages[i+1]][cells[0]]=cells[i].Replace("\\n", "\n");
                }
            // Language-neutral product names, symbols and example values.
            foreach (var target in new[] { Language.Spanish, Language.PortugueseBrazil, Language.French, Language.German, Language.Japanese })
            {
                var values = Translations[target];
                foreach (var key in new[] { "sub.pro_badge", "sub.subscribed_badge", "sub.paywall_title", "menu.title", "replays.vip_locked",
                    "action.vertical", "action.rotate", "action.resize", "action.delete", "settings.close", "credits.source", "credits.license",
                    "reports.pdf", "size.standard_desc", "size.medium_desc", "join.placeholder_code", "join.placeholder_ip" }) values[key]=_en[key];
            }
            return Translations[language];
        }

        // Transitional support for older UI components using inline English/Chinese pairs.
        public static string Text(string english, string chinese)
        {
            if (!_initialized) AutoDetect();
            if (Current == Language.Chinese) return chinese;
            foreach(var pair in _en) if(pair.Value == english) return Get(pair.Key);
            return Get("text:" + english) is string translated && translated != "text:" + english ? translated : english;
        }
    }
}
