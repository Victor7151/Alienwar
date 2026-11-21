using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

public static class HUDSetup
{
    [MenuItem("STARFALL/Setup HUD")]
    public static void SetupHUD()
    {
        // --- Canvas ---
        var hudGo = GameObject.Find("HUDCanvas");
        if (hudGo == null)
        {
            hudGo = new GameObject("HUDCanvas");
        }

        var canvas = hudGo.GetComponent<Canvas>();
        if (canvas == null) canvas = hudGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = hudGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = hudGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        if (hudGo.GetComponent<GraphicRaycaster>() == null)
            hudGo.AddComponent<GraphicRaycaster>();

        if (hudGo.GetComponent<Starfall.UI.GameplayHUD>() == null)
            hudGo.AddComponent<Starfall.UI.GameplayHUD>();

        // --- Score Text (top-center) ---
        var scoreGo = MakeText(hudGo, "ScoreText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -80), new Vector2(600, 80), 48, TextAlignmentOptions.Center);
        scoreGo.GetComponent<TextMeshProUGUI>().text = "SCORE: 0";

        // --- Shield Text (top-left) ---
        var shieldGo = MakeText(hudGo, "ShieldText", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(30, -80), new Vector2(500, 60), 36, TextAlignmentOptions.Left);
        shieldGo.GetComponent<TextMeshProUGUI>().text = "SHIELD: ||||||";
        shieldGo.GetComponent<TextMeshProUGUI>().color = new Color(0f, 0.9f, 1f);

        // --- Hull Text (top-left, below shield) ---
        var hullGo = MakeText(hudGo, "HullText", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(30, -150), new Vector2(500, 60), 36, TextAlignmentOptions.Left);
        hullGo.GetComponent<TextMeshProUGUI>().text = "HULL: ||||||";
        hullGo.GetComponent<TextMeshProUGUI>().color = new Color(0f, 1f, 0.4f);

        // --- Game Over Panel ---
        var panelGo = new GameObject("GameOverPanel");
        panelGo.transform.SetParent(hudGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.75f);
        panelGo.SetActive(false);

        // Final Score inside panel
        var finalScore = MakeText(panelGo, "FinalScoreText", new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
            Vector2.zero, new Vector2(800, 100), 64, TextAlignmentOptions.Center);
        finalScore.GetComponent<TextMeshProUGUI>().text = "FINAL SCORE: 0";
        finalScore.GetComponent<TextMeshProUGUI>().color = Color.white;

        // Restart Button inside panel
        var btnGo = new GameObject("RestartButton");
        btnGo.transform.SetParent(panelGo.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.35f);
        btnRect.anchorMax = new Vector2(0.5f, 0.35f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(400, 100);
        btnRect.anchoredPosition = Vector2.zero;
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.08f, 0.55f, 0.87f);
        var btn = btnGo.AddComponent<Button>();

        var btnTextGo = MakeText(btnGo, "ButtonText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(380, 90), 40, TextAlignmentOptions.Center);
        btnTextGo.GetComponent<TextMeshProUGUI>().text = "RESTART";
        btnTextGo.GetComponent<TextMeshProUGUI>().color = Color.white;

        // Wire button -> HUD.OnRestartClicked
        var hud = hudGo.GetComponent<Starfall.UI.GameplayHUD>();
        btn.onClick.AddListener(hud.OnRestartClicked);

        // --- EventSystem ---
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // Wire SerializedObject fields
        var so = new SerializedObject(hud);
        so.FindProperty("scoreText").objectReferenceValue = scoreGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("shieldText").objectReferenceValue = shieldGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("hullText").objectReferenceValue = hullGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("gameOverPanel").objectReferenceValue = panelGo;
        so.FindProperty("finalScoreText").objectReferenceValue = finalScore.GetComponent<TextMeshProUGUI>();
        so.FindProperty("restartButton").objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[HUDSetup] HUD built and wired successfully!");
    }

    private static GameObject MakeText(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos,
        Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        return go;
    }
}
