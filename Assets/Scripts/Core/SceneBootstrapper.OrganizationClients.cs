using System;
using System.Globalization;
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
        private GameObject _organizationTherapistClientsPanel;
        private Transform _organizationTherapistClientList;
        private Transform _organizationTherapistClientDetail;
        private TMP_Text _organizationTherapistClientStatus;
        private Button _organizationTherapistNewClientButton;
        private Button _organizationTherapistArchivedButton;
        private Button _independentOrganizationSwitchButton;
        private Button _organizationClientsPersonalButton;
        private BackendClient.OrganizationWorkspace _independentOrganizationWorkspace;
        private BackendClient.OrganizationWorkspace _activeOrganizationClientWorkspace;
        private bool _independentOrganizationClientMode;
        private int _independentOrganizationWorkspaceRequest;
        private bool _organizationTherapistShowArchived;
        private int _organizationTherapistClientRequest;
        private string _selectedOrganizationTherapistClientId;
        private BackendClient.OrganizationClient[] _organizationTherapistClients =
            Array.Empty<BackendClient.OrganizationClient>();
        private bool _organizationTherapistClientsOffline;
        private string _organizationClientSearch = "";
        private bool _organizationClientReportsTab;
        private bool _organizationClientReplaysTab;
        private Button _organizationClientsRefreshButton;
        private readonly OrganizationClientSession[] _organizationClientSessions = new OrganizationClientSession[2];
        private int _organizationClientsSessionAccount = -1, _organizationClientsSessionEpoch = -1;
        private bool _organizationClientsLoading, _organizationClientsLoadingArchived;

        private sealed class OrganizationClientSession
        {
            public BackendClient.OrganizationWorkspace Workspace;
            public BackendClient.OrganizationClient[] Clients;
            public bool Offline;
        }

        private void ForceRefreshOrganizationClients()
        {
            Array.Clear(_organizationClientSessions, 0, _organizationClientSessions.Length);
            _organizationClientsLoading = false;
            ++_organizationTherapistClientRequest;
            RefreshOrganizationTherapistClientsPage();
        }

        private bool OrganizationClientsPageVisible => _clientsPage != null && _clientsPage.activeInHierarchy &&
            (BackendClient.Instance.IsOrganizationTherapist || _independentOrganizationClientMode);

        private void CompleteOrganizationClientsLoad(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationClient[] clients, bool archived, bool offline)
        {
            _organizationClientsLoading = false;
            _organizationClientSessions[archived ? 1 : 0] = new OrganizationClientSession
                { Workspace = workspace, Clients = clients, Offline = offline };
            if (OrganizationClientsPageVisible && archived == _organizationTherapistShowArchived)
            {
                _activeOrganizationClientWorkspace = workspace;
                _organizationClientsRefreshButton.interactable = true;
                RenderOrganizationTherapistClients(workspace, clients, offline);
            }
        }

        private void FailOrganizationClientsLoad(string error)
        {
            _organizationClientsLoading = false;
            if (!OrganizationClientsPageVisible || _organizationClientsLoadingArchived != _organizationTherapistShowArchived) return;
            _organizationClientsRefreshButton.interactable = true;
            _organizationTherapistClientStatus.text = error;
        }

        [Serializable]
        private sealed class OrganizationClientCache
        {
            public int schema = 1;
            public int user_id;
            public string organization_id;
            public string saved_at;
            public BackendClient.OrganizationClient[] clients;
        }

        private void BuildOrganizationTherapistClientsPanel()
        {
            _independentOrganizationSwitchButton = ClientButton(_clientsPage.transform,
                F("Organization clients", "机构来访者"), .50f,.91f,.18f,.065f, () =>
                {
                    if (_independentOrganizationWorkspace == null) return;
                    _independentOrganizationClientMode = true;
                    RefreshClientsPage();
                });
            _independentOrganizationSwitchButton.gameObject.SetActive(false);
            var panel = ClientRect(_clientsPage.transform, "OrganizationTherapistClients", 0,0,1,1);
            panel.gameObject.AddComponent<Image>().color = HomeBg;
            _organizationTherapistClientsPanel = panel.gameObject;
            ClientText(panel,Localization.Get("clients.title"),30,.01f,.91f,.65f,.07f,HomeText);
            _organizationClientsPersonalButton=ClientButton(panel,F("Personal clients","个人来访者"),.50f,.91f,.20f,.065f,()=>
            {
                _independentOrganizationClientMode=false;
                _activeOrganizationClientWorkspace=null;
                RefreshClientsPage();
            });
            var search=ClientInput(panel,"",Localization.Get("clients.search"),.01f,.83f,.225f,.055f,100);
            search.onValueChanged.AddListener(value=>
            {
                _organizationClientSearch=value;
                if(_activeOrganizationClientWorkspace!=null)
                    RenderOrganizationTherapistClients(_activeOrganizationClientWorkspace,_organizationTherapistClients,_organizationTherapistClientsOffline);
            });
            _organizationTherapistArchivedButton=CreateMenuButton(panel,"ArchivedClientsToggle","",
                new Vector2(.245f,.83f),new Vector2(.285f,.885f),HomeCard);
            SearchIcon(_organizationTherapistArchivedButton.transform,Sandplay.UI.RecordSearchGlyph.Kind.Archive,HomeMuted);
            _organizationTherapistArchivedButton.onClick.AddListener(()=>
            {
                _organizationTherapistShowArchived=!_organizationTherapistShowArchived;
                _selectedOrganizationTherapistClientId=null;
                RefreshOrganizationTherapistClientsPage();
            });
            _organizationTherapistNewClientButton=ClientButton(panel,"clients.new",.79f,.91f,.14f,.065f,ShowManagedOrganizationClientCreateDialog,true);
            _organizationClientsRefreshButton=CreateMenuButton(panel,"RefreshOrganizationClients","",
                new Vector2(.95f,.91f),new Vector2(.99f,.975f),HomeCard);
            SearchIcon(_organizationClientsRefreshButton.transform,Sandplay.UI.RecordSearchGlyph.Kind.Refresh,HomeText);
            _organizationClientsRefreshButton.onClick.AddListener(ForceRefreshOrganizationClients);
            _organizationTherapistClientList=ClientScroll(panel,"OrganizationClientList",.01f,.025f,.275f,.79f);
            _organizationTherapistClientDetail=ClientRect(panel,"ClientDetailPane",.305f,.025f,.685f,.86f);
            var detailImage=_organizationTherapistClientDetail.gameObject.AddComponent<Image>();detailImage.color=HomeCard;ApplyHomeRoundedCorners(detailImage,12f);
            var detailOutline=_organizationTherapistClientDetail.gameObject.AddComponent<Outline>();detailOutline.effectColor=HomeCardBorder;detailOutline.effectDistance=new Vector2(1,-1);
            _organizationTherapistClientStatus=ClientText(panel,"",12,.01f,0,.98f,.022f,HomeMuted);
            _organizationTherapistClientsPanel.SetActive(false);
        }

        private void RefreshIndependentOrganizationWorkspaceAvailability()
        {
            if (_independentOrganizationSwitchButton == null ||
                BackendClient.Instance.UserType != "psychologist") return;
            int request = ++_independentOrganizationWorkspaceRequest;
            int account = BackendClient.Instance.UserId;
            _independentOrganizationSwitchButton.gameObject.SetActive(
                _independentOrganizationWorkspace != null && !_independentOrganizationClientMode);
            BackendClient.Instance.FetchOrganizationWorkspaces(workspaces =>
            {
                if (this == null || request != _independentOrganizationWorkspaceRequest ||
                    BackendClient.Instance.UserId != account || BackendClient.Instance.UserType != "psychologist") return;
                _independentOrganizationWorkspace = null;
                foreach (var item in workspaces ?? Array.Empty<BackendClient.OrganizationWorkspace>())
                    if (item != null && item.role == "therapist" && item.membership_status == "active")
                    { _independentOrganizationWorkspace = item; break; }
                _independentOrganizationSwitchButton.gameObject.SetActive(
                    _independentOrganizationWorkspace != null && !_independentOrganizationClientMode);
            }, _ =>
            {
                if (request == _independentOrganizationWorkspaceRequest)
                    _independentOrganizationSwitchButton.gameObject.SetActive(
                        Application.internetReachability == NetworkReachability.NotReachable &&
                        _independentOrganizationWorkspace != null && !_independentOrganizationClientMode);
            });
        }

        private void RefreshOrganizationTherapistClientsPage()
        {
            if (_organizationTherapistClientsPanel == null || _organizationTherapistClientList == null) return;
            _organizationTherapistClientsPanel.SetActive(true);
            _organizationTherapistClientsPanel.transform.SetAsLastSibling();
            bool independent = BackendClient.Instance.UserType == "psychologist";
            _organizationClientsPersonalButton.gameObject.SetActive(independent);
            int account = BackendClient.Instance.UserId;
            if (_organizationClientsSessionAccount != account || _organizationClientsSessionEpoch != LocalAccountStorage.Epoch)
            {
                Array.Clear(_organizationClientSessions, 0, _organizationClientSessions.Length);
                _organizationClientsSessionAccount = account;
                _organizationClientsSessionEpoch = LocalAccountStorage.Epoch;
                _organizationClientsLoading = false;
                ++_organizationTherapistClientRequest;
                _selectedOrganizationTherapistClientId = null;
            }
            bool archived = _organizationTherapistShowArchived;
            _organizationTherapistArchivedButton.GetComponent<Image>().color=archived?HomePrimary:HomeCard;
            var session = _organizationClientSessions[archived ? 1 : 0];
            if (session != null)
            {
                _activeOrganizationClientWorkspace = session.Workspace;
                _organizationClientsRefreshButton.interactable = true;
                RenderOrganizationTherapistClients(session.Workspace, session.Clients, session.Offline);
                return;
            }
            _organizationClientsRefreshButton.interactable = false;
            bool alreadyLoading = _organizationClientsLoading && _organizationClientsLoadingArchived == archived;
            ClearClientChildren(_organizationTherapistClientList);
            ClearClientChildren(_organizationTherapistClientDetail);
            _organizationTherapistClients=Array.Empty<BackendClient.OrganizationClient>();
            _activeOrganizationClientWorkspace=null;
            _organizationTherapistClientStatus.text = F("Loading…", "正在加载…");
            _organizationTherapistNewClientButton.gameObject.SetActive(false);
            _organizationTherapistArchivedButton.GetComponent<Image>().color=_organizationTherapistShowArchived?HomePrimary:HomeCard;
            if (alreadyLoading) return;
            _organizationClientsLoading = true;
            _organizationClientsLoadingArchived = archived;
            int request = ++_organizationTherapistClientRequest;
            BackendClient.Instance.FetchOrganizationWorkspaces(workspaces =>
            {
                if (!CurrentOrganizationTherapistClientRequest(request, account)) return;
                BackendClient.OrganizationWorkspace workspace = null;
                foreach (var item in workspaces ?? Array.Empty<BackendClient.OrganizationWorkspace>())
                    if (item != null && item.role == "therapist" && item.membership_status == "active")
                    { workspace = item; break; }
                if (workspace == null)
                {
                    FailOrganizationClientsLoad(F("Organization workspace unavailable.", "机构工作区不可用。"));
                    return;
                }
                if (independent) _independentOrganizationWorkspace = workspace;
                BackendClient.Instance.FetchOrganizationClients(workspace.id, "",
                    archived, clients =>
                    {
                        if (!CurrentOrganizationTherapistClientRequest(request, account)) return;
                        SaveOrganizationTherapistClientCache(workspace.id,
                            archived, clients);
                        CompleteOrganizationClientsLoad(workspace, clients, archived, false);
                    }, error =>
                    {
                        if (!CurrentOrganizationTherapistClientRequest(request, account)) return;
                        if (Application.internetReachability == NetworkReachability.NotReachable &&
                            TryLoadOrganizationTherapistClientCache(workspace.id,
                                archived, out var cached))
                            CompleteOrganizationClientsLoad(workspace, cached, archived, true);
                        else
                        {
                            DeleteOrganizationTherapistClientCache(workspace.id);
                            FailOrganizationClientsLoad(error);
                        }
                    });
            }, error =>
            {
                if (!CurrentOrganizationTherapistClientRequest(request, account)) return;
                string organizationId = independent
                    ? _independentOrganizationWorkspace?.id : BackendClient.Instance.ManagedOrganizationId;
                if (Application.internetReachability == NetworkReachability.NotReachable &&
                    !string.IsNullOrEmpty(organizationId) &&
                    TryLoadOrganizationTherapistClientCache(organizationId,
                        archived, out var cached))
                    CompleteOrganizationClientsLoad(
                        new BackendClient.OrganizationWorkspace { id = organizationId }, cached, archived, true);
                else
                {
                    if (!string.IsNullOrEmpty(organizationId))
                        DeleteOrganizationTherapistClientCache(organizationId);
                    FailOrganizationClientsLoad(error);
                }
            });
        }

        private void RenderOrganizationTherapistClients(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationClient[] clients, bool offline)
        {
            ClearClientChildren(_organizationTherapistClientList);
            _organizationTherapistClients=clients??Array.Empty<BackendClient.OrganizationClient>();
            _organizationTherapistClientsOffline=offline;
            _organizationTherapistClientStatus.text = offline
                ? F("Offline · showing saved organization clients (read-only).",
                    "离线 · 正在显示已保存的机构来访者（只读）。") : "";
            _organizationTherapistNewClientButton.gameObject.SetActive(!offline &&
                !_organizationTherapistShowArchived && _activeOrganizationClientWorkspace != null &&
                !BackendClient.Instance.IsOrganizationTherapist && _activeOrganizationClientWorkspace.can_create_clients);
            if (_organizationTherapistClients.Length == 0)
            {
                var empty = ClientRow(_organizationTherapistClientList, "Empty", 84);
                ClientText(empty, _organizationTherapistShowArchived
                    ? F("No archived clients.", "暂无已归档来访者。")
                    : F("No organization clients are assigned to you.", "暂无分配给您的机构来访者。"),
                    15,.04f,.10f,.92f,.80f,HomeMuted);
                _selectedOrganizationTherapistClientId=null;
                RenderOrganizationTherapistClientDetail(workspace,null,offline);
                return;
            }
            if (!_organizationTherapistClients.Any(item=>item!=null&&item.id==_selectedOrganizationTherapistClientId))
                _selectedOrganizationTherapistClientId=_organizationTherapistClients.FirstOrDefault(item=>item!=null)?.id;
            foreach (var client in _organizationTherapistClients.Where(item=>item!=null &&
                (item.name+" "+item.reference+" "+item.email).IndexOf(_organizationClientSearch.Trim(),StringComparison.OrdinalIgnoreCase)>=0))
                AddOrganizationTherapistClientRow(workspace, client, !offline);
            if(_organizationTherapistClientList.childCount==0)
                ClientText(ClientRow(_organizationTherapistClientList,"Empty",72),F("No matching clients.","没有匹配的来访者。"),14,.04f,.1f,.92f,.8f,HomeMuted);
            RenderOrganizationTherapistClientDetail(workspace,
                _organizationTherapistClients.FirstOrDefault(item=>item!=null&&item.id==_selectedOrganizationTherapistClientId),offline);
        }

        private bool CurrentOrganizationTherapistClientRequest(int request, int account)
        {
            return this != null && request == _organizationTherapistClientRequest &&
                _organizationClientsSessionEpoch == LocalAccountStorage.Epoch &&
                BackendClient.Instance.IsLoggedIn &&
                (BackendClient.Instance.IsOrganizationTherapist || BackendClient.Instance.UserType == "psychologist") &&
                BackendClient.Instance.UserId == account;
        }

        private void AddOrganizationTherapistClientRow(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationClient client, bool editable)
        {
            if(client==null)return;
            var row = ClientRow(_organizationTherapistClientList, "OrganizationClient", 72);
            row.GetComponent<Image>().color=client.id==_selectedOrganizationTherapistClientId?HomePrimary:HomeCard;
            ApplyHomeRoundedCorners(row.GetComponent<Image>(),12f);
            var avatar=ClientRect(row,"Avatar",.035f,.16f,.18f,.68f);
            var circle=avatar.gameObject.AddComponent<Image>();circle.sprite=Sandplay.UI.SessionAvatars.Circle();circle.color=HomeTeal;
            string initial=string.IsNullOrWhiteSpace(client.name)?"?":StringInfo.GetNextTextElement(client.name.Trim()).ToUpperInvariant();
            var initialLabel=ClientText(avatar,initial,18,0,0,1,1,Color.white);initialLabel.alignment=TextAlignmentOptions.Center;
            avatar.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(client.linked_user_id,client.avatar_url,initialLabel);
            ClientText(row, client.name ?? "", 17,.24f,.49f,.72f,.34f,HomeText).richText=false;
            string secondary=string.IsNullOrWhiteSpace(client.reference)
                ?(client.archived?F("Archived","已归档"):""):client.reference;
            ClientText(row, secondary, 12,.24f,.14f,.72f,.28f,HomeMuted).richText=false;
            var select=row.gameObject.AddComponent<Button>();select.targetGraphic=row.GetComponent<Image>();
            select.onClick.AddListener(()=>
            {
                _selectedOrganizationTherapistClientId=client.id;
                _organizationClientReportsTab=false;_organizationClientReplaysTab=false;
                RenderOrganizationTherapistClients(workspace,_organizationTherapistClients,_organizationTherapistClientsOffline);
            });
        }

        private void RenderOrganizationTherapistClientDetail(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationClient client,bool offline)
        {
            if(_organizationTherapistClientDetail==null)return;
            ClearClientChildren(_organizationTherapistClientDetail);
            if(client==null)
            {
                ClientText(_organizationTherapistClientDetail,F("Select a client.","请选择来访者。"),16,.08f,.42f,.84f,.12f,HomeMuted).alignment=TextAlignmentOptions.Center;
                return;
            }
            var avatar=ClientRect(_organizationTherapistClientDetail,"Avatar",.035f,.865f,.09f,.11f);
            var circle=avatar.gameObject.AddComponent<Image>();circle.sprite=Sandplay.UI.SessionAvatars.Circle();circle.color=HomeTeal;
            string initial=string.IsNullOrWhiteSpace(client.name)?"?":StringInfo.GetNextTextElement(client.name.Trim()).ToUpperInvariant();
            var initialLabel=ClientText(avatar,initial,28,0,0,1,1,Color.white);initialLabel.alignment=TextAlignmentOptions.Center;
            avatar.gameObject.AddComponent<Sandplay.UI.AccountAvatar>().SetPerson(client.linked_user_id,client.avatar_url,initialLabel);
            ClientText(_organizationTherapistClientDetail,client.name??"",24,.145f,.865f,.62f,.11f,HomeText).richText=false;
            string details=client.reference??"";
            if(!string.IsNullOrWhiteSpace(client.email))details+=(details.Length>0?" · ":"")+client.email;
            if(client.archived)details+=(details.Length>0?" · ":"")+F("Archived","已归档");
            ClientText(_organizationTherapistClientDetail,details,12,.035f,.815f,.93f,.04f,HomeMuted).richText=false;
            bool canHost=!offline&&!client.archived&&(BackendClient.Instance.IsOrganizationTherapist
                ?BackendClient.Instance.ManagedCanHostSessions:workspace.can_host_sessions);
            bool canSchedule=!offline&&!client.archived&&(BackendClient.Instance.IsOrganizationTherapist
                ?BackendClient.Instance.ManagedCanCreateSchedules:workspace.can_create_schedules);
            bool canReport=!offline&&!client.archived&&(BackendClient.Instance.IsOrganizationTherapist
                ?BackendClient.Instance.ManagedCanCreateReports:workspace.can_create_reports);
            if(!offline)ClientButton(_organizationTherapistClientDetail,client.archived?"clients.restore":"clients.archive",
                .825f,.885f,.14f,.065f,()=>BackendClient.Instance.SetOrganizationClientArchived(workspace.id,client,!client.archived,
                    ForceRefreshOrganizationClients,error=>{if(_organizationTherapistClientStatus!=null)_organizationTherapistClientStatus.text=error;}));
            var tablesTab=ClientButton(_organizationTherapistClientDetail,"clients.tables",.035f,.74f,.31f,.06f,()=>
            { _organizationClientReportsTab=false;_organizationClientReplaysTab=false;RenderOrganizationTherapistClientDetail(workspace,client,offline); });
            var reportsTab=ClientButton(_organizationTherapistClientDetail,"clients.reports",.345f,.74f,.31f,.06f,()=>
            { _organizationClientReportsTab=true;_organizationClientReplaysTab=false;RenderOrganizationTherapistClientDetail(workspace,client,offline); });
            var replaysTab=ClientButton(_organizationTherapistClientDetail,F("Replays","回放"),.655f,.74f,.31f,.06f,()=>
            { _organizationClientReportsTab=false;_organizationClientReplaysTab=true;RenderOrganizationTherapistClientDetail(workspace,client,offline); });
            StyleContentTab(tablesTab,!_organizationClientReportsTab&&!_organizationClientReplaysTab);
            StyleContentTab(reportsTab,_organizationClientReportsTab);StyleContentTab(replaysTab,_organizationClientReplaysTab);
            var host=ClientButton(_organizationTherapistClientDetail,"host.title",.035f,.665f,.45f,.06f,()=>
                ShowNameDialog(Localization.Get("dialog.new_board"),"",name=>
                {
                    if(!string.IsNullOrWhiteSpace(name))ShowSizeDialog(name,null,true,workspace.id,client.id,
                        InviteTargetForOrganizationClient(workspace,client));
                }),true);host.interactable=canHost;
            var schedule=ClientButton(_organizationTherapistClientDetail,F("Schedule session","预约会话"),.515f,.665f,.45f,.06f,
                ()=>ShowOrganizationScheduleProposal(workspace,client));schedule.interactable=canSchedule;
            var boards=(SessionManager.Instance?.GetSavedSessions(includeArchived:true)??new System.Collections.Generic.List<SessionListEntry>())
                .Where(item=>item!=null && string.Equals(item.OrganizationId,workspace.id,StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.OrganizationClientId,client.id,StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item=>item.ModifiedAt).ToList();
            float searchY=.59f;
            if(_organizationClientReportsTab)
            {
                var create=ClientButton(_organizationTherapistClientDetail,F("+ New report","+ 新建报告"),.035f,searchY,.93f,.055f,()=>
                {
                    var box=ClientDialog(F("Choose a board","选择沙盘"),700,600);
                    var choices=ClientScroll(box,"Boards",.04f,.08f,.92f,.72f);
                    foreach(var board in boards.Where(item=>!item.Archived))
                    {
                        var choice=ClientButton(ClientRow(choices,"Board",92),board.SessionName,.02f,.08f,.96f,.84f,()=>OpenReportWorkspace(board.SessionName));
                        AddBoardChooserThumbnail(choice,board.SessionName);
                    }
                    if(choices.childCount==0)ClientText(ClientRow(choices,"Empty",80),F("Create a board for this client first.","请先为此来访者创建沙盘。"),15,.03f,0,.94f,1,HomeMuted);
                },true);create.interactable=canReport;searchY-=.07f;
            }
            var content=ClientScroll(_organizationTherapistClientDetail,"TablesAndReports",.035f,.025f,.93f,searchY+.03f);
            void Render(string query)
            {
                ClearClientChildren(content);
                if (_organizationClientReplaysTab) { RenderClientReplays(content, boards, query); return; }
                foreach(var board in boards)
                {
                    if(_organizationClientReportsTab)
                    {
                        var data=SessionManager.Instance?.LoadSessionData(board.SessionName);
                        foreach(var report in data?.Reports??new System.Collections.Generic.List<AnalysisReport>())
                        {
                            if(report==null||report.Archived||report.AuthorUserId!=BackendClient.Instance.UserId ||
                                (board.SessionName+" "+report.ResultText).IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                            var row=ClientRow(content,"Report",72);
                            ClientText(row,board.SessionName,15,.03f,.47f,.67f,.46f,HomeText).richText=false;
                            ClientText(row,DateTimeOffset.TryParse(report.CreatedAt,out var date)?date.ToLocalTime().ToString("g"):report.CreatedAt,12,.03f,.08f,.67f,.35f,HomeMuted);
                            ClientButton(row,"clients.view",.73f,.20f,.24f,.60f,()=>
                            {if(report.Source=="ai" || !canReport)ShowReportDetail(report,board.SessionName,!canReport);else OpenReportWorkspaceRecord(board.SessionName,false,report.ReportId);});
                        }
                    }
                    else
                    {
                        if(board.SessionName.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                        var row=ClientRow(content,"Board",80);
                        var image=ClientRect(row,"Preview",.02f,.12f,.15f,.76f).gameObject.AddComponent<Image>();
                        image.sprite=ScreenshotManager.LoadThumbnail(board.SessionName);image.preserveAspect=true;image.raycastTarget=false;
                        ClientText(row,board.SessionName,15,.20f,.47f,.50f,.46f,HomeText).richText=false;
                        ClientText(row,DateTimeOffset.TryParse(board.ModifiedAt,out var date)?date.ToLocalTime().ToString("g"):board.ModifiedAt,12,.20f,.08f,.50f,.35f,HomeMuted);
                        var open=ClientButton(row,"host.title",.73f,.20f,.24f,.60f,()=>
                        {
                            BackendClient.Instance.ActiveHostingOrganizationId=workspace.id;_pendingHostMode=HostMode.Cloud;
                            EnterSandbox(board.SessionName,false,inviteTarget:InviteTargetForOrganizationClient(workspace,client));
                        });open.interactable=canHost;
                    }
                }
                if(content.childCount==0)ClientText(ClientRow(content,"Empty",90),Localization.Get(_organizationClientReportsTab?"clients.no_reports":"clients.no_tables"),14,.04f,.1f,.92f,.8f,HomeMuted);
            }
            Render("");
        }

        private void ShowOrganizationClientBoardHistory(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationClient client)
        {
            var box=ClientDialog(F("Board history","沙盘历史"),760,660);
            var dialog=_clientDialog;
            ClientText(box,client.name??"",18,.05f,.76f,.90f,.07f,HomeText);
            var list=ClientScroll(box,"OrganizationClientBoards",.05f,.10f,.90f,.62f);
            var boards=(SessionManager.Instance?.GetSavedSessions(includeArchived:true) ??
                new System.Collections.Generic.List<SessionListEntry>())
                .Where(item=>item!=null &&
                    string.Equals(item.OrganizationId,workspace.id,StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.OrganizationClientId,client.id,StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item=>DateTimeOffset.TryParse(item.ModifiedAt,CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,out var date)?date:DateTimeOffset.MinValue).ToList();
            foreach(var board in boards)
            {
                var row=ClientRow(list,"OrganizationClientBoard",76);
                ApplyHomeRoundedCorners(row.GetComponent<Image>(),12f);
                var preview=ClientRect(row,"Preview",.015f,.12f,.13f,.76f).gameObject.AddComponent<Image>();
                preview.sprite=ScreenshotManager.LoadThumbnail(board.SessionName);
                preview.preserveAspect=true;
                preview.color=preview.sprite!=null?Color.white:new Color(.20f,.25f,.32f);
                preview.raycastTarget=false;
                ClientText(row,board.SessionName,15,.17f,.48f,.54f,.42f,HomeText);
                string modified=DateTimeOffset.TryParse(board.ModifiedAt,out var date)
                    ?date.ToLocalTime().ToString("g",Localization.Culture):"";
                ClientText(row,modified,12,.17f,.10f,.54f,.32f,HomeMuted);
                bool canHost=BackendClient.Instance.IsOrganizationTherapist
                    ?BackendClient.Instance.ManagedCanHostSessions:workspace.can_host_sessions;
                var host=ClientButton(row,F("Host session","主持会话"),.74f,.20f,.23f,.60f,()=>
                {
                    if(dialog==null||dialog!=_clientDialog)return;
                    BackendClient.Instance.ActiveHostingOrganizationId=workspace.id;
                    _pendingHostMode=HostMode.Cloud;
                    CloseClientDialog();EnterSandbox(board.SessionName,false,
                        inviteTarget:InviteTargetForOrganizationClient(workspace,client));
                },true);
                host.interactable=canHost;
            }
            if(boards.Count==0)
                ClientText(ClientRow(list,"Empty",90),F("No boards saved for this client yet.",
                    "此来访者尚无已保存沙盘。"),14,.05f,.05f,.90f,.90f,HomeMuted);
        }

        private static string OrganizationTherapistClientCachePath(string organizationId, bool archived)
        {
            if (!Guid.TryParse(organizationId, out var id))
                throw new InvalidDataException("Invalid organization cache scope.");
            string account = LocalAccountStorage.AccountPath(Application.persistentDataPath,
                BackendClient.BaseUrl, BackendClient.Instance.UserId);
            return Path.Combine(account, "OrganizationCache", id.ToString("N"),
                archived ? "clients-archived.json" : "clients-active.json");
        }

        private static void SaveOrganizationTherapistClientCache(string organizationId, bool archived,
            BackendClient.OrganizationClient[] clients)
        {
            try
            {
                var guard = LocalAccountStorage.CaptureGuard();
                var source = clients ?? Array.Empty<BackendClient.OrganizationClient>();
                var safe = new BackendClient.OrganizationClient[source.Length];
                for (int index = 0; index < source.Length; index++)
                {
                    var item = source[index];
                    safe[index] = item == null ? null : new BackendClient.OrganizationClient
                    {
                        id = item.id,
                        linked_user_id = item.linked_user_id,
                        avatar_url = item.avatar_url,
                        name = item.name,
                        reference = item.reference,
                        assigned_membership_id = item.assigned_membership_id,
                        assigned_therapist_name = item.assigned_therapist_name,
                        archived = item.archived,
                        revision = item.revision,
                        updated_at = item.updated_at,
                    };
                }
                var cache = new OrganizationClientCache
                {
                    user_id = BackendClient.Instance.UserId,
                    organization_id = organizationId,
                    saved_at = DateTimeOffset.UtcNow.ToString("o"),
                    clients = safe,
                };
                guard();
                LocalRecordFile.Write(OrganizationTherapistClientCachePath(organizationId, archived),
                    JsonUtility.ToJson(cache));
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Organization clients] Cache save skipped: " + error.GetType().Name);
            }
        }

        private static bool TryLoadOrganizationTherapistClientCache(string organizationId, bool archived,
            out BackendClient.OrganizationClient[] clients)
        {
            clients = null;
            try
            {
                var guard = LocalAccountStorage.CaptureGuard();
                string path = OrganizationTherapistClientCachePath(organizationId, archived);
                if (!File.Exists(path) && File.Exists(path + ".bak")) path += ".bak";
                if (!File.Exists(path)) return false;
                var cache = JsonUtility.FromJson<OrganizationClientCache>(File.ReadAllText(path));
                guard();
                if (cache == null || cache.schema != 1 ||
                    cache.user_id != BackendClient.Instance.UserId ||
                    !string.Equals(cache.organization_id, organizationId, StringComparison.OrdinalIgnoreCase))
                    return false;
                clients = cache.clients ?? Array.Empty<BackendClient.OrganizationClient>();
                return true;
            }
            catch (Exception) { return false; }
        }

        private static void DeleteOrganizationTherapistClientCache(string organizationId)
        {
            try
            {
                foreach (bool archived in new[] { false, true })
                {
                    string path = OrganizationTherapistClientCachePath(organizationId, archived);
                    foreach (string candidate in new[] { path, path + ".bak", path + ".tmp" })
                        if (File.Exists(candidate)) File.Delete(candidate);
                }
            }
            catch (Exception) { }
        }

        private void ShowManagedOrganizationClientCreateDialog()
        {
            var workspace = _activeOrganizationClientWorkspace;
            bool allowed = workspace != null && (BackendClient.Instance.IsOrganizationTherapist
                ? BackendClient.Instance.ManagedCanCreateClients : workspace.can_create_clients);
            if (!allowed) return;
            var box = ClientDialog(F("Add organization client", "添加机构来访者"), 640, 570);
            var dialog = _clientDialog;
            var selectedLabel = ClientText(box, F("No client account selected", "尚未选择来访者账号"),
                16,.06f,.73f,.88f,.08f,HomeText);
            FriendPerson selected = null;
            Button create = null;
            var status = ClientText(box, F("Choose a Personal account created by the client.",
                "选择由来访者本人创建的个人账号。"), 13,.06f,.15f,.88f,.12f,HomeMuted);
            void Select(bool friends)
            {
                ShowClientAccountPicker(dialog.transform, friends, person =>
                {
                    if (dialog != _clientDialog) return;
                    if (person.user_type != "normal")
                    {
                        selected = null;
                        selectedLabel.text = F("No client account selected", "尚未选择来访者账号");
                        status.text = F("User type is not Personal.", "该用户类型不是个人用户。");
                        if (create != null) create.interactable = false;
                        return;
                    }
                    selected = person;
                    string id = string.IsNullOrEmpty(person.friend_code) ? person.code : person.friend_code;
                    selectedLabel.text = person.name + " · " + id;
                    if (create != null) create.interactable = true;
                });
            }
            ClientButton(box, F("Search account ID", "搜索账号 ID"), .06f,.61f,.42f,.085f, () => Select(false));
            if(BackendClient.Instance.ExternalContactsAllowed)
                ClientButton(box, F("From friends", "从好友选择"), .52f,.61f,.42f,.085f, () => Select(true));
            ClientText(box, F("Reference (optional)", "编号（可选）"), 13,.06f,.51f,.88f,.05f,HomeMuted);
            var reference = ClientInput(box, "", F("Internal reference", "内部编号"), .06f,.41f,.88f,.075f,80);
            bool busy = false;
            ClientButton(box, F("Cancel", "取消"), .06f,.05f,.40f,.09f, CloseClientDialog);
            create = ClientButton(box, F("Add client", "添加来访者"), .54f,.05f,.40f,.09f, () =>
            {
                if (busy || dialog != _clientDialog) return;
                if (selected == null)
                { status.text = F("Choose a client account first.", "请先选择来访者账号。"); return; }
                busy = true; create.interactable = false;
                BackendClient.Instance.CreateOrganizationClient(workspace.id, selected.code, reference.text,
                    workspace.membership_id, _ =>
                    {
                        if (dialog != _clientDialog) return;
                        CloseClientDialog(); ForceRefreshOrganizationClients();
                    }, error =>
                    {
                        if (dialog != _clientDialog) return;
                        busy = false; create.interactable = true; status.text = error;
                    });
            }, true);
            create.interactable = false;
        }
    }
}
