using NUnit.Framework;
using Sandplay.Core;
namespace Sandplay.Tests
{
    public class SocialLoginTests
    {
        [Test] public void Avatar_OnlyAllowsGoogleHttpsPhotos()
        {
            Assert.IsTrue(Sandplay.UI.AccountAvatar.IsGooglePhoto("https://lh3.googleusercontent.com/photo"));
            Assert.IsFalse(Sandplay.UI.AccountAvatar.IsGooglePhoto("http://lh3.googleusercontent.com/photo"));
            Assert.IsFalse(Sandplay.UI.AccountAvatar.IsGooglePhoto("https://googleusercontent.com.evil.com/photo"));
            Assert.IsFalse(Sandplay.UI.AccountAvatar.IsGooglePhoto("https://user@lh3.googleusercontent.com/photo"));
            Assert.IsFalse(Sandplay.UI.AccountAvatar.IsGooglePhoto(null));
        }
        [Test] public void SocialButtons_AreLogoOnlySquaresInOneRow()
        {
            var root = new UnityEngine.GameObject("SocialTest", typeof(UnityEngine.RectTransform));
            try
            {
                var factory = typeof(Sandplay.UI.LoginScreen).GetMethod("MakeSocialIconButton",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                foreach (var provider in new[] { "Google", "Apple" })
                {
                    float x = provider == "Google" ? -32f : 32f;
                    var button = (UnityEngine.UI.Button)factory.Invoke(null,
                        new object[] { provider + "SignIn", root.transform, provider, x });
                    var rt = button.GetComponent<UnityEngine.RectTransform>();
                    Assert.AreEqual(new UnityEngine.Vector2(48, 48), rt.sizeDelta);
                    Assert.AreEqual(new UnityEngine.Vector2(.5f, .13f), rt.anchorMin);
                    Assert.AreEqual(rt.anchorMin, rt.anchorMax);
                    Assert.AreEqual(new UnityEngine.Vector2(x, 0), rt.anchoredPosition);
                    Assert.IsNull(button.GetComponentInChildren<TMPro.TMP_Text>());
                    var image = button.GetComponent<UnityEngine.UI.RawImage>();
                    Assert.IsNotNull(image.texture, provider + " artwork must be bundled");
                    Assert.AreEqual(image.texture.width, image.texture.height);
                    Assert.AreSame(image, button.targetGraphic);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test] public void GoogleAuthorizationUrl_AllowsOnlyExpectedHttpsEndpoint()
        {
            Assert.IsTrue(BackendClient.IsValidSocialAuthorizationUrl("google", "https://accounts.google.com/o/oauth2/v2/auth?state=test"));
            Assert.IsFalse(BackendClient.IsValidSocialAuthorizationUrl("google", "http://accounts.google.com/o/oauth2/v2/auth"));
            Assert.IsFalse(BackendClient.IsValidSocialAuthorizationUrl("google", "https://accounts.google.com.evil.example/o/oauth2/v2/auth"));
            Assert.IsFalse(BackendClient.IsValidSocialAuthorizationUrl("google", "https://evil@accounts.google.com/o/oauth2/v2/auth"));
            Assert.IsFalse(BackendClient.IsValidSocialAuthorizationUrl("google", "javascript:alert(1)"));
        }
        [Test] public void AppleAuthorizationUrl_RejectsOtherProvidersAndPaths()
        {
            Assert.IsTrue(BackendClient.IsValidSocialAuthorizationUrl("apple", "https://appleid.apple.com/auth/authorize?state=test"));
            Assert.IsFalse(BackendClient.IsValidSocialAuthorizationUrl("apple", "https://appleid.apple.com/other"));
            Assert.IsFalse(BackendClient.IsValidSocialAuthorizationUrl("apple", "https://accounts.google.com/o/oauth2/v2/auth"));
            Assert.IsFalse(BackendClient.IsValidSocialAuthorizationUrl("other", "https://appleid.apple.com/auth/authorize"));
            Assert.IsFalse(BackendClient.IsValidSocialAuthorizationUrl("apple", null));
        }
    }
}
