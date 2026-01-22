using UnityEngine;
using UnityEngine.Rendering;
using Features.Drawing.Presentation;

namespace Features.Drawing.Domain
{
    [CreateAssetMenu(fileName = "NewBrush", menuName = "Drawing/Brush Strategy")]
    public class BrushStrategy : ScriptableObject
    {
        [Header("Appearance")]
        [Tooltip("The base texture for the brush stamp.")]
        public Texture2D MainTexture;
        
        [Tooltip("If true, the texture will be generated procedurally at runtime (e.g. for pixel-perfect hard brushes).")]
        public bool UseRuntimeGeneration;

        [Header("Rendering")]
        [Range(0.001f, 1.0f)]
        public float SpacingRatio = 0.15f;
        
        public BrushRotationMode RotationMode = BrushRotationMode.None;
        
        [Range(0f, 360f)]
        public float AngleJitter = 0f;
        
        [Range(0f, 1f)]
        public float Opacity = 1.0f;

        [Header("Blending")]
        public BlendOp BlendOp = BlendOp.Add;
        public BlendMode SrcBlend = BlendMode.One;
        public BlendMode DstBlend = BlendMode.OneMinusSrcAlpha;
    }
}