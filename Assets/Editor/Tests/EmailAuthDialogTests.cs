using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sandplay.UI;
using Sandplay.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sandplay.Tests
{
    public class EmailAuthDialogTests
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject root;
        LoginScreen screen;
        [SetUp] public void SetUp()
        {
            root=new GameObject("EmailTest",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,800);
            screen=root.AddComponent<LoginScreen>();
            var source=new GameObject("SourceEmail",typeof(RectTransform)).AddComponent<TMP_InputField>();
            source.transform.SetParent(root.transform);source.text="owner@example.test";
            typeof(LoginScreen).GetField("_emailField",Flags).SetValue(screen,source);
        }
        [TearDown] public void TearDown()=>Object.DestroyImmediate(root);
        void Open(bool reset)=>typeof(LoginScreen).GetMethod("ShowEmailFlow",Flags).Invoke(screen,new object[]{reset});
        GameObject Overlay=>(GameObject)typeof(LoginScreen).GetField("_emailFlow",Flags).GetValue(screen);
        [Test] public void VerificationHasEmailAndCodeAndCanReopen()
        {
            Open(false);var first=Overlay;Open(true);Assert.AreSame(first,Overlay);
            var fields=Overlay.GetComponentsInChildren<TMP_InputField>();Assert.AreEqual(2,fields.Length);
            Assert.AreEqual("owner@example.test",fields[0].text);Assert.AreEqual(6,fields[1].characterLimit);
            Overlay.GetComponentsInChildren<Button>().Single(b=>b.name=="Close").onClick.Invoke();
            Assert.IsNull(Overlay);Open(false);Assert.IsNotNull(Overlay);
        }
        [Test] public void ResetMasksPasswordsAndRejectsMismatchBeforeNetwork()
        {
            Open(true);var fields=Overlay.GetComponentsInChildren<TMP_InputField>();Assert.AreEqual(4,fields.Length);
            Assert.AreEqual(TMP_InputField.InputType.Password,fields[2].inputType);
            Assert.AreEqual(TMP_InputField.InputType.Password,fields[3].inputType);
            fields[2].text="Different!123";fields[3].text="Mismatch!123";
            Overlay.GetComponentsInChildren<Button>().Single(b=>b.name=="Confirm").onClick.Invoke();
            Assert.AreEqual(FriendsClient.Text("Passwords must match.","两次输入的密码必须一致。"),Overlay.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Status").text);
        }
    }
}
