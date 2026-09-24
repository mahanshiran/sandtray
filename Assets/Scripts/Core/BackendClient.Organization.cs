using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Sandplay.Core
{
    public partial class BackendClient
    {
        [Serializable] public class OrganizationWorkspace
        {
            public string id;
            public string name;
            public string logo_url;
            public string accent_color;
            public string login_domain;
            public string status;
            public string role;
            public string membership_status;
            public string membership_id;
            public int? active_client_limit;
            public int? hosting_seconds_limit;
            public bool can_create_clients;
            public bool can_create_schedules;
            public bool can_host_sessions;
            public bool can_create_reports;
            public int? client_limit;
        }

        [Serializable] public class OrganizationWorkspaceResponse
        {
            public OrganizationWorkspace[] organizations;
        }

        [Serializable] public class OrganizationInvitation
        {
            public string id;
            public string email;
            public string status;
            public string expires_at;
            public string accepted_at;
            public string created_at;
            public string token;
            public bool is_expired;
            public int therapist_id;
            public string therapist_name;
        }

        [Serializable] public class IncomingOrganizationInvitation
        {
            public string id;
            public string organization_id;
            public string organization_name;
            public string invited_by_name;
            public string status;
            public string expires_at;
            public string created_at;
            public bool is_expired;
        }

        [Serializable] private class IncomingOrganizationInvitationResponse
        {
            public IncomingOrganizationInvitation[] invitations;
        }

        [Serializable] public class OrganizationMember
        {
            public string id;
            public int therapist_id;
            public string therapist_name;
            public string therapist_email;
            public string status;
            public int? client_limit;
            public int? hosting_seconds_limit;
            public int active_client_count;
            public bool managed_account;
            public bool is_locked;
            public string locked_at;
            public bool must_change_password;
            public bool can_create_clients;
            public bool can_create_schedules;
            public bool can_host_sessions;
            public bool can_create_reports;
            public bool can_invite_clients;
            public bool allow_external_contacts;
            public string joined_at;
            public string ended_at;
        }

        [Serializable] private class OrganizationMemberPage
        {
            public OrganizationMember[] results;
        }

        [Serializable] public class OrganizationUsageMember
        {
            public string membership_id;
            public string therapist_name;
            public string therapist_email;
            public bool managed_account;
            public int? allocation_seconds;
            public int used_seconds;
            public int reserved_seconds;
            public int? remaining_seconds;
        }

        [Serializable] public class OrganizationUsageDay
        {
            public string date;
            public int used_seconds;
        }

        [Serializable] public class OrganizationUsageSession
        {
            public string id;
            public string started_at;
            public string ended_at;
            public string status;
            public int used_seconds;
        }

        [Serializable] public class OrganizationUsageMemberDetail : OrganizationUsageMember
        {
            public OrganizationUsageDay[] daily;
            public OrganizationUsageSession[] sessions;
            public bool has_more;
        }

        [Serializable] public class OrganizationUsage
        {
            public int allocation_revision;
            public bool is_current;
            public string period_start;
            public string period_end;
            public string usage_unit;
            public int? limit_seconds;
            public int allocated_seconds;
            public int? unallocated_seconds;
            public int used_seconds;
            public int reserved_seconds;
            public int? remaining_seconds;
            public OrganizationUsageDay[] daily;
            public OrganizationUsageMember[] members;
            public OrganizationUsageMemberDetail selected_member;
        }

        [Serializable] public class OrganizationActivity
        {
            public string id;
            public string action;
            public string category;
            public string created_at;
            public string actor_name;
            public string membership_id;
            public string therapist_name;
            public string client_id;
            public string client_name;
            public string target_kind;
            public string target_id;
            public string target_label;
            public OrganizationActivityMetadata metadata;
        }

        [Serializable] public class OrganizationActivityMetadata
        {
            public int duration_seconds;
            public string started_at;
            public string ended_at;
            public string session_type;
            public string usage_scope;
            public string metering_mode;
            public string ended_reason;
        }

        [Serializable] public class OrganizationActivityPage
        {
            public OrganizationActivity[] results;
            public string next_cursor;
        }

        [Serializable] public class OrganizationClient
        {
            public string id;
            public int linked_user_id;
            public string avatar_url;
            public string name;
            public string reference;
            public string email;
            public string phone;
            public string assigned_membership_id;
            public string assigned_therapist_name;
            public bool archived;
            public int revision;
            public string created_at;
            public string updated_at;
        }

        [Serializable] private class OrganizationClientPage
        {
            public OrganizationClient[] results;
        }

        [Serializable] private class OrganizationResponse { public string id; public string name; }
        [Serializable] private class InvitationAcceptResponse { public OrganizationWorkspace workspace; }

        public void FetchOrganizationWorkspaces(Action<OrganizationWorkspace[]> success, Action<string> failure)
        {
            if (!IsLoggedIn) { failure?.Invoke("Please sign in first."); return; }
            StartCoroutine(Get($"{BaseUrl}/auth/organizations/", AccessToken, json =>
            {
                var response = JsonUtility.FromJson<OrganizationWorkspaceResponse>(json);
                success?.Invoke(response?.organizations ?? Array.Empty<OrganizationWorkspace>());
            }, failure));
        }

        public void CreateOrganization(string name, Action<OrganizationWorkspace> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "organization")
            { failure?.Invoke("Sign in with an organization account."); return; }
            var body = "{\"name\":\"" + EscapeForJson(name?.Trim()) + "\"}";
            StartCoroutine(Post($"{BaseUrl}/auth/organizations/", body, AccessToken, json =>
            {
                var response = JsonUtility.FromJson<OrganizationResponse>(json);
                if (response == null || string.IsNullOrEmpty(response.id) || string.IsNullOrWhiteSpace(response.name))
                { failure?.Invoke("Unexpected organization response. Please try again."); return; }
                success?.Invoke(new OrganizationWorkspace { id = response.id, name = response.name, role = "owner", membership_status = "active" });
            }, failure));
        }

        public void InviteOrganizationTherapist(string organizationId, string email,
            Action<OrganizationInvitation> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "organization")
            { failure?.Invoke("Sign in with an organization account."); return; }
            if (string.IsNullOrWhiteSpace(organizationId)) { failure?.Invoke("Organization unavailable."); return; }
            var body = "{\"email\":\"" + EscapeForJson(email?.Trim()) + "\"}";
            StartCoroutine(Post($"{BaseUrl}/auth/organizations/{organizationId}/invitations/", body, AccessToken, json =>
            {
                var response = JsonUtility.FromJson<OrganizationInvitation>(json);
                if (response == null || string.IsNullOrEmpty(response.token))
                { failure?.Invoke("Invitation was not created. Please try again."); return; }
                success?.Invoke(response);
            }, failure));
        }

        public void InviteOrganizationTherapistAccount(string organizationId, string therapistCode,
            Action<OrganizationInvitation> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "organization")
            { failure?.Invoke("Sign in with an organization account."); return; }
            if (string.IsNullOrWhiteSpace(organizationId) || string.IsNullOrWhiteSpace(therapistCode))
            { failure?.Invoke("Choose a therapist account first."); return; }
            var body = "{\"therapist_code\":\"" + EscapeForJson(therapistCode.Trim()) + "\"}";
            StartCoroutine(Post($"{BaseUrl}/auth/organizations/{organizationId}/invitations/", body, AccessToken, json =>
            {
                OrganizationInvitation response = null;
                try { response = JsonConvert.DeserializeObject<OrganizationInvitation>(json); }
                catch (JsonException) { }
                if (response == null || string.IsNullOrEmpty(response.id) || response.therapist_id <= 0)
                { failure?.Invoke("Invitation was not created. Please try again."); return; }
                success?.Invoke(response);
            }, failure));
        }

        public void AcceptOrganizationInvitation(string token, Action<OrganizationWorkspace> success, Action<string> failure)
        {
            if (!IsLoggedIn) { failure?.Invoke("Please sign in first."); return; }
            var body = "{\"token\":\"" + EscapeForJson(token?.Trim()) + "\"}";
            StartCoroutine(Post($"{BaseUrl}/auth/organizations/invitations/accept/", body, AccessToken, json =>
            {
                var response = JsonUtility.FromJson<InvitationAcceptResponse>(json);
                if (response?.workspace == null || string.IsNullOrEmpty(response.workspace.id))
                { failure?.Invoke("Invitation was not accepted. Please try again."); return; }
                success?.Invoke(response.workspace);
            }, failure));
        }

        public void FetchIncomingOrganizationInvitations(
            Action<IncomingOrganizationInvitation[]> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "psychologist")
            { failure?.Invoke("Sign in with a therapist account."); return; }
            StartCoroutine(Get($"{BaseUrl}/auth/organizations/invitations/incoming/", AccessToken, json =>
            {
                var response = JsonUtility.FromJson<IncomingOrganizationInvitationResponse>(json);
                success?.Invoke(response?.invitations ?? Array.Empty<IncomingOrganizationInvitation>());
            }, failure));
        }

        public void RespondOrganizationInvitation(string invitationId, bool accept,
            Action<OrganizationWorkspace> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "psychologist")
            { failure?.Invoke("Sign in with a therapist account."); return; }
            if (string.IsNullOrWhiteSpace(invitationId))
            { failure?.Invoke("Invitation unavailable."); return; }
            var body = new JObject
            {
                ["invitation_id"] = invitationId,
                ["action"] = accept ? "accept" : "decline",
            };
            StartCoroutine(Post($"{BaseUrl}/auth/organizations/invitations/incoming/",
                body.ToString(Formatting.None), AccessToken, json =>
                {
                    if (!accept) { success?.Invoke(null); return; }
                    var response = JsonUtility.FromJson<InvitationAcceptResponse>(json);
                    if (response?.workspace == null || string.IsNullOrEmpty(response.workspace.id))
                    { failure?.Invoke("Invitation was not accepted. Please try again."); return; }
                    success?.Invoke(response.workspace);
                }, failure));
        }

        public void FetchOrganizationMembers(string organizationId,
            Action<OrganizationMember[]> success, Action<string> failure)
        {
            OrganizationOwnerGet($"organizations/{organizationId}/members/", json =>
            {
                try
                {
                    var page = JsonConvert.DeserializeObject<OrganizationMemberPage>(json);
                    success?.Invoke(page?.results ?? Array.Empty<OrganizationMember>());
                }
                catch (JsonException) { failure?.Invoke("Unable to read organization members."); }
            }, failure);
        }

        public void FetchOrganizationUsage(string organizationId, string month,
            Action<OrganizationUsage> success, Action<string> failure)
        {
            string query = string.IsNullOrEmpty(month) ? "" : "?month=" + UnityWebRequest.EscapeURL(month);
            OrganizationOwnerGet($"organizations/{organizationId}/usage/{query}", json =>
            {
                try
                {
                    var usage = JsonConvert.DeserializeObject<OrganizationUsage>(json);
                    if (usage == null) { failure?.Invoke("Unable to read organization usage."); return; }
                    usage.members ??= Array.Empty<OrganizationUsageMember>();
                    usage.daily ??= Array.Empty<OrganizationUsageDay>();
                    success?.Invoke(usage);
                }
                catch (JsonException) { failure?.Invoke("Unable to read organization usage."); }
            }, failure);
        }

        public void FetchOrganizationUsageMember(string organizationId, string month,
            string membershipId, int offset, Action<OrganizationUsage> success, Action<string> failure)
        {
            string query = "?membership_id=" + UnityWebRequest.EscapeURL(membershipId) +
                "&offset=" + Math.Max(0, offset);
            if (!string.IsNullOrEmpty(month))
                query += "&month=" + UnityWebRequest.EscapeURL(month);
            OrganizationOwnerGet($"organizations/{organizationId}/usage/{query}", json =>
            {
                try
                {
                    var usage = JsonConvert.DeserializeObject<OrganizationUsage>(json);
                    if (usage?.selected_member == null)
                    { failure?.Invoke("Unable to read therapist usage."); return; }
                    usage.selected_member.daily ??= Array.Empty<OrganizationUsageDay>();
                    usage.selected_member.sessions ??= Array.Empty<OrganizationUsageSession>();
                    success?.Invoke(usage);
                }
                catch (JsonException) { failure?.Invoke("Unable to read therapist usage."); }
            }, failure);
        }

        public void FetchOrganizationActivity(string organizationId, string membershipId,
            string clientId, string category, string action, string from, string to, string cursor,
            Action<OrganizationActivityPage> success, Action<string> failure)
        {
            var query = new StringBuilder();
            void Add(string key, string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                query.Append(query.Length == 0 ? '?' : '&').Append(key).Append('=')
                    .Append(UnityWebRequest.EscapeURL(value));
            }
            Add("membership_id", membershipId); Add("client_id", clientId);
            Add("category", category); Add("action", action); Add("from", from);
            Add("to", to); Add("cursor", cursor);
            OrganizationOwnerGet($"organizations/{organizationId}/activity/{query}", json =>
            {
                try
                {
                    var page = JsonConvert.DeserializeObject<OrganizationActivityPage>(json);
                    if (page == null) { failure?.Invoke("Unable to read organization activity."); return; }
                    page.results ??= Array.Empty<OrganizationActivity>();
                    success?.Invoke(page);
                }
                catch (JsonException) { failure?.Invoke("Unable to read organization activity."); }
            }, failure);
        }

        public void RecordOrganizationBoardCreated(string organizationId,
            string organizationClientId, string boardId, string title)
        {
            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(organizationClientId) ||
                string.IsNullOrEmpty(boardId)) return;
            var body = new JObject
            {
                ["organization_client_id"] = organizationClientId,
                ["board_id"] = boardId,
                ["title"] = (title ?? "").Trim(),
            };
            StartCoroutine(OrganizationWorkspaceRequest("POST",
                $"organizations/{organizationId}/activity/boards/",
                body.ToString(Formatting.None), _ => { },
                error => Debug.LogWarning("[Organization] Board activity was not synced: " + error)));
        }

        public void FetchOrganizationInvitations(string organizationId,
            Action<OrganizationInvitation[]> success, Action<string> failure)
        {
            OrganizationOwnerGet($"organizations/{organizationId}/invitations/", json =>
            {
                try { success?.Invoke(JsonConvert.DeserializeObject<OrganizationInvitation[]>(json) ?? Array.Empty<OrganizationInvitation>()); }
                catch (JsonException) { failure?.Invoke("Unable to read organization invitations."); }
            }, failure);
        }

        public void FetchOrganizationClients(string organizationId, string assignedMembershipId,
            bool archived, Action<OrganizationClient[]> success, Action<string> failure)
        {
            string assignment = string.IsNullOrEmpty(assignedMembershipId)
                ? "" : "&assigned_membership_id=" + UnityWebRequest.EscapeURL(assignedMembershipId);
            OrganizationWorkspaceGet($"organizations/{organizationId}/clients/?archived={(archived ? "true" : "false")}{assignment}", json =>
            {
                try
                {
                    var page = JsonConvert.DeserializeObject<OrganizationClientPage>(json);
                    success?.Invoke(page?.results ?? Array.Empty<OrganizationClient>());
                }
                catch (JsonException) { failure?.Invoke("Unable to read organization clients."); }
            }, failure);
        }

        public void FetchAllOrganizationClients(string organizationId,
            Action<OrganizationClient[]> success, Action<string> failure)
        {
            OrganizationWorkspaceGet($"organizations/{organizationId}/clients/?archived=all", json =>
            {
                try
                {
                    var page = JsonConvert.DeserializeObject<OrganizationClientPage>(json);
                    success?.Invoke(page?.results ?? Array.Empty<OrganizationClient>());
                }
                catch (JsonException) { failure?.Invoke("Unable to read organization clients."); }
            }, failure);
        }

        public void InviteOrganizationClientToSession(string organizationId,
            string organizationClientId, string roomCode, Action success, Action<string> failure)
        {
            var body = new JObject
            {
                ["nonce"] = Guid.NewGuid().ToString(),
                ["room"] = (roomCode ?? "").Trim().ToUpperInvariant(),
            };
            StartCoroutine(OrganizationWorkspaceRequest("POST",
                $"organizations/{organizationId}/clients/{organizationClientId}/session-invitations/",
                body.ToString(Formatting.None), _ => success?.Invoke(), failure));
        }

        public void CreateOrganizationClient(string organizationId, string clientCode, string reference,
            string assignedMembershipId, Action<OrganizationClient> success, Action<string> failure)
        {
            var body = new JObject
            {
                ["client_code"] = (clientCode ?? "").Trim(),
                ["reference"] = (reference ?? "").Trim(),
                ["assigned_membership_id"] = string.IsNullOrEmpty(assignedMembershipId)
                    ? JValue.CreateNull() : JToken.FromObject(assignedMembershipId),
            };
            StartCoroutine(OrganizationWorkspaceRequest("POST", $"organizations/{organizationId}/clients/",
                body.ToString(Formatting.None), json =>
                {
                    try { success?.Invoke(JsonConvert.DeserializeObject<OrganizationClient>(json)); }
                    catch (JsonException) { failure?.Invoke("Unable to read the organization client."); }
                }, failure));
        }

        public void AssignOrganizationClient(string organizationId, OrganizationClient client,
            string assignedMembershipId, Action success, Action<string> failure)
        {
            var body = new JObject
            {
                ["revision"] = client.revision,
                ["assigned_membership_id"] = string.IsNullOrEmpty(assignedMembershipId)
                    ? JValue.CreateNull() : JToken.FromObject(assignedMembershipId),
            };
            StartCoroutine(OrganizationWorkspaceRequest("PATCH",
                $"organizations/{organizationId}/clients/{client.id}/", body.ToString(Formatting.None),
                _ => success?.Invoke(), failure));
        }

        public void SetOrganizationClientArchived(string organizationId, OrganizationClient client,
            bool archived, Action success, Action<string> failure)
        {
            var body = new JObject { ["revision"] = client.revision, ["archived"] = archived };
            StartCoroutine(OrganizationWorkspaceRequest("PATCH",
                $"organizations/{organizationId}/clients/{client.id}/", body.ToString(Formatting.None),
                _ => success?.Invoke(), failure));
        }

        public void UpdateOrganization(string organizationId, string name, string logoUrl, string accentColor,
            Action success, Action<string> failure)
        {
            var body = new JObject
            {
                ["name"] = (name ?? "").Trim(),
                ["logo_url"] = (logoUrl ?? "").Trim(),
                ["accent_color"] = (accentColor ?? "").Trim(),
            };
            StartCoroutine(OrganizationRequest("PATCH", $"organizations/{organizationId}/", body.ToString(Formatting.None),
                _ => success?.Invoke(), failure));
        }

        public void UpdateOrganizationMember(string organizationId, string membershipId,
            int? clientLimit, Action success, Action<string> failure,
            bool? canCreateClients = null, bool? canCreateSchedules = null,
            bool? canHostSessions = null, bool? canCreateReports = null,
            bool? canInviteClients = null, bool? allowExternalContacts = null,
            string temporaryPassword = null)
        {
            var body = new JObject
            {
                ["client_limit"] = clientLimit.HasValue ? JToken.FromObject(clientLimit.Value) : JValue.CreateNull(),
            };
            if (canCreateClients.HasValue) body["can_create_clients"] = canCreateClients.Value;
            if (canCreateSchedules.HasValue) body["can_create_schedules"] = canCreateSchedules.Value;
            if (canHostSessions.HasValue) body["can_host_sessions"] = canHostSessions.Value;
            if (canCreateReports.HasValue) body["can_create_reports"] = canCreateReports.Value;
            if (canInviteClients.HasValue) body["can_invite_clients"] = canInviteClients.Value;
            if (allowExternalContacts.HasValue) body["allow_external_contacts"] = allowExternalContacts.Value;
            if (!string.IsNullOrEmpty(temporaryPassword)) body["temporary_password"] = temporaryPassword;
            StartCoroutine(OrganizationRequest("PATCH",
                $"organizations/{organizationId}/members/{membershipId}/", body.ToString(Formatting.None),
                _ => success?.Invoke(), failure));
        }

        public void UpdateOrganizationHostingAllocations(string organizationId,
            OrganizationUsage usage, string membershipId, int? hostingSecondsLimit,
            Action success, Action<string> failure)
        {
            var allocations = new JArray();
            foreach (var member in usage.members ?? Array.Empty<OrganizationUsageMember>())
            {
                int? seconds = member.membership_id == membershipId
                    ? hostingSecondsLimit : member.allocation_seconds;
                allocations.Add(new JObject
                {
                    ["membership_id"] = member.membership_id,
                    ["seconds"] = seconds.HasValue ? JToken.FromObject(seconds.Value) : JValue.CreateNull(),
                });
            }
            var body = new JObject
            {
                ["revision"] = usage.allocation_revision,
                ["allocations"] = allocations,
            };
            StartCoroutine(OrganizationRequest("PUT", $"organizations/{organizationId}/usage/",
                body.ToString(Formatting.None), _ => success?.Invoke(), failure));
        }

        public void CreateManagedOrganizationTherapist(string organizationId, string username,
            string name, string temporaryPassword, Action<OrganizationMember> success, Action<string> failure)
        {
            var body = new JObject
            {
                ["username"] = (username ?? "").Trim(),
                ["name"] = (name ?? "").Trim(),
                ["temporary_password"] = temporaryPassword ?? "",
            };
            StartCoroutine(OrganizationRequest("POST", $"organizations/{organizationId}/managed-therapists/",
                body.ToString(Formatting.None), json =>
                {
                    try
                    {
                        var member = JsonConvert.DeserializeObject<OrganizationMember>(json);
                        if (member == null || string.IsNullOrEmpty(member.id))
                        { failure?.Invoke("Managed therapist account was not created."); return; }
                        success?.Invoke(member);
                    }
                    catch (JsonException) { failure?.Invoke("Unable to read the managed therapist account."); }
                }, failure));
        }

        public void RemoveOrganizationMember(string organizationId, string membershipId,
            Action success, Action<string> failure)
        {
            StartCoroutine(OrganizationRequest("PATCH", $"organizations/{organizationId}/members/{membershipId}/",
                "{\"status\":\"removed\"}", _ => success?.Invoke(), failure));
        }

        public void SetManagedOrganizationTherapistLocked(string organizationId, string membershipId,
            bool locked, Action success, Action<string> failure)
        {
            var body = new JObject { ["locked"] = locked };
            StartCoroutine(OrganizationRequest("PATCH",
                $"organizations/{organizationId}/members/{membershipId}/",
                body.ToString(Formatting.None), _ => success?.Invoke(), failure));
        }

        public void RevokeOrganizationInvitation(string organizationId, string invitationId,
            Action success, Action<string> failure)
        {
            StartCoroutine(OrganizationRequest("DELETE",
                $"organizations/{organizationId}/invitations/{invitationId}/", null,
                _ => success?.Invoke(), failure));
        }

        private void OrganizationOwnerGet(string path, Action<string> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "organization")
            { failure?.Invoke("Sign in with an organization account."); return; }
            StartCoroutine(Get($"{BaseUrl}/auth/{path}", AccessToken, success, failure));
        }

        private void OrganizationWorkspaceGet(string path, Action<string> success, Action<string> failure)
        {
            if (!IsLoggedIn)
            { failure?.Invoke("Please sign in first."); return; }
            StartCoroutine(Get($"{BaseUrl}/auth/{path}", AccessToken, success, failure));
        }

        private IEnumerator OrganizationWorkspaceRequest(string method, string path, string jsonBody,
            Action<string> success, Action<string> failure)
        {
            if (!IsLoggedIn)
            { failure?.Invoke("Please sign in first."); yield break; }
            var guard = CaptureCredentialGuard();
            int epoch = Sandplay.Data.LocalAccountStorage.Epoch;
            using var request = new UnityWebRequest($"{BaseUrl}/auth/{path}", method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            if (jsonBody != null)
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            yield return request.SendWebRequest();
            if (!guard() || epoch != Sandplay.Data.LocalAccountStorage.Epoch) yield break;
            if (request.result == UnityWebRequest.Result.Success)
            {
                InvalidateAccess();
                success?.Invoke(request.downloadHandler.text);
            }
            else failure?.Invoke(ExtractError(request.downloadHandler.text, request.responseCode));
        }

        private IEnumerator OrganizationRequest(string method, string path, string jsonBody,
            Action<string> success, Action<string> failure)
        {
            if (!IsLoggedIn || UserType != "organization")
            { failure?.Invoke("Sign in with an organization account."); yield break; }
            var guard = CaptureCredentialGuard();
            int epoch = Sandplay.Data.LocalAccountStorage.Epoch;
            using var request = new UnityWebRequest($"{BaseUrl}/auth/{path}", method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
            if (jsonBody != null)
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            yield return request.SendWebRequest();
            if (!guard() || epoch != Sandplay.Data.LocalAccountStorage.Epoch) yield break;
            if (request.result == UnityWebRequest.Result.Success)
            {
                InvalidateAccess();
                success?.Invoke(request.downloadHandler.text);
            }
            else failure?.Invoke(ExtractError(request.downloadHandler.text, request.responseCode));
        }
    }
}
