using System.Collections.Generic;
using UnityEngine;
namespace Sandplay.Core
{
    public static partial class Localization
    {
        private static Dictionary<string,string[]> _reportTranslations;
        private static bool TryGetReportTranslation(string key,out string value)
        {
            if(_reportTranslations==null)
            {
                _reportTranslations=new Dictionary<string,string[]>();
                var asset=Resources.Load<TextAsset>("Localization/ReportWorkspace");
                if(asset!=null)foreach(var row in asset.text.Split('\n'))
                {var columns=row.TrimEnd('\r').Split('|');if(columns.Length==9)_reportTranslations[columns[0]]=columns;}
            }
            value=null;if(!_reportTranslations.TryGetValue(key,out var text))return false;
            value=text[(int)_current+1];return true;
        }
    }
}
