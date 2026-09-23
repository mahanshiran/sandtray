using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Data;
using Sandplay.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private Button _organizationWorkspaceButton;
        private GameObject _organizationPage;
        private Transform _organizationPageContent;
        private TMP_Text _organizationPageStatus;
        private int _organizationPageRequest;
        private string _organizationTab = "therapists";
        private Button _organizationTherapistsTab;
        private Button _organizationUsageTab;
        private Button _organizationActivityTab;
        private Button _organizationSettingsTab;
        private string _organizationActivityWorkspaceId;
        private string _organizationActivityMembershipId = "";
        private string _organizationActivityClientId = "";
        private string _organizationActivityCategory = "sessions";
        private string _organizationActivityAction = "";
        private string _organizationActivityFrom = "";
        private string _organizationActivityTo = "";
        private BackendClient.OrganizationMember[] _organizationActivityMembers = Array.Empty<BackendClient.OrganizationMember>();
        private BackendClient.OrganizationClient[] _organizationActivityClients = Array.Empty<BackendClient.OrganizationClient>();
        private BackendClient.OrganizationActivity[] _organizationActivityEvents = Array.Empty<BackendClient.OrganizationActivity>();
        private string _organizationActivityNextCursor;
        private DateTime _organizationUsageMonth = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        private void BuildOrganizationPage()
        {
            _organizationPage = CreateHomePrimaryPage("OrganizationPage");
            _organizationTherapistsTab = ClientButton(_organizationPage.transform,
                F("Therapists", "治疗师"), .01f,.915f,.245f,.065f,
                () => SelectOrganizationTab("therapists"));
            _organizationUsageTab = ClientButton(_organizationPage.transform,
                F("Usage", "用量"), .255f,.915f,.245f,.065f,
                () => SelectOrganizationTab("usage"));
            _organizationActivityTab = ClientButton(_organizationPage.transform,
                F("Activity", "活动"), .50f,.915f,.245f,.065f,
                () => SelectOrganizationTab("activity"));
            _organizationSettingsTab = ClientButton(_organizationPage.transform,
                F("Settings", "设置"), .745f,.915f,.245f,.065f,
                () => SelectOrganizationTab("settings"));
            _organizationPageContent = ClientScroll(_organizationPage.transform,
                "OrganizationContent", .01f,.055f,.98f,.84f);
            _organizationPageStatus = ClientText(_organizationPage.transform, "", 13,
                .01f,.01f,.98f,.035f,HomeMuted);
            ApplyOrganizationTabVisuals();
            _organizationPage.SetActive(false);
            _homePages["organization"] = _organizationPage;
        }

        private void SelectOrganizationTab(string tab)
        {
            if (_organizationTab == tab) return;
            _organizationTab = tab;
            ApplyOrganizationTabVisuals();
            RefreshOrganizationPage();
        }

        private void ApplyOrganizationTabVisuals()
        {
            ApplyOrganizationTabVisual(_organizationTherapistsTab, _organizationTab == "therapists");
            ApplyOrganizationTabVisual(_organizationUsageTab, _organizationTab == "usage");
            ApplyOrganizationTabVisual(_organizationActivityTab, _organizationTab == "activity");
            ApplyOrganizationTabVisual(_organizationSettingsTab, _organizationTab == "settings");
        }

        private void ApplyOrganizationTabVisual(Button button, bool selected)
        {
            if (button == null) return;
            StyleContentTab(button, selected);
        }

        private void RefreshOrganizationPage()
        {
            if (_organizationPage == null || _organizationPageContent == null) return;
            ClearClientChildren(_organizationPageContent);
            ApplyOrganizationTabVisuals();
            int request = ++_organizationPageRequest;
            var backend = BackendClient.Instance;
            if (!backend.IsLoggedIn || backend.UserType != "organization")
            {
                _organizationPageStatus.text = "";
                AddOrganizationEmptyRow(F("Switch to an Organization account to manage a workspace.",
                    "请切换为机构账户以管理工作区。"));
                return;
            }
            int account = backend.UserId;
            bool Current() => this != null && request == _organizationPageRequest &&
                _organizationPage != null && _organizationPage.activeInHierarchy &&
                backend.IsLoggedIn && backend.UserId == account && backend.UserType == "organization";
            _organizationPageStatus.text = "";
            AddOrganizationEmptyRow(F("Loading…", "正在加载…"));
            backend.FetchOrganizationWorkspaces(workspaces =>
            {
                if (!Current()) return;
                var workspace = workspaces.FirstOrDefault(item => item != null && item.role == "owner");
                ClearClientChildren(_organizationPageContent);
                if (workspace == null)
                {
                    _organizationPageStatus.text = "";
                    RenderOrganizationSetup();
                    return;
                }
                if (_organizationTab == "settings")
                {
                    _organizationPageStatus.text = "";
                    RenderOrganizationSettings(workspace);
                    return;
                }
                if (_organizationTab == "usage")
                {
                    backend.FetchOrganizationUsage(workspace.id,
                        _organizationUsageMonth.ToString("yyyy-MM-01", CultureInfo.InvariantCulture), usage =>
                    {
                        if (!Current()) return;
                        _organizationPageStatus.text = "";
                        RenderOrganizationUsage(workspace, usage);
                    }, error => { if (Current()) _organizationPageStatus.text = error; });
                    return;
                }
                if (_organizationTab == "activity")
                {
                    if (_organizationActivityWorkspaceId != workspace.id)
                    {
                        _organizationActivityWorkspaceId = workspace.id;
                        ClearOrganizationActivityFilters();
                    }
                    backend.FetchOrganizationMembers(workspace.id, members =>
                    {
                        if (!Current()) return;
                        _organizationActivityMembers = members ?? Array.Empty<BackendClient.OrganizationMember>();
                        backend.FetchAllOrganizationClients(workspace.id, clients =>
                        {
                            if (!Current()) return;
                            _organizationActivityClients = clients ?? Array.Empty<BackendClient.OrganizationClient>();
                            LoadOrganizationActivityPage(workspace, "", request, account, false);
                        }, error => { if (Current()) _organizationPageStatus.text = error; });
                    }, error => { if (Current()) _organizationPageStatus.text = error; });
                    return;
                }
                backend.FetchOrganizationMembers(workspace.id, members =>
                {
                    if (!Current()) return;
                    backend.FetchOrganizationInvitations(workspace.id, invitations =>
                    {
                        if (!Current()) return;
                        _organizationPageStatus.text = "";
                        RenderOrganizationTherapists(workspace, members, invitations);
                    }, error =>
                    {
                        if (!Current()) return;
                        _organizationPageStatus.text = error;
                        RenderOrganizationTherapists(workspace, members,
                            Array.Empty<BackendClient.OrganizationInvitation>());
                    });
                }, error => { if (Current()) _organizationPageStatus.text = error; });
            }, error => { if (Current()) _organizationPageStatus.text = error; });
        }

        private void RenderOrganizationSetup()
        {
            var row = ClientRow(_organizationPageContent, "OrganizationSetup", 220);
            ClientText(row, F("Create your organization workspace", "创建机构工作区"), 23,
                .03f,.68f,.75f,.22f,HomeText);
            var note = ClientText(row, F(
                "This creates one owner-managed workspace. Existing personal boards and client records stay private on this device.",
                "这会创建一个由所有者管理的工作区。现有个人沙盘和来访者记录仍私密保存在此设备上。"),
                14,.03f,.31f,.88f,.30f,HomeMuted);
            note.enableWordWrapping = true;
            ClientButton(row, F("Create workspace", "创建工作区"), .72f,.12f,.25f,.24f,
                ShowOrganizationCreateDialog, true);
        }

        private void RenderOrganizationTherapists(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationMember[] members, BackendClient.OrganizationInvitation[] invitations)
        {
            members ??= Array.Empty<BackendClient.OrganizationMember>();
            invitations ??= Array.Empty<BackendClient.OrganizationInvitation>();

            var actions = ClientRow(_organizationPageContent, "TherapistActions", 76);
            ClientButton(actions, F("Unassigned clients", "未分配来访者"), .03f,.20f,.22f,.60f,
                () => ShowOrganizationClientsDialog(workspace, null, members));
            ClientButton(actions, F("Archived clients", "已归档来访者"), .26f,.20f,.20f,.60f,
                () => ShowOrganizationClientsDialog(workspace, null, members, true));
            ClientButton(actions, F("Create managed", "创建托管账号"), .56f,.20f,.20f,.60f,
                () => ShowManagedTherapistCreateDialog(workspace), true);
            ClientButton(actions, F("Invite therapist", "邀请治疗师"), .77f,.20f,.21f,.60f,
                () => ShowOrganizationInviteDialog(workspace), true);
            if (members.Length == 0)
                AddOrganizationEmptyRow(F("No therapists have joined yet.", "尚无治疗师加入。"));
            foreach (var member in members)
                AddOrganizationMemberRow(workspace, member, members);

            AddOrganizationSectionHeading(F("Invitations", "邀请"));
            if (invitations.Length == 0)
                AddOrganizationEmptyRow(F("No invitations yet.", "暂无邀请。"));
            foreach (var invitation in invitations)
                AddOrganizationInvitationRow(workspace, invitation);
        }

        private void RenderOrganizationSettings(BackendClient.OrganizationWorkspace workspace)
        {
            var row = ClientRow(_organizationPageContent, "OrganizationSettings", 176);
            ClientText(row, workspace.name ?? "", 21,.03f,.65f,.62f,.22f,HomeText);
            ClientText(row, F("Login domain", "登录域") + ": @" + (workspace.login_domain ?? ""),
                13,.03f,.38f,.62f,.18f,HomeMuted);
            string accent = string.IsNullOrWhiteSpace(workspace.accent_color)
                ? F("Default accent", "默认强调色") : workspace.accent_color;
            ClientText(row, accent, 13,.03f,.16f,.62f,.16f,HomeMuted);
            ClientButton(row, F("Edit branding", "编辑品牌"), .73f,.35f,.24f,.30f,
                () => ShowOrganizationBrandingDialog(workspace), true);
        }

        private void RenderOrganizationUsage(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationUsage usage)
        {
            DateTime period = _organizationUsageMonth;
            if (DateTime.TryParse(usage.period_start, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out DateTime parsedPeriod))
                period = parsedPeriod;
            var periodRow = ClientRow(_organizationPageContent, "UsagePeriod", 72);
            ClientButton(periodRow, "‹", .03f,.20f,.12f,.60f, () =>
            {
                _organizationUsageMonth = period.AddMonths(-1);
                RefreshOrganizationPage();
            });
            var periodLabel = ClientText(periodRow,
                period.ToString("MMMM yyyy", Localization.Culture), 17,.20f,.20f,.60f,.60f,HomeText);
            periodLabel.alignment = TextAlignmentOptions.Center;
            var next = ClientButton(periodRow, "›", .85f,.20f,.12f,.60f, () =>
            {
                _organizationUsageMonth = period.AddMonths(1);
                RefreshOrganizationPage();
            });
            DateTime currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            next.interactable = period.Date < currentMonth.Date;

            var summary = ClientRow(_organizationPageContent, "UsageSummary", 132);
            string allowance = usage.limit_seconds.HasValue
                ? FormatOrganizationDuration(usage.limit_seconds.Value)
                : usage.is_current ? F("Not configured", "未配置") : F("Historical usage", "历史用量");
            ClientText(summary, (usage.is_current ? F("Monthly allowance", "每月额度") : F("Recorded usage", "记录用量")) + " · " + allowance,
                19,.03f,.58f,.62f,.24f,HomeText);
            if (usage.is_current) AddOrganizationUsageWarning(summary,
                OrganizationUsageWarningLevel(usage.limit_seconds, usage.used_seconds, usage.reserved_seconds),
                .67f, .60f, .30f, .20f);
            string totals = usage.is_current
                ? F("Used", "已使用") + ": " + FormatOrganizationDuration(usage.used_seconds) +
                  "  ·  " + F("Reserved", "已预留") + ": " + FormatOrganizationDuration(usage.reserved_seconds) +
                  "  ·  " + F("Remaining", "剩余") + ": " +
                  (usage.remaining_seconds.HasValue ? FormatOrganizationDuration(usage.remaining_seconds.Value) : "—") +
                  "\n" + F("Allocated", "已分配") + ": " + FormatOrganizationDuration(usage.allocated_seconds) +
                  "  ·  " + F("Unallocated", "未分配") + ": " +
                  (usage.unallocated_seconds.HasValue ? FormatOrganizationDuration(usage.unallocated_seconds.Value) : "—")
                : F("Used", "已使用") + ": " + FormatOrganizationDuration(usage.used_seconds);
            var totalsLabel = ClientText(summary, totals, 13,.03f,.12f,.94f,.38f,HomeMuted);
            totalsLabel.enableWordWrapping = true;
            RenderOrganizationDailyUsage(usage);
            if (usage.members == null || usage.members.Length == 0)
            {
                AddOrganizationEmptyRow(usage.is_current
                    ? F("No active therapists.", "暂无在职治疗师。")
                    : F("No recorded organization hosting in this month.", "本月没有机构主持记录。"));
                return;
            }
            foreach (var member in usage.members)
            {
                var row = ClientRow(_organizationPageContent, "UsageMember", 132);
                var open = row.gameObject.AddComponent<Button>();
                open.targetGraphic = row.GetComponent<Image>();
                open.transition = Selectable.Transition.ColorTint;
                open.onClick.AddListener(() => ShowOrganizationTherapistUsage(
                    workspace, usage, member, 0));
                string name = string.IsNullOrWhiteSpace(member.therapist_name)
                    ? member.therapist_email : member.therapist_name;
                ClientText(row, name, 17,.03f,.67f,.52f,.22f,HomeText);
                if (usage.is_current) AddOrganizationUsageWarning(row,
                    OrganizationUsageWarningLevel(member.allocation_seconds,
                        member.used_seconds, member.reserved_seconds),
                    .55f, .69f, .17f, .17f);
                string allocation = !usage.is_current ? F("Not recorded", "未记录")
                    : member.allocation_seconds.HasValue
                    ? FormatOrganizationDuration(member.allocation_seconds.Value) : F("Unallocated", "未分配");
                string details = usage.is_current
                    ? F("Allocation", "分配") + ": " + allocation + "  ·  " +
                      F("Used", "已使用") + ": " + FormatOrganizationDuration(member.used_seconds) + "  ·  " +
                      F("Remaining", "剩余") + ": " +
                      (member.remaining_seconds.HasValue ? FormatOrganizationDuration(member.remaining_seconds.Value) : "—")
                    : F("Used", "已使用") + ": " + FormatOrganizationDuration(member.used_seconds) +
                      "  ·  " + F("Allocation", "分配") + ": " + allocation;
                ClientText(row, details, 12,.03f,.39f,.70f,.20f,HomeMuted);
                AddOrganizationUsageMeter(row, member.allocation_seconds,
                    member.used_seconds, member.reserved_seconds, .03f,.17f,.69f,.10f);
                if (usage.is_current)
                    ClientButton(row, F("Edit allocation", "编辑分配"), .75f,.43f,.22f,.30f,
                        () => ShowOrganizationHostingAllocationDialog(workspace, usage, member));
                ClientText(row, F("View details ›", "查看详情 ›"), 11,.75f,.13f,.22f,.18f,
                    HomeMuted).alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        private void AddOrganizationUsageMeter(Transform parent, int? allowanceSeconds,
            int usedSeconds, int reservedSeconds, float x, float y, float width, float height)
        {
            var track = ClientRect(parent, "AllowanceTrack", x,y,width,height);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = HomeCardBorder;
            ApplyHomeRoundedCorners(trackImage, 5);
            if (!allowanceSeconds.HasValue || allowanceSeconds.Value <= 0) return;
            float fraction = Mathf.Clamp01((float)Math.Max(0, usedSeconds) / allowanceSeconds.Value);
            if (fraction <= 0) return;
            int warning = OrganizationUsageWarningLevel(
                allowanceSeconds, usedSeconds, reservedSeconds);
            Color fillColor = warning == 2
                ? new Color(.94f,.30f,.32f)
                : warning == 1 ? HomeGold : HomePrimary;
            var fill = ClientRect(track, "Used", 0,0,fraction,1);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = fillColor;
            ApplyHomeRoundedCorners(fillImage, 5);
        }

        private void ShowOrganizationTherapistUsage(
            BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationUsage summary,
            BackendClient.OrganizationUsageMember member, int offset)
        {
            var box = ClientDialog(string.IsNullOrWhiteSpace(member.therapist_name)
                ? member.therapist_email : member.therapist_name, 880, 780);
            var dialog = _clientDialog;
            var status = ClientText(box, F("Loading…", "正在加载…"), 13,
                .05f,.79f,.90f,.045f,HomeMuted);
            var list = ClientScroll(box, "TherapistUsage", .04f,.12f,.92f,.65f);
            string month = summary.period_start;
            ClientButton(box, F("Refresh", "刷新"), .63f,.025f,.32f,.065f,
                () => ShowOrganizationTherapistUsage(workspace, summary, member, offset));
            BackendClient.Instance.FetchOrganizationUsageMember(workspace.id, month,
                member.membership_id, offset, usage =>
                {
                    if (dialog == null || dialog != _clientDialog) return;
                    status.text = usage.is_current
                        ? F("Monthly organization allowance", "机构每月额度")
                        : F("Recorded organization usage", "机构历史用量");
                    RenderOrganizationTherapistUsageDetail(
                        box, list, workspace, summary, member, usage, offset);
                }, error => { if (dialog != null && dialog == _clientDialog) status.text = error; });
        }

        private void RenderOrganizationTherapistUsageDetail(Transform box, Transform list,
            BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationUsage summary,
            BackendClient.OrganizationUsageMember member,
            BackendClient.OrganizationUsage usage, int offset)
        {
            var detail = usage.selected_member;
            var totals = ClientRow(list, "UsageSummary", 92);
            void Metric(int index, string title, string value)
            {
                float x = .02f + index * .33f;
                ClientText(totals, title, 12,x,.65f,.30f,.25f,HomeMuted);
                var label = ClientText(totals, value, 21,x,.10f,.30f,.48f,HomeText);
                label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 21;
            }
            Metric(0, F("Used this month", "本月已用"),
                FormatOrganizationDuration(detail.used_seconds));
            Metric(1, F("Remaining", "剩余"), detail.remaining_seconds.HasValue
                ? FormatOrganizationDuration(detail.remaining_seconds.Value) : "—");
            Metric(2, F("Monthly allowance", "每月额度"), detail.allocation_seconds.HasValue
                ? FormatOrganizationDuration(detail.allocation_seconds.Value) : "—");

            var meter = ClientRow(list, "Allowance", 80);
            int warning = OrganizationUsageWarningLevel(detail.allocation_seconds,
                detail.used_seconds, detail.reserved_seconds);
            long committed = Math.Max(0L, (long)detail.used_seconds) +
                Math.Max(0L, (long)detail.reserved_seconds);
            string consumption = !detail.allocation_seconds.HasValue || detail.allocation_seconds.Value <= 0
                ? F("No allowance", "无可用额度")
                : Math.Min(100, committed * 100 / detail.allocation_seconds.Value).ToString() + F("% used", "% 已使用");
            ClientText(meter, consumption, 12,.02f,.60f,.96f,.30f,
                warning == 2 ? new Color(.94f,.30f,.32f) : warning == 1 ? HomeGold : HomeMuted);
            AddOrganizationUsageMeter(meter, detail.allocation_seconds,
                detail.used_seconds, detail.reserved_seconds, .025f,.25f,.95f,.12f);

            var chartRow = ClientRow(list, "Daily trend", 255);
            ClientText(chartRow, F("Usage over time", "用量趋势"), 17,
                .025f,.84f,.50f,.13f,HomeText);
            var selected = ClientText(chartRow, "", 13,.025f,.70f,.95f,.12f,HomeMuted);
            var graph = ClientRect(chartRow, "Recorded usage", .10f,.18f,.85f,.48f)
                .gameObject.AddComponent<UsageLineChart>();
            graph.color = HomePrimary;
            var maxLabel = ClientText(chartRow, "", 10,.005f,.60f,.09f,.07f,HomeMuted);
            ClientText(chartRow, "0", 10,.005f,.15f,.09f,.07f,HomeMuted);
            DateTime period = DateTime.TryParse(usage.period_start, out var parsed)
                ? parsed : DateTime.UtcNow.Date;
            int dayCount = usage.is_current ? DateTime.UtcNow.Day
                : DateTime.DaysInMonth(period.Year, period.Month);
            var valuesByDay = (detail.daily ?? Array.Empty<BackendClient.OrganizationUsageDay>())
                .Where(item => DateTime.TryParse(item.date, out _))
                .ToDictionary(item => DateTime.Parse(item.date).Day, item => item.used_seconds);
            var values = Enumerable.Range(1, Math.Max(1, dayCount))
                .Select(day => valuesByDay.TryGetValue(day, out int value) ? value : 0).ToArray();
            var minutes = values.Select(value => value / 60f).ToArray();
            float maximum = Mathf.Max(1, Mathf.Ceil(minutes.Max() / 5) * 5);
            graph.Refresh(minutes, maximum);
            maxLabel.text = maximum.ToString("0") + F("m", "分");
            ClientText(chartRow, period.ToString("MMM 1", Localization.Culture), 11,
                .10f,.04f,.40f,.10f,HomeMuted);
            var last = ClientText(chartRow, period.ToString("MMM ", Localization.Culture) + dayCount,
                11,.56f,.04f,.39f,.10f,HomeMuted);
            last.alignment = TextAlignmentOptions.MidlineRight;
            graph.OnSelect = index => selected.text = period.AddDays(index).ToString("MMM d") +
                " · " + FormatOrganizationDuration(values[index]);
            graph.OnSelect(values.Length - 1);
            if (values.All(value => value == 0))
                selected.text = F("No recorded hosting usage yet", "暂无主持用量记录");

            ClientText(ClientRow(list, "History heading", 44),
                F("Session history", "会话记录"), 16,.02f,.05f,.96f,.9f,HomeText);
            if (detail.sessions == null || detail.sessions.Length == 0)
                ClientText(ClientRow(list, "No sessions", 44),
                    F("No sessions recorded.", "暂无会话记录。"), 13,.02f,.05f,.96f,.9f,HomeMuted);
            foreach (var session in detail.sessions ?? Array.Empty<BackendClient.OrganizationUsageSession>())
            {
                string date = DateTimeOffset.TryParse(session.started_at, out var start)
                    ? start.ToLocalTime().ToString("MMM d · HH:mm") : session.started_at;
                var row = ClientRow(list, "Session", 46);
                ClientText(row,date,13,.025f,.10f,.46f,.80f,HomeText);
                ClientText(row,session.status=="active"?F("Active","进行中"):F("Ended","已结束"),
                    11,.49f,.10f,.18f,.80f,session.status=="active"?HomePrimary:HomeMuted);
                var duration = ClientText(row,FormatOrganizationDuration(session.used_seconds),
                    13,.68f,.10f,.29f,.80f,HomeText);
                duration.alignment = TextAlignmentOptions.MidlineRight;
            }
            if (offset > 0) ClientButton(box,"‹",.05f,.025f,.2f,.065f,
                () => ShowOrganizationTherapistUsage(workspace,summary,member,Math.Max(0,offset-50)));
            if (detail.has_more) ClientButton(box,"›",.29f,.025f,.2f,.065f,
                () => ShowOrganizationTherapistUsage(workspace,summary,member,offset+50));
        }

        private void RenderOrganizationDailyUsage(BackendClient.OrganizationUsage usage)
        {
            var days = usage.daily ?? Array.Empty<BackendClient.OrganizationUsageDay>();
            if (days.Length == 0) return;
            int maximum = Math.Max(1, days.Max(day => day.used_seconds));
            foreach (var day in days)
            {
                var row = ClientRow(_organizationPageContent, "UsageDay", 46);
                string label = day.date;
                if (DateTime.TryParse(day.date, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out DateTime parsed))
                    label = parsed.ToString("MMM d", Localization.Culture);
                ClientText(row, label, 12,.03f,.18f,.17f,.64f,HomeMuted);
                float fraction = Mathf.Clamp01((float)day.used_seconds / maximum);
                var track = ClientRect(row, "DailyTrack", .22f,.34f,.55f,.32f);
                var trackImage = track.gameObject.AddComponent<Image>();
                trackImage.color = new Color(HomePrimary.r, HomePrimary.g, HomePrimary.b,
                    HomeIsLight ? .10f : .18f);
                var fill = ClientRect(track, "DailyFill", 0,0,fraction,1);
                fill.gameObject.AddComponent<Image>().color = HomePrimary;
                ClientText(row, FormatOrganizationDuration(day.used_seconds), 12,
                    .79f,.18f,.18f,.64f,HomeText).alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        private static int OrganizationUsageWarningLevel(int? allowanceSeconds,
            int usedSeconds, int reservedSeconds)
        {
            if (!allowanceSeconds.HasValue || allowanceSeconds.Value <= 0) return 0;
            long committed = Math.Max(0L, (long)usedSeconds) + Math.Max(0L, (long)reservedSeconds);
            if (committed >= allowanceSeconds.Value) return 2;
            return committed * 100L >= (long)allowanceSeconds.Value * 80L ? 1 : 0;
        }

        private void AddOrganizationUsageWarning(Transform parent, int warningLevel,
            float x, float y, float width, float height)
        {
            if (warningLevel == 0) return;
            string label = warningLevel == 2
                ? F("100% used", "已使用 100%")
                : F("80% used", "已使用 80%");
            Color color = warningLevel == 2
                ? (HomeIsLight ? new Color(.78f,.16f,.14f,1f) : new Color(1f,.46f,.42f,1f))
                : HomeGold;
            var warning = ClientText(parent, label, 13, x, y, width, height, color);
            warning.alignment = TextAlignmentOptions.MidlineRight;
        }

        private void ClearOrganizationActivityFilters()
        {
            _organizationActivityMembershipId = _organizationActivityClientId =
                _organizationActivityAction = _organizationActivityTo = "";
            _organizationActivityFrom = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _organizationActivityEvents = Array.Empty<BackendClient.OrganizationActivity>();
            _organizationActivityNextCursor = null;
        }

        private bool OrganizationActivityRequestIsCurrent(int request, int account)
        {
            var backend = BackendClient.Instance;
            return this != null && request == _organizationPageRequest &&
                _organizationPage != null && _organizationPage.activeInHierarchy &&
                _organizationTab == "activity" && backend.IsLoggedIn &&
                backend.UserId == account && backend.UserType == "organization";
        }

        private void LoadOrganizationActivityPage(BackendClient.OrganizationWorkspace workspace,
            string cursor, int request, int account, bool append)
        {
            _organizationPageStatus.text = F("Loading…", "正在加载…");
            BackendClient.Instance.FetchOrganizationActivity(workspace.id,
                _organizationActivityMembershipId, _organizationActivityClientId,
                _organizationActivityCategory, _organizationActivityAction, _organizationActivityFrom,
                _organizationActivityTo, cursor, page =>
                {
                    if (!OrganizationActivityRequestIsCurrent(request, account)) return;
                    _organizationActivityEvents = append
                        ? _organizationActivityEvents.Concat(page.results).ToArray()
                        : page.results;
                    _organizationActivityNextCursor = page.next_cursor;
                    _organizationPageStatus.text = "";
                    ClearClientChildren(_organizationPageContent);
                    RenderOrganizationActivity(workspace);
                }, error =>
                {
                    if (OrganizationActivityRequestIsCurrent(request, account))
                        _organizationPageStatus.text = error;
                });
        }

        private void RenderOrganizationActivity(BackendClient.OrganizationWorkspace workspace)
        {
            bool filtered = !string.IsNullOrEmpty(_organizationActivityMembershipId) ||
                !string.IsNullOrEmpty(_organizationActivityClientId) ||
                !string.IsNullOrEmpty(_organizationActivityAction) ||
                _organizationActivityFrom != new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ||
                !string.IsNullOrEmpty(_organizationActivityTo);
            var categories = ClientRow(_organizationPageContent, "ActivityCategories", 76);
            string[] categoryIds = { "sessions", "clients", "therapists", "workspace" };
            string[] categoryNames = {
                F("Sessions", "会话"), F("Clients", "来访者"),
                F("Therapists", "治疗师"), F("Workspace", "工作区") };
            for (int index = 0; index < categoryIds.Length; index++)
            {
                string category = categoryIds[index];
                ClientButton(categories, categoryNames[index], .015f + index * .247f,
                    .18f, .235f, .64f, () =>
                    {
                        if (_organizationActivityCategory == category) return;
                        _organizationActivityCategory = category;
                        _organizationActivityAction = "";
                        _organizationActivityEvents = Array.Empty<BackendClient.OrganizationActivity>();
                        _organizationActivityNextCursor = null;
                        RefreshOrganizationPage();
                    }, _organizationActivityCategory == category);
            }

            var controls = ClientRow(_organizationPageContent, "ActivityFilters", 76);
            ClientText(controls, filtered ? F("Filters active", "筛选已启用") : F("Current period", "当前周期"),
                15,.03f,.20f,.48f,.60f,filtered ? HomePrimary : HomeMuted);
            ClientButton(controls, F("Filters", "筛选"), filtered ? .58f : .76f,.20f,.20f,.60f,
                () => ShowOrganizationActivityFilters(workspace), true);
            if (filtered)
                ClientButton(controls, F("Clear", "清除"), .79f,.20f,.18f,.60f, () =>
                {
                    ClearOrganizationActivityFilters(); RefreshOrganizationPage();
                });

            var events = _organizationActivityEvents;
            if (events == null || events.Length == 0)
            {
                AddOrganizationEmptyRow(filtered
                    ? F("No organization activity for these filters.", "这些筛选条件下暂无机构活动。")
                    : F("No organization activity yet.", "暂无机构活动记录。"));
                return;
            }
            foreach (var item in events)
            {
                bool completedSession = item.action == "session_completed";
                var row = ClientRow(_organizationPageContent, "Activity", completedSession ? 108 : 92);
                string person = !string.IsNullOrWhiteSpace(item.therapist_name)
                    ? item.therapist_name : item.actor_name;
                string client = string.IsNullOrWhiteSpace(item.client_name)
                    ? "" : " · " + item.client_name;
                string title = OrganizationActivityLabel(item.action);
                if (completedSession && item.metadata != null)
                    title += " · " + FormatOrganizationDuration(item.metadata.duration_seconds);
                ClientText(row, title, 16,
                    .03f,.54f,.62f,.28f,HomeText);
                if (completedSession)
                    client += " · " + F("Organization usage", "机构用量");
                ClientText(row, (person ?? "") + client, 12,
                    .03f,.18f,.68f,.24f,HomeMuted);
                string when = item.created_at ?? "";
                if (completedSession && item.metadata != null &&
                    DateTimeOffset.TryParse(item.metadata.started_at, out var started) &&
                    DateTimeOffset.TryParse(item.metadata.ended_at, out var ended))
                    when = started.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) +
                        " – " + ended.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
                else if (DateTimeOffset.TryParse(when, out var parsed))
                    when = parsed.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
                ClientText(row, when, 12,.73f,.31f,.24f,.30f,HomeMuted);
            }
            if (!string.IsNullOrEmpty(_organizationActivityNextCursor))
                ClientButton(ClientRow(_organizationPageContent, "ActivityMore", 70),
                    F("Load more", "加载更多"), .36f,.18f,.28f,.64f, () =>
                    {
                        int request = _organizationPageRequest;
                        int account = BackendClient.Instance.UserId;
                        LoadOrganizationActivityPage(workspace, _organizationActivityNextCursor,
                            request, account, true);
                    });
        }

        private void ShowOrganizationActivityFilters(BackendClient.OrganizationWorkspace workspace)
        {
            var box = ClientDialog(F("Filter activity", "筛选活动"), 680, 820);
            var dialog = _clientDialog;
            Transform Row(string name, string label, float bottom, float top)
            {
                var row = ClientRect(box, name, .05f,bottom,.95f,top);
                row.gameObject.AddComponent<Image>().color = HomeCard;
                ClientText(row, label, 14,.025f,.18f,.49f,.64f,HomeText);
                return row;
            }
            var memberIds = new[] { "" }.Concat(_organizationActivityMembers.Select(item => item.id)).ToArray();
            var memberNames = new[] { F("All therapists", "全部治疗师") }.Concat(
                _organizationActivityMembers.Select(item => item.therapist_name)).ToArray();
            int memberIndex = Math.Max(0, Array.IndexOf(memberIds, _organizationActivityMembershipId));
            var member = SettingsDropdown(Row("ActivityTherapist", F("Therapist", "治疗师"), .79f,.88f),
                "ActivityTherapistDropdown", memberNames, memberIndex, _ => { });

            var clientIds = new[] { "" }.Concat(_organizationActivityClients.Select(item => item.id)).ToArray();
            var clientNames = new[] { F("All clients", "全部来访者") }.Concat(
                _organizationActivityClients.Select(item => item.name)).ToArray();
            int clientIndex = Math.Max(0, Array.IndexOf(clientIds, _organizationActivityClientId));
            var client = SettingsDropdown(Row("ActivityClient", F("Client", "来访者"), .67f,.76f),
                "ActivityClientDropdown", clientNames, clientIndex, _ => { });

            string[] actions = OrganizationActivityActions(_organizationActivityCategory);
            var actionNames = actions.Select(value => string.IsNullOrEmpty(value)
                ? F("All activity types", "全部活动类型") : OrganizationActivityLabel(value)).ToArray();
            int actionIndex = Math.Max(0, Array.IndexOf(actions, _organizationActivityAction));
            var action = SettingsDropdown(Row("ActivityType", F("Activity type", "活动类型"), .55f,.64f),
                "ActivityTypeDropdown", actionNames, actionIndex, _ => { });

            ClientText(box, F("From (YYYY-MM-DD)", "开始日期（YYYY-MM-DD）"), 13,.05f,.47f,.42f,.04f,HomeMuted);
            ClientText(box, F("To (YYYY-MM-DD)", "结束日期（YYYY-MM-DD）"), 13,.53f,.47f,.42f,.04f,HomeMuted);
            var from = ClientInput(box, _organizationActivityFrom, "YYYY-MM-DD", .05f,.39f,.42f,.07f,10);
            var to = ClientInput(box, _organizationActivityTo, "YYYY-MM-DD", .53f,.39f,.42f,.07f,10);
            var status = ClientText(box, "", 13,.05f,.20f,.90f,.12f,HomeMuted);
            ClientButton(box, F("Cancel", "取消"), .05f,.06f,.42f,.09f, CloseClientDialog);
            ClientButton(box, F("Apply", "应用"), .53f,.06f,.42f,.09f, () =>
            {
                if (dialog != _clientDialog) return;
                string fromValue = from.text.Trim(), toValue = to.text.Trim();
                bool validFrom = string.IsNullOrEmpty(fromValue) || DateTime.TryParseExact(fromValue,
                    "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
                bool validTo = string.IsNullOrEmpty(toValue) || DateTime.TryParseExact(toValue,
                    "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
                if (!validFrom || !validTo || (!string.IsNullOrEmpty(fromValue) &&
                    !string.IsNullOrEmpty(toValue) && string.CompareOrdinal(fromValue,toValue) > 0))
                { status.text = F("Enter a valid date range.", "请输入有效的日期范围。"); return; }
                _organizationActivityMembershipId = memberIds[member.value];
                _organizationActivityClientId = clientIds[client.value];
                _organizationActivityAction = actions[action.value];
                _organizationActivityFrom = fromValue; _organizationActivityTo = toValue;
                CloseClientDialog(); RefreshOrganizationPage();
            }, true);
        }

        private string OrganizationActivityLabel(string action)
        {
            switch (action)
            {
                case "client_created": return F("Client added", "已添加来访者");
                case "client_assigned": return F("Client assigned", "已分配来访者");
                case "client_reassigned": return F("Client reassigned", "已重新分配来访者");
                case "client_unassigned": return F("Client unassigned", "来访者已取消分配");
                case "client_archived": return F("Client archived", "来访者已归档");
                case "client_restored": return F("Client restored", "来访者已恢复");
                case "therapist_invited": return F("Therapist invited", "已邀请治疗师");
                case "invitation_revoked": return F("Invitation revoked", "邀请已撤销");
                case "therapist_joined": return F("Therapist joined", "治疗师已加入");
                case "managed_therapist_created": return F("Managed therapist created", "已创建托管治疗师");
                case "therapist_locked": return F("Therapist locked", "治疗师已锁定");
                case "therapist_unlocked": return F("Therapist unlocked", "治疗师已解锁");
                case "therapist_removed": return F("Therapist removed", "治疗师已移除");
                case "hosting_allocation_changed": return F("Hosting allocation changed", "主持时长分配已更改");
                case "client_limit_changed": return F("Client limit changed", "来访者上限已更改");
                case "therapist_permissions_changed": return F("Therapist permissions changed", "治疗师权限已更改");
                case "branding_changed": return F("Organization settings changed", "机构设置已更改");
                case "session_completed": return F("Live session", "实时会话");
                case "session_invited": return F("Session invitation sent", "已发送会话邀请");
                case "schedule_requested": return F("Session requested", "已请求预约");
                case "schedule_accepted": return F("Session accepted", "预约已接受");
                case "schedule_declined": return F("Session declined", "预约已拒绝");
                case "schedule_cancelled": return F("Session cancelled", "预约已取消");
                case "board_created": return F("Board created", "已创建沙盘");
                case "board_updated": return F("Board backed up", "沙盘已备份");
                case "report_created": return F("Report backed up", "报告已备份");
                case "report_shared": return F("Report shared", "已共享报告");
                default: return string.IsNullOrWhiteSpace(action) ? F("Organization activity", "机构活动") : action.Replace('_', ' ');
            }
        }

        private static string[] OrganizationActivityActions(string category)
        {
            switch (category)
            {
                case "clients": return new[] { "", "client_created", "client_assigned",
                    "client_reassigned", "client_unassigned", "client_archived", "client_restored" };
                case "therapists": return new[] { "", "therapist_invited", "invitation_revoked",
                    "therapist_joined", "managed_therapist_created", "therapist_locked",
                    "therapist_unlocked", "therapist_removed", "hosting_allocation_changed",
                    "client_limit_changed", "therapist_permissions_changed" };
                case "workspace": return new[] { "", "board_created", "board_updated",
                    "report_created", "report_shared", "branding_changed" };
                default: return new[] { "", "session_completed", "session_invited",
                    "schedule_requested", "schedule_accepted", "schedule_declined",
                    "schedule_cancelled" };
            }
        }

        private static string FormatOrganizationDuration(int seconds)
        {
            decimal hours = seconds / 3600m;
            if (seconds > 0 && seconds < 60) return seconds.ToString(CultureInfo.InvariantCulture) + " s";
            if (seconds > 0 && seconds < 3600)
                return (seconds / 60m).ToString("0.##", CultureInfo.InvariantCulture) + " min";
            return hours.ToString(hours == decimal.Truncate(hours) ? "0" : "0.##",
                CultureInfo.InvariantCulture) + " h";
        }

        private void ShowOrganizationHostingAllocationDialog(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationUsage usage, BackendClient.OrganizationUsageMember usageMember)
        {
            var box = ClientDialog(F("Hosting allocation", "主持时长分配"), 620, 470);
            var dialog = _clientDialog;
            ClientText(box, usageMember.therapist_name + "\n" + usageMember.therapist_email,
                18,.06f,.72f,.88f,.13f,HomeText);
            ClientText(box, F("Monthly hours (blank means unallocated)", "每月小时数（留空表示未分配）"),
                13,.06f,.58f,.88f,.05f,HomeMuted);
            var hours = ClientInput(box, usageMember.allocation_seconds.HasValue
                ? (usageMember.allocation_seconds.Value / 3600m).ToString("0.##", CultureInfo.InvariantCulture)
                : "", "", .06f,.47f,.88f,.085f,10);
            hours.contentType = TMP_InputField.ContentType.DecimalNumber;
            var status = ClientText(box, "", 13,.06f,.20f,.88f,.16f,HomeMuted);
            bool busy = false;
            ClientButton(box, F("Cancel", "取消"), .06f,.06f,.40f,.10f, CloseOrganizationDialog);
            Button save = null;
            save = ClientButton(box, F("Save allocation", "保存分配"), .54f,.06f,.40f,.10f, () =>
            {
                if (busy || dialog != _clientDialog) return;
                if (!TryOrganizationHours(hours.text, out int? seconds))
                { status.text = F("Enter a non-negative number, or leave it blank.", "请输入非负数，或留空。"); return; }
                busy = true; save.interactable = false;
                BackendClient.Instance.UpdateOrganizationHostingAllocations(workspace.id,
                    usage, usageMember.membership_id, seconds, () =>
                    {
                        if (dialog != _clientDialog) return;
                        CloseOrganizationDialog(); RefreshOrganizationPage();
                    }, error => { if (dialog == _clientDialog) { busy = false; save.interactable = true; status.text = error; } });
            }, true);
        }

        private void AddOrganizationSectionHeading(string title)
        {
            var row = ClientRow(_organizationPageContent, "Section", 54);
            ClientText(row, title, 18,.02f,.15f,.96f,.68f,HomeText).fontStyle = FontStyles.Bold;
        }

        private void AddOrganizationEmptyRow(string text)
        {
            var row = ClientRow(_organizationPageContent, "Empty", 70);
            ClientText(row, text, 14,.03f,.15f,.94f,.70f,HomeMuted);
        }

        private void ShowOrganizationClientsDialog(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationMember member, BackendClient.OrganizationMember[] members,
            bool archived = false)
        {
            string title = archived ? F("Archived clients", "已归档来访者")
                : member == null ? F("Unassigned clients", "未分配来访者")
                : member.therapist_name;
            var box = ClientDialog(title, 760, 760);
            var dialog = _clientDialog;
            var status = ClientText(box, F("Loading…", "正在加载…"), 13,
                .05f,.095f,.62f,.055f,HomeMuted);
            var list = ClientScroll(box, "OrganizationClients", .05f,.17f,.90f,.64f);
            ClientButton(box, F("Close", "关闭"), .05f,.035f,.30f,.07f, CloseOrganizationDialog);
            if (!archived)
                ClientButton(box, F("Add client", "添加来访者"), .73f,.835f,.22f,.07f,
                    () => ShowOrganizationClientCreateDialog(workspace, member, members), true);
            string assignment = archived ? "" : member == null ? "unassigned" : member.id;
            BackendClient.Instance.FetchOrganizationClients(workspace.id, assignment, archived, clients =>
            {
                if (dialog == null || dialog != _clientDialog) return;
                ClearClientChildren(list);
                status.text = "";
                if (clients == null || clients.Length == 0)
                {
                    var empty = ClientRow(list, "Empty", 76);
                    ClientText(empty, archived ? F("No archived clients.", "暂无已归档来访者。")
                        : F("No clients here yet.", "这里还没有来访者。"),
                        14,.04f,.10f,.92f,.80f,HomeMuted);
                    return;
                }
                foreach (var client in clients)
                {
                    var row = ClientRow(list, "OrganizationClient", 104);
                    ClientText(row, client.name ?? "", 17,.03f,.55f,.53f,.28f,HomeText);
                    string detail = string.IsNullOrWhiteSpace(client.reference)
                        ? (client.assigned_therapist_name ?? "")
                        : client.reference + (string.IsNullOrWhiteSpace(client.assigned_therapist_name)
                            ? "" : " · " + client.assigned_therapist_name);
                    ClientText(row, detail, 12,.03f,.19f,.56f,.24f,HomeMuted);
                    if (archived)
                    {
                        ClientButton(row, F("Restore", "恢复"), .77f,.31f,.20f,.40f, () =>
                        {
                            status.text = F("Restoring…", "正在恢复…");
                            BackendClient.Instance.SetOrganizationClientArchived(workspace.id, client, false,
                                () => ShowOrganizationClientsDialog(workspace, null, members, true),
                                error => { if (dialog == _clientDialog) status.text = error; });
                        }, true);
                    }
                    else
                    {
                        ClientButton(row, F("Assign", "分配"), .62f,.31f,.16f,.40f,
                            () => ShowOrganizationClientAssignmentDialog(workspace, client, members));
                        ClientButton(row, F("Archive", "归档"), .80f,.31f,.17f,.40f, () =>
                        {
                            status.text = F("Archiving…", "正在归档…");
                            BackendClient.Instance.SetOrganizationClientArchived(workspace.id, client, true,
                                () => ShowOrganizationClientsDialog(workspace, member, members),
                                error => { if (dialog == _clientDialog) status.text = error; });
                        });
                    }
                }
            }, error => { if (dialog == _clientDialog) status.text = error; });
        }

        private void ShowOrganizationClientCreateDialog(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationMember member, BackendClient.OrganizationMember[] members)
        {
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
            ClientButton(box, F("Cancel", "取消"), .06f,.05f,.40f,.09f,
                () => ShowOrganizationClientsDialog(workspace, member, members));
            create = ClientButton(box, F("Add client", "添加来访者"), .54f,.05f,.40f,.09f, () =>
            {
                if (busy || dialog != _clientDialog) return;
                if (selected == null)
                { status.text = F("Choose a client account first.", "请先选择来访者账号。"); return; }
                busy = true; create.interactable = false;
                BackendClient.Instance.CreateOrganizationClient(workspace.id, selected.code, reference.text,
                    member?.id, _ =>
                    {
                        if (dialog == _clientDialog)
                            ShowOrganizationClientsDialog(workspace, member, members);
                    }, error =>
                    {
                        if (dialog != _clientDialog) return;
                        busy = false; create.interactable = true; status.text = error;
                    });
            }, true);
            create.interactable = false;
        }

        private void ShowOrganizationClientAssignmentDialog(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationClient client, BackendClient.OrganizationMember[] members)
        {
            var box = ClientDialog(F("Assign client", "分配来访者"), 620, 610);
            var dialog = _clientDialog;
            ClientText(box, client.name ?? "", 18,.06f,.82f,.88f,.07f,HomeText);
            var status = ClientText(box, "", 13,.06f,.10f,.88f,.07f,HomeMuted);
            var list = ClientScroll(box, "TherapistChoices", .06f,.20f,.88f,.58f);
            void Assign(string membershipId)
            {
                if (dialog != _clientDialog) return;
                status.text = F("Saving…", "正在保存…");
                foreach (var button in box.GetComponentsInChildren<Button>()) button.interactable = false;
                BackendClient.Instance.AssignOrganizationClient(workspace.id, client, membershipId,
                    () => ShowOrganizationClientsDialog(workspace,
                        members?.FirstOrDefault(item => item != null && item.id == membershipId), members),
                    error =>
                    {
                        if (dialog != _clientDialog) return;
                        foreach (var button in box.GetComponentsInChildren<Button>()) button.interactable = true;
                        status.text = error;
                    });
            }
            var unassigned = ClientRow(list, "Unassigned", 64);
            ClientButton(unassigned, F("Unassigned", "未分配"), .02f,.10f,.96f,.80f, () => Assign(null));
            foreach (var choice in (members ?? Array.Empty<BackendClient.OrganizationMember>())
                .Where(item => item != null && item.status == "active"))
            {
                var membership = choice;
                var row = ClientRow(list, "Therapist", 64);
                ClientButton(row, membership.therapist_name, .02f,.10f,.96f,.80f,
                    () => Assign(membership.id));
            }
            ClientButton(box, F("Cancel", "取消"), .06f,.035f,.40f,.09f,
                () => ShowOrganizationClientsDialog(workspace,
                    members?.FirstOrDefault(item => item != null && item.id == client.assigned_membership_id), members));
        }

        private void AddOrganizationMemberRow(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationMember member, BackendClient.OrganizationMember[] members)
        {
            if (member == null) return;
            var row = ClientRow(_organizationPageContent, "Member", 118);
            string name = string.IsNullOrWhiteSpace(member.therapist_name) ? member.therapist_email : member.therapist_name;
            ClientText(row, name, 17,.03f,.57f,.45f,.28f,HomeText);
            ClientText(row, member.therapist_email ?? "", 12,.03f,.31f,.48f,.22f,HomeMuted);
            string clients = member.active_client_count + " / " +
                (member.client_limit.HasValue ? member.client_limit.Value.ToString() : "∞");
            string hosting = member.hosting_seconds_limit.HasValue
                ? FormatOrganizationDuration(member.hosting_seconds_limit.Value) : F("Unallocated", "未分配");
            string ownership = member.managed_account ? F("Managed", "机构托管") : F("Independent", "独立账号");
            string accountState = member.is_locked ? F("Locked", "已锁定") : member.status;
            ClientText(row, F($"{ownership} · Clients: {clients} · Hosting: {hosting} · {accountState}",
                $"{ownership} · 来访者：{clients} · 主持：{hosting} · {accountState}"),
                12,.03f,.07f,.62f,.20f,HomeMuted);
            if (member.status == "active")
            {
                if (member.managed_account)
                {
                    ClientButton(row, F("Clients", "来访者"), .43f,.52f,.10f,.31f,
                        () => ShowOrganizationClientsDialog(workspace, member, members));
                    ClientButton(row, F("Limits", "限额"), .54f,.52f,.09f,.31f,
                        () => ShowOrganizationLimitsDialog(workspace, member));
                    ClientButton(row, F("Permissions", "权限"), .64f,.52f,.12f,.31f,
                        () => ShowManagedTherapistPermissionsDialog(workspace, member));
                    ClientButton(row, member.is_locked ? F("Unlock", "解锁") : F("Lock", "锁定"),
                        .77f,.52f,.09f,.31f,
                        () => ConfirmManagedTherapistLock(workspace, member));
                }
                else
                {
                    ClientButton(row, F("Clients", "来访者"), .63f,.52f,.11f,.31f,
                        () => ShowOrganizationClientsDialog(workspace, member, members));
                    ClientButton(row, F("Limits", "限额"), .75f,.52f,.11f,.31f,
                        () => ShowOrganizationLimitsDialog(workspace, member));
                }
                ClientButton(row, F("Remove", "移除"), .87f,.52f,.11f,.31f,
                    () => ConfirmOrganizationMemberRemoval(workspace, member));
            }
        }

        private void AddOrganizationInvitationRow(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationInvitation invitation)
        {
            if (invitation == null) return;
            var row = ClientRow(_organizationPageContent, "Invitation", 88);
            ClientText(row, invitation.email ?? "", 16,.03f,.50f,.58f,.34f,HomeText);
            string state = invitation.is_expired ? F("expired", "已过期") : invitation.status;
            ClientText(row, state ?? "", 12,.03f,.16f,.45f,.25f,HomeMuted);
            if (invitation.status == "pending" && !invitation.is_expired)
                ClientButton(row, F("Revoke", "撤销"), .83f,.30f,.14f,.42f,
                    () => RevokeOrganizationInvitation(workspace, invitation));
        }

        private void ShowOrganizationCreateDialog()
        {
            var box = ClientDialog(F("Create organization", "创建机构"), 600, 420);
            var dialog = _clientDialog;
            ClientText(box, F("Organization name", "机构名称"), 14,.06f,.68f,.88f,.07f,HomeMuted);
            var name = ClientInput(box, "", F("Organization name", "机构名称"), .06f,.53f,.88f,.10f,160);
            var status = ClientText(box, "", 13,.06f,.22f,.88f,.20f,HomeMuted);
            bool busy = false;
            ClientButton(box, F("Cancel", "取消"), .06f,.06f,.40f,.12f, CloseOrganizationDialog);
            Button create = null;
            create = ClientButton(box, F("Create workspace", "创建工作区"), .54f,.06f,.40f,.12f, () =>
            {
                if (busy || dialog != _clientDialog) return;
                if (string.IsNullOrWhiteSpace(name.text)) { status.text = F("Enter an organization name.", "请输入机构名称。"); return; }
                busy = true; create.interactable = false;
                BackendClient.Instance.CreateOrganization(name.text, _ =>
                {
                    if (dialog != _clientDialog) return;
                    CloseOrganizationDialog(); RefreshOrganizationPage();
                }, error => { if (dialog == _clientDialog) { busy = false; create.interactable = true; status.text = error; } });
            }, true);
        }

        private void ShowOrganizationInviteDialog(BackendClient.OrganizationWorkspace workspace)
        {
            var box = ClientDialog(F("Invite therapist", "邀请治疗师"), 640, 520);
            var dialog = _clientDialog;
            ClientText(box, F("Choose a Sandtray account currently set to Therapist.",
                "选择一个当前设置为治疗师的 Sandtray 账户。"), 14,.06f,.74f,.88f,.08f,HomeMuted);
            var selectedLabel = ClientText(box, F("No therapist selected", "尚未选择治疗师"),
                16,.06f,.55f,.88f,.11f,HomeText);
            selectedLabel.richText = false;
            var status = ClientText(box, F(
                "The therapist will receive an in-app invitation and can accept or decline it.",
                "治疗师将在应用内收到邀请，并可接受或拒绝。"),
                13,.06f,.22f,.88f,.15f,HomeMuted);
            status.enableWordWrapping = true;
            FriendPerson selected = null;
            Button invite = null;
            void Select(bool friends)
            {
                ShowClientAccountPicker(dialog.transform, friends, person =>
                {
                    if (dialog != _clientDialog) return;
                    if (person.user_type != "psychologist")
                    {
                        selected = null;
                        selectedLabel.text = F("No therapist selected", "尚未选择治疗师");
                        status.text = F("User type is not Therapist.", "该用户类型不是治疗师。");
                        if (invite != null) invite.interactable = false;
                        return;
                    }
                    selected = person;
                    string id = string.IsNullOrEmpty(person.friend_code) ? person.code : person.friend_code;
                    selectedLabel.text = person.name + " · " + id;
                    if (invite != null) invite.interactable = true;
                });
            }
            ClientButton(box, F("Search account ID", "搜索账号 ID"), .06f,.43f,.42f,.10f, () => Select(false));
            if(BackendClient.Instance.ExternalContactsAllowed)
                ClientButton(box, F("From friends", "从好友选择"), .52f,.43f,.42f,.10f, () => Select(true));
            bool busy = false;
            ClientButton(box, F("Close", "关闭"), .06f,.06f,.40f,.11f, CloseOrganizationDialog);
            invite = ClientButton(box, F("Send invitation", "发送邀请"), .54f,.06f,.40f,.11f, () =>
            {
                if (busy || dialog != _clientDialog) return;
                if (selected == null || string.IsNullOrWhiteSpace(selected.code))
                { status.text = F("Choose a therapist account first.", "请先选择治疗师账户。"); return; }
                busy = true; invite.interactable = false;
                BackendClient.Instance.InviteOrganizationTherapistAccount(workspace.id, selected.code, result =>
                {
                    if (dialog != _clientDialog) return;
                    status.text = F("Invitation sent to " + (result.therapist_name ?? selected.name) + ".",
                        "邀请已发送给 " + (result.therapist_name ?? selected.name) + "。");
                    RefreshOrganizationPage();
                }, error => { if (dialog == _clientDialog) { busy = false; invite.interactable = true; status.text = error; } });
            }, true);
            invite.interactable = false;
        }

        private void ShowManagedTherapistCreateDialog(BackendClient.OrganizationWorkspace workspace)
        {
            var box = ClientDialog(F("Create managed therapist", "创建机构托管治疗师"), 660, 650);
            var dialog = _clientDialog;
            ClientText(box, F(
                "This account belongs only to the organization. It has no Personal workspace and cannot create private local clients.",
                "此账号仅属于机构，没有个人工作区，也不能创建私人本地来访者。"),
                13,.06f,.78f,.88f,.11f,HomeMuted).enableWordWrapping = true;
            ClientText(box,F("Therapist name","治疗师姓名"),13,.06f,.69f,.88f,.04f,HomeMuted);
            var name=ClientInput(box,"",F("Name","姓名"),.06f,.61f,.88f,.065f,150);
            ClientText(box,F("Login name","登录名"),13,.06f,.53f,.88f,.04f,HomeMuted);
            var username=ClientInput(box,"",F("for example: alex","例如：alex"),.06f,.45f,.52f,.065f,40);
            ClientText(box,"@"+(workspace.login_domain??"organization"),14,.60f,.45f,.34f,.065f,HomeText);
            ClientText(box,F("Temporary password","临时密码"),13,.06f,.37f,.88f,.04f,HomeMuted);
            var password=ClientInput(box,"","",.06f,.29f,.88f,.065f,128);
            password.inputType=TMP_InputField.InputType.Password;password.ForceLabelUpdate();
            var status=ClientText(box,F(
                "Default permissions allow schedules, hosting and reports. Client creation stays off until the shared organization directory is available.",
                "默认允许预约、主持和报告。在机构共享来访者目录可用前，创建来访者保持关闭。"),
                12,.06f,.13f,.88f,.12f,HomeMuted);status.enableWordWrapping=true;
            bool busy=false;
            ClientButton(box,F("Cancel","取消"),.06f,.035f,.40f,.075f,CloseOrganizationDialog);
            Button create=null;
            create=ClientButton(box,F("Create account","创建账号"),.54f,.035f,.40f,.075f,()=>
            {
                if(busy || dialog!=_clientDialog)return;
                if(string.IsNullOrWhiteSpace(name.text)||string.IsNullOrWhiteSpace(username.text)||password.text.Length==0)
                {status.text=F("Enter a name, login name and temporary password.","请输入姓名、登录名和临时密码。");return;}
                busy=true;create.interactable=false;
                BackendClient.Instance.CreateManagedOrganizationTherapist(workspace.id,username.text,name.text,password.text,_=>
                {
                    if(dialog!=_clientDialog)return;
                    CloseOrganizationDialog();RefreshOrganizationPage();
                },error=>{if(dialog==_clientDialog){busy=false;create.interactable=true;status.text=error;}});
            },true);
        }

        private void ShowManagedTherapistPermissionsDialog(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationMember member)
        {
            var box=ClientDialog(F("Managed therapist permissions","托管治疗师权限"),680,780);
            var dialog=_clientDialog;
            ClientText(box,member.therapist_name+"\n"+member.therapist_email,18,.06f,.82f,.88f,.10f,HomeText);
            bool clients=member.can_create_clients,schedules=member.can_create_schedules,
                hosting=member.can_host_sessions,reports=member.can_create_reports,invites=member.can_invite_clients,
                externalContacts=member.allow_external_contacts;
            string Label(string title,bool enabled)=>title+" · "+(enabled?F("Allowed","允许"):F("Not allowed","不允许"));
            Button clientsButton=null,schedulesButton=null,hostingButton=null,reportsButton=null,invitesButton=null,externalButton=null;
            clientsButton=ClientButton(box,Label(F("Create organization clients","创建机构来访者"),clients),.06f,.74f,.88f,.065f,()=>{clients=!clients;clientsButton.GetComponentInChildren<TMP_Text>().text=Label(F("Create organization clients","创建机构来访者"),clients);});
            schedulesButton=ClientButton(box,Label(F("Create schedules","创建预约"),schedules),.06f,.655f,.88f,.065f,()=>{schedules=!schedules;schedulesButton.GetComponentInChildren<TMP_Text>().text=Label(F("Create schedules","创建预约"),schedules);});
            hostingButton=ClientButton(box,Label(F("Host online sessions","主持在线会话"),hosting),.06f,.57f,.88f,.065f,()=>{hosting=!hosting;hostingButton.GetComponentInChildren<TMP_Text>().text=Label(F("Host online sessions","主持在线会话"),hosting);});
            reportsButton=ClientButton(box,Label(F("Create and share reports","创建和共享报告"),reports),.06f,.485f,.88f,.065f,()=>{reports=!reports;reportsButton.GetComponentInChildren<TMP_Text>().text=Label(F("Create and share reports","创建和共享报告"),reports);});
            invitesButton=ClientButton(box,Label(F("Invite client accounts","邀请来访者账号"),invites),.06f,.40f,.88f,.065f,()=>{invites=!invites;invitesButton.GetComponentInChildren<TMP_Text>().text=Label(F("Invite client accounts","邀请来访者账号"),invites);});
            externalButton=ClientButton(box,Label(F("Friends and external chat","好友和外部聊天"),externalContacts),.06f,.315f,.88f,.065f,()=>{externalContacts=!externalContacts;externalButton.GetComponentInChildren<TMP_Text>().text=Label(F("Friends and external chat","好友和外部聊天"),externalContacts);});
            var reset=ClientInput(box,"",F("New temporary password (optional)","新临时密码（可选）"),.06f,.205f,.88f,.065f,128);
            reset.inputType=TMP_InputField.InputType.Password;reset.ForceLabelUpdate();
            var status=ClientText(box,F("Permissions are enforced by the server. Client creation remains unavailable until the organization client directory is implemented.",
                "权限由服务器强制执行。在机构来访者目录实现前，创建来访者仍不可用。"),12,.06f,.105f,.88f,.07f,HomeMuted);
            bool busy=false;
            ClientButton(box,F("Cancel","取消"),.06f,.025f,.40f,.065f,CloseOrganizationDialog);
            Button save=null;
            save=ClientButton(box,F("Save permissions","保存权限"),.54f,.025f,.40f,.065f,()=>
            {
                if(busy||dialog!=_clientDialog)return;busy=true;save.interactable=false;
                BackendClient.Instance.UpdateOrganizationMember(workspace.id,member.id,member.client_limit,
                    ()=>{if(dialog==_clientDialog){CloseOrganizationDialog();RefreshOrganizationPage();}},
                    error=>{if(dialog==_clientDialog){busy=false;save.interactable=true;status.text=error;}},
                    clients,schedules,hosting,reports,invites,externalContacts,reset.text);
            },true);
        }

        private void ShowOrganizationBrandingDialog(BackendClient.OrganizationWorkspace workspace)
        {
            var box = ClientDialog(F("Organization settings", "机构设置"), 640, 650);
            var dialog = _clientDialog;
            ClientText(box, F("Name", "名称"), 13,.06f,.81f,.88f,.05f,HomeMuted);
            var name = ClientInput(box, workspace.name ?? "", "", .06f,.72f,.88f,.075f,160);
            ClientText(box, F("Logo URL (optional)", "标志网址（可选）"), 13,.06f,.63f,.88f,.05f,HomeMuted);
            var logo = ClientInput(box, workspace.logo_url ?? "", "https://", .06f,.54f,.88f,.075f,500);
            ClientText(box, F("Accent color (optional, for example #245B6B)", "强调色（可选，例如 #245B6B）"), 13,.06f,.45f,.88f,.05f,HomeMuted);
            var accent = ClientInput(box, workspace.accent_color ?? "", "#245B6B", .06f,.36f,.88f,.075f,7);
            var status = ClientText(box, "", 13,.06f,.19f,.88f,.10f,HomeMuted);
            bool busy = false;
            ClientButton(box, F("Cancel", "取消"), .06f,.05f,.40f,.10f, CloseOrganizationDialog);
            Button save = null;
            save = ClientButton(box, F("Save", "保存"), .54f,.05f,.40f,.10f, () =>
            {
                if (busy || dialog != _clientDialog) return;
                busy = true; save.interactable = false;
                BackendClient.Instance.UpdateOrganization(workspace.id, name.text, logo.text, accent.text, () =>
                {
                    if (dialog != _clientDialog) return;
                    CloseOrganizationDialog(); RefreshOrganizationPage();
                }, error => { if (dialog == _clientDialog) { busy = false; save.interactable = true; status.text = error; } });
            }, true);
        }

        private void ShowOrganizationLimitsDialog(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationMember member)
        {
            var box = ClientDialog(F("Therapist limits", "治疗师限额"), 640, 470);
            var dialog = _clientDialog;
            ClientText(box, member.therapist_name + "\n" + member.therapist_email, 19,.06f,.78f,.88f,.11f,HomeText);
            ClientText(box, F("Active-client cap (blank means no cap)", "在用来访者上限（留空表示无限制）"), 13,.06f,.67f,.88f,.05f,HomeMuted);
            var clients = ClientInput(box, member.client_limit?.ToString() ?? "", "", .06f,.58f,.88f,.075f,9);
            clients.contentType = TMP_InputField.ContentType.IntegerNumber;
            var status = ClientText(box, F(
                "Hosting allocation is managed from the Usage tab.",
                "主持时长分配请在“用量”页管理。"),
                13,.06f,.31f,.88f,.10f,HomeMuted);
            status.enableWordWrapping = true;
            bool busy = false;
            ClientButton(box, F("Cancel", "取消"), .06f,.04f,.40f,.10f, CloseOrganizationDialog);
            Button save = null;
            save = ClientButton(box, F("Save limits", "保存限额"), .54f,.04f,.40f,.10f, () =>
            {
                if (busy || dialog != _clientDialog) return;
                if (!TryOrganizationNonnegativeInt(clients.text, out int? clientLimit))
                { status.text = F("Enter a non-negative number, or leave it blank.", "请输入非负数，或将字段留空。"); return; }
                busy = true; save.interactable = false;
                BackendClient.Instance.UpdateOrganizationMember(workspace.id, member.id,
                    clientLimit, () =>
                    {
                        if (dialog != _clientDialog) return;
                        CloseOrganizationDialog(); RefreshOrganizationPage();
                    }, error => { if (dialog == _clientDialog) { busy = false; save.interactable = true; status.text = error; } });
            }, true);
        }

        private static bool TryOrganizationNonnegativeInt(string text, out int? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed < 0) return false;
            value = parsed; return true;
        }

        private static bool TryOrganizationHours(string text, out int? seconds)
        {
            seconds = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal hours) || hours < 0) return false;
            decimal rawSeconds = decimal.Round(hours * 3600m, 0, MidpointRounding.AwayFromZero);
            if (rawSeconds > int.MaxValue) return false;
            seconds = (int)rawSeconds; return true;
        }

        private void ConfirmOrganizationMemberRemoval(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationMember member)
        {
            var box = ClientDialog(F("Remove therapist?", "移除治疗师？"), 560, 360);
            var dialog = _clientDialog;
            var status = ClientText(box, F(
                $"Remove {member.therapist_name} from {workspace.name}? Their personal local records will not be deleted.",
                $"从 {workspace.name} 移除 {member.therapist_name}？其个人本地记录不会被删除。"),
                16,.07f,.34f,.86f,.42f,HomeText);
            status.enableWordWrapping = true;
            bool busy = false;
            ClientButton(box, F("Cancel", "取消"), .07f,.07f,.40f,.14f, CloseOrganizationDialog);
            Button remove = null;
            remove = ClientButton(box, F("Remove", "移除"), .53f,.07f,.40f,.14f, () =>
            {
                if (busy || dialog != _clientDialog) return;
                busy = true; remove.interactable = false;
                BackendClient.Instance.RemoveOrganizationMember(workspace.id, member.id, () =>
                {
                    if (dialog != _clientDialog) return;
                    CloseOrganizationDialog(); RefreshOrganizationPage();
                }, error => { if (dialog == _clientDialog) { busy = false; remove.interactable = true; status.text = error; } });
            }, true);
        }

        private void ConfirmManagedTherapistLock(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationMember member)
        {
            bool lockAccount = !member.is_locked;
            var box = ClientDialog(lockAccount
                ? F("Lock therapist account?", "锁定治疗师账户？")
                : F("Unlock therapist account?", "解锁治疗师账户？"), 580, 390);
            var dialog = _clientDialog;
            string message = lockAccount
                ? F($"Lock {member.therapist_name} immediately? They will be signed out and cannot sign in until you unlock the account. Their data and settings will be kept.",
                    $"立即锁定 {member.therapist_name}？该用户将退出登录，并且在您解锁账户前无法再次登录。其数据和设置将被保留。")
                : F($"Unlock {member.therapist_name}? They will be able to sign in again with their existing password.",
                    $"解锁 {member.therapist_name}？该用户将可以使用现有密码重新登录。");
            var status = ClientText(box, message, 16,.07f,.32f,.86f,.47f,HomeText);
            status.enableWordWrapping = true;
            bool busy = false;
            ClientButton(box, F("Cancel", "取消"), .07f,.07f,.40f,.14f, CloseOrganizationDialog);
            Button confirm = null;
            confirm = ClientButton(box,
                lockAccount ? F("Lock now", "立即锁定") : F("Unlock", "解锁"),
                .53f,.07f,.40f,.14f, () =>
                {
                    if (busy || dialog != _clientDialog) return;
                    busy = true; confirm.interactable = false;
                    BackendClient.Instance.SetManagedOrganizationTherapistLocked(
                        workspace.id, member.id, lockAccount, () =>
                        {
                            if (dialog != _clientDialog) return;
                            CloseOrganizationDialog(); RefreshOrganizationPage();
                        }, error =>
                        {
                            if (dialog == _clientDialog)
                            { busy = false; confirm.interactable = true; status.text = error; }
                        });
                }, true);
        }

        private void RevokeOrganizationInvitation(BackendClient.OrganizationWorkspace workspace,
            BackendClient.OrganizationInvitation invitation)
        {
            _organizationPageStatus.text = F("Revoking invitation…", "正在撤销邀请…");
            BackendClient.Instance.RevokeOrganizationInvitation(workspace.id, invitation.id,
                RefreshOrganizationPage, error => { if (_organizationPageStatus != null) _organizationPageStatus.text = error; });
        }

        private void CloseOrganizationDialog()
        {
            if (_clientDialog == null) return;
            var dialog = _clientDialog;
            _clientDialog = null;
            dialog.SetActive(false);
            if (Application.isPlaying) Destroy(dialog); else DestroyImmediate(dialog);
        }

        private void OpenOrganizationWorkspace()
        {
            var backend = BackendClient.Instance;
            if (!backend.IsLoggedIn) { OpenLoginScreen(OpenOrganizationWorkspace); return; }

            if (backend.UserType == "organization" && _organizationPage != null)
            {
                CloseHomeSettingsSheet();
                ShowHomeSection("organization");
                return;
            }

            var box = ClientDialog(F("Organization workspace", "机构工作区"), 680, 500);
            var dialog = _clientDialog;
            int account = backend.UserId;
            string token = backend.AccessToken;
            int epoch = LocalAccountStorage.Epoch;
            bool Current() => this != null && dialog != null && _clientDialog == dialog &&
                backend.IsLoggedIn && backend.UserId == account && backend.AccessToken == token &&
                LocalAccountStorage.Epoch == epoch;
            var status = ClientText(box, "", 13, .06f, .12f, .88f, .12f, HomeMuted);
            status.enableWordWrapping = true;

            if (backend.IsTherapistAccount)
            {
                status.text = F("Loading organization workspace…", "正在加载机构工作区…");
                backend.FetchOrganizationWorkspaces(workspaces =>
                {
                    if (!Current()) return;
                    BackendClient.OrganizationWorkspace membership = null;
                    foreach (var workspace in workspaces)
                        if (workspace != null && workspace.role == "therapist") { membership = workspace; break; }
                    if (membership == null && !backend.IsOrganizationTherapist)
                        BuildOrganizationInviteAcceptance(box, status, Current);
                    else if (membership == null)
                        status.text = F("This managed therapist account is not attached to an active organization.",
                            "此托管治疗师账户未关联到有效机构。");
                    else ShowOrganizationMembership(box, membership, status);
                }, error => { if (Current()) status.text = error; });
                return;
            }

            status.text = F(
                "Organization invitations are for therapist accounts. Change this account to Therapist before accepting an invitation.",
                "机构邀请仅适用于治疗师账户。请先将此账户切换为治疗师，再接受邀请。");
        }

        private void BuildOrganizationInviteAcceptance(Transform box, TMP_Text status, System.Func<bool> current)
        {
            ClientText(box, F("Join an organization", "加入机构"), 21, .06f, .80f, .88f, .07f, HomeText);
            ClientText(box, F(
                "Organization invitations sent to this Therapist account appear here.",
                "发送到此治疗师账户的机构邀请会显示在这里。"),
                13, .06f, .65f, .88f, .11f, HomeMuted).enableWordWrapping = true;
            var list = ClientScroll(box, "OrganizationInvitations", .06f,.23f,.88f,.38f);
            status.text = F("Loading invitations…", "正在加载邀请…");
            BackendClient.Instance.FetchIncomingOrganizationInvitations(invitations =>
            {
                if (!current()) return;
                ClearClientChildren(list);
                if (invitations == null || invitations.Length == 0)
                {
                    status.text = F("No pending organization invitations.", "暂无待处理的机构邀请。");
                    return;
                }
                status.text = F("Accepting membership does not share your existing local clients or boards.",
                    "接受成员资格不会共享您现有的本地来访者或沙盘。");
                foreach (var invitation in invitations)
                {
                    if (invitation == null || invitation.is_expired) continue;
                    var row = ClientRow(list, "OrganizationInvitation", 104);
                    ClientText(row, invitation.organization_name ?? "", 17,.03f,.56f,.60f,.30f,HomeText);
                    ClientText(row, F("Invited by ", "邀请人：") + (invitation.invited_by_name ?? ""),
                        12,.03f,.22f,.58f,.25f,HomeMuted);
                    bool busy = false;
                    Button accept = null, decline = null;
                    void Respond(bool join)
                    {
                        if (busy || !current()) return;
                        busy = true; accept.interactable = decline.interactable = false;
                        status.text = join ? F("Joining organization…", "正在加入机构…") : F("Declining invitation…", "正在拒绝邀请…");
                        BackendClient.Instance.RespondOrganizationInvitation(invitation.id, join, workspace =>
                        {
                            if (!current()) return;
                            if (join)
                            {
                                status.text = F("You joined " + workspace.name + ". Existing local clients and boards remain private.",
                                    "您已加入 " + workspace.name + "。现有本地来访者和沙盘仍保持私密。");
                                foreach (Transform child in list)
                                    foreach (var button in child.GetComponentsInChildren<Button>()) button.interactable = false;
                            }
                            else
                            {
                                Destroy(row.gameObject);
                                status.text = F("Invitation declined.", "已拒绝邀请。");
                            }
                        }, error =>
                        {
                            if (!current()) return;
                            busy = false; accept.interactable = decline.interactable = true; status.text = error;
                        });
                    }
                    accept = ClientButton(row, F("Accept", "接受"), .63f,.50f,.16f,.34f, () => Respond(true), true);
                    decline = ClientButton(row, F("Decline", "拒绝"), .81f,.50f,.16f,.34f, () => Respond(false));
                }
            }, error => { if (current()) status.text = error; });
        }

        private void ShowOrganizationMembership(Transform box, BackendClient.OrganizationWorkspace workspace, TMP_Text status)
        {
            ClientText(box, workspace.name, 22, .06f, .80f, .88f, .07f, HomeText);
            ClientText(box, F("Organization membership", "机构成员资格"), 15, .06f, .65f, .88f, .06f, HomeMuted);
            status.text = BackendClient.Instance.IsOrganizationTherapist
                ? F("This therapist account is issued and controlled by the organization. Its access and permissions are managed by the organization owner.",
                    "此治疗师账户由机构签发和控制，其访问权限由机构所有者管理。")
                : F("You are an independent therapist who joined this organization. Your freelance account, existing local clients, and boards remain independent.",
                    "您是加入该机构的独立治疗师。您的自由执业账户、现有本地来访者和沙盘仍保持独立。");
        }
    }
}
