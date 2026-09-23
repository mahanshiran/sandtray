using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Data;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private GameObject _clientsPage;
        private Transform _clientList;
        private Transform _clientDetail;
        private TMP_Text _clientStatus;
        private TMP_InputField _clientSearchInput;
        private Button _clientArchivedToggleButton;
        private ClientRecordStore _clientStore;
        private string _selectedClientId = "";
        private string _clientSearch = "";
        private bool _includeArchivedClients;
        private bool _clientReportsTab;
        private GameObject _clientDialog;
        private ClientRecordStore ClientStore => _clientStore ??= CreateClientStore();
        private ClientRecordStore CreateClientStore()
        {
            var guard = LocalAccountStorage.CaptureGuard();
            return new ClientRecordStore(Path.Combine(LocalAccountStorage.Root, "Clients"), guard,
                requireCreation: RequireClientCreationWorkspace, capacityProvider: () =>
                {
                    guard();
                    return SessionManager.Instance != null && SessionManager.Instance.CapacityEnforced && LocalAccountStorage.IsIsolated
                        ? new AggregateCapacityStore(LocalAccountStorage.OpenCapacityJournal(), guard, BackendClient.Instance.RequestLocalCapacity)
                        : null;
                });
        }

        private void BuildClientsPage()
        {
            _clientsPage = CreateHomePrimaryPage("ClientsPage");
            ClientText(_clientsPage.transform, Localization.Get("clients.title"), 30,
                .01f, .91f, .65f, .07f, HomeText);
            _clientStatus = ClientText(_clientsPage.transform, "", 12,
                .01f, 0f, .98f, .022f, HomeMuted);
            ClientButton(_clientsPage.transform, "clients.new", .85f, .91f, .14f, .065f,
                () => ShowClientEditor(null), true);
            var search = ClientInput(_clientsPage.transform, "", Localization.Get("clients.search"),
                .01f, .83f, .225f, .055f, 100);
            _clientSearchInput = search;
            search.SetTextWithoutNotify(_clientSearch);
            search.onValueChanged.AddListener(value => { _clientSearch = value; RefreshClientList(); });
            _clientArchivedToggleButton = CreateMenuButton(_clientsPage.transform, "ArchivedClientsToggle", "",
                new Vector2(.245f,.83f), new Vector2(.285f,.885f), HomeCard);
            ApplyHomeRoundedCorners(_clientArchivedToggleButton.GetComponent<Image>(), 10f);
            var archiveOutline = _clientArchivedToggleButton.gameObject.AddComponent<Outline>();
            archiveOutline.effectColor = HomeCardBorder;
            archiveOutline.effectDistance = new Vector2(1,-1);
            SearchIcon(_clientArchivedToggleButton.transform, Sandplay.UI.RecordSearchGlyph.Kind.Archive, HomeMuted);
            _clientArchivedToggleButton.onClick.AddListener(() =>
            {
                _includeArchivedClients = !_includeArchivedClients;
                RefreshClientsPage();
            });
            _clientList = ClientScroll(_clientsPage.transform, "ClientList", .01f, .025f, .275f, .79f);
            _clientDetail = ClientRect(_clientsPage.transform, "ClientDetail", .305f, .025f, .685f, .86f);
            var detailImage = _clientDetail.gameObject.AddComponent<Image>();
            detailImage.color = HomeCard;
            ApplyHomeRoundedCorners(detailImage, 12f);
            var detailOutline = _clientDetail.gameObject.AddComponent<Outline>();
            detailOutline.effectColor = HomeCardBorder;
            detailOutline.effectDistance = new Vector2(1,-1);
            BuildOrganizationTherapistClientsPanel();
            _homePages["clients"] = _clientsPage;
            _clientsPage.SetActive(false);
        }

        private void RefreshClientsPage()
        {
            if (_clientsPage == null) return;
            if (BackendClient.Instance.IsOrganizationTherapist)
            {
                RefreshOrganizationTherapistClientsPage();
                return;
            }
            if (_independentOrganizationClientMode &&
                BackendClient.Instance.UserType == "psychologist" &&
                _independentOrganizationWorkspace != null)
            {
                RefreshOrganizationTherapistClientsPage();
                return;
            }
            if (_organizationTherapistClientsPanel != null)
                _organizationTherapistClientsPanel.SetActive(false);
            try
            {
                if (_clientArchivedToggleButton != null)
                {
                    _clientArchivedToggleButton.GetComponent<Image>().color = _includeArchivedClients ? HomePrimary : HomeCard;
                    var archiveGlyph = _clientArchivedToggleButton.GetComponentInChildren<Sandplay.UI.RecordSearchGlyph>();
                    if (archiveGlyph != null) archiveGlyph.color = _includeArchivedClients ? Color.white : HomeMuted;
                }
                _clientStatus.text = ClientStore.RecoveredFromBackup ? Localization.Get("clients.recovered") : "";
                RefreshClientList();
                RefreshClientDetail();
                RefreshIndependentOrganizationWorkspaceAvailability();
            }
            catch (Exception) { _clientStatus.text = Localization.Get("clients.storage_error"); }
        }

        private List<SessionListEntry> ClientTables(string id)
        {
            var tables = SessionManager.Instance?.GetSavedSessions() ?? new List<SessionListEntry>();
            return FilterClientHistory(tables, id, "");
        }

        public static List<SessionListEntry> FilterClientHistory(IEnumerable<SessionListEntry> tables, string id, string query)
        {
            query = (query ?? "").Trim();
            return tables.Where(t => t != null && (t.ClientId ?? "") == (id ?? "") &&
                    (t.SessionName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(t => DateTimeOffset.TryParse(t.ModifiedAt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date : DateTimeOffset.MinValue)
                .ThenBy(t => t.SessionName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public sealed class ClientReportEntry
        {
            public string BoardName;
            public AnalysisReport Report;
        }

        public static List<ClientReportEntry> ClientReportHistory(IEnumerable<SessionListEntry> tables, string clientId,
            string query, Func<string, SessionData> load)
        {
            query = (query ?? "").Trim();
            return FilterClientHistory(tables, clientId, "")
                .SelectMany(t => (load(t.SessionName)?.Reports ?? new List<AnalysisReport>())
                    .Where(r => r != null && !r.Archived).Select(r => new ClientReportEntry { BoardName = t.SessionName, Report = r }))
                .Where(e => (e.BoardName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.Report.ResultText ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(e => DateTimeOffset.TryParse(e.Report.CreatedAt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date : DateTimeOffset.MinValue)
                .ThenBy(e => e.BoardName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Report.ReportId, StringComparer.Ordinal).ToList();
        }

        private void ConfirmDeleteClient(ClientRecord client)
        {
            var guard = LocalAccountStorage.CaptureGuard();
            var store = ClientStore;
            var box = ClientDialog(F("Delete client?", "删除来访者？"), 540, 320);
            var message = ClientText(box, client.Name + "\n\n" + F(
                "Delete this client profile from this device? Tables and reports will be kept under Unassigned tables. This cannot be undone here.",
                "从此设备删除该来访者资料？沙盘与报告将保留在未分配的沙盘中。此操作无法在此撤销。"), 16, .07f,.30f,.86f,.48f,HomeText);
            message.richText = false;
            var cancel = ClientButton(box,F("Cancel","取消"),.07f,.07f,.40f,.15f,()=>{if(_clientDialog!=null)Destroy(_clientDialog);_clientDialog=null;});
            Button confirm = null;
            confirm = ClientButton(box,F("Delete client","删除来访者"),.53f,.07f,.40f,.15f,()=>
            {
                try { guard(); } catch (Exception) { return; }
                confirm.interactable = cancel.interactable = false;
                store.DeleteAsync(client.Id, () =>
                {
                    guard();
                    var manager = SessionManager.Instance;
                    if(manager==null)throw new InvalidOperationException("Unable to access saved tables.");
                    foreach(var table in manager.GetSavedSessions(includeArchived:true).Where(t=>t.ClientId==client.Id))
                        manager.AssignClient(table.SessionName, "");
                    if(manager.CurrentClientId==client.Id)manager.CurrentClientId="";
                }, () =>
                {
                    if(box==null)return;
                    if(_clientDialog!=null)Destroy(_clientDialog);_clientDialog=null;
                    _selectedClientId="";RefreshClientsPage();
                }, error =>
                {
                    if(box==null)return;
                    message.text=F("Could not finish deleting the client. No tables or reports were deleted. Please retry.","无法完成删除。未删除任何沙盘或报告，请重试。");
                    confirm.interactable=cancel.interactable=true;
                });
            });
            confirm.GetComponent<Image>().color = new Color(.65f,.18f,.22f);
        }

        private void RefreshClientList()
        {
            if (_clientList == null) return;
            try
            {
                var clients = ClientStore.GetAll();
                if (ClientStore.RecoveredFromBackup && _clientStatus != null)
                    _clientStatus.text = Localization.Get("clients.recovered");
                var tables = SessionManager.Instance?.GetSavedSessions() ?? new List<SessionListEntry>();
                ClearClientChildren(_clientList);
                foreach (var client in clients.Where(c => _includeArchivedClients || !c.Archived)
                    .Where(c => string.IsNullOrWhiteSpace(_clientSearch) ||
                        ((c.Name ?? "") + " " + (c.Reference ?? "") + " " + c.Id + " " + (c.Account?.AccountCode ?? "")).IndexOf(_clientSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
                    AddClientListRow(client, tables);
                if (_clientList.childCount == 0)
                    ClientText(ClientRow(_clientList, "Empty", 80), Localization.Get("clients.empty"), 13,
                        .05f, .05f, .90f, .90f, HomeMuted);
            }
            catch (Exception) { _clientStatus.text = Localization.Get("clients.storage_error"); }
        }

        private void AddClientListRow(ClientRecord client, List<SessionListEntry> allTables)
        {
            string id = client?.Id ?? "";
            var tables = allTables.Where(t => (t.ClientId ?? "") == id).ToList();
            var row = ClientRow(_clientList, "Client", 72);
            bool selected = _selectedClientId == id;
            row.GetComponent<Image>().color = selected ? HomePrimary : HomeCard;
            ApplyHomeRoundedCorners(row.GetComponent<Image>(), 12f);
            var button = row.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                _selectedClientId = id;
                _clientReportsTab = false;
                RefreshClientsPage();
            });
            ClientRecordAvatar(row, client, .025f, .20f, .18f, .60f);
            ClientText(row, client?.Name ?? Localization.Get("clients.unassigned"), 16,
                .24f, .47f, .72f, .46f, selected ? Color.white : HomeText);
            string detail = client?.Archived == true ? Localization.Get("clients.archived") + " · " : "";
            if (!string.IsNullOrEmpty(client?.Reference)) detail += client.Reference + " · ";
            detail += Localization.Get("clients.counts", tables.Count, tables.Sum(t => t.ReportCount));
            ClientText(row, detail, 11, .24f, .08f, .72f, .36f,
                selected ? new Color(1,1,1,.72f) : HomeMuted);
        }

        private void RefreshClientDetail()
        {
            ClearClientChildren(_clientDetail);
            var client = ClientStore.GetAll().Find(c => c.Id == _selectedClientId);
            if (client == null) _selectedClientId = "";
            // Do not expose unassigned tables from the client workspace.
            var tables = client == null ? new List<SessionListEntry>() : ClientTables(_selectedClientId);
            ClientRecordAvatar(_clientDetail, client, .035f, .865f, .09f, .11f);
            var clientTitle = ClientText(_clientDetail, client?.Name ?? F("Select a client","请选择来访者"), 24,
                .145f, .865f, client == null ? .50f : .33f, .11f, HomeText);
            clientTitle.enableWordWrapping = false;
            clientTitle.overflowMode = TextOverflowModes.Ellipsis;
            clientTitle.richText = false;
            if (client == null)
            {
                var emptyPrompt = ClientText(_clientDetail,
                    F("Select a client to view tables and reports.", "请选择来访者以查看沙盘和报告。"),
                    14, .08f, .40f, .84f, .10f, HomeMuted);
                emptyPrompt.alignment = TextAlignmentOptions.Center;
                return;
            }
            // Keep the history controls directly below the last visible profile row.
            // Empty optional fields should not reserve a large blank band.
            float tabsY = .79f;
            if (client != null)
            {
                var deleteClient = ClientButton(_clientDetail, F("Delete client", "删除来访者"), .50f, .885f, .15f, .065f,
                    () => ConfirmDeleteClient(client));
                deleteClient.GetComponentInChildren<TMP_Text>().color = new Color(.94f,.30f,.32f);
                ClientButton(_clientDetail, "clients.edit", .67f, .885f, .135f, .065f,
                    () => ShowClientEditor(client));
                string info = string.Join("  ·  ", new[] { client.Reference,
                    string.IsNullOrEmpty(client.DateOfBirth) ? "" : ClientDateLabel(client.DateOfBirth), client.Email, client.Phone }
                    .Where(v => !string.IsNullOrWhiteSpace(v)));
                bool hasInfo = !string.IsNullOrWhiteSpace(info);
                bool hasNotes = !string.IsNullOrWhiteSpace(client.Notes);
                if (hasInfo) ClientText(_clientDetail, info, 12, .035f, .815f, .93f, .04f, HomeMuted);
                if (hasNotes) ClientText(_clientDetail, client.Notes, 12, .035f,
                    hasInfo ? .765f : .815f, .93f, .04f, HomeMuted);
                tabsY = hasInfo && hasNotes ? .69f : hasInfo || hasNotes ? .74f : .79f;
            }

            if (client != null)
            {
                ClientButton(_clientDetail, client.Archived ? "clients.restore" : "clients.archive",
                    .825f, .885f, .14f, .065f, () =>
                    {
                        try { client.Archived = !client.Archived; ClientStore.Save(client); RefreshClientsPage(); }
                        catch (Exception) { _clientStatus.text = Localization.Get("clients.storage_error"); }
                    });
            }
            var tablesTab = ClientButton(_clientDetail, "clients.tables", .035f, tabsY, .465f, .06f,
                () => { _clientReportsTab = false; RefreshClientDetail(); });
            var reportsTab = ClientButton(_clientDetail, "clients.reports", .50f, tabsY, .465f, .06f,
                () => { _clientReportsTab = true; RefreshClientDetail(); });
            StyleContentTab(tablesTab, !_clientReportsTab);
            StyleContentTab(reportsTab, _clientReportsTab);
            float nextRowY = tabsY - .075f;
            if (client != null && !client.Archived)
            {
                bool canSchedule = client.Account != null && client.Account.Backend == BackendClient.BaseUrl && Guid.TryParse(client.Account.IdentityCode, out _);
                ClientButton(_clientDetail, "host.title", .035f, nextRowY, canSchedule ? .45f : .93f, .06f,
                    () => CreateHostedClientTable(client.Id), true);
                if (canSchedule) ClientButton(_clientDetail, F("Schedule session", "预约会话"), .515f, nextRowY, .45f, .06f,
                    () => ShowScheduleProposal(client));
                nextRowY -= .075f;
            }
            if (_clientReportsTab)
            {
                ClientButton(_clientDetail, F("+ New report", "+ 新建报告"), .035f, nextRowY, .93f, .055f,
                    () => NewClientReport(_selectedClientId), true);
                nextRowY -= .07f;
            }
            float searchY = nextRowY;
            var historySearch = ClientInput(_clientDetail, "", Localization.Get(_clientReportsTab ? "clients.search_reports" : "clients.search_tables"),
                .035f, searchY, .93f, .055f, 100);
            var content = ClientScroll(_clientDetail, "TablesAndReports", .035f, .025f, .93f, searchY - .04f);
            ApplyHomeRoundedCorners(content.parent.GetComponent<Image>(), 12f);
            // This cache belongs to the open detail view, never another client/tab.
            var reportCache = new Dictionary<string, SessionData>();
            SessionData LoadHistory(string name)
            {
                if (!reportCache.TryGetValue(name, out var data))
                {
                    data = SessionManager.Instance.LoadSessionData(name);
                    reportCache[name] = data;
                }
                return data;
            }
            void RenderHistory(string query)
            {
                ClearClientChildren(content);
                if (_clientReportsTab)
                {
                    foreach (var entry in ClientReportHistory(tables, _selectedClientId, query,
                        LoadHistory))
                    {
                            var report = entry.Report;
                            var row = ClientRow(content, "Report", 72);
                            ApplyHomeRoundedCorners(row.GetComponent<Image>(), 12f);
                            ClientText(row, entry.BoardName, 15, .03f, .47f, .67f, .46f, HomeText);
                            string date = DateTimeOffset.TryParse(report.CreatedAt, out var created)
                                ? created.ToLocalTime().ToString("g", Localization.Culture) : report.CreatedAt;
                            ClientText(row, date ?? "", 12, .03f, .08f, .67f, .35f, HomeMuted);
                            ClientButton(row, "clients.view", .73f, .20f, .24f, .60f,
                                () => OpenReportWorkspaceRecord(entry.BoardName, false, report.ReportId));
                    }
                }
                else foreach (var table in FilterClientHistory(tables, _selectedClientId, query)) AddClientTableRow(content, table);
                if (content.childCount == 0)
                    ClientText(ClientRow(content, "Empty", 90), Localization.Get(!string.IsNullOrWhiteSpace(query)
                        ? "clients.history_no_matches" : _clientReportsTab ? "clients.no_reports" : "clients.no_tables"),
                        14, .05f, .05f, .90f, .90f, HomeMuted);
            }
            historySearch.onValueChanged.AddListener(RenderHistory);
            RenderHistory("");
        }

        private void AddClientTableRow(Transform content, SessionListEntry table)
        {
            var row = ClientRow(content, "Table", 72);
            ApplyHomeRoundedCorners(row.GetComponent<Image>(), 12f);
            var preview = ClientRect(row, "Preview", .015f, .12f, .16f, .76f).gameObject.AddComponent<Image>();
            preview.sprite = ScreenshotManager.LoadThumbnail(table.SessionName);
            preview.preserveAspect = true;
            preview.color = preview.sprite != null ? Color.white : new Color(.20f, .25f, .32f);
            preview.raycastTarget = false;
            ClientText(row, table.SessionName, 15, .20f, .51f, .47f, .40f, HomeText);
            ClientText(row, Localization.Get("clients.report_count", table.ReportCount), 11,
                .20f, .12f, .34f, .33f, HomeMuted);
            var openRow = row.gameObject.AddComponent<Button>();
            openRow.targetGraphic = row.GetComponent<Image>();
            openRow.onClick.AddListener(() =>
            {
                var client = ClientStore.GetAll().Find(item => item.Id == table.ClientId);
                if (client == null || client.Archived) return;
                if (!RequireMultiplayerAccount(() => OpenClientHistoryAsHost(table.SessionName, table.ClientId))) return;
                OpenClientHistoryAsHost(table.SessionName, table.ClientId);
            });
            var reports = ClientButton(row, "board.reports", .69f, .25f, .20f, .50f,
                () => ShowReportsPanel(table.SessionName));
            ApplyHomeRoundedCorners(reports.GetComponent<Image>(), 8f);
            var more = ClientButton(row, "", .915f, .25f, .065f, .50f,
                () => ToggleBoardOverflowMenu(row, table.SessionName, table.SessionName, table.SessionName));
            more.name = "Btn_More";
            ApplyHomeRoundedCorners(more.GetComponent<Image>(), 8f);
            AddHomeIconGraphic(more.transform, "more", new Vector2(.18f,.18f), new Vector2(.82f,.82f), HomeText);
        }

        private void NewClientReport(string clientId)
        {
            if (!BackendClient.Instance.IsLoggedIn) { OpenLoginScreen(() => NewClientReport(clientId)); return; }
            var account = LocalAccountStorage.CaptureGuard();
            var boards = ClientTables(clientId);
            if (boards.Count == 1) { OpenReportWorkspace(boards[0].SessionName); return; }
            var box = ClientDialog(F("New report", "新建报告"), 640, 580);
            var caption = ClientText(box, boards.Count == 0
                ? F("Reports are saved with a table. Create a table for this client first, then open Reports to write your report.", "报告随沙盘保存。请先为此来访者创建沙盘，再打开报告编写。")
                : F("Choose this client's table for the report.", "选择此报告所属的来访者沙盘。"), 16, .05f,.68f,.9f,.13f,HomeText);
            caption.richText = false;
            if (boards.Count == 0)
            {
                ClientButton(box,F("Create table","创建沙盘"),.05f,.50f,.90f,.09f,()=>
                { try { account(); } catch (Exception) { return; } Destroy(_clientDialog); _clientDialog=null; CreateClientTable(clientId); },true);
                return;
            }
            var list = ClientScroll(box,"Report table",.05f,.08f,.90f,.56f);
            foreach(var board in boards)
            {
                var row=ClientRow(list,"Report table",64);
                var button=ClientButton(row,board.SessionName,.025f,.08f,.95f,.84f,()=>
                {
                    try { account(); } catch (Exception) { return; }
                    var current=SessionManager.Instance.LoadSessionData(board.SessionName);
                    if(current==null || (current.ClientId??"")!=(clientId??""))
                    {caption.text=F("The table assignment changed. Reopen the client's reports.","沙盘归属已更改，请重新打开来访者报告。");return;}
                    OpenReportWorkspace(board.SessionName);
                });
                button.GetComponentInChildren<TMP_Text>().richText=false;
            }
        }

        private void CreateClientTable(string clientId)
        {
            ShowNameDialog(Localization.Get("dialog.new_board"),
                "Table " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), name =>
                {
                    if (!string.IsNullOrWhiteSpace(name)) ShowSizeDialog(name, clientId);
                });
        }

        public static string AssignmentClientLabel(string id, IEnumerable<ClientRecord> clients)
        {
            if (string.IsNullOrEmpty(id)) return Localization.Get("clients.history_unassigned");
            var client = clients.FirstOrDefault(c => c.Id == id);
            if (client == null) return Localization.Get("clients.history_missing", id);
            return client.Name + (client.Archived ? " · " + Localization.Get("clients.archived") : "");
        }

        private void ShowClientAssignmentHistory(string tableName)
        {
            var box = ClientDialog(Localization.Get("clients.history"), 660, 520);
            ClientText(box, tableName, 16, .05f, .76f, .9f, .09f, HomeText);
            ClientText(box, Localization.Get("clients.history_local"), 12, .05f, .64f, .9f, .11f, HomeMuted);
            var content = ClientScroll(box, "AssignmentHistory", .05f, .06f, .90f, .56f);
            try
            {
                var data = SessionManager.Instance.LoadSessionData(tableName);
                if (data == null) throw new InvalidDataException("Board unavailable");
                var clients = ClientStore.GetAll();
                var history = data.ClientAssignmentHistory ?? new List<ClientAssignmentChange>();
                // Stored append order remains meaningful even if the device clock changed.
                foreach (var change in history.AsEnumerable().Reverse().Where(c => c != null))
                {
                    var row = ClientRow(content, "AssignmentChange", 104);
                    string from = AssignmentClientLabel(change.PreviousClientId, clients);
                    string to = AssignmentClientLabel(change.ClientId, clients);
                    ClientText(row, Localization.Get("clients.history_change", from, to), 14,
                        .04f, .40f, .92f, .56f, HomeText);
                    string date = DateTimeOffset.TryParse(change.ChangedAt, out var instant)
                        ? instant.ToLocalTime().ToString("g", Localization.Culture)
                        : Localization.Get("clients.history_unknown_date");
                    ClientText(row, date, 12, .04f, .05f, .92f, .30f, HomeMuted);
                }
                if (content.childCount == 0)
                    ClientText(ClientRow(content, "Empty", 80), Localization.Get("clients.history_empty"),
                        14, .04f, .05f, .92f, .90f, HomeMuted);
            }
            catch (Exception)
            {
                ClearClientChildren(content);
                ClientText(ClientRow(content, "Error", 80), Localization.Get("clients.storage_error"),
                    14, .04f, .05f, .92f, .90f, HomeMuted);
            }
        }

        private void CreateHostedClientTable(string clientId)
        {
            if (!RequireMultiplayerAccount(() => CreateHostedClientTable(clientId))) return;
            var client = ClientStore.GetAll().Find(c => c.Id == clientId);
            if (client == null || client.Archived || !CanUsePersonalClientDirectory) return;
            _pendingHostMode = HostMode.None;
            // Practitioner hosts, client waits for explicit editing approval.
            _hostTherapistMode = true;
            ShowNameDialog(Localization.Get("dialog.new_board"),
                "Table " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), name =>
                {
                    if (!string.IsNullOrWhiteSpace(name)) ShowSizeDialog(name, clientId, hostOnline: true,
                        inviteTarget: InviteTargetForClient(client));
                });
        }

        private void OpenClientHistoryAsHost(string boardName, string clientId)
        {
            var client = ClientStore.GetAll().Find(item => item.Id == clientId);
            if (client == null || client.Archived || !CanUsePersonalClientDirectory) return;
            _hostTherapistMode = true;
            _pendingHostMode = HostMode.Cloud;
            EnterSandbox(boardName, false, inviteTarget: InviteTargetForClient(client));
        }

        private void ShowMoveTableDialog(string tableName)
        {
            if (!CanUsePersonalClientDirectory) return;
            var box = ClientDialog(Localization.Get("clients.move_title"), 620, 500);
            var title = box.GetComponentInChildren<TMP_Text>();
            if (title != null) title.rectTransform.anchorMax = new Vector2(.47f, title.rectTransform.anchorMax.y);
            var history = ClientButton(box, "clients.history", .49f, .885f, .34f, .065f,
                () => ShowClientAssignmentHistory(tableName));
            ApplyHomeRoundedCorners(history.GetComponent<Image>(), 8f);
            ClientText(box, tableName, 16, .05f, .77f, .90f, .10f, HomeMuted);
            var search = ClientInput(box, "", Localization.Get("clients.search"), .05f, .67f, .90f, .075f, 100);
            var content = ClientScroll(box, "Destinations", .05f, .06f, .90f, .58f);
            var status = ClientText(box, "", 12, .05f, .005f, .9f, .05f, new Color(1f,.65f,.55f));
            string confirmedDestination = null;
            void Populate(string query)
            {
                try
                {
                    var clients = ClientStore.GetAll().Where(c => !c.Archived &&
                        ((c.Name ?? "") + " " + (c.Reference ?? "") + " " + c.Id + " " + (c.Account?.AccountCode ?? "")).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(c => c.Name).ToList();
                    ClearClientChildren(content);
                    clients.Insert(0, new ClientRecord { Id = "", Name = Localization.Get("clients.unassigned") });
                    foreach (var client in clients)
                    {
                        var row = ClientRow(content, "Destination", 58);
                        var button = row.gameObject.AddComponent<Button>();
                        ClientText(row, confirmedDestination == client.Id ? Localization.Get("clients.confirm_move", client.Name)
                            : client.Name + (string.IsNullOrEmpty(client.Reference) ? "" : " · " + client.Reference),
                            16, .04f, .05f, .92f, .90f, HomeText);
                        button.onClick.AddListener(() =>
                        {
                            if (confirmedDestination != client.Id)
                            {
                                confirmedDestination = client.Id;
                                Populate(query);
                                return;
                            }
                            try
                            {
                                SessionManager.Instance.AssignClient(tableName, client.Id);
                                CloseClientDialog();
                                RefreshBoardList();
                                RefreshClientsPage();
                            }
                            catch (Exception) { status.text = Localization.Get("clients.storage_error"); }
                        });
                    }
                }
                catch (Exception) { status.text = Localization.Get("clients.storage_error"); }
            }
            search.onValueChanged.AddListener(query => { confirmedDestination = null; Populate(query); });
            Populate("");
        }

        private void ShowClientEditor(ClientRecord original)
        {
            var backend = BackendClient.Instance;
            if (backend != null && backend.IsOrganizationTherapist)
            {
                ShowLockedFeatureDialog(F(
                    "Managed therapist accounts use the organization client directory. That directory is not available in this first release, so personal client records cannot be created or edited here.",
                    "受管理的治疗师账户使用机构来访者目录。首个版本尚未提供该目录，因此无法在此创建或编辑个人来访者记录。"));
                return;
            }
            // Existing records stay editable after downgrade. New records require current access.
            if (original == null) { WithLocalCreation(() => WithAccess("clients.manage", () => ShowAuthorizedClientEditor(null), false, false)); return; }
            ShowAuthorizedClientEditor(original);
        }

        private void ShowAuthorizedClientEditor(ClientRecord original)
        {
            var box = ClientDialog(Localization.Get(original == null ? "clients.new" : "clients.edit"), 780, 740);
            var content = ClientScroll(box, "ProfileForm", .05f, .14f, .90f, .72f);
            var safeSize = ((RectTransform)_safeArea.transform).rect.size;
            bool narrow = ((RectTransform)box).rect.width < 560 || safeSize.y > safeSize.x;
            var account = original?.Account;
            Transform accountRow = null;
            if (original?.Account == null)
            {
                accountRow = ClientRow(content, "AccountLink", 88);
                var accountImage = accountRow.GetComponent<Image>();
                accountImage.color = HomeCard;
                ApplyHomeRoundedCorners(accountImage, 10f);
                var accountBorder = accountRow.gameObject.AddComponent<Outline>();
                accountBorder.effectColor = HomeCardBorder;
                accountBorder.effectDistance = new Vector2(1f, -1f);
            }
            var form = ClientRow(content, "Fields", narrow ? 880 : 550);
            form.GetComponent<Image>().color = Color.clear;
            float fieldWidth = narrow ? .96f : .46f;
            float fieldHeight = narrow ? .05f : .073f;
            TMP_InputField Field(string key, string value, float x, float y, int limit)
            {
                ClientText(form, Localization.Get(key), 12, x, y + fieldHeight, fieldWidth, narrow ? .034f : .05f, HomeMuted);
                return ClientInput(form, value ?? "", "", x, y, fieldWidth, fieldHeight, limit);
            }
            var name = Field("clients.name", original?.Name, .02f, narrow ? .69f : .66f, 100);
            byte[] draftPhoto = null;
            bool photoChanged = false;
            var avatar = ClientAvatar(form, original?.Name, original?.PhotoFile, .42f, .82f, .16f, .16f);
            bool manualPhoto = !string.IsNullOrEmpty(original?.PhotoFile) || original?.AccountPhotoSuppressed == true;
            bool photoLoading = false;
            int photoRequest = 0;
            var editorDialog = _clientDialog;
            int editorEpoch = LocalAccountStorage.Epoch;
            void RefreshPhotoOverlay()
            {
                SetClientPhotoOverlay(avatar, photoChanged ? draftPhoto != null : !string.IsNullOrEmpty(original?.PhotoFile));
            }
            RefreshPhotoOverlay();
            void ResetAccountPhoto()
            {
                photoRequest++;
                photoLoading = false;
                if (manualPhoto) return;
                draftPhoto = null;
                photoChanged = false;
                SetClientAvatar(avatar, name.text, null);
                RefreshPhotoOverlay();
            }
            void ImportAccountPhoto(FriendPerson person)
            {
                ResetAccountPhoto();
                if (manualPhoto || string.IsNullOrWhiteSpace(person.avatar_url)) return;
                int request = photoRequest;
                photoLoading = true;
                Sandplay.UI.AccountAvatar.LoadPublicPhoto(person.avatar_url, bytes =>
                {
                    if (this == null || editorDialog == null || _clientDialog != editorDialog ||
                        editorEpoch != LocalAccountStorage.Epoch || request != photoRequest || manualPhoto) return;
                    photoLoading = false;
                    if (bytes == null) return;
                    var source = new Texture2D(2, 2);
                    try
                    {
                        if (!source.LoadImage(bytes)) return;
                        draftPhoto = Sandplay.UI.ClientPhotoPicker.EncodeAvatar(source);
                        photoChanged = true;
                        SetClientAvatar(avatar, name.text, draftPhoto);
                        RefreshPhotoOverlay();
                    }
                    catch (Exception) { /* Keep initials and allow manual photo selection. */ }
                    finally { if (Application.isPlaying) Destroy(source); else DestroyImmediate(source); }
                });
            }
            void SelectAccount(bool friends)
            {
                ShowClientAccountPicker(_clientDialog.transform, friends, person =>
                {
                    account = new ClientAccountLink { UserId = person.id, IdentityCode = person.code, AccountCode = string.IsNullOrEmpty(person.friend_code) ? person.code : person.friend_code,
                        DisplayName = person.name, Backend = BackendClient.BaseUrl };
                    if (string.IsNullOrWhiteSpace(name.text)) name.text = person.name ?? "";
                    ImportAccountPhoto(person);
                });
            }
            if (original?.Account == null)
            {
                ClientEditorSecondaryButton(accountRow, F("Add from account ID", "从账号 ID 添加"),
                    .03f, .15f, .45f, .70f, () => SelectAccount(false));
                if (BackendClient.Instance.ExternalContactsAllowed)
                    ClientEditorSecondaryButton(accountRow, F("Add from friends", "从好友添加"),
                        .52f, .15f, .45f, .70f, () => SelectAccount(true));
                else
                    ClientText(accountRow, F("Link an account to this client", "为来访者关联账号"),
                        12, .52f, .20f, .45f, .60f, HomeMuted);
            }
            var reference = Field("clients.reference", original?.Reference, narrow ? .02f : .52f, narrow ? .59f : .66f, 60);
            string dob = original?.DateOfBirth ?? "";
            float dobY = narrow ? .49f : .51f;
            ClientText(form, Localization.Get("clients.dob"), 12, .02f, dobY + fieldHeight, fieldWidth, narrow ? .034f : .05f, HomeMuted);
            Button dateButton = null;
            dateButton = ClientEditorSecondaryButton(form, ClientDateLabel(dob), .02f, dobY, fieldWidth, fieldHeight,
                () => ShowClientDatePicker(_clientDialog.transform, dob, value =>
                {
                    dob = value;
                    dateButton.GetComponentInChildren<TMP_Text>().text = ClientDateLabel(value);
                }));
            var phone = Field("clients.phone", original?.Phone, narrow ? .02f : .52f, narrow ? .39f : .51f, 60);
            var email = Field("clients.email", original?.Email, .02f, narrow ? .29f : .36f, 150);
            ClientText(form, Localization.Get("clients.notes"), 12, .02f, narrow ? .22f : .28f, .96f, .05f, HomeMuted);
            var notes = ClientInput(form, original?.Notes ?? "", "", .02f, .03f, .96f, narrow ? .19f : .25f, 2000);
            notes.lineType = TMP_InputField.LineType.MultiLineNewline;
            notes.textComponent.alignment = TextAlignmentOptions.TopLeft;
            var error = ClientText(box, "", 12, .05f, .105f, .90f, .03f, new Color(1,.65f,.55f));
            var picker = new GameObject("ClientPhotoPicker-" + Guid.NewGuid().ToString("N"))
                .AddComponent<Sandplay.UI.ClientPhotoPicker>();
            picker.transform.SetParent(_clientDialog.transform, false);
            var avatarButton = avatar.gameObject.AddComponent<Button>();
            avatarButton.transition = Selectable.Transition.None;
            avatarButton.onClick.AddListener(() =>
            {
                error.text = "";
                picker.Pick(bytes =>
                {
                    manualPhoto = true;
                    photoRequest++;
                    photoLoading = false;
                    draftPhoto = bytes;
                    photoChanged = true;
                    SetClientAvatar(avatar, name.text, bytes);
                    RefreshPhotoOverlay();
                }, () => error.text = Localization.Get("clients.photo_error"), Localization.Get("clients.choose_photo"));
            });
            if (!narrow) ClientText(form, Localization.Get("clients.optional"), 12, .52f, .34f, .46f, .10f, HomeMuted);
            ClientEditorSecondaryButton(box, "dialog.cancel", .49f, .035f, .21f, .065f, CloseClientDialog);
            ClientRecord pendingClient=null;
            bool savePending=false;
            ClientButton(box, "clients.save", .73f, .035f, .22f, .065f, () =>
            {
                try
                {
                    if(savePending)return;
                    if (photoLoading) { error.text = F("Loading profile photo. Please wait, or remove it to continue.", "正在加载头像，请稍候，或移除头像后继续。"); return; }
                    var dialogAtSave=_clientDialog;
                    int epoch=LocalAccountStorage.Epoch;
                    pendingClient = new ClientRecord
                    {
                        Id = pendingClient?.Id ?? original?.Id, Name = name.text, Reference = reference.text,
                        DateOfBirth = dob, Phone = phone.text.Trim(), Email = email.text.Trim(),
                        Notes = notes.text.Trim(), Archived = original?.Archived ?? false, Account = account,
                        AccountPhotoSuppressed = photoChanged ? draftPhoto == null : original?.AccountPhotoSuppressed ?? false
                    };
                    savePending=true;
                    ClientStore.SaveAsync(pendingClient,draftPhoto,photoChanged,original==null,client=>
                    {
                    savePending=false;
                    if(this==null || epoch!=LocalAccountStorage.Epoch || _clientDialog!=dialogAtSave)return;
                    _selectedClientId = client.Id;
                    _clientSearch = "";
                    _clientSearchInput?.SetTextWithoutNotify("");
                    _includeArchivedClients = client.Archived;
                    CloseClientDialog();
                    RefreshClientsPage();
                    RefreshBoardList();
                    },message=>{savePending=false;if(error && epoch==LocalAccountStorage.Epoch && _clientDialog==dialogAtSave)error.text=Localization.Get(message);});
                }
                catch (ArgumentException ex) { error.text = Localization.Get(ex.Message); }
                catch (Exception) { error.text = Localization.Get("clients.storage_error"); }
            }, true);
        }

        private Transform ClientDialog(string title, float width, float height)
        {
            CloseClientDialog();
            Canvas.ForceUpdateCanvases();
            _clientDialog = ClientRect(_safeArea.transform, "ClientDialog", 0, 0, 1, 1).gameObject;
            _clientDialog.AddComponent<Sandplay.UI.KeyboardFocusScope>().Configure(true, CloseClientDialog);
            _clientDialog.AddComponent<Image>().color = new Color(0, 0, 0, HomeIsLight ? .42f : .72f);
            Sandplay.UI.DialogBackdrop.Apply(_clientDialog.GetComponent<Image>());
            var card = ClientRect(_clientDialog.transform, "Card", .5f, .5f, 0, 0);
            var available = ((RectTransform)_safeArea.transform).rect.size;
            card.sizeDelta = new Vector2(Mathf.Min(width, available.x - 32), Mathf.Min(height, available.y - 32));
            card.gameObject.AddComponent<Image>().color = HomeCard;
            ApplyHomeRoundedCorners(card.GetComponent<Image>(), 14f);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1,-1);
            ClientText(card, title, 24, .05f, .865f, .78f, .10f, HomeText);
            var close = CreateMenuButton(card, "Close", "×", new Vector2(.88f,.89f), new Vector2(.95f,.96f), HomeChromeButton);
            close.GetComponentInChildren<TextMeshProUGUI>().color = HomeText;
            close.onClick.AddListener(CloseClientDialog);
            return card;
        }

        private void CloseClientDialog()
        {
            if (_clientDialog != null)
            {
                _clientDialog.GetComponent<Sandplay.UI.ShortcutKeyCapture>()?.EndSession();
                _clientDialog.SetActive(false);
                if (Application.isPlaying) Destroy(_clientDialog);
                else DestroyImmediate(_clientDialog);
            }
            _clientDialog = null;
        }

        private void ShowClientMessage(string key)
        {
            var box = ClientDialog(Localization.Get("clients.tables"), 540, 260);
            ClientText(box, Localization.Get(key), 16, .05f, .2f, .90f, .60f, HomeText);
        }

        private RectTransform ClientRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x + w, y + h);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private TMP_Text ClientText(Transform parent, string value, int size, float x, float y, float w, float h, Color color)
        {
            if (IsSecondaryTextColor(color)) size = Mathf.Max(6, size - 2);
            var text = ClientRect(parent, "Text", x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = GetUIFont();
            text.text = value;
            text.fontSize = size;
            text.fontSizeMin = Mathf.Max(6, size - 2);
            text.fontSizeMax = size;
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            text.raycastTarget = false;
            return text;
        }

        private bool IsSecondaryTextColor(Color color)
        {
            return SameTextColor(color, HomeMuted) || SameTextColor(color, HomeSidebarMuted);
        }

        private static bool SameTextColor(Color left, Color right)
        {
            const float tolerance = .002f;
            return Mathf.Abs(left.r - right.r) < tolerance &&
                   Mathf.Abs(left.g - right.g) < tolerance &&
                   Mathf.Abs(left.b - right.b) < tolerance &&
                   Mathf.Abs(left.a - right.a) < tolerance;
        }

        private Button ClientButton(Transform parent, string key, float x, float y, float w, float h, Action action, bool primary = false)
        {
            var button = CreateMenuButton(parent, key, Localization.Get(key), new Vector2(x,y), new Vector2(x+w,y+h),
                primary ? HomePrimary : HomeChromeButton);
            ApplyHomeRoundedCorners(button.GetComponent<Image>(), 10f);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.color = primary ? Color.white : HomeText;
            text.fontSize = 13;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10;
            text.fontSizeMax = 13;
            button.onClick.AddListener(() => action());
            if (key == "report.back" || key == F("Back to notifications", "返回通知中心"))
                Sandplay.UI.RecordSearchGlyph.StyleBackButton(button, HomeText);
            return button;
        }

        private Button ClientEditorSecondaryButton(Transform parent, string key, float x, float y, float w, float h, Action action)
        {
            var button = ClientButton(parent, key, x, y, w, h, action);
            var image = button.GetComponent<Image>();
            image.color = HomeIsLight ? new Color(.90f,.96f,.95f,1f) : HomeChromeButton;
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = HomeIsLight ? new Color(.03f,.48f,.46f,.32f) : HomeCardBorder;
            outline.effectDistance = new Vector2(1f,-1f);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.color = HomeIsLight ? HomePrimary : HomeText;
            return button;
        }

        private TMP_InputField ClientInput(Transform parent, string value, string placeholder,
            float x, float y, float w, float h, int limit)
        {
            var rect = ClientRect(parent, "Input", x,y,w,h);
            rect.gameObject.AddComponent<Image>().color = HomeIsLight ? new Color(.965f,.972f,.97f) : new Color(.045f,.09f,.11f);
            ApplyHomeRoundedCorners(rect.GetComponent<Image>(), 10f);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = HomeCardBorder;
            outline.effectDistance = new Vector2(1,-1);
            var viewport = ClientRect(rect, "Viewport", 0,0,1,1);
            viewport.offsetMin = new Vector2(10,5);
            viewport.offsetMax = new Vector2(-10,-5);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = ClientText(viewport, "", 15, 0,0,1,1,HomeText);
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow;
            var hint = ClientText(viewport, placeholder, 13, 0,0,1,1,HomeMuted);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = limit;
            input.text = value;
            return input;
        }

        private Transform ClientScroll(Transform parent, string name, float x,float y,float w,float h)
        {
            var viewport = ClientRect(parent,name,x,y,w,h);
            viewport.gameObject.AddComponent<Image>().color = HomeIsLight ? new Color(.965f,.972f,.97f,.95f) : new Color(.04f,.08f,.10f,.82f);
            ApplyHomeRoundedCorners(viewport.GetComponent<Image>(), 12f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = ClientRect(viewport,"Content",0,1,1,0);
            content.pivot = new Vector2(.5f,1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8,16,8,8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            var track = ClientRect(viewport,"Scrollbar",1,0,0,1);
            track.pivot = new Vector2(1,.5f);
            track.sizeDelta = new Vector2(5,-12);
            track.anchoredPosition = new Vector2(-3,0);
            var handle = ClientRect(track,"Handle",0,0,1,1);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(.45f,.52f,.64f,.65f);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private Transform ClientRow(Transform parent, string name, float height)
        {
            var row = ClientRect(parent,name,0,0,1,0);
            row.gameObject.AddComponent<Image>().color = HomeCard;
            ApplyHomeRoundedCorners(row.GetComponent<Image>(), 12f);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        private void ClearClientChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
