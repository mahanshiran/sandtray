using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void ShowKeyboardShortcutSettings()
        {
            var box = ClientDialog(Localization.Get("shortcuts.title"), 850, 720);
            var session = _clientDialog.AddComponent<ShortcutKeyCapture>();
            session.BeginSession();
            ClientText(box, Localization.Get("shortcuts.hint"), 13, .05f, .765f, .90f, .085f, HomeMuted);
            var search = ClientInput(box, "", Localization.Get("shortcuts.search"), .05f, .68f, .62f, .065f, 80);
            var list = ClientScroll(box, "Shortcuts", .05f, .16f, .90f, .49f);
            var status = ClientText(box, "", 12, .05f, .08f, .90f, .06f, HomeMuted);
            void Populate(string query)
            {
                ClearClientChildren(list);
                foreach (var action in ShortcutMap.Actions)
                {
                    string name = Localization.Get("shortcuts.action." + action);
                    string binding = KeyboardShortcuts.Label(action);
                    if (!string.IsNullOrWhiteSpace(query) && (name + " " + binding).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var row = ClientRow(list, action.ToString(), 72);
                    ClientText(row, name, 15, .025f, .40f, .43f, .48f, HomeText);
                    ClientText(row, Localization.Get(KeyboardShortcuts.Map.IsCustom(action) ? "shortcuts.custom" : "shortcuts.default"),
                        11, .025f, .09f, .43f, .28f, HomeMuted);
                    ClientButton(row, binding, .48f, .17f, .32f, .66f,
                        () => ShowShortcutRecorder(box, session, action, () => { Populate(search.text); status.text = Localization.Get("shortcuts.saved"); }));
                    var reset = ClientButton(row, "shortcuts.reset", .82f, .17f, .155f, .66f, () =>
                    {
                        if (KeyboardShortcuts.TrySave(action, ShortcutMap.Default(action), out var error))
                        { Populate(search.text); status.text = Localization.Get("shortcuts.saved"); }
                        else status.text = ShortcutError(error);
                    });
                    reset.interactable = KeyboardShortcuts.Map.IsCustom(action);
                }
                if (list.childCount == 0) ClientText(ClientRow(list, "Empty", 60), Localization.Get("shortcuts.empty"), 14, .03f, .1f, .94f, .8f, HomeMuted);
            }
            ClientButton(box, "shortcuts.reset_all", .70f, .68f, .25f, .065f, () =>
            {
                var overlay = ShortcutOverlay(box, out var card, 460, 280);
                ClientText(card, Localization.Get("shortcuts.reset_confirm"), 18, .07f, .43f, .86f, .45f, HomeText);
                ClientButton(card, "dialog.cancel", .07f, .10f, .40f, .20f, () => DestroyShortcutOverlay(overlay));
                ClientButton(card, "shortcuts.reset_all", .53f, .10f, .40f, .20f, () =>
                {
                    status.text = Localization.Get(KeyboardShortcuts.ResetAll() ? "shortcuts.saved" : "shortcuts.save_error");
                    Populate(search.text); DestroyShortcutOverlay(overlay);
                }, true);
            });
            search.onValueChanged.AddListener(Populate);
            Populate("");
        }

        private string ShortcutError(string error) => error != null && error.StartsWith("shortcuts.action.")
            ? Localization.Get("shortcuts.conflict", Localization.Get(error)) : Localization.Get(error ?? "shortcuts.save_error");

        private GameObject ShortcutOverlay(Transform parent, out RectTransform card, float width, float height)
        {
            var overlay = ClientRect(parent, "ShortcutOverlay", 0, 0, 1, 1);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, HomeIsLight ? .42f : .72f);
            Sandplay.UI.DialogBackdrop.Apply(overlay.gameObject.GetComponent<Image>());
            card = ClientRect(overlay, "Card", .5f, .5f, 0, 0);
            var size = ((RectTransform)parent).rect.size;
            card.sizeDelta = new Vector2(Mathf.Min(width, size.x - 24), Mathf.Min(height, size.y - 24));
            card.gameObject.AddComponent<Image>().color = HomeCard;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1,-1);
            return overlay.gameObject;
        }
        private void DestroyShortcutOverlay(GameObject overlay)
        {
            overlay.SetActive(false);
            if (Application.isPlaying) Destroy(overlay); else DestroyImmediate(overlay);
        }
        private void ShowShortcutRecorder(Transform parent, ShortcutKeyCapture session, ShortcutAction action, Action saved)
        {
            var overlay = ShortcutOverlay(parent, out var card, 530, 420);
            var draft = KeyboardShortcuts.Map.Get(action);
            ClientText(card, Localization.Get("shortcuts.action." + action), 22, .07f, .82f, .86f, .11f, HomeText);
            ClientText(card, Localization.Get("shortcuts.press"), 13, .07f, .64f, .86f, .15f, HomeMuted);
            var preview = ClientText(card, KeyboardShortcuts.Label(draft), 27, .07f, .44f, .86f, .17f, HomeText);
            preview.alignment = TextAlignmentOptions.Center;
            var errorText = ClientText(card, "", 13, .07f, .24f, .86f, .17f, new Color(1, .72f, .5f));
            void Close() { session.StopListening(); DestroyShortcutOverlay(overlay); }
            var save = ClientButton(card, "shortcuts.save", .69f, .07f, .24f, .12f, () =>
            {
                if (KeyboardShortcuts.TrySave(action, draft, out var error)) { Close(); saved(); }
                else errorText.text = ShortcutError(error);
            }, true);
            void SetDraft(ShortcutBinding candidate)
            {
                candidate.key = ShortcutMap.Canonical(candidate.key);
                if (ShortcutMap.IsTransform(action)) candidate.shift = false;
                draft = candidate;
                preview.text = KeyboardShortcuts.Label(candidate);
                bool valid = KeyboardShortcuts.Map.CanSet(action, candidate, out var error);
                save.interactable = valid;
                errorText.text = valid ? (ShortcutMap.IsTransform(action) ? Localization.Get("shortcuts.fine") : "") : ShortcutError(error);
            }
            ClientButton(card, "dialog.cancel", .07f, .07f, .27f, .12f, Close);
            ClientButton(card, "shortcuts.unassign", .37f, .07f, .29f, .12f, () => SetDraft(new ShortcutBinding(KeyCode.None)));
            // Search loses focus before recording so key presses do not alter the search text.
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            session.Listen(SetDraft, Close);
            SetDraft(draft);
        }
    }
}
