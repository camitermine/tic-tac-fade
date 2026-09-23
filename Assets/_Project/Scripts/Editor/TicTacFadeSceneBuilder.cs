using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TicTacFade.Game;
using TicTacFade.UI;

namespace TicTacFade.EditorTools
{
    /// <summary>
    /// Builds the playable Tic-Tac-Fade scene with placeholders (no sprites/final art).
    /// Usage: menu "Tic-Tac-Fade > Build Placeholder Scene (1v1)".
    /// </summary>
    public static class TicTacFadeSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/TicTacFadeGame.unity";
        const string GameConfigAssetPath = "Assets/_Project/Config/GameConfig.asset";

        [MenuItem("Tic-Tac-Fade/Build Placeholder Scene (1v1)")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera();

            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemGO.AddComponent<StandaloneInputModule>();
#endif

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var boardPanel = CreateBoardPanel(canvasGO.transform, out var cellViews);
            var hud = CreateHud(canvasGO.transform);
            var winBanner = CreateWinBanner(canvasGO.transform, out var winLabel, out var restartButton);

            var boardView = boardPanel.gameObject.AddComponent<BoardView>();
            SetPrivateField(boardView, "cells", cellViews);

            var gameHud = hud.gameObject.AddComponent<GameHud>();
            SetPrivateField(gameHud, "turnLabel", hud.Find("TurnLabel").GetComponent<Text>());
            SetPrivateField(gameHud, "countLabelX", hud.Find("CountsRow/CountX").GetComponent<Text>());
            SetPrivateField(gameHud, "countLabelO", hud.Find("CountsRow/CountO").GetComponent<Text>());
            SetPrivateField(gameHud, "fadeWarningLabel", hud.Find("FadeWarning").GetComponent<Text>());
            SetPrivateField(gameHud, "gameEndedBanner", winBanner.gameObject);
            SetPrivateField(gameHud, "gameEndedLabel", winLabel);

            var gameManagerGO = new GameObject("GameManager");
            var gameManager = gameManagerGO.AddComponent<GameManager>();
            SetPrivateField(gameManager, "config", GetOrCreateGameConfigAsset());
            SetPrivateField(boardView, "gameManager", gameManager);
            SetPrivateField(gameHud, "gameManager", gameManager);

            restartButton.SetGameManager(gameManager);

            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = EditorBuildSettings.scenes;
            bool alreadyInBuild = System.Array.Exists(buildScenes, s => s.path == ScenePath);
            if (!alreadyInBuild)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(buildScenes)
                {
                    new EditorBuildSettingsScene(ScenePath, true)
                };
                EditorBuildSettings.scenes = list.ToArray();
            }

