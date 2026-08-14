using UnityEngine;

namespace Sandplay.Core
{
    /// <summary>
    /// Example showing how to check subscription status in Unity.
    /// Use BackendClient.Instance.IsSubscribed to gate premium features.
    /// </summary>
    public class SubscriptionExample : MonoBehaviour
    {
        void Start()
        {
            // Example: Check if user can access premium feature
            if (BackendClient.Instance.IsSubscribed)
            {
                Debug.Log("User has active subscription");
                EnablePremiumFeatures();
            }
            else
            {
                Debug.Log("User is not subscribed (check RevenueCat or admin grant)");
                ShowUpgradePrompt();
            }
        }

        void EnablePremiumFeatures()
        {
            // Enable premium-only features here
            // Examples:
            // - Unlimited boards
            // - Advanced analysis tools
            // - Custom catalogs
            // - Video calls with therapists
        }

        void ShowUpgradePrompt()
        {
            // Show UI prompting user to upgrade
            // - Link to App Store subscription
            // - Deep link to website subscription page
            // - QR code for WeChat/Alipay payment
        }

        // Example: Gate a specific feature
        public void OnAdvancedAnalysisButtonClicked()
        {
            if (!BackendClient.Instance.IsSubscribed)
            {
                Debug.Log("Advanced analysis requires premium subscription");
                ShowUpgradePrompt();
                return;
            }

            // User is subscribed, allow feature
            PerformAdvancedAnalysis();
        }

        void PerformAdvancedAnalysis()
        {
            // Your premium feature code here
        }
    }
}
