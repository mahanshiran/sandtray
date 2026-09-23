using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sandplay.Data;
using Sandplay.Objects;

namespace Sandplay.Core
{
    // A workspace reload inside the process: persistent authentication survives, private scene state does not.
    public sealed class LocalAccountTransition : MonoBehaviour
    {
        private bool running;
        public void Begin()
        {
            if (running) return;
            running = true; StartCoroutine(Switch());
        }
        private IEnumerator Switch()
        {
            // Login first clears the old identity, then resolves /me. Never create a guest workspace between those steps.
            while (BackendClient.Instance.IsLoggedIn && BackendClient.Instance.UserId <= 0) yield return null;
            yield return null;
            var bootstrap = FindObjectOfType<SceneBootstrapper>();
            if (bootstrap == null) { running = false; yield break; }
            GameConfig config = bootstrap.WorkspaceConfig;
            ObjectCatalog catalog = bootstrap.WorkspaceCatalog;
            var previous = SceneManager.GetActiveScene();
            // Stop persistent consumers before tearing down objects they might reference.
            foreach (var client in FindObjectsOfType<FriendsClient>()) { client.StopAllCoroutines(); Destroy(client.gameObject); }
            foreach (var client in FindObjectsOfType<ReportDeliveryClient>()) { client.StopAllCoroutines(); Destroy(client.gameObject); }
            if (NetworkBootstrapper.Instance != null) { NetworkBootstrapper.Instance.Disconnect(); Destroy(NetworkBootstrapper.Instance.gameObject); }
            if (AgoraManager.Instance != null) { AgoraManager.Instance.LeaveChannel(); Destroy(AgoraManager.Instance.gameObject); }
            yield return null;
            var next = SceneManager.CreateScene("AccountWorkspace-" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(next);
            yield return SceneManager.UnloadSceneAsync(previous);
            NetworkCatalogRegistry.Reset();
            EventBus.Clear();
            LocalAccountStorage.CompleteWorkspaceTransition();
            var go = new GameObject("__AccountWorkspace__");
            go.AddComponent<SceneBootstrapper>().Initialize(config, catalog);
            Destroy(gameObject);
        }
    }
}
