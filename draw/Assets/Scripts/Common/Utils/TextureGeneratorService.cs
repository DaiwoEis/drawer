using UnityEngine;

namespace Common.Utils
{
    public static class TextureGeneratorService
    {
        private static Texture2D _cachedHardBrush;

        /// <summary>
        /// Generates or returns a cached 128x128 hard circle texture with ultra-sharp edges (0.5px AA).
        /// Ideal for "Hard" brushes that need to avoid overlap artifacts.
        /// </summary>
        public static Texture2D GetSharpHardBrush()
        {
            if (_cachedHardBrush != null) return _cachedHardBrush;

            int size = 256; // Increased resolution
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 4f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    // Increased falloff from 0.5px to 8px to prevent aliasing at small sizes
                    // and scalloping at large sizes.
                    float alpha = 1.0f - Mathf.SmoothStep(radius - 4.0f, radius + 4.0f, dist);
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            
            _cachedHardBrush = tex;
            return tex;
        }
    }
}