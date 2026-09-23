using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Sandplay.Core;

namespace Sandplay.UI
{
    public sealed class SessionControlPanel : MonoBehaviour
    {
        public System.Action<SessionProfile> OpenProfile;
        public const float ToolbarWidth = 206f;
        private GameObject panel;
        private TMP_FontAsset font;
        private bool expanded;
        private string previous;
        private string confirmRemoval;
        private ScrollRect participantScroll;
        private GameObject lastSelection;
        private Transform toolbarSlot;
        private GameObject toolbarHeader;
        private enum ButtonTone { Neutral, Primary, Danger, Quiet }

        public void SetToolbarSlot(Transform slot) { toolbarSlot = slot; previous = null; }
        private static Sprite rounded;

        private static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            const int size = 32, radius = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Session control rounded background";
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius), 0);
                float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius), 0);
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy))));
            }
            texture.Apply(false, true);
            rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }

        private void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == lastSelection) return;
            lastSelection = selected;
            if (selected == null || participantScroll == null) return;
            var target = selected.transform as RectTransform;
            if (target == null || !target.IsChildOf(participantScroll.content)) return;
            RevealSelection(participantScroll, target);
        }

        public static void RevealSelection(ScrollRect scroll, RectTransform target)
        {
            if (scroll == null || scroll.viewport == null || scroll.content == null || target == null) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
            var view = scroll.viewport.rect;
            float shift = bounds.max.y > view.yMax ? view.yMax - bounds.max.y
                : bounds.min.y < view.yMin ? view.yMin - bounds.min.y : 0;
            scroll.StopMovement();
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + shift, 0, Mathf.Max(0, scroll.content.rect.height - view.height));
            scroll.content.anchoredPosition = position;
        }

        public void Initialize(TMP_FontAsset uiFont) { font = uiFont; }

        public static void RestoreFocus(GameObject root, string actionId)
        {
            if (root == null || actionId == null || EventSystem.current == null) return;
            Button fallback = null;
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                if (!button.IsInteractable()) continue;
                if (button.name == "session.manage") fallback = button;
                if (button.name != actionId) continue;
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                return;
            }
            // A departed participant must not redirect focus to another participant's action.
            EventSystem.current.SetSelectedGameObject(fallback != null ? fallback.gameObject : null);
        }

        public static Rect CalculatePanelRect(Vector2 available, bool open)
        {
            float margin = Mathf.Min(12, Mathf.Max(0, available.x / 4));
            float width = Mathf.Min(320, Mathf.Max(0, available.x - 2 * margin));
            float bottom = available.y >= 280 ? 72 : 8;
            float height = Mathf.Min(open ? 360 : 56, Mathf.Max(0, available.y - bottom - 8));
            return new Rect(Mathf.Max(0, available.x - margin - width), bottom, width, height);
        }

        private void Update()
        {
            var net = NetworkBootstrapper.Instance;
            bool visible = net != null && net.IsOnline && net.IsRelayMode;
            var parentRect = transform as RectTransform;
            Vector2 available = parentRect != null ? parentRect.rect.size : new Vector2(Screen.width, Screen.height);
            string key = visible ? net.IsHost + ":" + net.EditingPaused + ":" + net.EditorToken + ":" +
                GameManager.Instance.NetworkRole + ":" + expanded + ":" + net.SessionEditorName + ":" + net.SessionWaitingForEditor + ":" + net.IsSynchronizing : "offline";
            if (visible) foreach (var p in net.SessionParticipants) key += p.Token + p.Name + p.RequestsEditing;
            key += ":" + Localization.Current;
            key += ":" + available;
            if (key == previous) return;
            previous = key;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            string focusedAction = selected != null && ((panel != null && selected.transform.IsChildOf(panel.transform))
                || (toolbarHeader != null && selected.transform.IsChildOf(toolbarHeader.transform)))
                ? selected.name : null;
            float scrollPosition = participantScroll != null ? participantScroll.verticalNormalizedPosition : 1;
            if (focusedAction != null) EventSystem.current.SetSelectedGameObject(null);
            participantScroll = null;
            lastSelection = null;
            if (toolbarHeader != null)
            {
                toolbarHeader.SetActive(false);
                if (Application.isPlaying) Destroy(toolbarHeader); else DestroyImmediate(toolbarHeader);
            }
            if (panel != null)
            {
                panel.SetActive(false);
                if (Application.isPlaying) Destroy(panel);
                else DestroyImmediate(panel);
            }
            if (!visible) { confirmRemoval = null; expanded = false; return; }
            panel = new GameObject("SessionControls", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            Rect layout = CalculatePanelRect(available, expanded && net.IsHost);
            float width = layout.width;
            bool stacked = width < 280;
            // Fit the actual controls instead of leaving a large empty sheet.
            if (expanded && net.IsHost)
                layout.height = Mathf.Min(layout.height, 72 + (stacked ? 108 : 56)
                    + Mathf.Max(56, net.SessionParticipants.Count * (stacked ? 160 : 108)));
            rt.anchoredPosition = new Vector2(layout.xMax - available.x, layout.y);
            rt.sizeDelta = layout.size;
            panel.GetComponent<Image>().color = new Color(.025f, .035f, .05f, .92f);
            panel.GetComponent<Image>().sprite = Rounded();
            panel.GetComponent<Image>().type = Image.Type.Sliced;
            var border = panel.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, .10f);
            border.effectDistance = new Vector2(1, -1);
            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .34f);
            shadow.effectDistance = new Vector2(0, -3);
            string status = net.IsSynchronizing ? Localization.Get("board.loading_restore") : net.EditingPaused ? (string.IsNullOrEmpty(net.SessionEditorName)
                ? Localization.Get("session.paused") : Localization.Get("session.paused_name", net.SessionEditorName))
                : net.SessionWaitingForEditor ? Localization.Get("session.waiting")
                : Localization.Get("session.editing_name", net.SessionEditorName);
            if (!net.IsHost) status += " · " + Localization.Get(GameManager.Instance.IsPatient ? "session.you_edit" : "session.observing");
            if (toolbarSlot != null)
            {
                string dockStatus = net.IsSynchronizing || net.EditingPaused || net.SessionWaitingForEditor
                    ? status : net.SessionEditorName;
                var editor = AddStyledButton(toolbarSlot, dockStatus, 0, 0, ToolbarWidth,
                    net.IsHost ? (UnityEngine.Events.UnityAction)(() =>
                    { expanded = !expanded; confirmRemoval = null; previous = null; }) : null, ButtonTone.Quiet);
                editor.name = "session.manage";
                editor.GetComponent<Image>().color = Color.clear;
                AddButtonGlyph(editor, MeetingHudGlyph.Kind.Edit);
                var editorLabel = editor.GetComponentInChildren<TextMeshProUGUI>();
                editorLabel.enableWordWrapping = false;
                editorLabel.alignment = TextAlignmentOptions.MidlineLeft;
                toolbarHeader = editor.gameObject;
                // Permission popover sits above the dock; collapsed state is dock-only.
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 82);
                panel.SetActive(expanded && net.IsHost);
                if (expanded && net.IsHost)
                    AddPanelHeader(width, () => { expanded = false; previous = null; });
            }
            else
            {
                var statusButton = AddStyledButton(panel.transform,
                    status + (net.IsHost ? " · " + Localization.Get("session.manage") : ""), 4, 4, width - 8,
                    () => { if (net.IsHost) { expanded = !expanded; confirmRemoval = null; previous = null; } },
                    net.IsHost ? ButtonTone.Primary : ButtonTone.Quiet);
                statusButton.name = "session.manage";
            }
            if (!expanded || !net.IsHost) { RestorePanelFocus(focusedAction); return; }
            var viewport = new GameObject("Participants", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(panel.transform, false);
            var vr = (RectTransform)viewport.transform;
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = new Vector2(8, 8); vr.offsetMax = new Vector2(-8, -56);
            viewport.GetComponent<Image>().color = new Color(.01f, .02f, .03f, .28f);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = (RectTransform)content.transform;
            cr.anchorMin = new Vector2(0, 1); cr.anchorMax = Vector2.one; cr.pivot = new Vector2(.5f, 1);
            float innerWidth = width - 24;
            float half = (innerWidth - 8) / 2;
            float controlsHeight = stacked ? 116 : 64;
            float rowHeight = stacked ? 160 : 108;
            cr.sizeDelta = new Vector2(0, controlsHeight + Mathf.Max(56, net.SessionParticipants.Count * rowHeight));
            // All management actions scroll, so short landscape screens retain access.
            var pause = AddStyledButton(content.transform, Localization.Get(net.EditingPaused ? "session.resume" : "session.pause"), 4, 4,
                stacked ? innerWidth : half, () => net.SetSessionEditingPaused(!net.EditingPaused), ButtonTone.Neutral);
            pause.name = "session.pause"; AddButtonGlyph(pause, MeetingHudGlyph.Kind.Pause);
            var takeControl = AddStyledButton(content.transform, Localization.Get("session.take_control"), stacked ? 4 : half + 12,
                stacked ? 56 : 4, stacked ? innerWidth : half, () => net.SetSessionEditor(null), ButtonTone.Primary);
            takeControl.name = "session.take_control"; AddButtonGlyph(takeControl, MeetingHudGlyph.Kind.Edit);
            AddDivider(content.transform, controlsHeight - 5, innerWidth);
            var scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = vr; scroll.content = cr; scroll.horizontal = false;
            participantScroll = scroll;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            int row = 0;
            foreach (var p in net.SessionParticipants)
            {
                string token = p.Token;
                string name = (p.Name ?? "").Replace("<", "").Replace(">", "");
                string action = Localization.Get(token == net.EditorToken ? "session.editing" : p.RequestsEditing ? "session.approve" : "session.give_control");
                float y = controlsHeight + row++ * rowHeight + 4;
                AddParticipantButton(content.transform, name, action, token, y, innerWidth,
                    () => net.SetSessionEditor(token));
                if (p.RequestsEditing)
                    AddStyledButton(content.transform, Localization.Get("session.decline"), 4, y + 52, stacked ? innerWidth : half, () => net.DeclineSessionEditing(token), ButtonTone.Quiet).name = "decline:" + token;
                var remove = AddStyledButton(content.transform, Localization.Get(confirmRemoval == token ? "session.confirm_remove" : "session.remove"),
                    p.RequestsEditing && !stacked ? half + 12 : 4,
                    y + (p.RequestsEditing && stacked ? 104 : 52),
                    p.RequestsEditing && !stacked ? half : innerWidth, () =>
                {
                    if (confirmRemoval == token) { net.RemoveSessionParticipant(token); confirmRemoval = null; }
                    else confirmRemoval = token;
                    previous = null;
                }, ButtonTone.Danger);
                remove.name = "remove:" + token; AddButtonGlyph(remove, MeetingHudGlyph.Kind.Trash);
            }
            if (row == 0) AddStyledButton(content.transform, Localization.Get("session.waiting_people"), 4, controlsHeight + 4, innerWidth, null, ButtonTone.Quiet);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = scrollPosition;
            RestorePanelFocus(focusedAction);
        }

        private void RestorePanelFocus(string action)
        {
            if (toolbarHeader != null && action == "session.manage") RestoreFocus(toolbarHeader, action);
            else RestoreFocus(panel, action);
        }

        private Button AddButton(Transform parent, string text, float x, float y, float width,
            UnityEngine.Events.UnityAction action)
            => AddStyledButton(parent, text, x, y, width, action, ButtonTone.Neutral);

        private Button AddStyledButton(Transform parent, string text, float x, float y, float width,
            UnityEngine.Events.UnityAction action, ButtonTone tone)
        {
            var go = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, 48);
            var image = go.GetComponent<Image>();
            image.color = tone switch {
                ButtonTone.Primary => new Color(.08f, .48f, .52f),
                ButtonTone.Danger => new Color(.42f, .09f, .11f, .72f),
                ButtonTone.Quiet => new Color(1f, 1f, 1f, .055f),
                _ => new Color(1f, 1f, 1f, .10f)
            };
            go.GetComponent<Image>().sprite = Rounded();
            go.GetComponent<Image>().type = Image.Type.Sliced;
            var colors = go.GetComponent<Button>().colors;
            colors.highlightedColor = tone == ButtonTone.Danger ? new Color(.58f, .14f, .16f, .86f)
                : tone == ButtonTone.Primary ? new Color(.10f, .56f, .58f, .96f)
                : new Color(1f, 1f, 1f, .16f);
            colors.pressedColor = tone == ButtonTone.Primary ? new Color(.04f, .35f, .39f)
                : tone == ButtonTone.Danger ? new Color(.34f, .06f, .08f, .9f)
                : new Color(1f, 1f, 1f, .07f);
            colors.disabledColor = image.color;
            go.GetComponent<Button>().colors = colors;
            if (action != null) go.GetComponent<Button>().onClick.AddListener(action);
            else go.GetComponent<Button>().interactable = false;
            var label = new GameObject("Label", typeof(RectTransform)); label.transform.SetParent(go.transform, false);
            var tmp = label.AddComponent<TextMeshProUGUI>(); tmp.font = font; tmp.text = text;
            tmp.fontSize = 14; tmp.enableAutoSizing = true; tmp.fontSizeMin = 12; tmp.fontSizeMax = 14;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.color = tone == ButtonTone.Danger ? new Color(1f, .58f, .60f) : new Color(.94f, .97f, .98f);
            tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false; tmp.richText = false;
            var tr = tmp.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(6, 2); tr.offsetMax = new Vector2(-6, -2);
            return go.GetComponent<Button>();
        }

        private void AddPanelHeader(float width, UnityEngine.Events.UnityAction close)
        {
            var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            title.transform.SetParent(panel.transform, false);
            var rt = (RectTransform)title.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(14, -4); rt.sizeDelta = new Vector2(width - 66, 48);
            var text = title.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = Localization.Get("session.manage"); text.fontSize = 16;
            text.fontStyle = FontStyles.Bold; text.color = new Color(.95f, .97f, .98f);
            text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
            var button = AddStyledButton(panel.transform, "×", width - 48, 4, 44, close, ButtonTone.Quiet);
            button.name = "session.close";
            button.GetComponentInChildren<TextMeshProUGUI>().fontSizeMax = 20;
        }

        private void AddDivider(Transform parent, float y, float width)
        {
            var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(parent, false);
            var rt = (RectTransform)divider.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(4, -y); rt.sizeDelta = new Vector2(width, 1);
            divider.GetComponent<Image>().color = new Color(1f, 1f, 1f, .12f);
            divider.GetComponent<Image>().raycastTarget = false;
        }

        private void AddParticipantButton(Transform parent, string participantName, string action,
            string token, float y, float width, UnityEngine.Events.UnityAction onClick)
        {
            var button = AddStyledButton(parent, participantName + "     • " + action, 4, y, width, onClick, ButtonTone.Quiet);
            button.name = "edit:" + token;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.offsetMin = new Vector2(48, 2);

            var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatar.transform.SetParent(button.transform, false);
            var avatarRt = (RectTransform)avatar.transform;
            avatarRt.anchorMin = avatarRt.anchorMax = avatarRt.pivot = new Vector2(0, .5f);
            avatarRt.anchoredPosition = new Vector2(8, 0); avatarRt.sizeDelta = new Vector2(32, 32);
            var avatarImage = avatar.GetComponent<Image>();
            avatarImage.sprite = Rounded(); avatarImage.type = Image.Type.Sliced;
            avatarImage.color = new Color(.25f, .43f, .58f, .95f); avatarImage.raycastTarget = false;

            var initial = new GameObject("Initial", typeof(RectTransform), typeof(TextMeshProUGUI));
            initial.transform.SetParent(avatar.transform, false);
            var initialRt = (RectTransform)initial.transform;
            initialRt.anchorMin = Vector2.zero; initialRt.anchorMax = Vector2.one;
            initialRt.offsetMin = initialRt.offsetMax = Vector2.zero;
            var initialText = initial.GetComponent<TextMeshProUGUI>();
            initialText.font = font;
            initialText.text = string.IsNullOrWhiteSpace(participantName) ? "?" :
                System.Globalization.StringInfo.GetNextTextElement(participantName.Trim());
            initialText.fontSize = 13; initialText.color = Color.white;
            initialText.alignment = TextAlignmentOptions.Center; initialText.raycastTarget = false;
            var portrait = avatar.AddComponent<SessionAvatarBinding>();
            portrait.Initials = initialText;
            portrait.Resolve = () => System.Array.Find(NetworkBootstrapper.Instance?.LiveProfiles ?? new SessionProfile[0], p => p.token == token);
            portrait.Open = profile => OpenProfile?.Invoke(profile);
            portrait.Initialize();
        }

        private static void AddButtonGlyph(Button button, MeetingHudGlyph.Kind kind)
        {
            if (button == null) return;
            var glyphObject = new GameObject("HudGlyph", typeof(RectTransform), typeof(MeetingHudGlyph));
            glyphObject.transform.SetParent(button.transform, false);
            var rt = (RectTransform)glyphObject.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, .5f);
            rt.anchoredPosition = new Vector2(10, 0); rt.sizeDelta = new Vector2(28, 28);
            var glyph = glyphObject.GetComponent<MeetingHudGlyph>();
            glyph.Icon = kind; glyph.color = kind == MeetingHudGlyph.Kind.Trash
                ? new Color(1f, .58f, .60f) : new Color(.90f, .95f, .96f);
            glyph.raycastTarget = false;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(44, 0);
                label.rectTransform.offsetMax = new Vector2(-8, 0);
            }
        }
    }
}
