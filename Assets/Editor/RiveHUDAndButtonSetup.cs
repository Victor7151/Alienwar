using System.IO;
using File = System.IO.File;
using Directory = System.IO.Directory;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using Rive;
using Rive.Components;
using Starfall.UI;

namespace Starfall.Editor
{
    [InitializeOnLoad]
    public static class RiveHUDAndButtonSetup
    {
        public const string HealthRivPath = "Assets/new rive/alienwarhud.riv";
        public const string ButtonRivPath = "Assets/new rive/specialbutton.riv";
        public const string HomeRivPath = "Assets/new rive/home_hangar.riv";
        public const string HealthResPath = "Assets/Resources/Rive/alienwarhud.riv";
        public const string ButtonResPath = "Assets/Resources/Rive/specialbutton.riv";
        public const string HomeResPath = "Assets/Resources/Rive/home_hangar.riv";

        static RiveHUDAndButtonSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EnsureResourcesMirrored();
                }
            };
        }

        [MenuItem("STARFALL/Rive/0. Setup Rive HUD & Button in HudTest Scene", priority = 10)]
        public static void SetupHudTestScene()
        {
            EnsureResourcesMirrored();

            string scenePath = "Assets/Scenes/HudTest.unity";
            UnityEngine.SceneManagement.Scene testScene;

            if (File.Exists(scenePath))
            {
                testScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            else
            {
                testScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(testScene, scenePath);
                Debug.Log($"[RiveSetup] Created new scene '{scenePath}'.");
            }

            SetupRiveInScene(testScene, isTestScene: true);
            EditorSceneManager.SaveScene(testScene);
            Debug.Log("<color=#00ff88><b>[RiveSetup] HudTest scene configured and saved successfully!</b></color>");
        }

        [MenuItem("STARFALL/Rive/1. Setup Rive HUD & Button In All Levels", priority = 11)]
        public static void SetupAllLevels()
        {
            EnsureResourcesMirrored();

            string[] levelScenes = new string[]
            {
                "Assets/Scenes/HudTest.unity",
                "Assets/Scenes/Level1_EarthOrbit.unity",
                "Assets/Scenes/Level2_AsteroidBelt.unity",
                "Assets/Scenes/Level3_DeepNebula.unity",
                "Assets/Scenes/Level4_WarTornWreckage.unity"
            };

            foreach (var scenePath in levelScenes)
            {
                if (!File.Exists(scenePath))
                {
                    if (scenePath.EndsWith("HudTest.unity"))
                    {
                        var createdScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                        EditorSceneManager.SaveScene(createdScene, scenePath);
                    }
                    else
                    {
                        continue;
                    }
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                SetupRiveInScene(scene, isTestScene: scenePath.Contains("HudTest"));
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[RiveSetup] Successfully updated and saved scene: '{scene.name}'.");
            }

            Debug.Log("<color=#00ff88><b>[RiveSetup] All Level scenes configured with Rive HUD & Special Button!</b></color>");
        }

        private static void EnsureResourcesMirrored()
        {
            if (!Directory.Exists("Assets/Resources/Rive"))
            {
                Directory.CreateDirectory("Assets/Resources/Rive");
            }

            MirrorRivFile(HealthRivPath, HealthResPath);
            MirrorRivFile(ButtonRivPath, ButtonResPath);
            MirrorRivFile(HomeRivPath, HomeResPath);
        }

        private static void MirrorRivFile(string src, string dest)
        {
            if (File.Exists(src) && !File.Exists(dest))
            {
                File.Copy(src, dest, true);
                if (File.Exists(src + ".meta"))
                {
                    File.WriteAllText(dest + ".meta", File.ReadAllText(src + ".meta"));
                }
                AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[RiveSetup] Mirrored '{src}' -> '{dest}'");
            }
        }

        public static void SetupRiveInScene(UnityEngine.SceneManagement.Scene scene, bool isTestScene)
        {
            // 1. Ensure EventSystem exists
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Debug.Log($"[RiveSetup] Created EventSystem in scene '{scene.name}'.");
            }

            // 2. Locate or create Canvas (1080x1920)
            Canvas canvas = null;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                if (c.name.Contains("HUD") || c.name.Contains("Canvas") || c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas = c;
                    break;
                }
            }

            if (canvas == null)
            {
                var canvasGo = new GameObject("HUDCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
                Debug.Log($"[RiveSetup] Created HUDCanvas in scene '{scene.name}'.");
            }
            else
            {
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    scaler.referenceResolution = new Vector2(1080f, 1920f);
                }
            }

            // 3. Locate or create dedicated RivePanels under Canvas for HealthHUD and SpecialButton
            // Keeping them on separate panels ensures their RenderTextures and visibility/alphas are isolated.
            RivePanel GetOrCreatePanel(string panelName, PointerInputMode inputMode, float defaultAlpha = 1f)
            {
                RivePanel panel = null;
                var child = canvas.transform.Find(panelName);
                if (child != null) panel = child.GetComponent<RivePanel>();

                if (panel == null)
                {
                    var panelGo = new GameObject(panelName);
                    panelGo.transform.SetParent(canvas.transform, false);

                    var panelRect = panelGo.AddComponent<RectTransform>();
                    panelRect.anchorMin = Vector2.zero;
                    panelRect.anchorMax = Vector2.one;
                    panelRect.offsetMin = Vector2.zero;
                    panelRect.offsetMax = Vector2.zero;

                    panel = panelGo.AddComponent<RivePanel>();
                    var rcr = panelGo.AddComponent<RiveCanvasRenderer>();
                    rcr.PointerInputMode = inputMode;
                    var cg = panelGo.AddComponent<CanvasGroup>();
                    cg.alpha = defaultAlpha;
                    if (defaultAlpha == 0f)
                    {
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                    }
                }
                else
                {
                    var rcr = panel.GetComponent<RiveCanvasRenderer>() ?? panel.gameObject.AddComponent<RiveCanvasRenderer>();
                    rcr.PointerInputMode = inputMode;
                    var cg = panel.GetComponent<CanvasGroup>() ?? panel.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = defaultAlpha;
                    if (defaultAlpha == 0f)
                    {
                        cg.interactable = false;
                        cg.blocksRaycasts = false;
                    }
                }

                var panelSo = new SerializedObject(panel);
                var multiTouchProp = panelSo.FindProperty("m_multiTouchSupport");
                if (multiTouchProp != null) multiTouchProp.enumValueIndex = 1;
                panelSo.ApplyModifiedProperties();

                return panel;
            }

            var healthPanel = GetOrCreatePanel("Rive Panel - HealthHUD", PointerInputMode.DisablePointerInput);
            var buttonPanel = GetOrCreatePanel("Rive Panel - SpecialButton", PointerInputMode.EnablePointerInput);

            // Clean up any old combined "Rive Panel" or "Rive Panel (Canvas)" if empty
            var oldPanel = canvas.transform.Find("Rive Panel (Canvas)") ?? canvas.transform.Find("Rive Panel");
            if (oldPanel != null && oldPanel.name != "Rive Panel - HealthHUD" && oldPanel.name != "Rive Panel - SpecialButton")
            {
                // Move any existing widgets out if needed
                foreach (Transform child in oldPanel)
                {
                    if (child.name.Contains("Health") || child.name.Contains("alienwarhud"))
                        child.SetParent(healthPanel.transform, false);
                    else if (child.name.Contains("Special") || child.name.Contains("Button"))
                        child.SetParent(buttonPanel.transform, false);
                }
                if (oldPanel.childCount == 0) Object.DestroyImmediate(oldPanel.gameObject);
            }

            // Load Rive Assets
            var healthAsset = AssetDatabase.LoadAssetAtPath<Asset>(HealthRivPath) 
                           ?? AssetDatabase.LoadAssetAtPath<Asset>(HealthResPath);
            var buttonAsset = AssetDatabase.LoadAssetAtPath<Asset>(ButtonRivPath) 
                           ?? AssetDatabase.LoadAssetAtPath<Asset>(ButtonResPath);

            const string healthArtboard = "HealthHUD";
            const string healthSM = "Statemachine 1";
            const string buttonArtboard = "SpecialButton";
            const string buttonSM = "ButtonSM";

            // 4. Setup HealthHUD RiveWidget under healthPanel
            RiveWidget healthWidget = healthPanel.GetComponentInChildren<RiveWidget>(true);
            if (healthWidget == null)
            {
                var healthGo = new GameObject("RiveWidget_HealthHUD");
                healthGo.transform.SetParent(healthPanel.transform, false);
                healthWidget = healthGo.AddComponent<RiveWidget>();
            }

            var healthRect = healthWidget.GetComponent<RectTransform>();
            healthRect.anchorMin = new Vector2(0f, 1f);
            healthRect.anchorMax = new Vector2(0f, 1f);
            healthRect.pivot = new Vector2(0f, 1f);
            healthRect.anchoredPosition = new Vector2(25f, -25f);
            healthRect.sizeDelta = new Vector2(420f, 206f);

            var healthSo = new SerializedObject(healthWidget);
            healthSo.FindProperty("m_rivePanel").objectReferenceValue = healthPanel;
            if (healthAsset != null) healthSo.FindProperty("m_asset").objectReferenceValue = healthAsset;
            healthSo.FindProperty("m_artboardName").stringValue = healthArtboard;
            healthSo.FindProperty("m_stateMachineName").stringValue = healthSM;
            healthSo.FindProperty("m_fit").enumValueIndex = (int)Fit.Contain;
            healthSo.FindProperty("m_hitTestBehavior").enumValueIndex = (int)HitTestBehavior.None; // None: Don't block touches!
            healthSo.FindProperty("m_dataBindingMode").enumValueIndex = (int)RiveWidget.DataBindingMode.AutoBindDefault;
            healthSo.ApplyModifiedProperties();

            var healthHud = healthWidget.GetComponent<RiveHealthHUD>();
            if (healthHud == null) healthHud = healthWidget.gameObject.AddComponent<RiveHealthHUD>();
            healthHud.widget = healthWidget;
            healthHud.autoPositionAndScale = true;
            healthHud.hudPosition = new Vector2(25f, -25f);
            healthHud.hudSize = new Vector2(420f, 206f);

            // 5. Setup SpecialButton RiveWidget under buttonPanel
            RiveWidget buttonWidget = buttonPanel.GetComponentInChildren<RiveWidget>(true);
            if (buttonWidget == null)
            {
                var buttonGo = new GameObject("RiveWidget_SpecialButton");
                buttonGo.transform.SetParent(buttonPanel.transform, false);
                buttonWidget = buttonGo.AddComponent<RiveWidget>();
            }

            var buttonRect = buttonWidget.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-150f, 160f);
            buttonRect.sizeDelta = new Vector2(250f, 250f);

            var buttonSo = new SerializedObject(buttonWidget);
            buttonSo.FindProperty("m_rivePanel").objectReferenceValue = buttonPanel;
            if (buttonAsset != null) buttonSo.FindProperty("m_asset").objectReferenceValue = buttonAsset;
            buttonSo.FindProperty("m_artboardName").stringValue = buttonArtboard;
            buttonSo.FindProperty("m_stateMachineName").stringValue = buttonSM;
            buttonSo.FindProperty("m_fit").enumValueIndex = (int)Fit.Contain;
            buttonSo.FindProperty("m_hitTestBehavior").enumValueIndex = (int)HitTestBehavior.Opaque; // Opaque: receives touches
            buttonSo.FindProperty("m_dataBindingMode").enumValueIndex = (int)RiveWidget.DataBindingMode.AutoBindDefault;
            buttonSo.ApplyModifiedProperties();

            var specialButton = buttonWidget.GetComponent<RiveSpecialButton>();
            if (specialButton == null) specialButton = buttonWidget.gameObject.AddComponent<RiveSpecialButton>();
            specialButton.widget = buttonWidget;
            specialButton.autoPositionAndScale = true;
            specialButton.buttonPosition = new Vector2(-150f, 160f);
            specialButton.buttonSize = new Vector2(250f, 250f);
            specialButton.CooldownDuration = 8f;

            // Remove any legacy WarningBanner panel if present
            var legacyWarningPanel = canvas.transform.Find("Rive Panel - WarningBanner");
            if (legacyWarningPanel != null)
            {
                Object.DestroyImmediate(legacyWarningPanel.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("STARFALL/Rive/3. Setup Rive Home Bridge in HomeScene", priority = 12)]
        public static void SetupHomeSceneBridge()
        {
            EnsureResourcesMirrored();

            string scenePath = "Assets/Scenes/HomeScene.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[RiveSetup] HomeScene not found at '{scenePath}'.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SetupRiveHomeInScene(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=#00ff88><b>[RiveSetup] HomeScene configured with Rive Panel, RiveWidget & RiveHomeBridge!</b></color>");
        }

        public static void SetupRiveHomeInScene(UnityEngine.SceneManagement.Scene scene)
        {
            // 1. Ensure EventSystem exists
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log($"[RiveSetup] Created EventSystem in scene '{scene.name}'.");
            }

            // 2. Locate or create Canvas (1920x1080 Landscape) for Rive Background / Hub
            Canvas canvas = null;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                if (c.name == "Rive_Home_Canvas" || c.name.Contains("Rive"))
                {
                    canvas = c;
                    break;
                }
            }

            if (canvas == null)
            {
                var canvasGo = new GameObject("Rive_Home_Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10; // Under procedural UI (which is order 50)

                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // 3. Setup Rive Panel - HomeHub
            RivePanel homePanel = canvas.GetComponentInChildren<RivePanel>(true);
            if (homePanel == null)
            {
                var panelGo = new GameObject("Rive Panel - HomeHub");
                panelGo.transform.SetParent(canvas.transform, false);

                var panelRect = panelGo.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;

                homePanel = panelGo.AddComponent<RivePanel>();
                var rcr = panelGo.AddComponent<RiveCanvasRenderer>();
                rcr.PointerInputMode = PointerInputMode.EnablePointerInput;
                var cg = panelGo.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
            }
            else
            {
                var rcr = homePanel.GetComponent<RiveCanvasRenderer>() ?? homePanel.gameObject.AddComponent<RiveCanvasRenderer>();
                rcr.PointerInputMode = PointerInputMode.EnablePointerInput;
            }

            var panelSo = new SerializedObject(homePanel);
            var multiTouchProp = panelSo.FindProperty("m_multiTouchSupport");
            if (multiTouchProp != null) multiTouchProp.enumValueIndex = 1;
            panelSo.ApplyModifiedProperties();

            // 4. Setup RiveWidget_HomeHangar
            RiveWidget homeWidget = homePanel.GetComponentInChildren<RiveWidget>(true);
            if (homeWidget == null)
            {
                var widgetGo = new GameObject("RiveWidget_HomeHangar");
                widgetGo.transform.SetParent(homePanel.transform, false);
                homeWidget = widgetGo.AddComponent<RiveWidget>();
            }

            var widgetRect = homeWidget.GetComponent<RectTransform>();
            widgetRect.anchorMin = Vector2.zero;
            widgetRect.anchorMax = Vector2.one;
            widgetRect.offsetMin = Vector2.zero;
            widgetRect.offsetMax = Vector2.zero;

            // Load home asset if available
            var homeAsset = AssetDatabase.LoadAssetAtPath<Asset>(HomeRivPath)
                         ?? AssetDatabase.LoadAssetAtPath<Asset>(HomeResPath);

            var widgetSo = new SerializedObject(homeWidget);
            widgetSo.FindProperty("m_rivePanel").objectReferenceValue = homePanel;
            if (homeAsset != null) widgetSo.FindProperty("m_asset").objectReferenceValue = homeAsset;
            widgetSo.FindProperty("m_artboardName").stringValue = "home_hangar";
            widgetSo.FindProperty("m_stateMachineName").stringValue = "Home_SM";
            widgetSo.FindProperty("m_fit").enumValueIndex = (int)Fit.Cover;
            widgetSo.FindProperty("m_hitTestBehavior").enumValueIndex = (int)HitTestBehavior.Opaque;
            widgetSo.FindProperty("m_dataBindingMode").enumValueIndex = (int)RiveWidget.DataBindingMode.AutoBindDefault;
            widgetSo.ApplyModifiedProperties();

            // 5. Setup RiveHomeBridge
            var bridge = homeWidget.GetComponent<RiveHomeBridge>();
            if (bridge == null) bridge = homeWidget.gameObject.AddComponent<RiveHomeBridge>();
            bridge.widget = homeWidget;
            bridge.riveAsset = homeAsset;
            bridge.artboardName = "home_hangar";
            bridge.stateMachineName = "Home_SM";
            bridge.keepProceduralUIOverlay = true;
            bridge.enableParallax = true;

            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