            Debug.Log($"Tic-Tac-Fade: placeholder scene created at {ScenePath}. You can hit Play.");
        }

        static GameConfigAsset GetOrCreateGameConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(GameConfigAssetPath);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Config"))
                AssetDatabase.CreateFolder("Assets/_Project", "Config");

            var asset = ScriptableObject.CreateInstance<GameConfigAsset>();
            AssetDatabase.CreateAsset(asset, GameConfigAssetPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void CreateMainCamera()
        {
            var cameraGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGO.tag = "MainCamera";

            var camera = cameraGO.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f); // near-black, keeps the neon cell colors readable

            // The Canvas is ScreenSpaceOverlay, so this camera isn't required to render
            // the UI, but its absence is exactly what produces "No cameras rendering"
            // and the black Game view background.
            if (cameraGO.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGO.AddComponent<UniversalAdditionalCameraData>();
        }

        static RectTransform CreateBoardPanel(Transform parent, out CellView[] cells)
        {
            var panelRT = CreateUIObject("BoardPanel", parent, typeof(GridLayoutGroup));
            panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = new Vector2(0f, 60f);
            panelRT.sizeDelta = new Vector2(3 * 150f + 2 * 10f, 3 * 150f + 2 * 10f);

            var grid = panelRT.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150f, 150f);
            grid.spacing = new Vector2(10f, 10f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            cells = new CellView[9];
            for (int i = 0; i < 9; i++)
                cells[i] = CreateCell(panelRT, i);

            return panelRT;
        }

        static CellView CreateCell(Transform parent, int index)
        {
            var rt = CreateUIObject($"Cell_{index}", parent, typeof(Image), typeof(Button));
            var background = rt.GetComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            background.color = new Color(1f, 1f, 1f, 0.06f);

            var labelRT = CreateUIObject("Label", rt, typeof(Text));
            StretchFull(labelRT);
            var label = labelRT.GetComponent<Text>();
            ConfigureText(label, 72, TextAnchor.MiddleCenter, Color.white);

            var criticalRT = CreateUIObject("CriticalMark", rt, typeof(Image));
            criticalRT.anchorMin = criticalRT.anchorMax = new Vector2(1f, 1f);
            criticalRT.pivot = new Vector2(1f, 1f);
            criticalRT.sizeDelta = new Vector2(30f, 30f);
            criticalRT.anchoredPosition = new Vector2(-8f, -8f);
            var criticalMark = criticalRT.GetComponent<Image>();
            criticalMark.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            criticalMark.type = Image.Type.Sliced;
            criticalMark.color = Color.white; // real color (tinted by owner) set by CellView.SetOccupant()
            criticalMark.raycastTarget = false;
            criticalMark.enabled = false;

            var ghostRT = CreateUIObject("GhostOverlay", rt, typeof(Image));
            StretchFull(ghostRT);
            var ghostOverlay = ghostRT.GetComponent<Image>();
            ghostOverlay.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            ghostOverlay.type = Image.Type.Sliced;
            ghostOverlay.color = new Color(1f, 1f, 1f, 0f); // real color/alpha set by ShowGhost()
            ghostOverlay.raycastTarget = false;
            ghostOverlay.enabled = false;

            var highlightRT = CreateUIObject("Highlight", rt, typeof(Image));
            StretchFull(highlightRT);
            var highlight = highlightRT.GetComponent<Image>();
            highlight.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            highlight.type = Image.Type.Sliced;
            highlight.color = new Color(1f, 0.92f, 0.2f, 0.9f);
            highlight.raycastTarget = false;
            highlight.enabled = false;

            var cellView = rt.gameObject.AddComponent<CellView>();
            SetPrivateField(cellView, "label", label);
            SetPrivateField(cellView, "background", background);
            SetPrivateField(cellView, "highlightBorder", highlight);
            SetPrivateField(cellView, "criticalMark", criticalMark);
            SetPrivateField(cellView, "ghostOverlay", ghostOverlay);
            return cellView;
        }

        static Transform CreateHud(Transform parent)
        {
            // Wrapping the HUD in its own SafeArea container keeps the turn
            // label and counters clear of notches/cutouts and the Android
            // gesture bar without hardcoding per-device offsets. Uses Unity's
            // own built-in UnityEngine.UI.SafeArea component rather than a
            // hand-rolled one.
            var safeAreaRT = CreateUIObject("SafeAreaHUD", parent, typeof(SafeArea));
            StretchFull(safeAreaRT);

            var hudRT = CreateUIObject("HUD", safeAreaRT, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            hudRT.anchorMin = new Vector2(0f, 1f);
            hudRT.anchorMax = new Vector2(1f, 1f);
            hudRT.pivot = new Vector2(0.5f, 1f);
            hudRT.anchoredPosition = Vector2.zero;
            // A freshly created RectTransform keeps a nonzero default
            // sizeDelta; under a full-width stretch anchor that leaks
            // through as extra width instead of a clean 1:1 stretch, so it
            // has to be pinned to 0 explicitly (height is fine as-is, the
            // ContentSizeFitter below takes it over).
            hudRT.sizeDelta = new Vector2(0f, hudRT.sizeDelta.y);

            var hudLayout = hudRT.GetComponent<VerticalLayoutGroup>();
            hudLayout.padding = new RectOffset(32, 32, 32, 24);
            hudLayout.spacing = 16f;
            hudLayout.childAlignment = TextAnchor.UpperCenter;
            hudLayout.childControlWidth = true;
            hudLayout.childControlHeight = true;
            hudLayout.childForceExpandWidth = true;
            hudLayout.childForceExpandHeight = false;

            // Height has to grow with content instead of clipping, which is
            // what caused the old overlap bug (fixed-size box + variable-
            // length appended text).
            var hudFitter = hudRT.GetComponent<ContentSizeFitter>();
            hudFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            hudFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var turnRT = CreateUIObject("TurnLabel", hudRT, typeof(Text), typeof(LayoutElement));
            var turnLabel = turnRT.GetComponent<Text>();
            ConfigureText(turnLabel, 48, TextAnchor.MiddleCenter, Color.white);
            turnLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            turnLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var turnLayoutElement = turnRT.GetComponent<LayoutElement>();
            turnLayoutElement.preferredHeight = 70f;
            turnLayoutElement.flexibleWidth = 1f;

            var countsRowRT = CreateUIObject("CountsRow", hudRT, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var countsRowLayout = countsRowRT.GetComponent<HorizontalLayoutGroup>();
            countsRowLayout.spacing = 16f;
            countsRowLayout.childAlignment = TextAnchor.MiddleCenter;
            countsRowLayout.childControlWidth = true;
            countsRowLayout.childControlHeight = true;
            countsRowLayout.childForceExpandWidth = true;
            countsRowLayout.childForceExpandHeight = true;
            var countsRowLayoutElement = countsRowRT.GetComponent<LayoutElement>();
            // Fixed allowance for a single line of 32pt text (the fade
            // warning now lives in its own FadeWarning element below);
            // Overflow (not Clip) on the children is the safety net if this
            // is ever exceeded.
            countsRowLayoutElement.minHeight = 60f;
            // CountsRow is itself a LayoutGroup, so it also reports its own
            // preferred width (the sum of its children's natural widths) as
            // an ILayoutElement. Without an explicit flexibleWidth here, the
            // outer VerticalLayoutGroup falls back to that self-reported
            // preferred width instead of stretching it to fill the row, and
            // the inner group ends up overflowing past the screen edges.
            countsRowLayoutElement.flexibleWidth = 1f;

            var countXRT = CreateUIObject("CountX", countsRowRT, typeof(Text), typeof(LayoutElement));
            var countX = countXRT.GetComponent<Text>();
            ConfigureText(countX, 32, TextAnchor.MiddleCenter, new Color(0.13f, 0.95f, 0.95f));
            countX.horizontalOverflow = HorizontalWrapMode.Wrap;
            countX.verticalOverflow = VerticalWrapMode.Overflow;
            countXRT.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var countORT = CreateUIObject("CountO", countsRowRT, typeof(Text), typeof(LayoutElement));
            var countO = countORT.GetComponent<Text>();
            ConfigureText(countO, 32, TextAnchor.MiddleCenter, new Color(1f, 0.2f, 0.6f));
            countO.horizontalOverflow = HorizontalWrapMode.Wrap;
            countO.verticalOverflow = VerticalWrapMode.Overflow;
            countORT.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var fadeWarningRT = CreateUIObject("FadeWarning", hudRT, typeof(Text), typeof(LayoutElement));
            var fadeWarningLabel = fadeWarningRT.GetComponent<Text>();
            ConfigureText(fadeWarningLabel, 28, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.15f));
            fadeWarningLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            fadeWarningLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var fadeWarningLayoutElement = fadeWarningRT.GetComponent<LayoutElement>();
            fadeWarningLayoutElement.preferredHeight = 50f;
            fadeWarningLayoutElement.flexibleWidth = 1f;

            return hudRT;
        }

        static RectTransform CreateWinBanner(Transform parent, out Text winLabel, out RestartButton restartButton)
        {
            var bannerRT = CreateUIObject("WinBanner", parent, typeof(Image));
            bannerRT.anchorMin = bannerRT.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRT.sizeDelta = new Vector2(700f, 320f);
            bannerRT.anchoredPosition = Vector2.zero;
            var bg = bannerRT.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            bannerRT.gameObject.SetActive(false);

            var labelRT = CreateUIObject("WinLabel", bannerRT, typeof(Text));
            labelRT.anchorMin = new Vector2(0f, 0.45f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            winLabel = labelRT.GetComponent<Text>();
            ConfigureText(winLabel, 48, TextAnchor.MiddleCenter, Color.white);

            var buttonRT = CreateUIObject("RestartButton", bannerRT, typeof(Image), typeof(Button));
            buttonRT.anchorMin = new Vector2(0.5f, 0f);
            buttonRT.anchorMax = new Vector2(0.5f, 0f);
            buttonRT.pivot = new Vector2(0.5f, 0f);
            buttonRT.sizeDelta = new Vector2(280f, 90f);
            buttonRT.anchoredPosition = new Vector2(0f, 30f);
            var buttonImage = buttonRT.GetComponent<Image>();
            buttonImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = new Color(0.13f, 0.95f, 0.95f);
            restartButton = buttonRT.gameObject.AddComponent<RestartButton>();

            var buttonLabelRT = CreateUIObject("Label", buttonRT, typeof(Text));
            StretchFull(buttonLabelRT);
            var buttonLabel = buttonLabelRT.GetComponent<Text>();
            buttonLabel.text = "Reintentar";
            ConfigureText(buttonLabel, 32, TextAnchor.MiddleCenter, Color.black);

            return bannerRT;
        }

        static RectTransform CreateUIObject(string name, Transform parent, params System.Type[] extraComponents)
        {
            var types = new System.Type[extraComponents.Length + 1];
            types[0] = typeof(RectTransform);
            System.Array.Copy(extraComponents, 0, types, 1, extraComponents.Length);
            var go = new GameObject(name, types);
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void ConfigureText(Text text, int fontSize, TextAnchor alignment, Color color)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (field == null)
                throw new System.Exception($"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
