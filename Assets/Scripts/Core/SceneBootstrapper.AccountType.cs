using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private TMP_Dropdown _accountTypeDropdown;
        private TMP_Text _accountTypeStatus;
        private string _accountTypeStatusKey;

        private void BuildAccountTypeSettings(Transform parent)
        {
            var row = SettingsRow(parent, "account.type_title", 164);
            var title = row.Find("Title").GetComponent<RectTransform>();
            title.anchorMin = new Vector2(.025f,.64f);
            title.anchorMax = new Vector2(.515f,.94f);
            _accountTypeDropdown = SettingsDropdown(row, "AccountTypeDropdown",
                new[]
                {
                    Localization.Get("account.personal"),
                    Localization.Get("account.therapist"),
                    Localization.Get("account.organization"),
                }, 0, index => ChangeAccountType(index == 1 ? "psychologist" :
                    index == 2 ? "organization" : "normal"));
            var control = _accountTypeDropdown.GetComponent<RectTransform>();
            control.anchorMin = new Vector2(.55f,.66f); control.anchorMax = new Vector2(.975f,.94f);
            var hint = ClientText(row, Localization.Get("account.type_hint"), 12, .025f,.20f,.95f,.41f, HomeMuted);
            hint.enableWordWrapping = true;
            TrackLocalized((TextMeshProUGUI)hint, "account.type_hint");
            _accountTypeStatus = ClientText(row, "", 12, .025f,.035f,.95f,.16f, HomeMuted);
            _accountTypeStatus.name = "AccountTypeStatus";
            _therapistProfileButton = SettingsAction(parent, "Therapist profile", "TherapistProfileButton", "Edit profile", OpenTherapistProfile);
            _organizationWorkspaceButton = SettingsAction(parent, "Organization workspace", "OrganizationWorkspaceButton", "Open", OpenOrganizationWorkspace);
            RefreshAccountTypeSettings();
            var backend = BackendClient.Instance;
            if (backend.IsLoggedIn)
            {
                int accountId = backend.UserId;
                backend.RefreshAccountState(() =>
                {
                    if (this != null && BackendClient.Instance.UserId == accountId)
                        RefreshAccountTypeSettings();
                }, error =>
                {
                    if (this != null && _accountTypeStatus != null &&
                        BackendClient.Instance.UserId == accountId)
                        _accountTypeStatus.text = error;
                });
            }
        }

        private void RefreshAccountTypeSettings()
        {
            if (_therapistProfileButton != null)
                _therapistProfileButton.transform.parent.gameObject.SetActive(BackendClient.Instance.IsLoggedIn && BackendClient.Instance.UserType == "psychologist");
            if (_organizationWorkspaceButton != null)
                _organizationWorkspaceButton.transform.parent.gameObject.SetActive(BackendClient.Instance.IsLoggedIn &&
                    (BackendClient.Instance.UserType == "organization" || BackendClient.Instance.IsTherapistAccount));
            RefreshClientNavigationVisibility();
            RefreshSettingsValues();
            if (_accountTypeDropdown == null) return;
            var client = BackendClient.Instance;
            bool loggedIn = client.IsLoggedIn;
            // Only the internal organization-issued type is fixed. Normal test
            // accounts remain free to switch among the three public types.
            bool editable = loggedIn && client.UserType != "admin" && !client.IsOrganizationTherapist;
            int selected = client.UserType == "psychologist" ? 1 : client.UserType == "organization" ? 2 : 0;
            _accountTypeDropdown.SetValueWithoutNotify(selected);
            _accountTypeDropdown.interactable = editable && !client.IsUpdatingUserType;
            if (!editable)
                _accountTypeDropdown.captionText.text = Localization.Get(loggedIn ? "settings.managed" : "settings.sign_in");
            string key = !loggedIn ? "account.sign_in" : !editable ? "account.managed" :
                client.IsUpdatingUserType ? "account.saving" : _accountTypeStatusKey;
            if (_accountTypeStatus != null)
                _accountTypeStatus.text = string.IsNullOrEmpty(key) ? "" : Localization.Get(key);
        }

        private void ChangeAccountType(string userType)
        {
            _accountTypeStatusKey = null;
            BackendClient.Instance.UpdateUserType(userType, () =>
            {
                if (this == null) return;
                _accountTypeStatusKey = "account.saved";
                RefreshAccountTypeSettings();
            }, error =>
            {
                if (this == null) return;
                _accountTypeStatusKey = null;
                RefreshAccountTypeSettings();
                if (_accountTypeStatus != null) _accountTypeStatus.text = error;
            });
            RefreshAccountTypeSettings();
        }
    }
}
