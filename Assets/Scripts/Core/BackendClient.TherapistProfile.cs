using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Sandplay.Core
{
    [Serializable]
    public class TherapistQualification
    {
        public string title = "", institution = "", registration_number = "", issuing_body = "";
        public string country = "", region = "", valid_from = "", valid_until = "";
    }

    [Serializable]
    public class TherapistProfileData
    {
        public string display_name = "", professional_title = "", bio = "", practice_name = "";
        public string professional_email = "", professional_phone = "", website = "";
        public string country = "", region = "", city = "", timezone = "";
        public string[] languages = new string[0], specialties = new string[0], approaches = new string[0];
        public string[] age_groups = new string[0], session_formats = new string[0];
        public string accessibility = "", fees_information = "", therapistProfileImage = "";
        public string credential_status = "", updated_at = "";
        public TherapistCertificateData[] certificates = new TherapistCertificateData[0];
        public TherapistQualification[] qualifications = new TherapistQualification[0];
        public string image_action = "keep", image_data = "";
    }

    public partial class BackendClient
    {
        public void LoadTherapistProfile(Action<TherapistProfileData> success, Action<string> failure) =>
            StartCoroutine(TherapistProfileRequest(null, success, failure));

        public void SaveTherapistProfile(TherapistProfileData data, Action<TherapistProfileData> success, Action<string> failure) =>
            StartCoroutine(TherapistProfileRequest(JsonUtility.ToJson(data), success, failure));

        private IEnumerator TherapistProfileRequest(string body, Action<TherapistProfileData> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "psychologist") { failure("Sign in with a therapist account."); yield break; }
            var current = CaptureCredentialGuard();
            using var request = new UnityWebRequest(BaseUrl + "/auth/therapist-profile/", body == null ? "GET" : "PATCH");
            request.timeout = 30;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;
            yield return request.SendWebRequest();
            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;
            if (!current() || UserType != "psychologist") { failure("Account changed. Reopen your therapist profile."); yield break; }
            if (request.result != UnityWebRequest.Result.Success)
            {
                failure(request.responseCode == 401 ? "Your session expired. Please sign in again." :
                    request.responseCode == 404 ? "Therapist profiles are not available on this server yet." :
                    request.responseCode == 400 ? ExtractError(request.downloadHandler.text, request.responseCode) : "Unable to load or save the therapist profile. Please retry.");
                yield break;
            }
            TherapistProfileData profile = null;
            try { profile = JsonUtility.FromJson<TherapistProfileData>(request.downloadHandler.text); }
            catch (ArgumentException) { }
            if (profile == null) { failure("The server returned an invalid profile."); yield break; }
            if (body != null) Sandplay.UI.AccountAvatar.RefreshPhotos();
            success(profile);
        }

        public void LoadTherapistImage(Action<byte[]> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "psychologist") { failure?.Invoke("Sign in with a therapist account."); return; }
            var guard = CaptureCredentialGuard();
            int epoch = Sandplay.Data.LocalAccountStorage.Epoch;
            ProfileImageCache.Shared.Get(ProfileImageCache.AccountScope, "therapist-image",
                complete => StartCoroutine(TherapistImageRequest(complete)), bytes =>
                {
                    if (!guard() || epoch != Sandplay.Data.LocalAccountStorage.Epoch) return;
                    if (bytes != null) success?.Invoke(bytes); else failure?.Invoke("Unable to load the profile image.");
                });
        }

        private IEnumerator TherapistImageRequest(Action<byte[], double> complete)
        {
            if (!IsLoggedIn || UserType != "psychologist") yield break;
            var current = CaptureCredentialGuard();
            using var request = UnityWebRequest.Get(BaseUrl + "/auth/therapist-profile/image/");
            request.timeout = 30;
            request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            var requestGuard = CaptureCredentialGuard();
            int requestEpoch = Sandplay.Data.LocalAccountStorage.Epoch;
            yield return request.SendWebRequest();
            if (requestEpoch != Sandplay.Data.LocalAccountStorage.Epoch || !requestGuard()) yield break;
            if (!current() || UserType != "psychologist") yield break;
            if (request.result == UnityWebRequest.Result.Success)
                complete(request.downloadHandler.data, ProfileImageCache.Lifetime(request.GetResponseHeader("Cache-Control"), request.GetResponseHeader("Age")));
            else complete(null, 30);
        }
    }
}
