using UnityEngine;
using UnityEditor;
using Features.Drawing.Presentation;

namespace Editor.Tools
{
    public class ShaderVariantSetup
    {
        [MenuItem("Tools/Drawing/Assign Shader Variants")]
        public static void AssignVariants()
        {
            // 1. Load the asset
            // Note: Resources.Load path is relative to Resources folder and without extension
            var variants = Resources.Load<ShaderVariantCollection>("Shaders/DrawingShaderVariants");
            
            if (variants == null)
            {
                // Fallback: Try AssetDatabase if Resources load fails (e.g. if not yet imported fully)
                string path = "Assets/Resources/Shaders/DrawingShaderVariants.shadervariants";
                variants = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
            }

            if (variants == null)
            {
                Debug.LogError("Could not find ShaderVariantCollection at Assets/Resources/Shaders/DrawingShaderVariants.shadervariants");
                return;
            }

            // 2. Find Renderer
            var renderer = Object.FindObjectOfType<Features.Drawing.Presentation.CanvasRenderer>();
            if (renderer == null)
            {
                Debug.LogError("Could not find CanvasRenderer in the scene! Please open the Drawing Scene.");
                return;
            }

            // 3. Assign using SerializedObject to support Undo and dirty state
            SerializedObject so = new SerializedObject(renderer);
            SerializedProperty prop = so.FindProperty("_shaderVariants");
            
            if (prop != null)
            {
                prop.objectReferenceValue = variants;
                bool changed = so.ApplyModifiedProperties();
                
                if (changed)
                {
                    Debug.Log($"Successfully assigned '{variants.name}' to CanvasRenderer on '{renderer.name}'!");
                    EditorUtility.SetDirty(renderer);
                }
                else
                {
                    Debug.Log("ShaderVariantCollection was already assigned.");
                }
            }
            else
            {
                Debug.LogError("Could not find property '_shaderVariants' on CanvasRenderer.");
            }
        }
    }
}
