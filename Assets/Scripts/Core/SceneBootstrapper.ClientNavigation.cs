using UnityEngine;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        // Account type chosen in Settings; unrelated to the role in a multiplayer session.
        private bool CanShowClientNavigation => BackendClient.Instance.IsLoggedIn &&
            BackendClient.Instance.IsTherapistAccount;
        private bool CanUsePersonalClientDirectory => BackendClient.Instance.IsLoggedIn &&
            BackendClient.Instance.UserType == "psychologist";
        private bool CanShowOrganizationNavigation => BackendClient.Instance.IsLoggedIn &&
            BackendClient.Instance.UserType == "organization";

        private void RefreshClientNavigationVisibility()
        {
            bool visible = CanShowClientNavigation;
            float top = .855f;
            const float height = .052f;
            foreach (var key in new[] { "home", "boards", "clients", "organization", "schedules", "reports", "multiplayer" })
            {
                if (!_homeNavItems.TryGetValue(key, out var item) || item == null) continue;
                bool show = key == "clients" ? visible :
                    key == "reports" ? BackendClient.Instance.IsLoggedIn && BackendClient.Instance.UserType == "normal" :
                    key == "schedules" && BackendClient.Instance.IsOrganizationTherapist
                        ? BackendClient.Instance.ManagedCanCreateSchedules :
                    key != "organization" || CanShowOrganizationNavigation;
                item.SetActive(show);
                if (!show) continue;
                var rect = item.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(rect.anchorMin.x, top - height);
                rect.anchorMax = new Vector2(rect.anchorMax.x, top);
                top -= height + .012f;
            }
            if (_friendsSidebar != null)
                _friendsSidebar.anchorMax = new Vector2(_friendsSidebar.anchorMax.x, top);
            if (!visible)
            {
                if (_clientsPage != null) _clientsPage.SetActive(false);
                if (_activeHomeNav == "clients")
                {
                    CloseClientDialog();
                    ShowHomeSection("home");
                }
            }
            if (!CanShowOrganizationNavigation)
            {
                if (_organizationPage != null) _organizationPage.SetActive(false);
                if (_activeHomeNav == "organization")
                    ShowHomeSection("home");
            }
        }
    }
}
