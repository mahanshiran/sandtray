using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Sandplay.UI;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        TMP_Text _hostingWalletLabel;
        float _hostingWalletNext;
        int _hostingWalletUser=-1;
        void UpdateHostingWallet()
        {
            if(_hostingWalletLabel==null||!_hostingWalletLabel.gameObject.activeInHierarchy)return;
            var backend=BackendClient.Instance;
            int user=backend.IsLoggedIn?backend.UserId:0;
            if(user!=_hostingWalletUser){_hostingWalletUser=user;_hostingWalletNext=0;_hostingWalletLabel.text=F("Hosting minutes","主持分钟数");}
            if(user==0||Time.unscaledTime<_hostingWalletNext)return;
            _hostingWalletNext=Time.unscaledTime+30;
            var label=_hostingWalletLabel;
            backend.FetchHostingUsage(0,wallet=>
            {
                if(label==null||label!=_hostingWalletLabel)return;
                label.text=wallet.mode=="off"?F("Hosting usage","主持用量"):wallet.monthly_unlimited?F("Hosting · unlimited","主持 · 无限制"):F("Hosting · ","主持 · ")+AccessDuration(wallet.remaining_seconds)+F(" left"," 剩余");
            },error=>{if(label!=null&&label==_hostingWalletLabel)label.text=F("Hosting usage · refresh","主持用量 · 刷新");});
        }
        void ShowHostingUsage(int offset=0,string scope=null)
        {
            if(!BackendClient.Instance.IsLoggedIn){OpenLoginScreen(()=>ShowHostingUsage());return;}
            var box=ClientDialog("",880,780);var dialog=_clientDialog;
            var status=ClientText(box,"",13,.05f,.86f,.78f,.035f,HomeMuted);
            // Keep the usage content close to the card header. Scope tabs, when present,
            // move the viewport down in the callback below.
            var list=ClientScroll(box,"HostingUsage",.04f,.06f,.92f,.86f);
            BackendClient.Instance.FetchHostingUsage(offset,scope,wallet=>
            {
                if(dialog==null||dialog!=_clientDialog)return;
                var scopes=wallet.available_scopes??Array.Empty<string>();
                if(scopes.Length>1)
                {
                    ((RectTransform)list.parent).anchorMax=new Vector2(.96f,.78f);
                    float width=.43f;
                    for(int i=0;i<scopes.Length;i++)
                    {
                        string tabScope=scopes[i];
                        string title=tabScope=="organization"?F("Organization","机构"):F("Personal","个人");
                        ClientButton(box,title,.05f+i*.45f,.79f,width,.07f,
                            ()=>ShowHostingUsage(0,tabScope),tabScope==wallet.scope);
                    }
                }
                else
                {
                    // With no workspace switcher, reclaim the unused title band.
                    ((RectTransform)list.parent).anchorMax=new Vector2(.96f,.84f);
                }
                // Hosting has its own detailed ledger. The access snapshot adds every
                // other measurable plan allowance to the same usage screen.
                BackendClient.Instance.FetchAccessSnapshot(
                    snapshot =>
                    {
                        if(dialog==null||dialog!=_clientDialog)return;
                        RenderHostingWallet(box,list,status,wallet,offset,wallet.scope,snapshot);
                    },
                    _ =>
                    {
                        if(dialog==null||dialog!=_clientDialog)return;
                        RenderHostingWallet(box,list,status,wallet,offset,wallet.scope,null);
                    });
            },error=>{if(dialog!=null&&dialog==_clientDialog)status.text=error;});
        }
        void RenderHostingWallet(Transform box,Transform list,TMP_Text status,HostingWallet wallet,int offset,string scope,AccessSnapshot access)
        {
                string workspace=scope=="organization"&&!string.IsNullOrEmpty(wallet.workspace_name)?wallet.workspace_name+" · ":"";
                status.text=wallet.mode=="off"?F("Usage tracking is not active yet.","用量统计尚未启用。"):"";
                var orange=new Color(.92f,.55f,.18f);
                if(wallet.mode!="off")
                {
                    var totals=ClientRow(list,"Usage summary",92);
                    void Metric(int index,string title,string value)
                    {
                        float x=.02f+index*.245f;
                        ClientText(totals,title,12,x,.65f,.23f,.25f,HomeMuted);
                        var label=ClientText(totals,value,21,x,.10f,.23f,.48f,HomeText);
                        label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=21;
                    }
                    Metric(0,F("Online used","线上已用"),AccessDuration(wallet.used_seconds));
                    Metric(1,F("Offline used","线下已用"),AccessDuration(wallet.offline_used_seconds));
                    Metric(2,F("Online remaining","线上剩余"),wallet.monthly_unlimited?F("Unlimited","无限制"):AccessDuration(wallet.remaining_seconds));
                    Metric(3,F("Online allowance","线上额度"),wallet.monthly_unlimited?F("Unlimited","无限制"):AccessDuration(wallet.monthly_limit_seconds));
                    var meter=ClientRow(list,"Allowance",112);
                    float fraction=wallet.monthly_limit_seconds>0?(float)wallet.used_seconds/wallet.monthly_limit_seconds:0;
                    string reset=DateTimeOffset.TryParse(wallet.reset_at,out var resetDate)?resetDate.UtcDateTime.ToString("MMM d, yyyy"):wallet.reset_at;
                    ClientText(meter,F("Online","线上")+" · "+(wallet.monthly_unlimited?F("Unlimited","无限制"):AccessDuration(wallet.used_seconds)+" / "+AccessDuration(wallet.monthly_limit_seconds)),12,.02f,.72f,.95f,.20f,HomePrimary);
                    var onlineTrack=ClientRect(meter,"Online allowance",.025f,.59f,.95f,.08f).gameObject.AddComponent<Image>();onlineTrack.color=HomeCardBorder;ApplyHomeRoundedCorners(onlineTrack,5);
                    if(!wallet.monthly_unlimited && fraction>0)
                    {var fill=ClientRect(onlineTrack.transform,"Online used",0,0,Mathf.Clamp01(fraction),1).gameObject.AddComponent<Image>();fill.color=HomePrimary;ApplyHomeRoundedCorners(fill,5);}
                    ClientText(meter,F("Offline","线下")+" · "+AccessDuration(wallet.offline_used_seconds)+" / "+(wallet.offline_limit_seconds>0?AccessDuration(wallet.offline_limit_seconds):F("Unlimited","无限制")),12,.02f,.36f,.95f,.20f,orange);
                    var offlineTrack=ClientRect(meter,"Offline allowance",.025f,.23f,.95f,.08f).gameObject.AddComponent<Image>();offlineTrack.color=HomeCardBorder;ApplyHomeRoundedCorners(offlineTrack,5);
                    if(wallet.offline_used_seconds>0 && wallet.offline_limit_seconds>0)
                    {var fill=ClientRect(offlineTrack.transform,"Offline used",0,0,Mathf.Clamp01((float)wallet.offline_used_seconds/wallet.offline_limit_seconds),1).gameObject.AddComponent<Image>();fill.color=orange;ApplyHomeRoundedCorners(fill,5);}
                    ClientText(meter,F("Resets ","重置于 ")+reset+" UTC",10,.65f,.88f,.32f,.10f,HomeMuted);
                    if(wallet.reserved_seconds>0)
                    {var live=ClientRow(list,"Active reservation",38);ClientText(live,F("Reserved for active session: ","当前会话预留：")+AccessDuration(wallet.reserved_seconds),12,.02f,.1f,.96f,.8f,HomeMuted);}
                    var chartRow=ClientRow(list,"Monthly usage trend",255);
                    ClientText(chartRow,F("Usage over time","用量趋势"),17,.025f,.84f,.5f,.13f,HomeText);
                    var detail=ClientText(chartRow,"",13,.025f,.70f,.95f,.12f,HomeMuted);
                    ClientText(chartRow,F("● Online    ","● 线上    ")+F("● Offline","● 线下"),12,.32f,.84f,.24f,.13f,HomeMuted);
                    var graph=ClientRect(chartRow,"Recorded usage",.10f,.18f,.85f,.48f).gameObject.AddComponent<UsageLineChart>();graph.color=HomePrimary;graph.SecondaryColor=orange;
                    var maxLabel=ClientText(chartRow,"",10,.005f,.60f,.09f,.07f,HomeMuted);
                    ClientText(chartRow,"0",10,.005f,.15f,.09f,.07f,HomeMuted);
                    var firstLabel=ClientText(chartRow,"",11,.10f,.04f,.40f,.10f,HomeMuted);
                    var lastLabel=ClientText(chartRow,"",11,.56f,.04f,.39f,.10f,HomeMuted);lastLabel.alignment=TextAlignmentOptions.MidlineRight;
                    DateTime now=DateTimeOffset.TryParse(wallet.server_time,out var server)?server.UtcDateTime:DateTime.UtcNow;
                    var month=new DateTime(now.Year,now.Month,1);
                    void Trend(int count)
                    {
                        var dates=Enumerable.Range(0,count).Select(i=>month.AddMonths(i-count+1)).ToArray();
                        var values=dates.Select(d=>d==month?wallet.used_seconds:(wallet.months??Array.Empty<HostingMonthUsage>()).Where(m=>m.month==d.ToString("yyyy-MM-dd")).Select(m=>m.used_seconds).FirstOrDefault()).ToArray();
                        var offlineValues=dates.Select(d=>d==month?wallet.offline_used_seconds:(wallet.offline_months??Array.Empty<HostingMonthUsage>()).Where(m=>m.month==d.ToString("yyyy-MM-dd")).Select(m=>m.used_seconds).FirstOrDefault()).ToArray();
                        var minutes=values.Select(v=>v/60f).ToArray();var offlineMinutes=offlineValues.Select(v=>v/60f).ToArray();float maximum=Mathf.Max(1,Mathf.Ceil(Mathf.Max(minutes.Max(),offlineMinutes.Max())/5)*5);
                        graph.Refresh(minutes,offlineMinutes,maximum);maxLabel.text=maximum.ToString("0")+F("m","分");
                        firstLabel.text=dates[0].ToString("MMM yyyy");lastLabel.text=month.ToString("MMM yyyy");
                        graph.OnSelect=i=>detail.text=dates[i].ToString("MMM yyyy")+" · "+F("Online ","线上 ")+AccessDuration(values[i])+" · "+F("Offline ","线下 ")+AccessDuration(offlineValues[i])+(i==count-1?F(" · so far"," · 截至目前"):"");
                        graph.OnSelect(count-1);
                    }
                    ClientButton(chartRow,F("6 months","6 个月"),.57f,.85f,.18f,.12f,()=>Trend(6));
                    ClientButton(chartRow,F("12 months","12 个月"),.77f,.85f,.20f,.12f,()=>Trend(12));
                    Trend(6);
                    if(wallet.used_seconds==0 && wallet.offline_used_seconds==0 && !(wallet.months??Array.Empty<HostingMonthUsage>()).Any(m=>m.used_seconds>0) && !(wallet.offline_months??Array.Empty<HostingMonthUsage>()).Any(m=>m.used_seconds>0))
                        detail.text=F("No recorded usage yet","暂无用量记录");
                    var limitRow=ClientRow(list,"Session limit",36);
                    ClientText(limitRow,F("Per-session limit: ","每次会话上限：")+(wallet.session_unlimited?F("None","无"):AccessDuration(wallet.session_limit_seconds)),12,.02f,.1f,.96f,.8f,HomeMuted);
                }
                ClientText(ClientRow(list,"History heading",44),F("Session history","会话记录"),16,.02f,.05f,.96f,.9f,HomeText);
                if(wallet.sessions==null || wallet.sessions.Length==0)
                    ClientText(ClientRow(list,"No sessions",44),F("No sessions recorded.","暂无会话记录。"),13,.02f,.05f,.96f,.9f,HomeMuted);
                foreach(var session in wallet.sessions??Array.Empty<HostingSessionUsage>())
                {
                    string date=DateTimeOffset.TryParse(session.started_at,out var start)?start.ToLocalTime().ToString("MMM d · HH:mm"):session.started_at;
                    var row=ClientRow(list,"Session",46);
                    ClientText(row,date,13,.025f,.10f,.46f,.80f,HomeText);
                    ClientText(row,session.status=="active"?F("Active","进行中"):F("Ended","已结束"),11,.49f,.10f,.18f,.80f,session.status=="active"?HomePrimary:HomeMuted);
                    var duration=ClientText(row,AccessDuration(session.used_seconds),13,.68f,.10f,.29f,.80f,HomeText);duration.alignment=TextAlignmentOptions.MidlineRight;
                }
                RenderPlanUsageDetails(list,wallet,access,orange);
                if(offset>0)ClientButton(box,"‹",.05f,.025f,.2f,.065f,()=>ShowHostingUsage(Math.Max(0,offset-50),scope));
                if(wallet.has_more)ClientButton(box,"›",.29f,.025f,.2f,.065f,()=>ShowHostingUsage(offset+50,scope));
        }

        static readonly string[] PlanUsageOrder =
        {
            "tables.capacity", "reports.capacity", "clients.capacity", "catalog.custom.capacity",
            "ai.analyze", "pdf.export", "sessions.host_minutes", "sessions.offline_minutes",
            "sessions.session_minutes", "sessions.participants", "cloud.records.capacity", "cloud.storage_bytes"
        };

        internal static AccessCapability[] PlanUsageCapabilities(AccessSnapshot snapshot)
        {
            var measurable=(snapshot?.capabilities??Array.Empty<AccessCapability>())
                .Where(item=>item!=null && item.kind!="boolean")
                .ToDictionary(item=>item.key,item=>item);
            return PlanUsageOrder.Where(measurable.ContainsKey).Select(key=>measurable[key])
                .Concat(measurable.Values.Where(item=>!PlanUsageOrder.Contains(item.key)).OrderBy(item=>item.key))
                .ToArray();
        }

        internal static string PlanUsageValue(AccessCapability item,HostingWallet wallet)
        {
            if(item==null)return Localization.Get("access.unknown");
            if(item.key=="sessions.host_minutes" && wallet!=null)
                return AccessDuration(wallet.used_seconds)+" / "+(wallet.monthly_unlimited
                    ? Localization.Get("access.unlimited") : AccessDuration(wallet.monthly_limit_seconds));
            if(item.key=="sessions.offline_minutes" && wallet!=null)
                return AccessDuration(wallet.offline_used_seconds)+" / "+(wallet.offline_limit_seconds>0
                    ? AccessDuration(wallet.offline_limit_seconds) : Localization.Get("access.unlimited"));
            string limit=item.unlimited?Localization.Get("access.unlimited"):
                item.unit=="bytes"?AccessBytes(item.limit):item.limit.ToString(Localization.Culture);
            if(!item.entitled && !item.unlimited && item.limit==0)
                return Localization.Text("Not included","不包含");
            if(item.key=="sessions.session_minutes" || item.key=="sessions.participants")
                return Localization.Text("Limit ","上限 ")+limit;
            if(!item.usage_ready)return Localization.Get("access.unknown")+" / "+limit;
            string used=item.usage_unit=="seconds"?AccessDuration(item.used):
                item.unit=="bytes"?AccessBytes(item.used):item.used.ToString(Localization.Culture);
            return used+" / "+limit;
        }

        void RenderPlanUsageDetails(Transform list,HostingWallet wallet,AccessSnapshot access,Color offlineColor)
        {
            ClientText(ClientRow(list,"Usage details heading",44),F("Usage details","用量详情"),16,.025f,.05f,.95f,.90f,HomeText);
            var capabilities=PlanUsageCapabilities(access);
            if(capabilities.Length==0)
            {
                capabilities=new[]
                {
                    new AccessCapability{key="sessions.host_minutes",kind="monthly",entitled=true},
                    new AccessCapability{key="sessions.offline_minutes",kind="monthly",entitled=true}
                };
            }
            foreach(var item in capabilities)
            {
                var row=ClientRow(list,"Usage detail "+item.key,42);
                string label=item.key=="sessions.host_minutes"?F("Online hosting","线上主持"):
                    item.key=="sessions.offline_minutes"?F("Offline hosting","线下主持"):
                    Localization.Get("access."+item.key);
                ClientText(row,label,12,.025f,.08f,.49f,.84f,HomeMuted);
                var color=item.key=="sessions.offline_minutes"?offlineColor:HomePrimary;
                var value=ClientText(row,PlanUsageValue(item,wallet),12,.52f,.08f,.455f,.84f,color);
                value.alignment=TextAlignmentOptions.MidlineRight;
            }
        }
    }
}
