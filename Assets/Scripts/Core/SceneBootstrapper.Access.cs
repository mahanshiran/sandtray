using System;
using TMPro;
using UnityEngine;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private Transform _accessCard, _accessList;
        private TMP_Text _accessStatus;
        private int _accessUser, _accessRequest;
        private string _accessToken;
        private double _accessFreshUntil;

        private void ShowAccessUsage()
        {
            _accessCard = ClientDialog(Localization.Get("access.title"), 760, 740);
            _accessStatus = ClientText(_accessCard, "", 16, .05f,.72f,.90f,.13f, HomeMuted);
            _accessStatus.richText = false;
            _accessList = ClientScroll(_accessCard, "AccessList", .04f,.12f,.92f,.58f);
            ClientButton(_accessCard, Localization.Get("access.refresh"), .65f,.025f,.30f,.065f, RefreshAccessUsage);
            ClientButton(_accessCard, Localization.Current == Language.Chinese ? "本地保存恢复" : "Local save recovery", .05f,.025f,.55f,.065f, ShowCapacityRecovery);
            RefreshAccessUsage();
        }

        private void RefreshAccessUsage()
        {
            var client = BackendClient.Instance;
            _accessUser = client.UserId; _accessToken = client.AccessToken;
            int request = ++_accessRequest;
            var card = _accessCard;
            _accessFreshUntil = 0;
            ClearClientChildren(_accessList);
            _accessStatus.text = Localization.Get(client.IsLoggedIn ? "access.loading" : "access.signin");
            if (!client.IsLoggedIn) return;
            bool Current() => card != null && card == _accessCard && card.gameObject.activeInHierarchy &&
                request == _accessRequest && client.UserId == _accessUser && client.AccessToken == _accessToken;
            client.FetchAccessSnapshot(snapshot =>
            {
                if (!Current()) return;
                _accessStatus.text = Localization.Get(!snapshot.active ? "access.inactive" :
                    snapshot.enforcement == "partial" ? "access.partial" : "access.preview");
                _accessFreshUntil = Time.realtimeSinceStartupAsDouble + Math.Max(0,
                    (DateTimeOffset.Parse(snapshot.valid_until) - DateTimeOffset.Parse(snapshot.server_time)).TotalSeconds);
                if (snapshot.sources == null || snapshot.sources.Length == 0)
                    AccessRow(Localization.Get("access.baseline"), "", 62);
                else foreach (var source in snapshot.sources)
                {
                    string origin = source.source == "manual" ? Localization.Get("access.manual") :
                        source.source == "promotion" ? Localization.Get("access.promotion") : source.source;
                    AccessRow(Localization.Get("access.source", source.plan, origin, AccessDate(source.ends_at)), "", 90);
                }
                foreach (var credit in snapshot.credits ?? new AccessCredit[0])
                    AccessRow(Localization.Get("access.credit_source", Localization.Get("access." + credit.key)),
                        Localization.Get("access.credit_detail", credit.remaining, credit.reserved, credit.used, credit.quantity,
                            AccessDate(credit.ends_at), Localization.Get(credit.restricted ? "access.credit_restricted" : credit.active ? "access.credit_active" : "access.credit_inactive")), 170);
                AccessRow(Localization.Text("Board quota is checked in the background. Local boards remain editable when capacity is full or the service is offline. Cloud backup has separate limits.",
                    "沙盘额度在后台检查。额度已满或服务离线时，仍可编辑本地沙盘。云端备份使用独立额度。"), "", 116);
                foreach (var capability in snapshot.capabilities)
                    AccessRow(Localization.Get("access." + capability.key), AccessValue(capability), capability.usage_unit == "seconds" || capability.credit_remaining > 0 ? 170 : 116);
            }, error =>
            {
                if (Current()) _accessStatus.text = Localization.Get("access.error");
            });
        }

        private void AccessRow(string label, string value, float height)
        {
            var row = ClientRow(_accessList, "AccessRow", height);
            var title = ClientText(row, label, 17, .03f,.08f, string.IsNullOrEmpty(value) ? .94f : .40f,.84f, HomeText);
            title.richText = false; title.enableWordWrapping = true;
            if (string.IsNullOrEmpty(value)) return;
            var detail = ClientText(row, value, 15, .46f,.08f,.51f,.84f, HomeMuted);
            detail.richText = false; detail.enableWordWrapping = true;
        }

        internal static string AccessDate(string value) => DateTimeOffset.TryParse(value, out var date)
            ? date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'") : "—";

        internal static string AccessDuration(long seconds)
        {
            seconds = Math.Max(0, seconds);
            return Localization.Get("access.duration", seconds / 3600, (seconds % 3600) / 60, seconds % 60);
        }

        internal static string AccessBytes(long bytes)
        {
            string[] units={"B","KiB","MiB","GiB","TiB","PiB"};
            double value=Math.Max(0,bytes); int index=0;
            while(value>=1024 && index<units.Length-1) {value/=1024;index++;}
            return value.ToString("0.##",Localization.Culture)+" "+units[index];
        }

        internal static string AccessValue(AccessCapability item)
        {
            if (item.kind == "boolean") return Localization.Get(item.entitled ? "access.yes" : "access.no");
            string allowance = item.unlimited ? Localization.Get("access.unlimited") : item.limit.ToString(Localization.Culture);
            if (item.unit == "bytes" && !item.unlimited) allowance = AccessBytes(item.limit);
            if (item.key == "sessions.host_minutes" && !item.unlimited) allowance = AccessDuration(item.limit * 60);
            if (item.kind == "monthly") allowance = Localization.Get("access.month", allowance);
            if (item.credit_remaining > 0) allowance += "\n" + Localization.Get("access.extra_credits", item.credit_remaining);
            if (!item.usage_ready) return allowance + "\n" + Localization.Get("access.unknown");
            string remaining = item.unlimited ? Localization.Get("access.unlimited") : item.remaining.ToString(Localization.Culture);
            string result = allowance + "\n" + (item.usage_unit == "seconds"
                ? Localization.Get("access.host_balance", AccessDuration(item.used), AccessDuration(item.reserved),
                    item.unlimited ? Localization.Get("access.unlimited") : AccessDuration(item.remaining))
                : item.unit == "bytes" ? Localization.Get("access.balance",AccessBytes(item.used),AccessBytes(item.reserved),item.unlimited ? Localization.Get("access.unlimited") : AccessBytes(item.remaining))
                : Localization.Get("access.balance", item.used, item.reserved, remaining));
            if (!string.IsNullOrEmpty(item.reset_at)) result += "\n" + Localization.Get("access.reset", AccessDate(item.reset_at));
            return result;
        }

        private void UpdateAccessUsage()
        {
            if (_accessCard == null || !_accessCard.gameObject.activeInHierarchy) return;
            var client = BackendClient.Instance;
            if (client.UserId != _accessUser || client.AccessToken != _accessToken)
            {
                ++_accessRequest;
                _accessCard = null;
                CloseClientDialog();
                return;
            }
            if (_accessFreshUntil > 0 && Time.realtimeSinceStartupAsDouble >= _accessFreshUntil)
            {
                _accessFreshUntil = 0;
                ClearClientChildren(_accessList);
                _accessStatus.text = Localization.Get("access.stale");
            }
        }
    }
}
