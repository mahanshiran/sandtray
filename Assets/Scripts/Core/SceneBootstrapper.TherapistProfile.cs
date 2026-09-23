using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private Button _therapistProfileButton;

        private void OpenTherapistProfile()
        {
            var backend = BackendClient.Instance;
            if (!backend.IsLoggedIn || backend.UserType != "psychologist") return;
            var box = ClientDialog(Localization.Get("Therapist profile"), 780, 800);
            var dialog = _clientDialog;
            string token = backend.AccessToken;
            int user = backend.UserId;
            bool Current() => this != null && dialog != null && _clientDialog == dialog &&
                backend == BackendClient.Instance && backend.AccessToken == token && backend.UserId == user &&
                backend.UserType == "psychologist";
            var status = ClientText(box, Localization.Get("Loading…"), 13, .05f, .115f, .90f, .07f, HomeMuted);
            status.enableWordWrapping = true;
            bool Visible() => this != null && dialog != null && _clientDialog == dialog;
            Button retry = null;
            void Load()
            {
                if (!Current()) return;
                retry.gameObject.SetActive(false);
                status.text = Localization.Get("Loading…");
                backend.LoadTherapistProfile(profile =>
                {
                    if (!Current()) return;
                    status.text = "";
                    BuildTherapistProfileForm(box, profile, status, Current);
                }, error =>
                {
                    if (!Visible()) return;
                    status.text = Localization.Get(error);
                    retry.gameObject.SetActive(Current());
                });
            }
            retry = ClientButton(box, "Retry", .55f, .025f, .40f, .075f, Load);
            Load();
        }

        private void BuildTherapistProfileForm(Transform box, TherapistProfileData data, TMP_Text status, Func<bool> current)
        {
            var content = ClientScroll(box, "TherapistProfileFields", .04f, .20f, .92f, .65f);
            var intro = ClientRow(content, "About this profile", 154);
            var note = ClientText(intro, Localization.Get("All details are optional. People in your live session can view this profile. Qualifications are self-reported and have not been verified. Use professional contact details."), 14, .025f, .05f, .95f, .90f, HomeMuted);
            note.enableWordWrapping = true;
            var readers = new List<Action>();
            bool dirty = false;
            TMP_InputField Field(string label, string value, int limit, Action<string> assign, bool multiline = false, Transform parent = null)
            {
                var row = ClientRow(parent ?? content, label, multiline ? 156 : 94);
                ClientText(row, Localization.Get(label), 14, .025f, .68f, .95f, .27f, HomeText);
                var input = ClientInput(row, value ?? "", Localization.Get("Optional"), .025f, .07f, .95f, .57f, limit);
                input.name = label;
                input.onValueChanged.AddListener(_ => dirty = true);
                if (multiline) input.lineType = TMP_InputField.LineType.MultiLineNewline;
                readers.Add(() => { if (input != null) assign(input.text.Trim()); });
                return input;
            }
            string Join(string[] values) => string.Join(", ", values ?? new string[0]);
            string[] Split(string value) => value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToArray();
            var imageRow = ClientRow(content, "Therapist photo", 136);
            var photoSlot = ClientRect(imageRow, "PhotoSlot", .025f, .05f, .20f, .90f);
            var avatar = ClientRect(photoSlot, "Photo", 0, 0, 1, 1);
            avatar.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            avatar.GetComponent<AspectRatioFitter>().aspectRatio = 1;
            SetClientAvatar(avatar, data.display_name, null);
            bool imageChanged = false, saving = false;
            if (!string.IsNullOrEmpty(data.therapistProfileImage))
                BackendClient.Instance.LoadTherapistImage(bytes =>
                { if (current() && !imageChanged) SetClientAvatar(avatar, data.display_name, bytes); },
                error => { if (current() && !imageChanged) status.text = Localization.Get(error); });
            var picker = box.gameObject.AddComponent<ClientPhotoPicker>();
            ClientButton(imageRow, "Choose photo", .27f, .53f, .70f, .39f, () => picker.Pick(bytes =>
            {
                if (!current() || saving) return;
                dirty = true; imageChanged = true; data.image_action = "replace"; data.image_data = Convert.ToBase64String(bytes);
                SetClientAvatar(avatar, data.display_name, bytes);
            }, () => { if (current()) status.text = Localization.Get("Unable to read the photo."); }, Localization.Get("Choose therapist photo")));
            ClientButton(imageRow, "Remove photo", .27f, .07f, .70f, .39f, () =>
            {
                dirty = true; imageChanged = true; data.image_action = "remove"; data.image_data = "";
                SetClientAvatar(avatar, data.display_name, null);
            });
            Field("Professional display name", data.display_name, 150, v => data.display_name = v);
            Field("Professional title", data.professional_title, 150, v => data.professional_title = v);
            Field("About your practice", data.bio, 5000, v => data.bio = v, true);
            Field("Practice name", data.practice_name, 200, v => data.practice_name = v);
            Field("Professional email", data.professional_email, 254, v => data.professional_email = v);
            Field("Professional phone (include country code)", data.professional_phone, 50, v => data.professional_phone = v);
            Field("Website (https://…)", data.website, 200, v => data.website = v);
            Field("Country code (for example CN, US, GB)", data.country, 2, v => data.country = v.ToUpperInvariant());
            Field("State / province / region", data.region, 100, v => data.region = v);
            Field("City", data.city, 100, v => data.city = v);
            Field("Time zone (for example Asia/Shanghai)", data.timezone, 100, v => data.timezone = v);
            Field("Languages (comma separated)", Join(data.languages), 1000, v => data.languages = Split(v));
            Field("Areas of practice (comma separated)", Join(data.specialties), 2000, v => data.specialties = Split(v));
            Field("Therapeutic approaches (comma separated)", Join(data.approaches), 2000, v => data.approaches = Split(v));
            Field("Age groups (comma separated)", Join(data.age_groups), 500, v => data.age_groups = Split(v));
            var formats = new HashSet<string>(data.session_formats ?? new string[0]);
            foreach (var pair in new[] { ("in_person", "In person"), ("online", "Online"), ("phone", "Phone") })
            {
                var row = ClientRow(content, pair.Item2, 60);
                Button button = null;
                void Refresh() => button.GetComponentInChildren<TMP_Text>().text = (formats.Contains(pair.Item1) ? "✓ " : "○ ") + Localization.Get(pair.Item2);
                button = ClientButton(row, pair.Item2, .025f, .08f, .95f, .84f, () =>
                { if (!formats.Add(pair.Item1)) formats.Remove(pair.Item1); dirty = true; Refresh(); });
                Refresh();
            }
            Field("Accessibility and accommodations", data.accessibility, 2000, v => data.accessibility = v, true);
            Field("Fees, currency and payment information", data.fees_information, 2000, v => data.fees_information = v, true);
            BuildCertificateEditor(content, current);
            var qualifications = new List<TherapistQualification>(data.qualifications ?? new TherapistQualification[0]);
            var qualificationContent = new GameObject("Qualification entries", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            qualificationContent.transform.SetParent(content, false);
            var layout = qualificationContent.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8; layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false;
            qualificationContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            void AddQualification(TherapistQualification q)
            {
                var group = new GameObject("Qualification", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                group.transform.SetParent(qualificationContent.transform, false);
                var l = group.GetComponent<VerticalLayoutGroup>(); l.spacing = 5; l.childControlHeight = l.childControlWidth = true; l.childForceExpandHeight = false;
                group.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                Field("Qualification / licence / registration", q.title, 200, v => q.title = v, parent: group.transform);
                Field("Training institution", q.institution, 200, v => q.institution = v, parent: group.transform);
                Field("Issuing or regulatory body", q.issuing_body, 200, v => q.issuing_body = v, parent: group.transform);
                Field("Registration / membership number", q.registration_number, 100, v => q.registration_number = v, parent: group.transform);
                Field("Credential country code", q.country, 2, v => q.country = v.ToUpperInvariant(), parent: group.transform);
                Field("Credential state / province / region", q.region, 100, v => q.region = v, parent: group.transform);
                Field("Valid from (YYYY-MM-DD)", q.valid_from, 10, v => q.valid_from = v, parent: group.transform);
                Field("Valid until (YYYY-MM-DD)", q.valid_until, 10, v => q.valid_until = v, parent: group.transform);
                var remove = ClientRow(group.transform, "Remove qualification", 60);
                ClientButton(remove, "Remove qualification", .025f, .08f, .95f, .84f, () =>
                { dirty = true; qualifications.Remove(q); group.SetActive(false); if (Application.isPlaying) Destroy(group); else DestroyImmediate(group); });
            }
            foreach (var q in qualifications) AddQualification(q);
            var add = ClientRow(content, "Add qualification", 60);
            ClientButton(add, "Add qualification or registration", .025f, .08f, .95f, .84f, () =>
            {
                if (qualifications.Count >= 20) { status.text = Localization.Get("You can add up to 20 qualifications."); return; }
                var q = new TherapistQualification(); qualifications.Add(q); AddQualification(q); dirty = true;
                Canvas.ForceUpdateCanvases();
                var profileScroll = content.parent.GetComponent<ScrollRect>();
                if (profileScroll != null) profileScroll.verticalNormalizedPosition = 0;
            });
            var formState = content.gameObject.AddComponent<CanvasGroup>();
            Button save = null;
            GameObject confirmation = null;
            void RequestClose()
            {
                if (saving || confirmation != null) return;
                if (!dirty) { CloseClientDialog(); return; }
                confirmation = ClientRect(box, "DiscardConfirmation", 0, 0, 1, 1).gameObject;
                confirmation.AddComponent<Image>().color = HomeCard;
                formState.interactable = false; save.interactable = false;
                var prompt = ClientText(confirmation.transform, Localization.Get("Discard unsaved changes?"), 20, .07f,.52f,.86f,.22f, HomeText);
                prompt.enableWordWrapping = true;
                var keep = ClientButton(confirmation.transform, "Keep editing", .07f,.30f,.86f,.09f, () =>
                {
                    confirmation.SetActive(false); if (Application.isPlaying) Destroy(confirmation); else DestroyImmediate(confirmation); confirmation = null;
                    formState.interactable = true; save.interactable = true;
                });
                ClientButton(confirmation.transform, "Discard changes", .07f,.16f,.86f,.09f, CloseClientDialog);
                keep.Select();
            }
            var close = box.Find("Close").GetComponent<Button>();
            close.onClick.RemoveAllListeners(); close.onClick.AddListener(RequestClose);
            ClientButton(box, "dialog.cancel", .05f, .025f, .40f, .075f, RequestClose);
            save = ClientButton(box, "Save profile", .55f, .025f, .40f, .075f, () =>
            {
                if (saving || !current()) return;
                foreach (var read in readers) read();
                data.qualifications = qualifications.ToArray(); data.session_formats = formats.ToArray();
                saving = true; formState.interactable = false; save.interactable = false; status.text = Localization.Get("Saving…");
                BackendClient.Instance.SaveTherapistProfile(data, saved =>
                {
                    if (!current()) return;
                    saving = false; formState.interactable = true; save.interactable = true;
                    dirty = false; data.image_action = "keep"; data.image_data = "";
                    status.text = Localization.Get("Therapist profile saved.");
                }, error => { if (box != null) { saving = false; formState.interactable = current(); save.interactable = true; status.text = Localization.Get(error); } });
            }, true);
        }
    }
}
