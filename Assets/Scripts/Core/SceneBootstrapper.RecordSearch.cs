using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sandplay.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void OpenRecordSearch(RecordKind kind = RecordKind.Board, string clientId = null)
        {
            var backend = BackendClient.Instance;
            var manager = SessionManager.Instance;
            if (!backend.IsLoggedIn || backend.UserId <= 0) { ShowClientMessage("report.sign_in"); return; }
            if (manager == null) return;
            int account = backend.UserId;
            string token = backend.AccessToken, role = backend.UserType;
            bool clientsAllowed = CanUsePersonalClientDirectory;
            var options = new RecordSearchOptions { Kind = kind, ClientId = clientId };
            var box = ClientDialog(Localization.Get("search.title"), 1200, 810);
            var card = (RectTransform)box;
            ApplyHomeRoundedCorners(card.GetComponent<Image>(), 20f);
            bool compact = card.rect.width < 700;
            foreach (var text in box.GetComponentsInChildren<TMP_Text>()) text.gameObject.SetActive(false);
            var close = box.Find("Close");
            if (close != null) {
                close.GetComponent<Image>().color = Color.clear;
                var rt = (RectTransform)close; rt.anchorMin = rt.anchorMax = new Vector2(1,1);
                rt.pivot = Vector2.one; rt.anchoredPosition = new Vector2(-20,-20); rt.sizeDelta = new Vector2(44,44);
                SearchIcon(close, Sandplay.UI.RecordSearchGlyph.Kind.Close, HomeMuted);
            }
            var badge = ClientRect(box, "SearchBadge", .026f,.875f,.062f,.09f);
            badge.gameObject.AddComponent<Image>().color = HomeIsLight ? new Color(.93f,.97f,.97f) : HomeChromeButton;
            ApplyHomeRoundedCorners(badge.GetComponent<Image>(), 10);
            SearchIcon(badge, Sandplay.UI.RecordSearchGlyph.Kind.DocumentSearch, HomeTeal);
            var heading = ClientText(box, Localization.Get("search.title"), 28, .108f,.912f,.73f,.055f,HomeText);
            heading.fontStyle = FontStyles.Bold;
            ClientText(box,F("Find boards, clients and your reports.","查找沙盘、来访者和你的报告。"),17,.108f,.866f,.73f,.045f,HomeMuted);
            ClientText(box,F("Search in","搜索范围"),15,.026f,.805f,.40f,.035f,HomeMuted);
            var dialog = _clientDialog;
            bool Current() => dialog != null && _clientDialog == dialog && backend == BackendClient.Instance &&
                backend.UserId == account && backend.AccessToken == token && backend.UserType == role && manager == SessionManager.Instance;
            var records = new List<RecordSearchEntry>();
            var clients = new List<ClientRecord>();
            bool loadFailed = false;
            int page = 0;
            const int pageSize = 30;
            var kinds = clientsAllowed ? new[] { RecordKind.Board, RecordKind.Report, RecordKind.Client } : new[] { RecordKind.Board, RecordKind.Report };
            if (!kinds.Contains(options.Kind)) options.Kind = RecordKind.Board;
            Transform results = null;
            RectTransform empty = null;
            TMP_Text emptyTitle = null, emptyHint = null;
            TMP_Text status = null, pageLabel = null;
            Button previous = null, next = null;
            var selectorRow = ClientRect(box, "SearchKind", .026f, .727f, compact ? .65f : .275f, .072f);
            var selector = SettingsDropdown(selectorRow, "SearchKindDropdown", kinds.Select(k => Localization.Get("search.kind_" + k.ToString().ToLowerInvariant())).ToArray(), Array.IndexOf(kinds, options.Kind), index =>
            {
                if (!Current()) return;
                options.Kind = kinds[index]; page = 0; Render();
            });
            StretchFull(selector.gameObject);
            SearchControl(selector.GetComponent<Image>());
            selector.captionText.color = HomeText;
            selector.captionText.rectTransform.offsetMin = new Vector2(48,0);
            var kindIcon = ClientRect(selector.transform,"KindIcon",0,.5f,0,0);
            kindIcon.pivot = new Vector2(0,.5f); kindIcon.anchoredPosition = new Vector2(12,0); kindIcon.sizeDelta = new Vector2(28,28);
            SearchIcon(kindIcon, Sandplay.UI.RecordSearchGlyph.Kind.Document, HomeMuted);
            var search = ClientInput(box, "", Localization.Get("search.placeholder"), compact ? .026f : .315f, compact ? .635f : .727f, compact ? .948f : .52f, .072f, 200);
            SearchControl(search.GetComponent<Image>());
            search.textViewport.offsetMin = new Vector2(48,5);
            var queryIcon = ClientRect(search.transform,"SearchIcon",0,.5f,0,0);
            queryIcon.pivot = new Vector2(0,.5f); queryIcon.anchoredPosition = new Vector2(12,0); queryIcon.sizeDelta = new Vector2(28,28);
            SearchIcon(queryIcon, Sandplay.UI.RecordSearchGlyph.Kind.Search, HomeMuted);
            search.name = "RecordQuery";
            float resultTop = compact ? .61f : .696f;
            var divider = ClientRect(box,"SearchDivider",.026f,resultTop,.948f,0);
            divider.sizeDelta = new Vector2(0,1); divider.gameObject.AddComponent<Image>().color = HomeCardBorder;
            results = ClientScroll(box, "SearchResults", .026f, .20f, .948f, resultTop-.22f);
            results.parent.GetComponent<Image>().color = Color.clear;
            empty = ClientRect(box,"EmptySearch",.026f,.21f,.948f,resultTop-.24f);
            var emptyBadge = ClientRect(empty,"EmptyIcon",.5f,.72f,0,0);
            emptyBadge.sizeDelta = new Vector2(112,112);
            emptyBadge.gameObject.AddComponent<Image>().sprite = Sandplay.UI.SessionAvatars.Circle();
            emptyBadge.GetComponent<Image>().color = HomeIsLight ? new Color(.94f,.97f,.97f) : HomeChromeButton;
            SearchIcon(emptyBadge,Sandplay.UI.RecordSearchGlyph.Kind.DocumentSearch,HomeTeal);
            emptyTitle = ClientText(empty,"",26,.04f,.36f,.92f,.12f,HomeText);
            emptyTitle.alignment = TextAlignmentOptions.Center; emptyTitle.fontStyle = FontStyles.Bold;
            emptyHint = ClientText(empty,"",17,.04f,.22f,.92f,.12f,HomeMuted);
            emptyHint.alignment = TextAlignmentOptions.Center;
            var browse = ClientButton(empty,"",.30f,.035f,.40f,.13f,()=> {
                options.Kind=RecordKind.Board; options.Query=""; options.ClientId=null;
                options.From=options.To=options.Source=""; options.Archive=ArchiveFilter.Active;
                selector.SetValueWithoutNotify(0); search.SetTextWithoutNotify(""); page=0; Render();
            });
            browse.GetComponent<Image>().color=Color.clear;
            var browseText=browse.GetComponentInChildren<TMP_Text>(); browseText.text=F("Browse all records","浏览全部记录");
            browseText.color=HomeTeal; browseText.fontSizeMax=18; browseText.fontStyle=FontStyles.Bold;
            var back=ClientRect(browse.transform,"BackIcon",0,.15f,.13f,.7f);
            SearchIcon(back,Sandplay.UI.RecordSearchGlyph.Kind.Back,HomeTeal);
            var footerLine=ClientRect(box,"FooterDivider",0,.133f,1,0);
            footerLine.sizeDelta=new Vector2(0,1); footerLine.gameObject.AddComponent<Image>().color=HomeCardBorder;
            var info=ClientRect(box,"StorageInfo",.026f,.049f,.033f,.05f);
            SearchIcon(info,Sandplay.UI.RecordSearchGlyph.Kind.Info,HomeMuted);
            status = ClientText(box, "", 14, .073f, .023f, .90f, .089f, HomeMuted);
            previous = ClientButton(box, "search.previous", .026f, .145f, .20f, .045f, () => { if (Current()) { page--; Render(); } });
            pageLabel = ClientText(box, "", 13, .335f, .145f, .33f, .045f, HomeMuted);
            pageLabel.alignment = TextAlignmentOptions.Center;
            next = ClientButton(box, "search.next", .774f, .145f, .20f, .045f, () => { if (Current()) { page++; Render(); } });
            void Load()
            {
                records.Clear(); clients.Clear(); loadFailed = false;
                if (clientsAllowed)
                {
                    try { clients = ClientStore.GetAll(); }
                    catch (Exception) { loadFailed = true; }
                }
                var names = clients.ToDictionary(c => c.Id, c => c.Name);
                foreach (var client in clients)
                    records.Add(new RecordSearchEntry { Kind = RecordKind.Client, Name = client.Name, ClientId = client.Id,
                        ClientName = client.Name, Date = client.UpdatedAt, Archived = client.Archived, Client = client,
                        Text = string.Join(" ", new[] { client.Reference, client.Email, client.Phone, client.Notes }) });
                foreach (var table in manager.GetSavedSessions(true))
                {
                    var data = manager.LoadSessionData(table.SessionName);
                    if (data == null) { loadFailed = true; continue; }
                    names.TryGetValue(data.ClientId ?? "", out var clientName);
                    var ownReports = (data.Reports ?? new List<AnalysisReport>()).Where(r => r != null && r.AuthorUserId == account).ToList();
                    records.Add(new RecordSearchEntry { Kind = RecordKind.Board, Name = table.SessionName, BoardName = table.SessionName,
                        ClientId = data.ClientId, ClientName = clientName, Date = data.ModifiedAt, Archived = data.Archived,
                        Text = (data.TherapistNotes ?? "") + "\n" + string.Join("\n", ownReports.Where(r => !r.Archived).Select(r => r.ResultText)) });
                    foreach (var report in ownReports)
                        records.Add(new RecordSearchEntry { Kind = RecordKind.Report, Name = table.SessionName, BoardName = table.SessionName,
                            ClientId = data.ClientId, ClientName = clientName, Date = report.CreatedAt, Archived = report.Archived,
                            AuthorUserId = report.AuthorUserId, Source = report.Source, Report = report,
                            Text = string.Join("\n", new[] { report.ResultText, report.Sections?.Observations, report.Sections?.ClientPerspective,
                                report.Sections?.PractitionerNotes, report.Sections?.NextSteps, report.Sections?.AIReflection }) });
                }
            }
            void ChangeArchive(RecordSearchEntry entry)
            {
                if (!Current()) return;
                try
                {
                    if (entry.Kind == RecordKind.Board) manager.SetBoardArchived(entry.BoardName, !entry.Archived);
                    else if (entry.Kind == RecordKind.Report) manager.SetReportArchived(entry.BoardName, entry.Report.ReportId, !entry.Archived);
                    else
                    {
                        var latest = ClientStore.GetAll().Find(c => c.Id == entry.ClientId);
                        if (latest == null) throw new InvalidOperationException();
                        latest.Archived = !entry.Archived; ClientStore.Save(latest);
                    }
                    Load(); Render();
                    RefreshMyBoardsList();
                    if (_clientsPage != null) RefreshClientsPage();
                }
                catch (Exception) { status.text = Localization.Get("clients.storage_error"); }
            }
            void Render()
            {
                if (!Current()) return;
                ClearClientChildren(results);
                var matches = RecordSearch.Filter(records, options, account);
                int pages = Math.Max(1, (matches.Count + pageSize - 1) / pageSize);
                page = Mathf.Clamp(page, 0, pages - 1);
                previous.interactable = page > 0; next.interactable = page + 1 < pages;
                pageLabel.text = (page + 1) + " / " + pages;
                status.text = Localization.Get(loadFailed ? "search.partial" : "search.local_notice");
                empty.gameObject.SetActive(matches.Count == 0);
                results.parent.gameObject.SetActive(matches.Count != 0);
                previous.gameObject.SetActive(pages > 1); next.gameObject.SetActive(pages > 1); pageLabel.gameObject.SetActive(pages > 1);
                bool hasRecords = records.Any(r => r.Kind == options.Kind);
                emptyTitle.text = hasRecords ? F("No matching records","没有匹配的记录") : options.Kind == RecordKind.Report ? F("No reports yet","暂无报告") : options.Kind == RecordKind.Client ? F("No clients yet","暂无来访者") : F("No boards yet","暂无沙盘");
                emptyHint.text = hasRecords ? F("Try another search or change your filters.","尝试其他关键词或调整筛选条件。") : options.Kind == RecordKind.Report ? F("Your reports will appear here when available.","保存的报告将显示在这里。") : F("Your saved records will appear here.","保存的记录将显示在这里。");
                foreach (var entry in matches.Skip(page * pageSize).Take(pageSize))
                {
                    bool reportRow = entry.Kind == RecordKind.Report;
                    var row = ClientRow(results, "SearchResult", compact ? 152 : reportRow ? 144 : 116);
                    SearchControl(row.GetComponent<Image>());
                    ApplyHomeRoundedCorners(row.GetComponent<Image>(), 12);
                    var badge = ClientRect(row,"RecordIcon",0,.5f,0,0);
                    badge.pivot=new Vector2(0,.5f); badge.anchoredPosition=new Vector2(20,compact ? 22 : 0); badge.sizeDelta=new Vector2(58,58);
                    badge.gameObject.AddComponent<Image>().color=HomeIsLight ? new Color(.93f,.97f,.97f) : HomeChromeButton;
                    ApplyHomeRoundedCorners(badge.GetComponent<Image>(),9);
                    SearchIcon(badge,entry.Kind==RecordKind.Board ? Sandplay.UI.RecordSearchGlyph.Kind.Grid : entry.Kind==RecordKind.Client ? Sandplay.UI.RecordSearchGlyph.Kind.Person : Sandplay.UI.RecordSearchGlyph.Kind.Document,HomeTeal);
                    float textX = compact ? .22f : .10f, textWidth = compact ? .73f : .57f;
                    var title=ClientText(row, entry.Name, 20, textX, compact ? .69f : reportRow ? .71f : .56f, textWidth, .24f, HomeText);
                    title.fontStyle=FontStyles.Bold; title.richText=false; title.enableWordWrapping=false; title.overflowMode=TextOverflowModes.Ellipsis;
                    var date = RecordSearch.ParseDate(entry.Date);
                    string detail = date == DateTimeOffset.MinValue ? "" : date.ToLocalTime().ToString("g", Localization.Culture);
                    if (!string.IsNullOrEmpty(entry.ClientName)) detail += " · " + entry.ClientName;
                    if (entry.Kind == RecordKind.Report) detail += " · " + Localization.Get(entry.Source == "ai" ? "report.ai_source" : entry.Source == "manual" ? "report.manual_source" : "report.legacy");
                    if (entry.Archived) detail += " · " + Localization.Get("clients.archived");
                    var metadata=ClientText(row, detail, 14, textX, compact ? .48f : reportRow ? .49f : .24f, textWidth, .22f, HomeMuted);
                    metadata.richText=false; metadata.enableWordWrapping=false; metadata.overflowMode=TextOverflowModes.Ellipsis;
                    if (reportRow)
                    {
                        string preview = (entry.Report.ResultText ?? "").Replace('\r', ' ').Replace('\n', ' ');
                        int match = string.IsNullOrWhiteSpace(options.Query) ? 0 : preview.IndexOf(options.Query.Trim(), StringComparison.OrdinalIgnoreCase);
                        int start = Math.Max(0, match - 30);
                        preview = (start > 0 ? "…" : "") + preview.Substring(start, Math.Min(150, preview.Length - start));
                        var excerpt=ClientText(row, preview, 13, textX, compact ? .30f : .15f, textWidth, .20f, HomeMuted);
                        excerpt.richText=false; excerpt.enableWordWrapping=false; excerpt.overflowMode=TextOverflowModes.Ellipsis;
                    }
                    var openRow=row.gameObject.AddComponent<Button>();
                    openRow.targetGraphic=row.GetComponent<Image>();
                    var rowColors=openRow.colors; rowColors.highlightedColor=new Color(.94f,.98f,.98f); openRow.colors=rowColors;
                    openRow.onClick.AddListener(() =>
                    {
                        if (!Current()) return;
                        if (entry.Kind == RecordKind.Report)
                        {
                            var fresh = manager.LoadSessionData(entry.BoardName)?.Reports?.Find(r => r != null && r.ReportId == entry.Report.ReportId);
                            if (fresh == null || fresh.AuthorUserId != account) { Load(); Render(); return; }
                            CloseClientDialog(); ShowReportDetail(fresh, entry.BoardName);
                        }
                        else if (entry.Kind == RecordKind.Board) { CloseClientDialog(); EnterSandbox(entry.BoardName, false); }
                        else { CloseClientDialog(); _selectedClientId = entry.ClientId; _includeArchivedClients = entry.Archived; ShowHomeSection("clients"); RefreshClientsPage(); }
                    });
                    float actionY=compact ? .055f : .29f, actionHeight=compact ? .26f : .48f;
                    if (entry.Kind == RecordKind.Board)
                    {
                        var history=ClientButton(row,"versions.title",compact ? .40f : .70f,actionY,compact ? .39f : .21f,actionHeight,
                            ()=> { if(Current()) OpenBoardVersions(entry.BoardName); });
                        SearchControl(history.GetComponent<Image>());
                        var label=history.GetComponentInChildren<TMP_Text>(); label.color=HomeTeal; label.rectTransform.offsetMin=new Vector2(32,0);label.fontSizeMax=15;
                        var icon=ClientRect(history.transform,"HistoryIcon",0,.5f,0,0);icon.pivot=new Vector2(0,.5f);icon.anchoredPosition=new Vector2(7,0);icon.sizeDelta=new Vector2(26,26);
                        SearchIcon(icon,Sandplay.UI.RecordSearchGlyph.Kind.History,HomeTeal);
                    }
                    var more=ClientButton(row,"RecordMore",compact ? .84f : .925f,actionY,compact ? .12f : .055f,actionHeight,()=>{});
                    SearchControl(more.GetComponent<Image>());
                    more.GetComponentInChildren<TMP_Text>().text="";
                    SearchIcon(more.transform,Sandplay.UI.RecordSearchGlyph.Kind.More,HomeText);
                    more.onClick.AddListener(()=>
                    {
                        if(!Current())return;
                        var overlay=ClientRect(box,"RecordActions",0,0,1,1);
                        overlay.gameObject.AddComponent<Image>().color=Color.clear;
                        overlay.gameObject.AddComponent<Button>().onClick.AddListener(()=>RemoveReportOverlay(overlay.gameObject));
                        Canvas.ForceUpdateCanvases();
                        var corners=new Vector3[4];more.GetComponent<RectTransform>().GetWorldCorners(corners);
                        var position=(Vector2)card.InverseTransformPoint(corners[3]);
                        float menuWidth=Mathf.Min(220,card.rect.width-32);
                        position.x=Mathf.Clamp(position.x,card.rect.xMin+menuWidth+12,card.rect.xMax-12);
                        position.y=Mathf.Clamp(position.y,card.rect.yMin+80,card.rect.yMax-12);
                        var menu=ClientRect(overlay,"ActionsCard",.5f,.5f,0,0);
                        menu.pivot=new Vector2(1,1);menu.anchoredPosition=position;menu.sizeDelta=new Vector2(menuWidth,66);
                        menu.gameObject.AddComponent<Image>();SearchControl(menu.GetComponent<Image>());ApplyHomeRoundedCorners(menu.GetComponent<Image>(),12);
                        var archive=ClientButton(menu,entry.Archived?"search.restore_action":"search.archive_action",.025f,.08f,.95f,.84f,()=>
                        { RemoveReportOverlay(overlay.gameObject);ChangeArchive(entry); });
                        archive.GetComponent<Image>().color=Color.clear;
                        var label=archive.GetComponentInChildren<TMP_Text>();label.rectTransform.offsetMin=new Vector2(36,0);label.fontSizeMax=16;
                        var icon=ClientRect(archive.transform,"ArchiveIcon",.04f,.22f,.15f,.56f);
                        SearchIcon(icon,Sandplay.UI.RecordSearchGlyph.Kind.Archive,HomeMuted);
                    });
                }
                Canvas.ForceUpdateCanvases(); results.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            }
            void Filters()
            {
                if (!Current() || box.Find("SearchFilters") != null) return;
                var panel = ClientRect(box, "SearchFilters", 0, 0, 1, 1);
                panel.gameObject.AddComponent<Image>().color = HomeCard;
                ClientText(panel, Localization.Get("search.filters"), 22, .04f, .88f, .92f, .09f, HomeText);
                var fields = ClientScroll(panel, "FilterFields", .025f, .20f, .95f, .65f);
                var message = ClientText(panel, Localization.Get("search.date_hint"), 12, .04f, .115f, .92f, .075f, HomeMuted);
                TMP_InputField DateInput(string key, string value)
                {
                    var row = ClientRow(fields, key, 108);
                    ClientText(row, Localization.Get(key), 15, .025f, .67f, .95f, .29f, HomeText);
                    return ClientInput(row, value, "YYYY-MM-DD", .025f, .08f, .95f, .50f, 10);
                }
                var from = DateInput("search.from", options.From);
                var to = DateInput("search.to", options.To);
                Transform FilterRow(string key)
                {
                    var row = SettingsRow(fields, key, 90);
                    row.Find("Title").GetComponent<TMP_Text>().color = HomeText;
                    return row;
                }
                var archive = SettingsDropdown(FilterRow("search.archive"), "ArchiveFilter",
                    new[] { Localization.Get("search.active"), Localization.Get("search.archived"), Localization.Get("search.all") }, (int)options.Archive, _ => {});
                TMP_Dropdown clientFilter = null, source = null;
                var clientChoices = new List<string> { null, "" };
                if (clientsAllowed)
                {
                    clientChoices.AddRange(clients.OrderBy(c => c.Name).Select(c => c.Id));
                    if (options.ClientId != null && !clientChoices.Contains(options.ClientId)) clientChoices.Add(options.ClientId);
                    var labels = clientChoices.Select(id => id == null ? Localization.Get("search.all_clients") : id == "" ? Localization.Get("clients.unassigned") : clients.Find(c => c.Id == id)?.Name ?? Localization.Get("clients.history_missing", id)).ToArray();
                    clientFilter = SettingsDropdown(FilterRow("search.client"), "ClientFilter", labels, Math.Max(0, clientChoices.IndexOf(options.ClientId)), _ => {});
                }
                if (options.Kind == RecordKind.Report)
                    source = SettingsDropdown(FilterRow("search.source"), "SourceFilter",
                        new[] { Localization.Get("search.all"), Localization.Get("report.manual_source"), Localization.Get("report.ai_source") }, options.Source == "manual" ? 1 : options.Source == "ai" ? 2 : 0, _ => {});
                var reset = ClientRow(fields, "ResetFilters", 64);
                ClientButton(reset, "search.reset", .025f, .08f, .95f, .84f, () =>
                { from.text = to.text = ""; archive.value = 0; if (clientFilter != null) clientFilter.value = 0; if (source != null) source.value = 0; });
                ClientButton(panel, "search.apply", .025f, .025f, .46f, .075f, () =>
                {
                    if (!Current()) return;
                    var candidate = new RecordSearchOptions { From = from.text, To = to.text };
                    if (!candidate.TryDates(out _, out _)) { message.text = Localization.Get("search.invalid_dates"); return; }
                    options.From = from.text; options.To = to.text; options.Archive = (ArchiveFilter)archive.value;
                    if (clientFilter != null) options.ClientId = clientChoices[clientFilter.value];
                    options.Source = source == null ? "" : source.value == 1 ? "manual" : source.value == 2 ? "ai" : "";
                    RemoveReportOverlay(panel.gameObject); page = 0; Render();
                }, true);
                ClientButton(panel, "dialog.cancel", .515f, .025f, .46f, .075f, () => RemoveReportOverlay(panel.gameObject));
            }
            var filters = ClientButton(box, "search.filters", compact ? .70f : .85f, .727f, compact ? .274f : .124f, .072f, Filters);
            SearchControl(filters.GetComponent<Image>());
            var filterText=filters.GetComponentInChildren<TMP_Text>(); filterText.color=HomeTeal; filterText.rectTransform.offsetMin=new Vector2(36,0); filterText.fontSizeMax=17;
            var filterIcon=ClientRect(filters.transform,"FilterIcon",0,.5f,0,0);
            filterIcon.pivot=new Vector2(0,.5f);filterIcon.anchoredPosition=new Vector2(8,0);filterIcon.sizeDelta=new Vector2(28,28);
            SearchIcon(filterIcon,Sandplay.UI.RecordSearchGlyph.Kind.Filters,HomeTeal);
            search.onValueChanged.AddListener(value => { if (Current()) { options.Query = value; page = 0; Render(); } });
            try { Load(); Render(); }
            catch (Exception) { status.text = Localization.Get("clients.storage_error"); }
            StartCoroutine(Watch());
            IEnumerator Watch()
            {
                while (dialog != null && _clientDialog == dialog)
                {
                    if (!Current()) { CloseClientDialog(); yield break; }
                    yield return new WaitForSeconds(.25f);
                }
            }
        }
        private void SearchControl(Image image)
        {
            image.color=HomeCard; ApplyHomeRoundedCorners(image,7);
            var border=image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
            border.effectColor=HomeCardBorder; border.effectDistance=new Vector2(1,-1);
        }
        private void SearchIcon(Transform parent, Sandplay.UI.RecordSearchGlyph.Kind kind, Color color)
        {
            var icon=ClientRect(parent,"Glyph",.17f,.17f,.66f,.66f);
            var glyph=icon.gameObject.AddComponent<Sandplay.UI.RecordSearchGlyph>(); glyph.Icon=kind; glyph.color=color; glyph.raycastTarget=false;
        }
    }
}
