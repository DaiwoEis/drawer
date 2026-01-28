using UnityEngine;
using UnityEditor;
using Features.Drawing.App;
using Features.Drawing.Domain;
using System.Linq;
using System.Collections.Generic;

#if UNITY_EDITOR
namespace Features.Drawing.Editor.Tools
{
    /// <summary>
    /// Editor tool to automatically register all BrushStrategy assets into DrawingAppService.
    /// Ensures consistent ID mapping across clients by sorting assets by name.
    /// </summary>
    public class BrushRegistryTool
    {
        [MenuItem("Tools/Drawing/Update Brush Registry")]
        public static void UpdateBrushRegistry()
        {
            // 1. Find DrawingAppService in the current scene
            var appService = Object.FindObjectOfType<DrawingAppService>();
            if (appService == null)
            {
                Debug.LogError("[BrushRegistryTool] DrawingAppService not found in the open scene!");
                return;
            }

            // 2. Find all BrushStrategy assets in the project
            string[] guids = AssetDatabase.FindAssets("t:BrushStrategy");
            var strategies = new List<BrushStrategy>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var strategy = AssetDatabase.LoadAssetAtPath<BrushStrategy>(path);
                if (strategy != null)
                {
                    strategies.Add(strategy);
                }
            }

            // 3. Sort by Name to ensure consistent order across all clients
            // This is CRITICAL for Network ID consistency (Index -> ID)
            strategies.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            Debug.Log($"[BrushRegistryTool] Found {strategies.Count} brushes: {string.Join(", ", strategies.Select(s => s.name))}");

            // 4. Update DrawingAppService
            var so = new SerializedObject(appService);
            var prop = so.FindProperty("_registeredBrushes");

            if (prop == null)
            {
                Debug.LogError("[BrushRegistryTool] Could not find field '_registeredBrushes' in DrawingAppService. Make sure it is [SerializeField].");
                return;
            }

            // Clear and add
            prop.ClearArray();
            prop.arraySize = strategies.Count;

            for (int i = 0; i < strategies.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = strategies[i];
            }

            bool modified = so.ApplyModifiedProperties();
            
            if (modified)
            {
                Debug.Log($"[BrushRegistryTool] Successfully registered {strategies.Count} brushes to DrawingAppService.");
                EditorUtility.SetDirty(appService);
            }
            else
            {
                Debug.Log("[BrushRegistryTool] Registry is already up to date.");
            }
        }
    }
}
#endif
