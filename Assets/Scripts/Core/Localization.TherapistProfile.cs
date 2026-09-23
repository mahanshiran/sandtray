using System.Collections.Generic;
using UnityEngine;
namespace Sandplay.Core
{
    public static partial class Localization
    {
        private static Dictionary<string, string[]> _therapistTranslations;
        private static bool TryGetTherapistTranslation(string key, out string value)
        {
            if (_therapistTranslations == null)
            {
                _therapistTranslations = new Dictionary<string, string[]>();
                var asset = Resources.Load<TextAsset>("Localization/TherapistProfile");
                if (asset != null)
                    foreach (var row in asset.text.Split('\n'))
                    {
                        if (string.IsNullOrWhiteSpace(row) || row.StartsWith("#")) continue;
                        var cells = row.TrimEnd('\r').Split('|');
                        if (cells.Length == 8) _therapistTranslations[cells[0]] = cells;
                    }
            }
            value = null;
            if (!_therapistTranslations.TryGetValue(key, out var translations)) return false;
            int index = (int)_current;
            value = translations[index >= 0 && index < translations.Length ? index : 0];
            return true;
        }
    }
}
