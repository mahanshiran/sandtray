using System.Collections.Generic;
using UnityEngine;

namespace Sandplay.Core
{
    public static partial class Localization
    {
        private static Dictionary<string, string[]> _supportTranslations;

        private static bool TryGetSupportTranslation(string key, out string value)
        {
            if (_supportTranslations == null)
            {
                _supportTranslations = new Dictionary<string, string[]>();
                var asset = Resources.Load<TextAsset>("Localization/Support");
                if (asset != null)
                    foreach (var row in asset.text.Split('\n'))
                    {
                        var columns = row.TrimEnd('\r').Split('|');
                        if (columns.Length == 9) _supportTranslations[columns[0]] = columns;
                    }
            }
            value = null;
            if (!_supportTranslations.TryGetValue(key, out var text)) return false;
            value = text[(int)_current + 1];
            return true;
        }
    }
}
