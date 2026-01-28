using UnityEngine;
using Features.Drawing.App;

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Bootstrapper to wire up dependencies at runtime.
    /// Ensures DrawingAppService gets the NetworkService reference.
    /// </summary>
    public class NetworkBootstrapper : MonoBehaviour
    {
        [SerializeField] private DrawingAppService _appService;
        [SerializeField] private DrawingNetworkService _netService;
        [SerializeField] private MockNetworkClient _client; // Concrete type for Inspector, interface for logic

        private void Awake()
        {
            if (_netService != null && _client != null)
            {
                _netService.Initialize(_client);
            }

            if (_appService != null && _netService != null)
            {
                _appService.SetNetworkService(_netService);
            }
        }
    }
}
