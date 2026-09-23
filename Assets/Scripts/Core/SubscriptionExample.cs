using UnityEngine;
namespace Sandplay.Core
{
    /// <summary>Feature access comes from capabilities. Billing status is for billing UI only.</summary>
    public class SubscriptionExample : MonoBehaviour
    {
        public void OnAdvancedAnalysisButtonClicked()
        {
            BackendClient.Instance.FetchAccessSnapshot(snapshot =>
            {
                var decision = AccessPolicy.Evaluate(snapshot, "ai.analyze");
                Debug.Log("AI preflight: " + decision);
                // Call the metered analysis service on Allowed. It reserves usage atomically;
                // a cached preflight must never replace the server's final quota check.
            }, error => Debug.LogWarning(error));
        }
    }
}
