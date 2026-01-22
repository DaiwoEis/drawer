using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Features.Drawing.Presentation;

namespace Editor
{
    public class SceneSetupTool
    {
        [MenuItem("Drawing/Setup Scene (One Click)")]
        public static void SetupScene()
        {
            // 1. Ensure EventSystem exists
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            // 2. Ensure Canvas exists
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            }

            // 3. Create Drawing Board (RawImage)
            GameObject boardObj = new GameObject("DrawingBoard");
            boardObj.transform.SetParent(canvas.transform, false);
            
            RawImage rawImage = boardObj.AddComponent<RawImage>();
            rawImage.color = Color.white;
            
            // Make it stretch to fill screen
            RectTransform rect = boardObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero; // Reset offsets
            
            Undo.RegisterCreatedObjectUndo(boardObj, "Create DrawingBoard");

            // 4. Add Scripts
            Features.Drawing.Presentation.CanvasRenderer renderer = boardObj.AddComponent<Features.Drawing.Presentation.CanvasRenderer>();
            
            // Set resolution to match screen aspect ratio roughly, or high res
            // For now hardcoded 2048x2048, but better to match aspect
            // Let's use 2048x1536 (iPad like) or 1920x1080
            // Or use Screen.width/height if in play mode, but here we are in editor.
            // Let's stick to square for simplicity or 2048x2048, 
            // but the issue user reported is about distortion.
            // If resolution is 2048x2048 but screen is 1920x1080, pixels are stretched.
            // We need to ensure the Renderer knows the aspect ratio or uses square pixels.
            // Our fix in StrokeStampGenerator handles scaleFactor based on Min(scaleX, scaleY),
            // which ensures the brush remains circular (uniform scale) relative to the smallest dimension.
            // But if the RenderImage is stretched, the underlying pixels are stretched.
            // Ideally, we should set the RenderTexture resolution to match the Screen aspect ratio.
            
            // For MVP setup, let's keep 2048x2048. 
            // The previous fix in StrokeStampGenerator (multiplying by scaleFactor) 
            // fixes the size calculation in logic space.
            // However, we also need to ensure the QUAD drawn to the RT is not distorted by the RT's own non-square-ness relative to screen?
            // Actually, if RT is 2048x2048 and displayed on 1920x1080 Image (Stretched),
            // a perfect circle in RT (e.g. 100x100 pixels) will look flattened on screen.
            
            // To fix this visual distortion, the RT aspect ratio MUST match the Image Rect aspect ratio.
            // Since we can't know the runtime aspect ratio at setup time easily (dynamic window),
            // we should probably write a script to update RT resolution on Start() or Awake().
            
            MouseInputProvider input = boardObj.AddComponent<MouseInputProvider>();

            // 5. Create Toolbar UI
            CreateToolbarUI(canvas, renderer);

            // 6. Setup References (using SerializedObject to access private fields if needed, 
            // but since they are [SerializeField], direct assignment via reflection or SerializedObject is best)
            
            // Generate Default Brush Texture
            Texture2D brushTex = GenerateSoftCircleTexture();
            string brushPath = "Assets/DefaultBrush.png";
            System.IO.File.WriteAllBytes(brushPath, brushTex.EncodeToPNG());
            AssetDatabase.ImportAsset(brushPath);
            Texture2D savedBrush = AssetDatabase.LoadAssetAtPath<Texture2D>(brushPath);

            // Generate Pencil Brush Texture
            Texture2D pencilTex = GeneratePencilTexture();
            string pencilPath = "Assets/PencilBrush.png";
            System.IO.File.WriteAllBytes(pencilPath, pencilTex.EncodeToPNG());
            AssetDatabase.ImportAsset(pencilPath);

            // Assign via SerializedObject to support Undo and private fields
            SerializedObject soRenderer = new SerializedObject(renderer);
            soRenderer.FindProperty("_displayImage").objectReferenceValue = rawImage;
            soRenderer.FindProperty("_defaultBrushTip").objectReferenceValue = savedBrush;
            
            // Find shader
            Shader brushShader = Shader.Find("Drawing/BrushStamp");
            if (brushShader != null)
            {
                soRenderer.FindProperty("_brushShader").objectReferenceValue = brushShader;
            }
            else
            {
                Debug.LogWarning("Could not find shader 'Drawing/BrushStamp'. Please ensure it exists.");
            }
            soRenderer.ApplyModifiedProperties();

            SerializedObject soInput = new SerializedObject(input);
            soInput.FindProperty("_inputArea").objectReferenceValue = rect;
            soInput.ApplyModifiedProperties();

            // 7. Select the object
            Selection.activeGameObject = boardObj;
            
            Debug.Log("Scene Setup Complete! Click 'Play' to test.");
        }

