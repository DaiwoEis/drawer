using UnityEngine;
using UnityEngine.UI;
using Features.Drawing.App;
using Features.Drawing.Service.Network;
using Features.Drawing.Presentation;

#if UNITY_EDITOR
using UnityEditor;

namespace Features.Drawing.Editor.Tools
{
    public class NetworkSetupTool : EditorWindow
    {
        [MenuItem("Tools/Drawing/Setup Network (Mock)")]
        public static void SetupNetwork()
        {
            // 1. Find or Create Network Manager Object
            var netGo = GameObject.Find("NetworkManager");
            if (netGo == null)
            {
                netGo = new GameObject("NetworkManager");
                Undo.RegisterCreatedObjectUndo(netGo, "Create NetworkManager");
            }

            // 2. Add Components
            var mockClient = netGo.GetComponent<MockNetworkClient>();
            if (mockClient == null) mockClient = Undo.AddComponent<MockNetworkClient>(netGo);

            var netService = netGo.GetComponent<DrawingNetworkService>();
            if (netService == null) netService = Undo.AddComponent<DrawingNetworkService>(netGo);

            var ghostRenderer = netGo.GetComponent<GhostOverlayRenderer>();
            if (ghostRenderer == null) ghostRenderer = Undo.AddComponent<GhostOverlayRenderer>(netGo);

            // 3. Find Dependencies
            var appService = FindObjectOfType<DrawingAppService>();
            if (appService == null)
            {
                Debug.LogError("DrawingAppService not found in scene!");
                return;
            }

            var mainRenderer = FindObjectOfType<Features.Drawing.Presentation.CanvasRenderer>();
            var rawImage = FindObjectOfType<RawImage>(); // Heuristic: Find main display

            // 4. Wire up Dependencies (SerializedFields)
            // We use SerializedObject to modify private fields
            
            // Wire DrawingNetworkService
            var soNetService = new SerializedObject(netService);
            soNetService.FindProperty("_appService").objectReferenceValue = appService;
            soNetService.FindProperty("_ghostRenderer").objectReferenceValue = ghostRenderer;
            soNetService.ApplyModifiedProperties();

            // Wire GhostOverlayRenderer
            var soGhost = new SerializedObject(ghostRenderer);
            soGhost.FindProperty("_mainRenderer").objectReferenceValue = mainRenderer;
            soGhost.FindProperty("_displayImage").objectReferenceValue = rawImage;
            // Try to find default brush texture if null
            // soGhost.FindProperty("_defaultBrushTip").objectReferenceValue = ...; 
            soGhost.ApplyModifiedProperties();

            // Wire DrawingAppService
            var soApp = new SerializedObject(appService);
            // _networkService is private, but we added [SerializeField] or just private?
            // Wait, we added it as private field without [SerializeField]. 
            // We need to inject it via SetNetworkService at runtime, or make it serializable.
            // But we added a public setter: SetNetworkService.
            // For Editor setup, we can't call setter.
            // Let's add a Runtime Initializer script.
            
            var bootstrapper = netGo.GetComponent<NetworkBootstrapper>();
            if (bootstrapper == null) bootstrapper = Undo.AddComponent<NetworkBootstrapper>(netGo);
            
            var soBoot = new SerializedObject(bootstrapper);
            soBoot.FindProperty("_appService").objectReferenceValue = appService;
            soBoot.FindProperty("_netService").objectReferenceValue = netService;
            soBoot.FindProperty("_client").objectReferenceValue = mockClient;
            soBoot.ApplyModifiedProperties();

            Selection.activeGameObject = netGo;
            Debug.Log("Network Setup Complete! Created 'NetworkManager' with MockClient, Service, and GhostRenderer.");
        }
    }
}
#endif
