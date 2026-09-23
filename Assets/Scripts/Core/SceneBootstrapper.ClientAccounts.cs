using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        // A nested picker keeps all unsaved therapist fields intact.
        private void ShowClientAccountPicker(Transform parent, bool friends, Action<FriendPerson> selected)
        {
            if(friends && !BackendClient.Instance.ExternalContactsAllowed)return;
            var overlay = ClientRect(parent, "ClientAccountPicker", 0, 0, 1, 1);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .72f);
            Sandplay.UI.DialogBackdrop.Apply(overlay.GetComponent<Image>());
            var box = ClientRect(overlay, "Card", .5f, .5f, 0, 0);
            var available = ((RectTransform)parent).rect.size;
            box.sizeDelta = new Vector2(Mathf.Min(620, available.x - 48), Mathf.Min(friends ? 308 : 560, available.y - 48));
            box.gameObject.AddComponent<Image>().color = HomeCard;
            ApplyHomeRoundedCorners(box.GetComponent<Image>(), 14f);
            var service = FriendsClient.Instance;
            service.EnsureAccount();
            int epoch = Sandplay.Data.LocalAccountStorage.Epoch;
            int generation = service.Generation;
            bool Current() => this != null && overlay != null && overlay.gameObject.activeInHierarchy &&
                epoch == Sandplay.Data.LocalAccountStorage.Epoch && generation == service.Generation;
            void Close() { overlay.gameObject.SetActive(false); if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject); }
            ClientText(box, friends ? F("Choose a friend", "选择好友") : F("Search account ID", "搜索账号 ID"), 22,
                .05f, friends ? .83f : .86f, .90f, .10f, HomeText);
            var status = ClientText(box, "", 13, .05f, friends ? .68f : .59f, .90f, friends ? .09f : .11f, HomeMuted);
            var list = ClientScroll(box, "Accounts", .05f, friends ? .22f : .16f, .90f, .42f);
            ClientButton(box, "dialog.cancel", friends ? .70f : .60f, .035f, friends ? .25f : .35f, friends ? .11f : .08f, Close);
            void Result(FriendPerson person)
            {
                if (person == null || person.id <= 0 || person.id == BackendClient.Instance.UserId || string.IsNullOrWhiteSpace(person.code)) return;
                var row = ClientRow(list, "AccountResult", 64);
                var avatar = ClientRect(row, "Avatar", 0, .5f, 0, 0);
                avatar.pivot = new Vector2(0, .5f);
                avatar.anchoredPosition = new Vector2(10, 0);
                avatar.sizeDelta = new Vector2(40, 40);
                var circle = avatar.gameObject.AddComponent<Image>();
                circle.sprite = Sandplay.UI.SessionAvatars.Circle();
                circle.color = new Color(.28f,.39f,.52f);
                var initial = string.IsNullOrWhiteSpace(person.name) ? "?" :
                    System.Globalization.StringInfo.GetNextTextElement(person.name.Trim()).ToUpperInvariant();
                var avatarLabel = ClientText(avatar, initial, 16, 0, 0, 1, 1, Color.white);
                avatarLabel.alignment = TMPro.TextAlignmentOptions.Center;
                avatar.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(person.id, person.avatar_url, avatarLabel);
                var name = ClientText(row, person.name, 15, 0, .1f, 1, .8f, HomeText);
                name.rectTransform.offsetMin = new Vector2(62, 0);
                name.rectTransform.offsetMax = new Vector2(-132, 0);
                name.enableWordWrapping = false;
                name.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                name.richText = false;
                ClientButton(row, F("Add", "添加"), .84f, .15f, .13f, .70f, () =>
                {
                    if (!Current()) return;
                    selected(person);
                    Close();
                }, true);
            }
            if (!BackendClient.Instance.IsLoggedIn)
            {
                status.text = F("Sign in to search accounts or choose friends. Manual clients can be saved offline.", "登录后可搜索账号或选择好友。手动来访者可离线保存。");
                return;
            }
            if (friends)
            {
                status.text = F("Loading friends…", "正在加载好友…");
                service.Request<FriendsState>("state/", null, state =>
                {
                    if (!Current()) return;
                    var people = (state.people ?? new FriendPerson[0]).Where(p => p != null && p.state == "accepted").OrderBy(p => p.name).ToList();
                    box.sizeDelta = new Vector2(box.sizeDelta.x, Mathf.Min(240 + Mathf.Clamp(people.Count, 1, 3) * 68, available.y - 48));
                    foreach (var person in people) Result(person);
                    status.text = list.childCount == 0 ? F("No added friends. Use account ID search instead.", "暂无已添加好友，可通过账号 ID 搜索。") : F("Choose an account, then review and save the client form.", "选择账号后，请检查并保存来访者表单。");
                }, error => { if (Current()) status.text = error; });
            }
            else
            {
                var code = ClientInput(box, "", F("6-letter account ID", "6 位字母账号 ID"), .05f, .73f, .59f, .09f, 6);
                bool busy = false;
                ClientButton(box, F("Search", "搜索"), .68f, .73f, .27f, .09f, () =>
                {
                    if (busy) return;
                    string id = code.text.Trim().ToUpperInvariant();
                    ClearClientChildren(list);
                    if (id.Length != 6 || id.Any(c => c < 'A' || c > 'Z'))
                    { status.text = F("Enter the 6-letter account ID.", "请输入 6 位字母账号 ID。"); return; }
                    busy = true;
                    status.text = F("Searching…", "正在搜索…");
                    service.Request<FriendPerson>("search/?code=" + Uri.EscapeDataString(id), null, person =>
                    {
                        if (!Current()) return;
                        busy = false;
                        Result(person);
                        status.text = list.childCount == 0 ? F("No eligible account found.", "未找到可用账号。") : F("Check the name and ID before selecting.", "选择前请核对姓名和 ID。");
                    }, error => { if (Current()) { busy = false; status.text = error; } });
                });
            }
        }
    }
}