        private static void CreateToolbarUI(Canvas canvas, Features.Drawing.Presentation.CanvasRenderer renderer)
        {
            // Root Container
            GameObject root = new GameObject("DrawingUI_Root");
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            // 1. Secondary Panels (Floating above toolbar)
            // Container for panels
            GameObject panelContainer = new GameObject("PanelContainer");
            panelContainer.transform.SetParent(root.transform, false);
            RectTransform panelRect = panelContainer.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 100); // Sit above main toolbar
            panelRect.sizeDelta = new Vector2(0, 120); // Height of sub panel

            // Eraser Panel
            GameObject eraserPanel = CreateSubPanel(panelContainer, "EraserPanel");
            // Brush Panel
            GameObject brushPanel = CreateSubPanel(panelContainer, "BrushPanel");

            // 2. Main Toolbar (Bottom)
            GameObject mainToolbar = new GameObject("MainToolbar");
            mainToolbar.transform.SetParent(root.transform, false);
            RectTransform mainRect = mainToolbar.AddComponent<RectTransform>();
            // Use stretched width with padding
            mainRect.anchorMin = new Vector2(0.1f, 0); // 10% from left
            mainRect.anchorMax = new Vector2(0.9f, 0); // 10% from right
            mainRect.pivot = new Vector2(0.5f, 0);
            mainRect.sizeDelta = new Vector2(0, 100); // Taller: 100px
            mainRect.anchoredPosition = new Vector2(0, 30); // Safe area

            Image mainBg = mainToolbar.AddComponent<Image>();
            mainBg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            
            HorizontalLayoutGroup mainLayout = mainToolbar.AddComponent<HorizontalLayoutGroup>();
            mainLayout.childControlWidth = false;
            mainLayout.childForceExpandWidth = false;
            mainLayout.spacing = 80; // WIDER SPACING
            mainLayout.childAlignment = TextAnchor.MiddleCenter;

            // --- Populate Main Toolbar ---
            // Use larger buttons
            var btnEraser = CreateIconBtn(mainToolbar, "Btn_Eraser", Color.white, "Eraser");
            var btnBrush = CreateIconBtn(mainToolbar, "Btn_Brush", Color.white, "Brush");
            var btnClear = CreateIconBtn(mainToolbar, "Btn_Clear", Color.white, "Clear");

            // --- Populate Eraser Panel ---
            // 5 Size Dots
            HorizontalLayoutGroup eraserLayout = eraserPanel.AddComponent<HorizontalLayoutGroup>();
            eraserLayout.childAlignment = TextAnchor.MiddleCenter;
            eraserLayout.spacing = 30; // WIDER SPACING
            
            Button[] eraserSizes = new Button[5];
            for(int i=0; i<5; i++) eraserSizes[i] = CreateDotBtn(eraserPanel, $"Size_{i}", 20 + i*10, Color.black);

            // --- Populate Brush Panel ---
            // Need Vertical Layout: Row 1 = Color/Type, Row 2 = Size
            VerticalLayoutGroup brushVLayout = brushPanel.AddComponent<VerticalLayoutGroup>();
            brushVLayout.childControlHeight = false;
            brushVLayout.childForceExpandHeight = false;
            brushVLayout.spacing = 20; // WIDER SPACING
            brushVLayout.padding = new RectOffset(20,20,20,20);
            brushVLayout.childAlignment = TextAnchor.MiddleCenter;

            // Row 1: Colors + Types
            GameObject row1 = new GameObject("Row1_ColorsTypes");
            row1.transform.SetParent(brushPanel.transform, false);
            RectTransform r1Rect = row1.AddComponent<RectTransform>();
            r1Rect.sizeDelta = new Vector2(800, 60); // Wider container
            HorizontalLayoutGroup r1Layout = row1.AddComponent<HorizontalLayoutGroup>();
            r1Layout.childAlignment = TextAnchor.MiddleCenter;
            r1Layout.spacing = 40; // Split Colors and Types

            // Sub-group for Colors
            GameObject colorGroup = new GameObject("ColorGroup");
            colorGroup.transform.SetParent(row1.transform, false);
            RectTransform colorRect = colorGroup.AddComponent<RectTransform>();
            colorRect.sizeDelta = new Vector2(300, 50);
            HorizontalLayoutGroup colorLayout = colorGroup.AddComponent<HorizontalLayoutGroup>();
            colorLayout.childAlignment = TextAnchor.MiddleCenter;
            colorLayout.spacing = 15;

            // Colors
            Color[] palette = { Color.black, Color.red, Color.blue, Color.green, new Color(1f, 0.5f, 0f) };
            Button[] colorBtns = new Button[palette.Length];
            for(int i=0; i<palette.Length; i++) colorBtns[i] = CreateDotBtn(colorGroup, $"Col_{i}", 40, palette[i]);

