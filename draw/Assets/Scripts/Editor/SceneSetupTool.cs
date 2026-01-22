using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using TMPro;
using Features.Drawing.Presentation;
using Features.Drawing.Domain;

namespace Editor
{
    public class SceneSetupTool
    {
        private const float NAV_HEIGHT = 220f; // Much taller
        private const float PANEL_HEIGHT = 180f; // Taller panels
        private const float TOP_BAR_HEIGHT = 100f;

        [MenuItem("Drawing/Setup Scene (One Click)")]
        public static void SetupScene()
        {
            // 0. Setup Assets first
            SetupBrushAssets();

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
            
            // Ensure Scaler is configured for High DPI
            ConfigureCanvasScaler(canvas);

            // 3. Create Drawing Board (RawImage)
            GameObject boardObj = GameObject.Find("DrawingBoard");
            RawImage rawImage = null;
            RectTransform rect = null;
            
            if (boardObj == null)
            {
                boardObj = new GameObject("DrawingBoard");
                boardObj.transform.SetParent(canvas.transform, false);
                
                rawImage = boardObj.AddComponent<RawImage>();
                rawImage.color = Color.white;
                
                // Fix: Assign custom material to handle Premultiplied Alpha correctly
                Material uiMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/UI_Premultiply_Fix.mat");
                Shader premulShader = Shader.Find("UI/Premul");
                
                if (uiMat == null)
                {
                    if (premulShader == null) premulShader = Shader.Find("UI/Default"); // Fallback
                    uiMat = new Material(premulShader);
                    AssetDatabase.CreateAsset(uiMat, "Assets/UI_Premultiply_Fix.mat");
                }
                else if (premulShader != null && uiMat.shader != premulShader)
                {
                     uiMat.shader = premulShader;
                     EditorUtility.SetDirty(uiMat);
                }
                
                if (uiMat != null)
                {
                    rawImage.material = uiMat;
                }
                
                rawImage.raycastTarget = false; // Disable Raycast Target to allow input passthrough logic
                
                rect = boardObj.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                
                Undo.RegisterCreatedObjectUndo(boardObj, "Create DrawingBoard");
            }
            else
            {
                rawImage = boardObj.GetComponent<RawImage>();
                rect = boardObj.GetComponent<RectTransform>();
            }

            // ADJUST LAYOUT: Reserve space for Bottom Toolbar
            rect.offsetMin = new Vector2(0, NAV_HEIGHT); // Bottom offset
            rect.offsetMax = new Vector2(0, -TOP_BAR_HEIGHT); // Top offset (optional, but good for symmetry/safe area)

            // 4. Add Scripts (Ensure they exist)
            Features.Drawing.Presentation.CanvasRenderer renderer = boardObj.GetComponent<Features.Drawing.Presentation.CanvasRenderer>();
            if (renderer == null) renderer = boardObj.AddComponent<Features.Drawing.Presentation.CanvasRenderer>();
            
            MouseInputProvider input = boardObj.GetComponent<MouseInputProvider>();
            if (input == null) input = boardObj.AddComponent<MouseInputProvider>();

            // 5. Create App Service (Application Layer)
            Features.Drawing.App.DrawingAppService appService = Object.FindObjectOfType<Features.Drawing.App.DrawingAppService>();
            if (appService == null)
            {
                GameObject appObj = new GameObject("DrawingAppService");
                appService = appObj.AddComponent<Features.Drawing.App.DrawingAppService>();
                Undo.RegisterCreatedObjectUndo(appObj, "Create AppService");
            }
            
            SerializedObject soApp = new SerializedObject(appService);
            soApp.FindProperty("_concreteRenderer").objectReferenceValue = renderer;
            soApp.ApplyModifiedProperties();

            // 6. Create Toolbar UI (Clean recreate)
            CreateToolbarUI(canvas, appService);

            // 7. Setup References
            Texture2D savedBrush = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SoftBrush.png");

            SerializedObject soRenderer = new SerializedObject(renderer);
            soRenderer.FindProperty("_displayImage").objectReferenceValue = rawImage;
            soRenderer.FindProperty("_defaultBrushTip").objectReferenceValue = savedBrush;
            
            Shader brushShader = Shader.Find("Drawing/BrushStamp");
            if (brushShader != null)
                soRenderer.FindProperty("_brushShader").objectReferenceValue = brushShader;
            
            soRenderer.ApplyModifiedProperties();

            SerializedObject soInput = new SerializedObject(input);
            soInput.FindProperty("_inputArea").objectReferenceValue = rect;
            soInput.ApplyModifiedProperties();

            // 8. Select the object
            Selection.activeGameObject = boardObj;
            
            Debug.Log("Scene Setup Complete! UI Updated and Canvas Resized.");
        }

