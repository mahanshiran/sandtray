using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class LocalizationLanguageTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private int saved;
        private bool hadSaved, initialized;
        private Language previous;
        [SetUp]
        public void SetUp()
        {
            hadSaved=PlayerPrefs.HasKey("sandplay_lang");saved=PlayerPrefs.GetInt("sandplay_lang");
            previous=(Language)typeof(Localization).GetField("_current",Static).GetValue(null);
            initialized=(bool)typeof(Localization).GetField("_initialized",Static).GetValue(null);
        }
        [TearDown]
        public void TearDown()
        {
            if(hadSaved)PlayerPrefs.SetInt("sandplay_lang",saved);else PlayerPrefs.DeleteKey("sandplay_lang");
            PlayerPrefs.Save();
            typeof(Localization).GetField("_current",Static).SetValue(null,previous);
            typeof(Localization).GetField("_initialized",Static).SetValue(null,initialized);
        }
        private static string Placeholders(string s) => string.Join(",",Regex.Matches(s,@"\{\d+(?::[^}]+)?\}").Cast<Match>().Select(m=>m.Value).OrderBy(v=>v));
        [Test]
        public void AllFiveLanguages_CoverEveryEnglishKeyAndPreserveFormats()
        {
            var english=(Dictionary<string,string>)typeof(Localization).GetField("_en",Static).GetValue(null);
            foreach(var language in Localization.SupportedLanguages.Skip(2))
            {
                var table=(Dictionary<string,string>)typeof(Localization).GetMethod("Table",Static).Invoke(null,new object[]{language});
                foreach(var pair in english)
                {
                    Assert.IsTrue(table.ContainsKey(pair.Key),language+" missing "+pair.Key);
                    Assert.AreEqual(Placeholders(pair.Value),Placeholders(table[pair.Key]),language+" "+pair.Key);
                    Assert.DoesNotThrow(()=>string.Format(table[pair.Key],12.5,2,3),language+" "+pair.Key);
                }
            }
        }
        [Test]
        public void TranslationResource_HasValidColumnsAndUniqueKeys()
        {
            var resource=Resources.Load<TextAsset>("Localization/AdditionalLanguages");Assert.NotNull(resource);
            var keys=new HashSet<string>();
            foreach(var line in resource.text.Split('\n').Where(l=>!string.IsNullOrWhiteSpace(l)&&!l.StartsWith("#")))
            {
                var parts=line.TrimEnd('\r').Split('|');Assert.AreEqual(6,parts.Length,line);
                Assert.IsTrue(keys.Add(parts[0]),"Duplicate "+parts[0]);
                if(parts[0]!="net.viewers_s") foreach(var value in parts.Skip(1))Assert.IsNotEmpty(value,parts[0]);
            }
        }
        [Test]
        public void EveryLanguage_PersistsAndReloadsWithoutChangingExistingIds()
        {
            Assert.AreEqual(0,(int)Language.English);Assert.AreEqual(1,(int)Language.Chinese);
            foreach(var language in Localization.SupportedLanguages)
            {
                typeof(Localization).GetField("_current",Static).SetValue(null,(Language)(-1));
                Localization.SetLanguage(language);
                typeof(Localization).GetField("_initialized",Static).SetValue(null,false);
                Localization.AutoDetect();Assert.AreEqual(language,Localization.Current);
                Assert.IsNotEmpty(Localization.Culture.Name);
            }
        }
        [Test]
        public void AutoDetectionAndBrazilianCulture_MapCorrectly()
        {
            Assert.AreEqual(Language.PortugueseBrazil,Localization.FromSystemLanguage(SystemLanguage.Portuguese));
            Assert.AreEqual(Language.Japanese,Localization.FromSystemLanguage(SystemLanguage.Japanese));
            Assert.AreEqual(Language.English,Localization.FromSystemLanguage(SystemLanguage.Unknown));
            Localization.SetLanguage(Language.PortugueseBrazil);
            Assert.AreEqual("pt-BR",Localization.Culture.Name);
            StringAssert.Contains(",",Localization.Get("hud.radius",1.5));
        }
        [Test]
        public void Czech_DetectsFormatsAndSurvivesOtherLanguageLoads()
        {
            Assert.AreEqual(6, (int)Language.Japanese);
            Assert.AreEqual(7, (int)Language.Czech);
            Assert.AreEqual(Language.Czech, Localization.FromSystemLanguage(SystemLanguage.Czech));
            Assert.AreEqual("Čeština", Localization.NativeLanguageNames[Array.IndexOf(Localization.SupportedLanguages, Language.Czech)]);
            Localization.SetLanguage(Language.Czech);
            Assert.AreEqual("cs", Localization.LanguageCode);
            Assert.AreEqual("cs-CZ", Localization.Culture.Name);
            Assert.AreEqual("Poloměr: 1,5", Localization.Get("hud.radius", 1.5));
            Assert.AreEqual("Knihovna objektů", Localization.Text("Object library", "对象库"));
            foreach (var language in Localization.SupportedLanguages)
                typeof(Localization).GetMethod("Table", Static).Invoke(null, new object[] { language });
            Assert.AreEqual("Nastavení", Localization.Get("settings.title"));
            Assert.AreEqual("Vše", SceneBootstrapper.CatalogCategoryLabel(null));
            Assert.AreEqual("Zvířata", SceneBootstrapper.CatalogCategoryLabel("animals"));
            Assert.IsTrue(SceneBootstrapper.CatalogMatchesSearch(new Sandplay.Objects.NetworkCatalogItem
                { display_name = "Tree", category = "nature" }, "příroda"));
        }
        [Test]
        public void CzechResource_CoversLegacyKeysAndHasUniqueValidRows()
        {
            var resource = Resources.Load<TextAsset>("Localization/Czech");
            Assert.NotNull(resource);
            var keys = new HashSet<string>();
            foreach (var line in resource.text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")))
            {
                var parts = line.TrimEnd('\r').Split('|');
                Assert.AreEqual(2, parts.Length, line);
                Assert.IsTrue(keys.Add(parts[0]), "Duplicate " + parts[0]);
                if (parts[0] != "net.viewers_s") Assert.IsNotEmpty(parts[1], parts[0]);
                if (parts[0].StartsWith("text:"))
                    Assert.AreEqual(Placeholders(parts[0].Substring(5)), Placeholders(parts[1]), parts[0]);
            }
            foreach (var line in Resources.Load<TextAsset>("Localization/AdditionalLanguages").text.Split('\n'))
                if (line.StartsWith("text:")) Assert.IsTrue(keys.Contains(line.Split('|')[0]), line);
        }
        [Test]
        public void CzechFont_CoversAccentsThroughTheActualUiFallback()
        {
            Localization.SetLanguage(Language.Czech);
            var font = (TMPro.TMP_FontAsset)typeof(SceneBootstrapper).GetMethod("GetUIFont", Static).Invoke(null, null);
            Assert.NotNull(font);
            var source = Resources.Load<Font>("Fonts/LiberationSans");
            Assert.NotNull(source);
            foreach (var character in "áčďéěíňóřšťúůýžÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ")
            {
                Assert.IsTrue(source.HasCharacter(character), "Missing source glyph " + character);
                Assert.IsTrue(font.HasCharacter(character, true, true), "Missing UI glyph " + character);
            }
        }
        [Test]
        public void JapaneseFont_IsBundledAndCoversInterfaceGlyphs()
        {
            var source=Resources.Load<Font>("Fonts/NotoSansCJKjp-Regular");Assert.NotNull(source);
            var table=(Dictionary<string,string>)typeof(Localization).GetMethod("Table",Static).Invoke(null,new object[]{Language.Japanese});
            foreach(var character in string.Concat(table.Values).Distinct().Where(c=>c>=0x3000&&!char.IsSurrogate(c)))
                Assert.IsTrue(source.HasCharacter(character),"Missing Japanese glyph "+character);
        }
    }
}