            // Spacer (Empty Object)
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(row1.transform, false);
            spacer.AddComponent<RectTransform>().sizeDelta = new Vector2(50, 0);

            // Sub-group for Types
            GameObject typeGroup = new GameObject("TypeGroup");
            typeGroup.transform.SetParent(row1.transform, false);
            RectTransform typeRect = typeGroup.AddComponent<RectTransform>();
            typeRect.sizeDelta = new Vector2(300, 50);
            HorizontalLayoutGroup typeLayout = typeGroup.AddComponent<HorizontalLayoutGroup>();
            typeLayout.childAlignment = TextAnchor.MiddleCenter;
            typeLayout.spacing = 10;

            // Types
            Button btnSoft = CreateTextBtn(typeGroup, "Type_Soft", "Soft");
            Button btnHard = CreateTextBtn(typeGroup, "Type_Hard", "Hard");
            Button btnMarker = CreateTextBtn(typeGroup, "Type_Marker", "Marker");
            Button btnPencil = CreateTextBtn(typeGroup, "Type_Pencil", "Pencil");

            // Row 2: Sizes
            GameObject row2 = new GameObject("Row2_Sizes");
            row2.transform.SetParent(brushPanel.transform, false);
            RectTransform r2Rect = row2.AddComponent<RectTransform>();
            r2Rect.sizeDelta = new Vector2(600, 60);
            HorizontalLayoutGroup r2Layout = row2.AddComponent<HorizontalLayoutGroup>();
            r2Layout.childAlignment = TextAnchor.MiddleCenter;
            r2Layout.spacing = 30; // WIDER SPACING

            Button[] brushSizes = new Button[5];
            for(int i=0; i<5; i++) brushSizes[i] = CreateDotBtn(row2, $"Size_{i}", 20 + i*10, Color.black);


            // --- Connect Script ---
            var toolbarScript = root.AddComponent<Features.Drawing.Presentation.UI.DrawingToolbar>();
            SerializedObject so = new SerializedObject(toolbarScript);
            
            so.FindProperty("_renderer").objectReferenceValue = renderer;
            so.FindProperty("_panelEraser").objectReferenceValue = eraserPanel;
            so.FindProperty("_panelBrush").objectReferenceValue = brushPanel;
            
            so.FindProperty("_btnMainEraser").objectReferenceValue = btnEraser;
            so.FindProperty("_btnMainBrush").objectReferenceValue = btnBrush;
            so.FindProperty("_btnMainClear").objectReferenceValue = btnClear;

            // Arrays
            SerializedProperty spEraserSizes = so.FindProperty("_eraserSizeButtons");
            spEraserSizes.arraySize = 5;
            for(int i=0; i<5; i++) spEraserSizes.GetArrayElementAtIndex(i).objectReferenceValue = eraserSizes[i];

            SerializedProperty spBrushSizes = so.FindProperty("_brushSizeButtons");
            spBrushSizes.arraySize = 5;
            for(int i=0; i<5; i++) spBrushSizes.GetArrayElementAtIndex(i).objectReferenceValue = brushSizes[i];

            SerializedProperty spBrushColors = so.FindProperty("_brushColorButtons");
            spBrushColors.arraySize = 5;
            for(int i=0; i<5; i++) spBrushColors.GetArrayElementAtIndex(i).objectReferenceValue = colorBtns[i];

            so.FindProperty("_btnTypeSoft").objectReferenceValue = btnSoft;
            so.FindProperty("_btnTypeHard").objectReferenceValue = btnHard;
            so.FindProperty("_btnTypeMarker").objectReferenceValue = btnMarker;
            so.FindProperty("_btnTypePencil").objectReferenceValue = btnPencil;

            // Textures
            string softPath = "Assets/SoftBrush.png";
            string hardPath = "Assets/HardBrush.png";
            string markerPath = "Assets/MarkerBrush.png";
            string pencilPath = "Assets/PencilBrush.png";

            so.FindProperty("_softBrushTex").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(softPath);
            so.FindProperty("_hardBrushTex").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(hardPath);
            so.FindProperty("_markerBrushTex").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(markerPath);
            so.FindProperty("_pencilBrushTex").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(pencilPath);

