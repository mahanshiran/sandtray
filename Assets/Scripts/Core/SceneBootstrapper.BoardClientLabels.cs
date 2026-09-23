using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Data;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private Dictionary<string, string> BoardClientNames()
        {
            var names = new Dictionary<string, string>();
            try
            {
                // Include archived clients: archiving does not unlink their boards.
                foreach (var client in ClientStore.GetAll())
                    if (!string.IsNullOrEmpty(client.Id)) names[client.Id] = client.Name;
            }
            catch (Exception)
            {
                // Keep boards usable even if the local client file cannot be read.
                Debug.LogWarning("Client labels could not be loaded; linked boards will show a generic client label.");
            }
            return names;
        }

        private RectTransform AddBoardClientLabel(Transform parent, SessionListEntry entry,
            Dictionary<string, string> names, bool compact)
        {
            if (string.IsNullOrWhiteSpace(entry.ClientId)) return null;
            string label = names.TryGetValue(entry.ClientId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? Localization.Get("board.client_label", name)
                : Localization.Get("board.client_linked");
            var badge = ClientRect(parent, "ClientBadge", 0,0,1,0);
            badge.pivot = new Vector2(.5f,0);
            badge.offsetMin = new Vector2(0,0);
            badge.offsetMax = new Vector2(0,22);
            var background = badge.gameObject.AddComponent<Image>();
            background.color = HomeIsLight ? new Color(.82f,.95f,.94f,.98f) : new Color(.08f,.29f,.31f,.98f);
            background.raycastTarget = false;
            ApplyRoundedCorners(background);
            var text = ClientText(badge, label, compact ? 10 : 12, 0,0,1,1,
                HomeIsLight ? new Color(.02f,.28f,.29f) : new Color(.70f,.93f,.92f));
            text.name = "ClientName";
            text.rectTransform.offsetMin = new Vector2(7,1);
            text.rectTransform.offsetMax = new Vector2(-7,-1);
            text.enableWordWrapping = false;
            text.richText = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return badge;
        }
    }
}
