using UnityEngine;
using UnityEngine.Rendering;

namespace Features.Drawing.App
{
    /// <summary>
    /// Handles application startup tasks, including resource pre-warming.
    /// Should be attached to the first scene or a bootstrapper.
    /// </summary>
    public class StartupLoader : MonoBehaviour
    {
        [SerializeField] private bool _prewarmShaders = true;

        private void Start()
        {
            if (_prewarmShaders)
            {
                PrewarmResources();
            }
        }

        private void PrewarmResources()
        {
            // 1. Preload Compute Shader
            var compute = Resources.Load<ComputeShader>("Shaders/StrokeGeneration");
            if (compute != null)
            {
                // Just loading it into memory is often enough for "warm up"
                // but we can also dispatch a dummy kernel if needed.
                // For now, Resources.Load is the key step.
            }

            // 2. Preload Brush Textures
            // Assuming standard brushes are in a known path or referenced
            // Since we don't have a direct list, we might rely on what's referenced in the scene.
            
            // 3. ShaderVariantCollection (if available)
            // var variants = Resources.Load<ShaderVariantCollection>("Shaders/DrawingVariants");
            // if (variants != null) variants.WarmUp();
            
            Debug.Log("[StartupLoader] Resources pre-warmed.");
        }
    }
}
