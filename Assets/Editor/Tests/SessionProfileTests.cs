using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;
namespace Sandplay.Tests
{
    public class SessionProfileTests
    {
        [Test] public void PortraitShowsEmbeddedImageCroppedAndHidesInitials()
        {
            var root = new UnityEngine.GameObject("Avatar", typeof(UnityEngine.RectTransform));
            var source = new UnityEngine.Texture2D(8, 4);
            try
            {
                var text = new UnityEngine.GameObject("Initials", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
                text.transform.SetParent(root.transform, false);
                var profile = new SessionProfile { image_data = Convert.ToBase64String(UnityEngine.ImageConversion.EncodeToPNG(source)) };
                var load = Sandplay.UI.SessionAvatars.Photo(root.transform, text.GetComponent<TMPro.TMP_Text>(), profile);
                Assert.IsFalse(load.MoveNext());
                var photo = root.transform.Find("Photo").GetComponent<UnityEngine.UI.RawImage>();
                Assert.AreEqual(8, photo.texture.width);
                Assert.AreEqual(new UnityEngine.Rect(.25f, 0, .5f, 1), photo.uvRect);
                Assert.IsFalse(text.activeSelf);
                Assert.IsFalse(photo.raycastTarget);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test] public void PortraitTapOpensCurrentPersonAndIgnoresMissingPerson()
        {
            var root = new UnityEngine.GameObject("Avatar", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            try
            {
                var binding = root.AddComponent<Sandplay.UI.SessionAvatarBinding>();
                var person = new SessionProfile { token = "guest", name = "Alex" };
                SessionProfile opened = null;
                binding.Resolve = () => person;
                binding.Open = p => opened = p;
                binding.Initialize();
                var button = root.GetComponent<UnityEngine.UI.Button>();
                button.onClick.Invoke();
                Assert.AreSame(person, opened);
                person = new SessionProfile { token = "other", name = "Sam" };
                button.onClick.Invoke();
                Assert.AreSame(person, opened);
                person = null; opened = null;
                button.onClick.Invoke();
                Assert.IsNull(opened);
                Assert.IsNotNull(root.GetComponent<UnityEngine.UI.Mask>());
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test] public void ReadOnlyProjectionOmitsEmptyFieldsAndNormalAccountCredentials()
        {
            var profile=new SessionProfile { name="Alex", user_type="psychologist", therapist=new TherapistProfileData {
                professional_title="Counsellor", bio="  ", city="null", languages=new[]{"English","",null},
                qualifications=new[]{new TherapistQualification(),new TherapistQualification { title="Diploma",country="CN" }} } };
            var method=typeof(SceneBootstrapper).GetMethod("SessionProfileFields",BindingFlags.NonPublic|BindingFlags.Static);
            var fields=(List<KeyValuePair<string,string>>)method.Invoke(null,new object[]{profile});
            Assert.AreEqual(5,fields.Count);
            Assert.IsTrue(fields.Exists(f=>f.Value=="English"));
            Assert.IsFalse(fields.Exists(f=>string.IsNullOrWhiteSpace(f.Value)||f.Value=="null"));
            profile.user_type="normal";
            fields=(List<KeyValuePair<string,string>>)method.Invoke(null,new object[]{profile});
            Assert.AreEqual(1,fields.Count);Assert.AreEqual("Alex",fields[0].Value);
        }
    }
}
