using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private static void RemoveReportOverlay(GameObject overlay)
        {
            if (overlay == null) return;
            overlay.SetActive(false);
            overlay.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(overlay);
            else DestroyImmediate(overlay);
        }

        private ReportTemplateStore CurrentReportTemplates()
        {
            var client = BackendClient.Instance;
            if (!client.IsLoggedIn) throw new UnauthorizedAccessException();
            return new ReportTemplateStore(Path.Combine(Application.persistentDataPath, "ReportTemplates"), client.UserId, LocalAccountStorage.CaptureGuard());
        }

        private void OpenReportTemplateManager(Transform parent, Func<bool> current, Action changed)
        {
            if (!current() || parent.Find("ReportTemplates") != null) return;
            var store = CurrentReportTemplates();
            var overlay = ClientRect(parent, "ReportTemplates", 0, 0, 1, 1);
            overlay.gameObject.AddComponent<Image>().color = HomeCard;
            var title = ClientText(overlay, Localization.Get("templates.title"), 22, .04f, .89f, .76f, .08f, HomeText);
            var list = ClientScroll(overlay, "TemplateList", .025f, .20f, .95f, .66f);
            var status = ClientText(overlay, "", 13, .04f, .115f, .92f, .075f, HomeMuted);
            var footer = ClientRect(overlay, "TemplateActions", .025f, .025f, .95f, .075f);
            bool dirty = false;
            GameObject confirmation = null;
            bool Active() => overlay != null && current();
            void Close()
            {
                if (overlay == null) return;
                RemoveReportOverlay(overlay.gameObject);
                if (current()) changed?.Invoke();
            }
            void Confirm(string key, Action accept)
            {
                if (!Active() || confirmation != null) return;
                var panel = ClientRect(overlay, "Confirm", 0, 0, 1, 1);
                confirmation = panel.gameObject;
                panel.gameObject.AddComponent<Image>().color = HomeCard;
                ClientText(panel, Localization.Get(key), 19, .06f, .51f, .88f, .24f, HomeText);
                var actions = ClientScroll(panel, "Actions", .05f, .10f, .90f, .38f);
                var cancel = ClientRow(actions, "Cancel", 60);
                ClientButton(cancel, "dialog.cancel", .02f, .08f, .96f, .84f, () =>
                { RemoveReportOverlay(confirmation); confirmation = null; });
                var yes = ClientRow(actions, "Confirm", 60);
                ClientButton(yes, "templates.confirm", .02f, .08f, .96f, .84f, () =>
                {
                    if (!Active()) return;
                    RemoveReportOverlay(confirmation); confirmation = null;
                    accept();
                }, true);
            }
            void Guard(Action action)
            {
                if (!Active()) return;
                if (dirty) Confirm("Discard unsaved changes?", () => { dirty = false; action(); });
                else action();
            }
            void Error(Exception ex)
            {
                string key = ex is ArgumentException ? ex.Message : ex is InvalidOperationException ? "templates.stale" : "clients.storage_error";
                status.text = Localization.Get(key);
            }
            void ActionRow(string key, Action action)
            {
                var row = ClientRow(list, key, 60);
                ClientButton(row, key, .02f, .08f, .96f, .84f, () => { if (Active()) action(); });
            }
            void Edit(ReportTemplate source, bool duplicate)
            {
                ClearClientChildren(list); ClearClientChildren(footer);
                title.text = Localization.Get(source == null ? "templates.new" : duplicate ? "templates.duplicate" : "templates.edit");
                status.text = Localization.Get("templates.blank_notice");
                dirty = false;
                string initialName = duplicate ? "" : source?.Name ?? "";
                var nameRow = ClientRow(list, "TemplateName", 100);
                ClientText(nameRow, Localization.Get("templates.name"), 15, .02f, .68f, .96f, .28f, HomeText);
                var name = ClientInput(nameRow, initialName, Localization.Get("templates.name"), .02f, .08f, .96f, .54f, 80);
                var chosen = new HashSet<string>(source?.SectionKeys ?? ReportTemplateStore.StandardSections.Take(4));
                name.onValueChanged.AddListener(_ => dirty = true);
                foreach (var key in ReportTemplateStore.StandardSections.Where(key => key != "report.ai_reflection"))
                {
                    var row = ClientRow(list, key, 94);
                    ClientText(row, Localization.Get(key), 15, .025f, .08f, .66f, .84f, HomeText);
                    Button toggle = null;
                    void Label() => toggle.GetComponentInChildren<TMP_Text>().text = Localization.Get(chosen.Contains(key) ? "templates.included" : "templates.excluded");
                    toggle = ClientButton(row, "", .72f, .20f, .25f, .60f, () =>
                    {
                        if (!Active()) return;
                        if (!chosen.Remove(key)) chosen.Add(key);
                        dirty = true; Label();
                    });
                    Label();
                }
                ClientButton(footer, "templates.save", .02f, .05f, .46f, .90f, () =>
                {
                    if (!Active()) return;
                    try
                    {
                        store.Save(new ReportTemplate {
                            Id = duplicate ? null : source?.Id, Revision = duplicate ? 0 : source?.Revision ?? 0,
                            Name = name.text.Trim(), SectionKeys = ReportTemplateStore.StandardSections.Where(key => key != "report.ai_reflection" && chosen.Contains(key)).ToArray()
                        });
                        dirty = false; RenderList();
                        status.text = Localization.Get("templates.saved");
                    }
                    catch (Exception ex) { Error(ex); }
                }, true);
                ClientButton(footer, "dialog.cancel", .52f, .05f, .46f, .90f, () => Guard(RenderList));
                Canvas.ForceUpdateCanvases(); list.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            }
            void RenderList()
            {
                ClearClientChildren(list); ClearClientChildren(footer); dirty = false;
                title.text = Localization.Get("templates.title");
                status.text = Localization.Get("templates.local_notice");
                ClientButton(footer, "templates.new", .02f, .05f, .96f, .90f, () => { if (Active()) Edit(null, false); }, true);
                try
                {
                    var templates = store.GetAll().OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                    if (templates.Count == 0)
                    {
                        var empty = ClientRow(list, "EmptyTemplates", 90);
                        ClientText(empty, Localization.Get("templates.empty"), 16, .03f, .08f, .94f, .84f, HomeMuted);
                    }
                    foreach (var item in templates)
                    {
                        var row = ClientRow(list, "Template_" + item.Id, 120);
                        ClientText(row, item.Name, 17, .025f, .60f, .95f, .32f, HomeText);
                        ClientButton(row, "templates.edit", .02f, .07f, .30f, .43f, () => { if (Active()) Edit(item, false); });
                        ClientButton(row, "templates.duplicate", .35f, .07f, .30f, .43f, () => { if (Active()) Edit(item, true); });
                        ClientButton(row, "templates.delete", .68f, .07f, .30f, .43f, () => Confirm("templates.delete_confirm", () =>
                        {
                            try { store.Delete(item.Id, item.Revision); RenderList(); }
                            catch (Exception ex) { Error(ex); }
                        }));
                    }
                }
                catch (Exception ex) { Error(ex); ActionRow("templates.retry", RenderList); }
                Canvas.ForceUpdateCanvases(); list.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            }
            var close = ClientButton(overlay, "×", .86f, .89f, .10f, .08f, () => Guard(Close));
            close.name = "CloseTemplates";
            RenderList();
            StartCoroutine(Watch());
            IEnumerator Watch()
            {
                while (overlay != null)
                {
                    if (!current()) { RemoveReportOverlay(overlay.gameObject); yield break; }
                    yield return new WaitForSeconds(.25f);
                }
            }
        }
    }
}
