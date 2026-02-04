using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Presentation
{
    /// <summary>
    /// Base class for stroke renderers that use Mesh Stamping with CommandBuffers.
    /// Encapsulates resource management (Mesh, Material, CommandBuffer) and instanced drawing logic.
    /// </summary>
    public abstract class BaseStrokeRenderer : MonoBehaviour
    {
        [Header("Base Settings")]
        [SerializeField] protected Shader _brushShader;
        [SerializeField] protected Texture2D _defaultBrushTip;

        // Shared Resources
        protected Material _brushMaterial;
        protected CommandBuffer _cmd;
        protected Mesh _quadMesh;
        protected MaterialPropertyBlock _props;
        
        // Batching
        protected const int BATCH_SIZE = 1023;
        protected Matrix4x4[] _matrices = new Matrix4x4[BATCH_SIZE];

        protected virtual void OnDestroy()
        {
            ReleaseResources();
        }

        protected void ReleaseResources()
        {
            if (_cmd != null) _cmd.Release();
            if (_brushMaterial != null) Destroy(_brushMaterial);
            if (_quadMesh != null) Destroy(_quadMesh);
        }

        protected virtual void InitializeGraphics(string bufferName)
        {
            // Setup Material
            if (_brushShader == null) 
                _brushShader = Shader.Find("Drawing/BrushStamp");
            
            if (_brushShader == null)
            {
                Debug.LogError($"[{GetType().Name}] Brush Shader NOT FOUND!");
                return;
            }

            // Clean up existing material if any
            if (_brushMaterial != null) Destroy(_brushMaterial);
            _brushMaterial = new Material(_brushShader);
            
            if (_defaultBrushTip != null)
                _brushMaterial.mainTexture = _defaultBrushTip;

            // Setup CommandBuffer
            if (_cmd != null) _cmd.Release();
            _cmd = new CommandBuffer();
            _cmd.name = bufferName;

            // Create Quad Mesh
            if (_quadMesh == null) _quadMesh = CreateQuad();
            
            // Init props
            if (_props == null) _props = new MaterialPropertyBlock();
        }

        protected void DrawStampsBatch(
            List<StampData> stamps, 
            RenderTargetIdentifier target, 
            Vector2Int resolution,
            Color color, 
            float opacity, 
            bool isEraser, 
            bool useEraserRedTrail)
        {
            if (stamps == null || stamps.Count == 0) return;

            _cmd.Clear();
            _cmd.SetRenderTarget(target);
            
            // Setup Blend Modes
            SetupBlendModes(isEraser, useEraserRedTrail);

            // Setup Color
            Color drawColor = color;
            if (isEraser && useEraserRedTrail)
            {
                // Color is already passed as red trail color by caller usually, or we override here?
                // The caller passes 'color'. 
                // GhostOverlay passes _eraserTrailColor.
                // CanvasRenderer passes (0,0,0,1) for eraser but uses BlendOp.Reverse/Zero.
            }
            
            // Apply Opacity
            if (!isEraser || useEraserRedTrail)
            {
                drawColor.a *= opacity;
            }
            
            _props.SetColor("_Color", drawColor);

            _cmd.SetViewMatrix(Matrix4x4.identity);
            _cmd.SetProjectionMatrix(Matrix4x4.Ortho(0, resolution.x, 0, resolution.y, -1, 1));

            // Enable Instancing
            _brushMaterial.enableInstancing = true;

            int batchCount = 0;
            int totalCount = stamps.Count;

            for (int i = 0; i < totalCount; i++)
            {
                var stamp = stamps[i];
                
                if (batchCount >= BATCH_SIZE)
                {
                    _cmd.DrawMeshInstanced(_quadMesh, 0, _brushMaterial, 0, _matrices, batchCount, _props);
                    batchCount = 0;
                }

                _matrices[batchCount] = Matrix4x4.TRS(
                    new Vector3(stamp.Position.x, stamp.Position.y, 0),
                    Quaternion.Euler(0, 0, stamp.Rotation),
                    new Vector3(stamp.Size, stamp.Size, 1)
                );
                batchCount++;
            }

            if (batchCount > 0)
            {
                _cmd.DrawMeshInstanced(_quadMesh, 0, _brushMaterial, 0, _matrices, batchCount, _props);
            }

            Graphics.ExecuteCommandBuffer(_cmd);
        }

        protected virtual void SetupBlendModes(bool isEraser, bool useEraserRedTrail)
        {
            if (isEraser)
            {
                if (useEraserRedTrail)
                {
                    // Ghost Eraser (Red Trail)
                    _brushMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    _brushMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    _brushMaterial.SetInt("_BlendOp", (int)BlendOp.Add);
                }
                else
                {
                    // Real Eraser (Subtract Alpha)
                    _cmd.SetExecutionFlags(CommandBufferExecutionFlags.None);
                    _brushMaterial.SetInt("_SrcBlend", (int)BlendMode.Zero);
                    _brushMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    _brushMaterial.SetInt("_BlendOp", (int)BlendOp.Add);
                }
            }
            // Normal brush blending is assumed to be set by ConfigureBrush previously, 
            // OR we can default it here if not using strategy.
            // But since ConfigureBrush sets it on the material, we should probably leave it unless it's eraser override.
        }

        private Mesh CreateQuad()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0)
            };
            mesh.uv = new Vector2[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            return mesh;
        }
    }
}
