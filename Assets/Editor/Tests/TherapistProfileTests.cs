using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class TherapistProfileTests
    {
        [Test]
        public void ProfileTranslationsCoverEverySupportedLanguage()
        {
            var original = Localization.Current;
            try
            {
                var rows = Resources.Load<TextAsset>("Localization/TherapistProfile").text.Split('\n')
                    .Where(r => !string.IsNullOrWhiteSpace(r) && !r.StartsWith("#")).Select(r => r.TrimEnd('\r').Split('|')).ToArray();
                foreach (var row in rows)
                {
                    Assert.AreEqual(8, row.Length, row[0]);
                    Assert.IsTrue(row.All(v => !string.IsNullOrWhiteSpace(v)), row[0]);
                    for (int i = 0; i < 8; i++)
                    {
                        Localization.SetLanguage((Language)i);
                        Assert.AreEqual(row[i], Localization.Get(row[0]));
                    }
                }
            }
            finally { Localization.SetLanguage(original); }
        }

        [Test]
        public void ProfileRoundTripPreservesOptionalDetailsAndMultipleJurisdictions()
        {
            var data = new TherapistProfileData { display_name = "Therapist", image_action = "keep",
                qualifications = new[] { new TherapistQualification { country = "CN" }, new TherapistQualification { country = "GB" } },
                languages = new[] { "zh", "en" } };
            var copy = JsonUtility.FromJson<TherapistProfileData>(JsonUtility.ToJson(data));
            Assert.AreEqual(2, copy.qualifications.Length);
            Assert.AreEqual("CN", copy.qualifications[0].country);
            Assert.AreEqual("", copy.qualifications[0].valid_from);
            Assert.AreEqual("keep", copy.image_action);
        }

        [Test]
        public void EditorBuildsScrollableOptionalFieldsAndAddsQualification()
        {
            var canvas = new GameObject("Profile test canvas", typeof(RectTransform), typeof(Canvas));
            var bootstrap = new GameObject("Profile bootstrap");
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(390, 844);
                var scene = bootstrap.AddComponent<SceneBootstrapper>();
                typeof(SceneBootstrapper).GetField("_safeArea", flags).SetValue(scene, canvas);
                var box = (Transform)typeof(SceneBootstrapper).GetMethod("ClientDialog", flags)
                    .Invoke(scene, new object[] { "Therapist profile", 780f, 800f });
                var status = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
                status.transform.SetParent(box, false);
                typeof(SceneBootstrapper).GetMethod("BuildTherapistProfileForm", flags)
                    .Invoke(scene, new object[] { box, new TherapistProfileData(), status.GetComponent<TMP_Text>(), (Func<bool>)(() => true) });
                Assert.IsNotNull(box.GetComponentInChildren<ScrollRect>());
                var inputs = box.GetComponentsInChildren<TMP_InputField>();
                Assert.AreEqual(17, inputs.Length);
                Assert.IsTrue(inputs.All(input => input.text == ""));
                var add = box.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>().text == "Add qualification or registration");
                add.onClick.Invoke();
                Assert.AreEqual(inputs.Length + 8, box.GetComponentsInChildren<TMP_InputField>().Length);
                Canvas.ForceUpdateCanvases();
                var field = box.GetComponentsInChildren<TMP_InputField>().First();
                Assert.GreaterOrEqual(((RectTransform)field.transform).rect.height, 48);
                Assert.IsTrue(box.GetComponentsInChildren<Button>().Any(b => b.GetComponentInChildren<TMP_Text>().text == "Save profile"));
                var slot = box.GetComponentsInChildren<RectTransform>().Single(r => r.name == "PhotoSlot");
                var photo = (RectTransform)slot.Find("Photo");
                Assert.LessOrEqual(photo.rect.width, slot.rect.width + .1f);
                Assert.LessOrEqual(photo.rect.height, slot.rect.height + .1f);
                box.Find("Close").GetComponent<Button>().onClick.Invoke();
                Assert.IsNotNull(box.Find("DiscardConfirmation"));
                box.Find("DiscardConfirmation").GetComponentsInChildren<Button>()
                    .Single(b => b.GetComponentInChildren<TMP_Text>().text == Localization.Get("Keep editing")).onClick.Invoke();
                Assert.IsNull(box.Find("DiscardConfirmation"));
                box.Find("Close").GetComponent<Button>().onClick.Invoke();
                box.Find("DiscardConfirmation").GetComponentsInChildren<Button>()
                    .Single(b => b.GetComponentInChildren<TMP_Text>().text == Localization.Get("Discard changes")).onClick.Invoke();
                Assert.IsNull(typeof(SceneBootstrapper).GetField("_clientDialog", flags).GetValue(scene));
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); UnityEngine.Object.DestroyImmediate(bootstrap); }
        }
    }
}
