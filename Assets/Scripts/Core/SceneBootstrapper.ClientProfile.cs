using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Data;
using Sandplay.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private CultureInfo ClientDateCulture => Localization.Culture;

        private string ClientDateLabel(string iso)
        {
            return DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)
                ? date.ToString("d", ClientDateCulture)
                : Localization.Get("clients.choose_date");
        }

        private Transform ClientRecordAvatar(Transform parent, ClientRecord client,
            float x, float y, float w, float h)
        {
            var square = ClientAvatar(parent, client?.Name, client?.PhotoFile, x, y, w, h);
            var photo = square.GetComponentInChildren<RawImage>();
            var link = client?.Account;
            if (photo.enabled || client?.AccountPhotoSuppressed == true || link == null ||
                link.UserId <= 0 || link.Backend != BackendClient.BaseUrl || !BackendClient.Instance.IsLoggedIn)
                return square;
            string scope = ProfileImageCache.AccountScope;
            bool Current() => this != null && square != null && photo != null &&
                square.gameObject.activeInHierarchy && scope == ProfileImageCache.AccountScope;
            void ShowPerson(FriendPerson person)
            {
                if (!Current() || person == null || person.id != link.UserId) return;
                AccountAvatar.LoadPublicPhoto(person.avatar_url, bytes =>
                {
                    if (!Current() || bytes == null) return;
                    // Updating the existing view cannot write or replace a therapist's local photo.
                    var owner = photo.GetComponent<ClientAvatarTexture>();
                    bool loaded = false;
                    try { loaded = owner.Set(photo, bytes); } catch (Exception) { }
                    photo.enabled = loaded;
                    if (loaded)
                    {
                        float aspect = (float)photo.texture.width / photo.texture.height;
                        photo.uvRect = aspect > 1 ? new Rect((1 - 1 / aspect) / 2, 0, 1 / aspect, 1)
                            : new Rect(0, (1 - aspect) / 2, 1, aspect);
                        foreach (var label in square.GetComponentsInChildren<TMP_Text>()) label.gameObject.SetActive(false);
                    }
                });
            }
            var service = FriendsClient.Instance;
            service.EnsureAccount();
            var known = Array.Find(service.State?.people ?? new FriendPerson[0], p => p != null && p.id == link.UserId);
            if (known != null && !string.IsNullOrEmpty(known.avatar_url)) ShowPerson(known);
            else if (!string.IsNullOrWhiteSpace(link.AccountCode))
            {
                // Older linked records have no saved photo. Resolve by account identity, never by name.
                ProfileImageCache.Shared.Get(scope, "client-account-lookup:" + link.UserId + ":" + link.AccountCode,
                    complete => service.Request<FriendPerson>("search/?code=" + Uri.EscapeDataString(link.AccountCode), null,
                        person => complete(person != null && person.id == link.UserId
                            ? System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(person)) : null, 300),
                        error => complete(null, 30)), bytes =>
                    {
                        if (!Current() || bytes == null) return;
                        ShowPerson(JsonUtility.FromJson<FriendPerson>(System.Text.Encoding.UTF8.GetString(bytes)));
                    });
            }
            return square;
        }

        private Transform ClientAvatar(Transform parent, string name, string photoFile,
            float x, float y, float w, float h)
        {
            var outer = ClientRect(parent, "Avatar", x, y, w, h);
            var square = ClientRect(outer, "Square", 0, 0, 1, 1);
            var aspect = square.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1;
            byte[] bytes = null;
            try { bytes = ClientStore.ReadPhoto(photoFile); } catch (Exception) { /* initials fallback */ }
            SetClientAvatar(square, name, bytes);
            return square;
        }

        private void SetClientAvatar(Transform square, string name, byte[] bytes)
        {
            ClearClientChildren(square);
            var background = ClientRect(square, "Background", 0, 0, 1, 1).gameObject.AddComponent<Image>();
            background.color = HomeIsLight ? new Color(.90f, .94f, .94f, 1f) : HomeCard;
            ApplyHomeRoundedCorners(background, 10f);
            background.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var raw = ClientRect(background.transform, "Photo", 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var owner = raw.gameObject.AddComponent<ClientAvatarTexture>();
            bool loaded = false;
            try { loaded = owner.Set(raw, bytes); } catch (Exception) { }
            raw.enabled = loaded;
            if (!loaded)
            {
                string initials = "?";
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var words = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    initials = StringInfo.GetNextTextElement(words[0]);
                    if (words.Length > 1) initials += StringInfo.GetNextTextElement(words[words.Length - 1]);
                }
                if (!string.IsNullOrEmpty(initials))
                {
                    var text = ClientText(background.transform, initials.ToUpperInvariant(), 22, .05f, .05f, .9f, .9f,
                        HomeIsLight ? HomePrimary : Color.white);
                    text.alignment = TextAlignmentOptions.Center;
                }
            }
        }

        private void SetClientPhotoOverlay(Transform avatar, bool hasPhoto)
        {
            if (avatar == null) return;
            var old = avatar.Find("PhotoOverlay");
            if (old != null)
            {
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            if (!hasPhoto)
                foreach (var text in avatar.GetComponentsInChildren<TMP_Text>(true))
                    text.gameObject.SetActive(false);
            var overlay = ClientRect(avatar, "PhotoOverlay", 0, 0, 1, .28f);
            var image = overlay.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, .10f);
            image.raycastTarget = false;
            var label = ClientText(overlay, hasPhoto ? F("Change", "更换") : F("Select", "选择"),
                12, 0, 0, 1, 1, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
        }

        // Nested modal preserves the underlying edit form and only commits on Done.
        private void ShowClientDatePicker(Transform parent, string current, Action<string> onSelected)
        {
            ShowDatePicker(parent, current, Localization.Get("clients.dob"), DateTime.MinValue.Date,
                DateTime.Today, true, onSelected);
        }

        private void ShowDatePicker(Transform parent, string current, string title, DateTime minimum,
            DateTime maximum, bool allowClear, Action<string> onSelected)
        {
            DateTime? selected = DateTime.TryParseExact(current, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) ? parsed : (DateTime?)null;
            minimum = minimum.Date;
            maximum = maximum.Date;
            if (maximum < minimum) maximum = minimum;
            if (selected.HasValue && (selected.Value.Date < minimum || selected.Value.Date > maximum))
                selected = null;
            DateTime month = selected ?? (DateTime.Today < minimum ? minimum : DateTime.Today > maximum ? maximum : DateTime.Today);
            month = new DateTime(month.Year, month.Month, 1);
            var overlay = ClientRect(parent, "DatePicker", 0, 0, 1, 1);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, HomeIsLight ? .42f : .72f);
            Sandplay.UI.DialogBackdrop.Apply(overlay.gameObject.GetComponent<Image>());
            var card = ClientRect(overlay, "Calendar", .5f, .5f, 0, 0);
            var available = ((RectTransform)parent).rect.size;
            card.sizeDelta = new Vector2(Mathf.Min(480, available.x - 24), Mathf.Min(550, available.y - 24));
            card.gameObject.AddComponent<Image>().color = HomeCard;
            ApplyHomeRoundedCorners(card.GetComponent<Image>(), 14f);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1,-1);
            void Close()
            {
                overlay.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject);
            }
            ClientText(card, title, 22, .06f, .87f, .7f, .09f, HomeText);
            ClientButton(card, "×", .84f, .88f, .1f, .075f, Close);
            var picked = ClientText(card, "", 15, .06f, .80f, .88f, .06f, HomeMuted);
            var header = ClientRect(card, "Navigation", .06f, .685f, .88f, .085f);
            var grid = ClientRect(card, "Days", .06f, .18f, .88f, .47f);
            int mode = 0; // 0 days, 1 months, 2 years
            int firstYear = Math.Max(1, month.Year - 11);
            void Render()
            {
                ClearClientChildren(header);
                ClearClientChildren(grid);
                picked.text = selected.HasValue ? ClientDateLabel(selected.Value.ToString("yyyy-MM-dd")) : Localization.Get("clients.no_date");
                var prev = ClientButton(header, "‹", 0, 0, .12f, 1, () =>
                {
                    if (mode == 2) firstYear = Math.Max(1, firstYear - 12);
                    else if (mode == 1 && month.Year > 1) month = month.AddYears(-1);
                    else if (mode == 0 && month > DateTime.MinValue) month = month.AddMonths(-1);
                    Render();
                });
                var next = ClientButton(header, "›", .88f, 0, .12f, 1, () =>
                {
                    if (mode == 2) firstYear += 12;
                    else if (mode == 1) month = month.AddYears(1);
                    else month = month.AddMonths(1);
                    Render();
                });
                DateTime minimumMonth = new DateTime(minimum.Year, minimum.Month, 1);
                DateTime maximumMonth = new DateTime(maximum.Year, maximum.Month, 1);
                prev.interactable = mode == 2 ? firstYear > minimum.Year
                    : mode == 1 ? month.Year > minimum.Year : month > minimumMonth;
                next.interactable = mode == 2 ? firstYear + 11 < maximum.Year
                    : mode == 1 ? month.Year < maximum.Year : month < maximumMonth;
                if (mode == 0)
                {
                    ClientButton(header, month.ToString("MMMM", ClientDateCulture), .15f, 0, .40f, 1, () => { mode = 1; Render(); });
                    ClientButton(header, month.Year.ToString(), .58f, 0, .27f, 1, () => { mode = 2; firstYear = Math.Max(1, month.Year - 11); Render(); });
                    var weekdays = new string[7];
                    for (int i = 0; i < 7; i++) weekdays[i] = ClientDateCulture.DateTimeFormat.GetShortestDayName((DayOfWeek)((i+1)%7));
                    for (int i = 0; i < 7; i++)
                        ClientText(grid, weekdays[i], 12, i / 7f, .87f, 1 / 7f, .12f, HomeMuted).alignment = TextAlignmentOptions.Center;
                    int offset = ((int)month.DayOfWeek + 6) % 7;
                    for (int day = 1; day <= DateTime.DaysInMonth(month.Year, month.Month); day++)
                    {
                        var date = new DateTime(month.Year, month.Month, day);
                        int index = offset + day - 1;
                        var button = ClientButton(grid, day.ToString(), (index % 7) / 7f + .006f,
                            .72f - (index / 7) * .142f, 1 / 7f - .012f, .13f,
                            () => { selected = date; Render(); }, selected?.Date == date);
                        button.interactable = date >= minimum && date <= maximum;
                    }
                }
                else
                {
                    ClientButton(header, mode == 1 ? month.Year.ToString() : firstYear + " – " + (firstYear + 11),
                        .15f, 0, .70f, 1, () => { mode = mode == 1 ? 2 : 0; Render(); });
                    for (int i = 0; i < 12; i++)
                    {
                        int value = mode == 1 ? i + 1 : firstYear + i;
                        bool months = mode == 1;
                        string label = months ? new DateTime(2000, value, 1).ToString("MMM", ClientDateCulture) : value.ToString();
                        var button = ClientButton(grid, label, (i % 3) / 3f + .012f, .76f - (i / 3) * .25f,
                            .31f, .22f, () =>
                            {
                                month = months ? new DateTime(month.Year, value, 1) : new DateTime(value, month.Month, 1);
                                if (month < minimumMonth) month = minimumMonth;
                                if (month > maximumMonth) month = maximumMonth;
                                mode = months ? 0 : 1;
                                Render();
                            });
                        if (months)
                        {
                            var candidate = new DateTime(month.Year, value, 1);
                            button.interactable = candidate <= maximumMonth && candidate.AddMonths(1).AddDays(-1) >= minimum;
                        }
                        else button.interactable = value >= minimum.Year && value <= maximum.Year;
                    }
                }
            }
            if (allowClear) ClientButton(card, "clients.clear_date", .06f, .05f, .24f, .085f,
                () => { selected = null; Render(); });
            ClientButton(card, "dialog.cancel", allowClear ? .37f : .06f, .05f, allowClear ? .25f : .42f, .085f, Close);
            ClientButton(card, "clients.done", allowClear ? .65f : .52f, .05f, allowClear ? .29f : .42f, .085f, () =>
            {
                onSelected(selected?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
                Close();
            }, true);
            Render();
        }
    }
}