        [MenuItem("Drawing/Update UI Only")]
        public static void UpdateUI()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            Features.Drawing.App.DrawingAppService appService = Object.FindObjectOfType<Features.Drawing.App.DrawingAppService>();
            
            if (canvas != null && appService != null)
            {
                // Ensure Scaler is configured
                ConfigureCanvasScaler(canvas);

                CreateToolbarUI(canvas, appService);
                
                // Also update Canvas rect
                GameObject boardObj = GameObject.Find("DrawingBoard");
                if (boardObj != null)
                {
                    RectTransform rect = boardObj.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.offsetMin = new Vector2(0, NAV_HEIGHT);
                        rect.offsetMax = new Vector2(0, -TOP_BAR_HEIGHT);
                    }
                }

                Debug.Log("UI Updated Successfully (Large Mode).");
            }
            else
            {
                Debug.LogError("Canvas or AppService not found!");
            }
        }

        private static void ConfigureCanvasScaler(Canvas canvas)
        {
            if (canvas == null) return;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void SetupBrushAssets()
        {
            // 1. Generate Textures
            Texture2D softTex = GenerateSoftCircleTexture();
            Texture2D hardTex = GenerateHardCircleTexture();
            Texture2D markerTex = GenerateMarkerTexture();
            Texture2D pencilTex = GeneratePencilTexture();

            string softTexPath = "Assets/SoftBrush.png";
            string hardTexPath = "Assets/HardBrush.png";
            string markerTexPath = "Assets/MarkerBrush.png";
            string pencilTexPath = "Assets/PencilBrush.png";

            System.IO.File.WriteAllBytes(softTexPath, softTex.EncodeToPNG());
            System.IO.File.WriteAllBytes(hardTexPath, hardTex.EncodeToPNG());
            System.IO.File.WriteAllBytes(markerTexPath, markerTex.EncodeToPNG());
            System.IO.File.WriteAllBytes(pencilTexPath, pencilTex.EncodeToPNG());

            AssetDatabase.ImportAsset(softTexPath);
            AssetDatabase.ImportAsset(hardTexPath);
            AssetDatabase.ImportAsset(markerTexPath);
            AssetDatabase.ImportAsset(pencilTexPath);

            // 1.5 Configure Texture Importers
            ConfigureTextureImporter(softTexPath);
            ConfigureTextureImporter(hardTexPath);
            ConfigureTextureImporter(markerTexPath);
            ConfigureTextureImporter(pencilTexPath);

            // 2. Load Textures
            softTex = AssetDatabase.LoadAssetAtPath<Texture2D>(softTexPath);
            hardTex = AssetDatabase.LoadAssetAtPath<Texture2D>(hardTexPath);
            markerTex = AssetDatabase.LoadAssetAtPath<Texture2D>(markerTexPath);
            pencilTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pencilTexPath);

            // 3. Configure Strategies
            string domainPath = "Assets/Scripts/Features/Drawing/Domain";
            ConfigureStrategy(domainPath + "/SoftBrush.asset", softTex, 0.5f, 0.15f, BlendOp.Add, BrushRotationMode.None);
            ConfigureStrategy(domainPath + "/HardBrush.asset", hardTex, 1.0f, 0.05f, BlendOp.Add, BrushRotationMode.None, false); // DISABLED RUNTIME GENERATION
            ConfigureStrategy(domainPath + "/MarkerBrush.asset", markerTex, 0.8f, 0.1f, BlendOp.Add, BrushRotationMode.Fixed);
            ConfigureStrategy(domainPath + "/PencilBrush.asset", pencilTex, 0.6f, 0.2f, BlendOp.Add, BrushRotationMode.None, false, 360f);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureStrategy(string path, Texture2D tex, float opacity, float spacing, BlendOp op, BrushRotationMode rot, bool runtime = false, float jitter = 0f)
        {
            BrushStrategy strategy = AssetDatabase.LoadAssetAtPath<BrushStrategy>(path);
            if (strategy == null)
            {
                strategy = ScriptableObject.CreateInstance<BrushStrategy>();
                AssetDatabase.CreateAsset(strategy, path);
            }

            strategy.MainTexture = tex;
            strategy.Opacity = opacity;
            strategy.SpacingRatio = spacing;
            strategy.BlendOp = op;
            strategy.RotationMode = rot;
            strategy.UseRuntimeGeneration = runtime;
            strategy.AngleJitter = jitter;
            
            strategy.SrcBlend = BlendMode.One; 
            strategy.DstBlend = BlendMode.OneMinusSrcAlpha;

            EditorUtility.SetDirty(strategy);
        }

        private static void ConfigureTextureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // Ensure high quality for brushes
                importer.SaveAndReimport();
            }
        }

        private static void CreateToolbarUI(Canvas canvas, Features.Drawing.App.DrawingAppService appService)
        {
            // Clean up existing
            Transform existing = canvas.transform.Find("DrawingUI_Root");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            // Root Container
            GameObject root = new GameObject("DrawingUI_Root");
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            // --- Top Bar (Undo / Clear) ---
            GameObject topBar = new GameObject("TopBar");
            topBar.transform.SetParent(root.transform, false);
            RectTransform topRect = topBar.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.pivot = new Vector2(0.5f, 1);
            topRect.sizeDelta = new Vector2(0, TOP_BAR_HEIGHT);
            topRect.anchoredPosition = Vector2.zero;

            // Top Buttons Container (Right Aligned)
            HorizontalLayoutGroup topLayout = topBar.AddComponent<HorizontalLayoutGroup>();
            topLayout.childAlignment = TextAnchor.MiddleRight;
            topLayout.padding = new RectOffset(20, 20, 10, 10);
            topLayout.spacing = 30;

            var btnUndo = CreateTextBtn(topBar, "Btn_Undo", "Undo");
            var btnClear = CreateTextBtn(topBar, "Btn_Clear", "Clear");

            // --- Sub Panels Container (Above Bottom Bar) ---
            GameObject panelContainer = new GameObject("PanelContainer");
            panelContainer.transform.SetParent(root.transform, false);
            RectTransform panelRect = panelContainer.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, NAV_HEIGHT); // Above taller nav bar
            panelRect.sizeDelta = new Vector2(0, PANEL_HEIGHT); 

            Image panelBg = panelContainer.AddComponent<Image>();
            panelBg.color = new Color(1, 1, 1, 0.95f); // Almost opaque

            // 1. Brush Panel
            GameObject brushPanel = CreateSubPanel(panelContainer, "BrushPanel");
            HorizontalLayoutGroup brushLayout = brushPanel.AddComponent<HorizontalLayoutGroup>();
            brushLayout.childAlignment = TextAnchor.MiddleCenter;
            brushLayout.spacing = 50;
            
            Button btnSoft = CreateIconBtn(brushPanel, "Type_Soft", Color.gray, "Soft");
            Button btnHard = CreateIconBtn(brushPanel, "Type_Hard", Color.black, "Hard");
            Button btnMarker = CreateIconBtn(brushPanel, "Type_Marker", Color.blue, "Marker");
            Button btnPencil = CreateIconBtn(brushPanel, "Type_Pencil", Color.red, "Pencil");

            // 2. Size Panel
            GameObject sizePanel = CreateSubPanel(panelContainer, "SizePanel");
            HorizontalLayoutGroup sizeLayout = sizePanel.AddComponent<HorizontalLayoutGroup>();
            sizeLayout.childAlignment = TextAnchor.MiddleCenter;
            sizeLayout.spacing = 60;
            
            Button[] sizeBtns = new Button[5];
            for(int i=0; i<5; i++) sizeBtns[i] = CreateDotBtn(sizePanel, $"Size_{i}", 30 + i*20, Color.black);

            // 3. Color Panel
            GameObject colorPanel = CreateSubPanel(panelContainer, "ColorPanel");
            HorizontalLayoutGroup colorLayout = colorPanel.AddComponent<HorizontalLayoutGroup>();
            colorLayout.childAlignment = TextAnchor.MiddleCenter;
            colorLayout.spacing = 30;
            
            Color[] palette = { Color.black, Color.red, new Color(1f, 0.8f, 0f), new Color(0.2f, 1f, 0.2f), new Color(0f, 0.8f, 1f), Color.blue, new Color(0.6f, 0f, 1f), new Color(1f, 0f, 0.5f) };
            Button[] colorBtns = new Button[palette.Length];
            for(int i=0; i<palette.Length; i++) colorBtns[i] = CreateDotBtn(colorPanel, $"Col_{i}", 80, palette[i]);

            // --- Bottom Navigation Bar ---
            GameObject navBar = new GameObject("NavBar");
            navBar.transform.SetParent(root.transform, false);
            RectTransform navRect = navBar.AddComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0, 0);
            navRect.anchorMax = new Vector2(1, 0);
            navRect.pivot = new Vector2(0.5f, 0);
            navRect.sizeDelta = new Vector2(0, NAV_HEIGHT); // Height increased

            Image navBg = navBar.AddComponent<Image>();
            navBg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            
            // Add shadow/border to top of navbar if possible, or just simple color
            
            HorizontalLayoutGroup navLayout = navBar.AddComponent<HorizontalLayoutGroup>();
            navLayout.childControlWidth = true;
            navLayout.childForceExpandWidth = true;
            navLayout.padding = new RectOffset(20, 20, 10, 10);
            
            var tabBrush = CreateNavTab(navBar, "Tab_Brush", "Brush");
            var tabEraser = CreateNavTab(navBar, "Tab_Eraser", "Eraser");
            var tabSize = CreateNavTab(navBar, "Tab_Size", "Size");
            var tabColor = CreateNavTab(navBar, "Tab_Color", "Color");

            // --- Connect Script ---
            var toolbarScript = root.AddComponent<Features.Drawing.Presentation.UI.DrawingToolbar>();
            SerializedObject so = new SerializedObject(toolbarScript);
            
            so.FindProperty("_appService").objectReferenceValue = appService;
            
            so.FindProperty("_btnUndo").objectReferenceValue = btnUndo;
            so.FindProperty("_btnClear").objectReferenceValue = btnClear;

            so.FindProperty("_tabBrush").objectReferenceValue = tabBrush;
            so.FindProperty("_tabEraser").objectReferenceValue = tabEraser;
            so.FindProperty("_tabSize").objectReferenceValue = tabSize;
            so.FindProperty("_tabColor").objectReferenceValue = tabColor;

            so.FindProperty("_panelContainer").objectReferenceValue = panelContainer;
            so.FindProperty("_panelBrush").objectReferenceValue = brushPanel;
            so.FindProperty("_panelSize").objectReferenceValue = sizePanel;
            so.FindProperty("_panelColor").objectReferenceValue = colorPanel;

            so.FindProperty("_btnTypeSoft").objectReferenceValue = btnSoft;
            so.FindProperty("_btnTypeHard").objectReferenceValue = btnHard;
            so.FindProperty("_btnTypeMarker").objectReferenceValue = btnMarker;
            so.FindProperty("_btnTypePencil").objectReferenceValue = btnPencil;

            SerializedProperty spSizeBtns = so.FindProperty("_sizeButtons");
            spSizeBtns.arraySize = 5;
            for(int i=0; i<5; i++) spSizeBtns.GetArrayElementAtIndex(i).objectReferenceValue = sizeBtns[i];

            SerializedProperty spColorBtns = so.FindProperty("_colorButtons");
            spColorBtns.arraySize = palette.Length;
            for(int i=0; i<palette.Length; i++) spColorBtns.GetArrayElementAtIndex(i).objectReferenceValue = colorBtns[i];

            string softPath = "Assets/Scripts/Features/Drawing/Domain/SoftBrush.asset";
            string hardPath = "Assets/Scripts/Features/Drawing/Domain/HardBrush.asset";
            string markerPath = "Assets/Scripts/Features/Drawing/Domain/MarkerBrush.asset";
            string pencilPath = "Assets/Scripts/Features/Drawing/Domain/PencilBrush.asset";

            so.FindProperty("_softBrushStrategy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Features.Drawing.Domain.BrushStrategy>(softPath);
            so.FindProperty("_hardBrushStrategy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Features.Drawing.Domain.BrushStrategy>(hardPath);
            so.FindProperty("_markerBrushStrategy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Features.Drawing.Domain.BrushStrategy>(markerPath);
            so.FindProperty("_pencilBrushStrategy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Features.Drawing.Domain.BrushStrategy>(pencilPath);

            so.ApplyModifiedProperties();
        }

        private static GameObject CreateSubPanel(GameObject parent, string name)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform, false);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.SetActive(false);
            return panel;
        }

        private static TMP_FontAsset GetTMPFont()
        {
            // Try loading default TMP font
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null)
            {
                // Fallback: Try finding any TMP font in the project
                string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                }
            }
            return font;
        }

        private static Button CreateNavTab(GameObject parent, string name, string text)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            
            Image img = btnObj.AddComponent<Image>();
            img.color = Color.clear; // Transparent hit area

            Button btn = btnObj.AddComponent<Button>();
            
            // Icon Placeholder (Circle)
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(80, 80); // Bigger Icon
            iconRect.anchoredPosition = new Vector2(0, 20);
            
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            iconImg.color = Color.gray;

            // Text Label
            GameObject label = new GameObject("Label");
            label.transform.SetParent(btnObj.transform, false);
            RectTransform lRect = label.AddComponent<RectTransform>();
            lRect.sizeDelta = new Vector2(150, 60); // Increased height to prevent clipping
            lRect.anchoredPosition = new Vector2(0, -40);
            
            TextMeshProUGUI txt = label.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.font = GetTMPFont();
            txt.color = Color.black;
            txt.alignment = TextAlignmentOptions.Center;
            txt.overflowMode = TextOverflowModes.Overflow; // Ensure text shows even if bounds are tight
            txt.fontSize = 36; // Bigger Text (was 24)

            return btn;
        }

        private static Button CreateIconBtn(GameObject parent, string name, Color color, string text)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 120); // Bigger Button (was 100)

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f); // Bg

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject label = new GameObject("Label");
            label.transform.SetParent(btnObj.transform, false);
            RectTransform labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero; // Ensure it stretches
            
            TextMeshProUGUI txt = label.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.font = GetTMPFont();
            txt.color = Color.black;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 28; // Bigger Text (was 18)
            
            return btn;
        }

        private static Button CreateDotBtn(GameObject parent, string name, float diameter, Color color)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 100); // Touch target

            Button btn = btnObj.AddComponent<Button>();

            GameObject dot = new GameObject("Dot");
            dot.transform.SetParent(btnObj.transform, false);
            RectTransform dotRect = dot.AddComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(diameter, diameter);
            
            Image dotImg = dot.AddComponent<Image>();
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
            rect.sizeDelta = new Vector2(120, 60); // Bigger Top Buttons
            
            Image bg = btnObj.AddComponent<Image>();
            bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            
            Button btn = btnObj.AddComponent<Button>();
            
            GameObject label = new GameObject("Label");
            label.transform.SetParent(btnObj.transform, false);
            RectTransform lRect = label.AddComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.offsetMin = Vector2.zero;
            lRect.offsetMax = Vector2.zero; // Ensure it stretches
            
            TextMeshProUGUI txt = label.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.font = GetTMPFont();
            txt.color = Color.black;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 20;
            
            return btn;
        }

        private static Texture2D GeneratePencilTexture()
        {
            int size = 64; 
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;
            
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
                    float alpha = 1.0f - Mathf.SmoothStep(0.5f, 1.0f, normalizedDist);
                    float noise = Mathf.PerlinNoise(x / 5f + offsetX, y / 5f + offsetY);
                    alpha *= Mathf.Lerp(0.2f, 1.0f, noise);
                    
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
            int size = 256; // Increased resolution for better quality
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 4f; // Slight padding

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    
                    // Simple Hard Edge
                    // If inside radius, Alpha = 1. Else Alpha = 0.
                    // We add a tiny anti-aliasing rim (1-2 pixels) to look decent but stay "hard".
                    float alpha = 0f;
                    
                    if (dist < radius - 1.0f)
                    {
                        alpha = 1.0f;
                    }
                    else if (dist < radius + 1.0f)
                    {
                        // Anti-aliasing edge
                        alpha = 1.0f - Mathf.InverseLerp(radius - 1.0f, radius + 1.0f, dist);
                    }
                    
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateMarkerTexture()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            
            float noiseScale = 20f;
            float offsetX = Random.Range(0f, 100f);
            float offsetY = Random.Range(0f, 100f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center.x) / (size / 2f);
                    float dy = Mathf.Abs(y - center.y) / (size / 2f);
                    float dist = Mathf.Max(dx, dy); 
                    float noise = Mathf.PerlinNoise(x / noiseScale + offsetX, y / noiseScale + offsetY);
                    float alpha = 1.0f - Mathf.SmoothStep(0.7f, 1.0f, dist); 
                    alpha *= Mathf.Lerp(0.5f, 1.0f, noise); 
                    
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
