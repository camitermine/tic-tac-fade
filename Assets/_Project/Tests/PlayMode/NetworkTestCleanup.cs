using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// NetworkManager moves itself to DontDestroyOnLoad, so every test that
    /// reloads the scene would leave one more behind (the game loads the
    /// scene only once, the tests reload it every time). Leftovers make the
    /// next scene's services talk to a stale NetworkManager.Singleton and
    /// trigger Netcode's "Singleton is not null after invoking OnDestroy"
    /// warning when Play Mode exits.
    /// </summary>
    public static class NetworkTestCleanup
    {
        public static IEnumerator DestroyAllNetworkManagers()
        {
            foreach (var networkManager in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include))
                Object.Destroy(networkManager.gameObject);

            yield return null; // let Destroy (and NetworkManager.OnDestroy) run
        }
    }
}