            so.ApplyModifiedProperties();
        }

        private static GameObject CreateSubPanel(GameObject parent, string name)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform, false);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0);
            rect.anchorMax = new Vector2(0.9f, 1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.9f); // Semi-transparent white
            
            // Add rounded corner mask if possible, or just standard rect
            // For now standard rect.
            
            panel.SetActive(false);
            return panel;
        }

        private static Button CreateIconBtn(GameObject parent, string name, Color color, string text)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60, 60);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f); // Bg

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            // Inner Icon/Text
            GameObject label = new GameObject("Label");
            label.transform.SetParent(btnObj.transform, false);
            RectTransform labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(5, 5);
            labelRect.offsetMax = new Vector2(-5, -5);

            Text txt = label.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 10;
            txt.resizeTextMaxSize = 20;
            
            return btn;
        }

        private static Button CreateDotBtn(GameObject parent, string name, float diameter, Color color)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(50, 50); // Touch target

            Button btn = btnObj.AddComponent<Button>();

            // The visible dot
            GameObject dot = new GameObject("Dot");
            dot.transform.SetParent(btnObj.transform, false);
            RectTransform dotRect = dot.AddComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(diameter, diameter);
            
            Image dotImg = dot.AddComponent<Image>();
            // Use sprite knob for circle
            dotImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            dotImg.color = color;
            
            btn.targetGraphic = dotImg;
            return btn;
        }

        private static Button CreateTextBtn(GameObject parent, string name, string text)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60, 40);
            
            Image bg = btnObj.AddComponent<Image>();
            bg.color = new Color(0.82f, 0.82f, 0.82f, 1f); // Light Gray
            
            Button btn = btnObj.AddComponent<Button>();
            
            GameObject label = new GameObject("Label");
            label.transform.SetParent(btnObj.transform, false);
            RectTransform lRect = label.AddComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            
            Text txt = label.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleCenter;
            
            return btn;
        }

        private static Texture2D GeneratePencilTexture()
        {
            // Pencil: Small, grainy, circular but noisy
            int size = 64; // Smaller texture for pencil
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;
            
            float noiseScale = 50f;
            float offsetX = Random.Range(0f, 100f);
            float offsetY = Random.Range(0f, 100f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    
                    if (dist > radius)
                    {
                        colors[y * size + x] = Color.clear;
                        continue;
                    }

                    float normalizedDist = dist / radius;
                    
                    // Base alpha: Falloff
                    // Pencil has a relatively hard core but fuzzy edges
                    float alpha = 1.0f - Mathf.SmoothStep(0.5f, 1.0f, normalizedDist);

                    // Noise: High frequency noise for graphite grain
                    float noise = Mathf.PerlinNoise(x / 5f + offsetX, y / 5f + offsetY);
                    
                    // Modulate alpha by noise
                    // Pencil strokes are never fully solid black, they have gaps
                    alpha *= Mathf.Lerp(0.2f, 1.0f, noise);
                    
                    // Random salt-and-pepper noise for extra grain
                    if (Random.value > 0.9f) alpha *= 0.5f;

                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateSoftCircleTexture()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = 1f - Mathf.Clamp01(dist / radius);
                    // Cubic ease out for softer edge
                    alpha = alpha * alpha * (3 - 2 * alpha); 
                    
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateHardCircleTexture()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            // Important: Use Bilinear for smooth edges when scaled, 
            // but if we want super sharp pixel art style we'd use Point.
            // For standard "Hard" brush, Bilinear is better to avoid aliasing artifacts.
            // Also need Clamp wrapping to avoid edge bleeding.
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2f; // Leave 2px padding to avoid clipping

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    // Hard edge with slight AA (1.5 pixel falloff)
                    // dist - radius > 0 means outside.
                    // We want alpha 1 inside, alpha 0 outside.
                    // smoothstep(edge0, edge1, x): returns 0 if x < edge0, 1 if x > edge1
                    // We want: 1 if dist < radius, 0 if dist > radius + aa
                    
                    // Inverse smoothstep for falloff
                    float alpha = 1.0f - Mathf.SmoothStep(radius - 0.5f, radius + 1.0f, dist);
                    
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateMarkerTexture()
        {
            // Square-ish shape with noise
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            
            // Perlin noise offsets
            float noiseScale = 20f;
            float offsetX = Random.Range(0f, 100f);
            float offsetY = Random.Range(0f, 100f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Box shape
                    float dx = Mathf.Abs(x - center.x) / (size / 2f);
                    float dy = Mathf.Abs(y - center.y) / (size / 2f);
                    
                    // Box SDF
                    float dist = Mathf.Max(dx, dy); // 0 center, 1 edge
                    
                    // Add noise
                    float noise = Mathf.PerlinNoise(x / noiseScale + offsetX, y / noiseScale + offsetY);
                    
                    // Combine: Box fade + noise texture
                    // We want: Solid in center, fade at edges, texture throughout
                    
                    float alpha = 1.0f - Mathf.SmoothStep(0.7f, 1.0f, dist); // Fade out at edges
                    
                    // Texture modulation
                    alpha *= Mathf.Lerp(0.5f, 1.0f, noise); 
                    
                    // Rough edges
                    float edgeNoise = Mathf.PerlinNoise(x / 5f, y / 5f) * 0.1f;
                    if (dist > 0.8f + edgeNoise) alpha *= 0.5f;

                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }
    }
}
