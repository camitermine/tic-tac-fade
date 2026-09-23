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

            var gameManagerGO = new GameObject("GameManager");
            var gameManager = gameManagerGO.AddComponent<GameManager>();
            SetPrivateField(gameManager, "config", GetOrCreateGameConfigAsset());

            var matchFlowGO = new GameObject("MatchFlow");
            var matchFlow = matchFlowGO.AddComponent<MatchFlow>();
            SetPrivateField(matchFlow, "gameManager", gameManager);

            // Sibling order = draw order: the menu goes last so it covers
            // everything while visible.
            CreateGameScreen(canvasGO.transform, gameManager, matchFlow);
            CreateResultScreen(canvasGO.transform, matchFlow);
            CreateMenuScreen(canvasGO.transform, matchFlow);

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

        /// <summary>
        /// Root of every screen: full-screen container that respects the
        /// device safe area on all four edges, shown only in the given flow
        /// states. UnityEngine.UI.SafeArea has no Reset() and no initializer
        /// for its edges, so when added from code it respects NO edge unless
        /// Edges is set explicitly — that is what left the HUD under the
        /// punch-hole camera (ai-log [11], corrected in [12]).
        /// </summary>
        static RectTransform CreateSafeAreaScreen(string name, Transform parent, MatchFlow flow, params FlowState[] visibleIn)
        {
            var screenRT = CreateUIObject(name, parent, typeof(SafeArea), typeof(CanvasGroup), typeof(FlowScreenVisibility));
            StretchFull(screenRT);

            screenRT.GetComponent<SafeArea>().Edges =
                SafeArea.SafeAreaMode.Top | SafeArea.SafeAreaMode.Right | SafeArea.SafeAreaMode.Bottom | SafeArea.SafeAreaMode.Left;

            var visibility = screenRT.GetComponent<FlowScreenVisibility>();
            SetPrivateField(visibility, "flow", flow);
            SetPrivateField(visibility, "visibleIn", visibleIn);
            return screenRT;
        }

        static void CreateGameScreen(Transform parent, GameManager gameManager, MatchFlow flow)
        {
            // Visible in Result too: the board and its highlighted winning
            // line stay on screen above the result panel.
            var screenRT = CreateSafeAreaScreen("GameScreen", parent, flow, FlowState.Playing, FlowState.Result);

            var boardPanel = CreateBoardPanel(screenRT, out var cellViews);
            var boardView = boardPanel.gameObject.AddComponent<BoardView>();
            SetPrivateField(boardView, "cells", cellViews);
            SetPrivateField(boardView, "gameManager", gameManager);

            var hud = CreateHud(screenRT, flow);
            var gameHud = hud.gameObject.AddComponent<GameHud>();
            SetPrivateField(gameHud, "gameManager", gameManager);
            SetPrivateField(gameHud, "turnLabel", hud.Find("TurnLabel").GetComponent<Text>());
            SetPrivateField(gameHud, "countLabelX", hud.Find("CountsRow/CountX").GetComponent<Text>());
            SetPrivateField(gameHud, "countLabelO", hud.Find("CountsRow/CountO").GetComponent<Text>());
            SetPrivateField(gameHud, "fadeWarningLabel", hud.Find("FadeWarning").GetComponent<Text>());
        }

        static void CreateResultScreen(Transform parent, MatchFlow flow)
        {
            var screenRT = CreateSafeAreaScreen("ResultScreen", parent, flow, FlowState.Result);

            // Bottom panel, not a full-screen overlay: the board above keeps
            // showing the winning line (GDD §4.2).
            var panelRT = CreateUIObject("ResultPanel", screenRT, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelRT.anchorMin = new Vector2(0f, 0f);
            panelRT.anchorMax = new Vector2(1f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRT.sizeDelta = new Vector2(0f, panelRT.sizeDelta.y);
            panelRT.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            ConfigureVerticalLayout(panelRT, new RectOffset(48, 48, 40, 48), 32f);

            var labelRT = CreateUIObject("ResultLabel", panelRT, typeof(Text), typeof(LayoutElement));
            var resultLabel = labelRT.GetComponent<Text>();
            ConfigureText(resultLabel, 52, TextAnchor.MiddleCenter, Color.white);
            resultLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            resultLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var labelLayout = labelRT.GetComponent<LayoutElement>();
            labelLayout.preferredHeight = 150f;
            labelLayout.flexibleWidth = 1f;

            var buttonsRT = CreateUIObject("ResultButtons", panelRT, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var buttonsLayout = buttonsRT.GetComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 24f;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;
            buttonsLayout.childForceExpandHeight = true;
            var buttonsLayoutElement = buttonsRT.GetComponent<LayoutElement>();
            buttonsLayoutElement.minHeight = 110f;
            buttonsLayoutElement.flexibleWidth = 1f; // nested layout group: see CountsRow in CreateHud

            var rematchButton = CreateButton("RematchButton", buttonsRT, "Revancha", 40, new Color(0.13f, 0.95f, 0.95f), Color.black, 110f);
            var menuButton = CreateButton("MenuButton", buttonsRT, "Menú", 40, new Color(1f, 1f, 1f, 0.15f), Color.white, 110f);

            var resultScreen = screenRT.gameObject.AddComponent<ResultScreen>();
            SetPrivateField(resultScreen, "flow", flow);
            SetPrivateField(resultScreen, "resultLabel", resultLabel);
            SetPrivateField(resultScreen, "rematchButton", rematchButton);
            SetPrivateField(resultScreen, "menuButton", menuButton);
        }

        static void CreateMenuScreen(Transform parent, MatchFlow flow)
        {
            var screenRT = CreateSafeAreaScreen("MenuScreen", parent, flow, FlowState.Menu);

            // Opaque background so the (hidden anyway) game screen never
            // shows through while the menu is up.
            var backgroundRT = CreateUIObject("Background", screenRT, typeof(Image));
            StretchFull(backgroundRT);
            backgroundRT.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f);

            var layoutRT = CreateUIObject("MenuLayout", screenRT, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            layoutRT.anchorMin = new Vector2(0f, 0.5f);
            layoutRT.anchorMax = new Vector2(1f, 0.5f);
            layoutRT.pivot = new Vector2(0.5f, 0.5f);
            layoutRT.anchoredPosition = Vector2.zero;
            layoutRT.sizeDelta = new Vector2(0f, layoutRT.sizeDelta.y);
            ConfigureVerticalLayout(layoutRT, new RectOffset(96, 96, 0, 0), 96f);

            var titleRT = CreateUIObject("Title", layoutRT, typeof(Text), typeof(LayoutElement));
            var title = titleRT.GetComponent<Text>();
            ConfigureText(title, 96, TextAnchor.MiddleCenter, Color.white);
            title.text = "Tic-Tac-Fade";
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            var titleLayout = titleRT.GetComponent<LayoutElement>();
            titleLayout.preferredHeight = 160f;
            titleLayout.flexibleWidth = 1f;

            // The online buttons (create room, join with code) are added
            // here as siblings of PlayLocalButton; the layout needs no change.
            var buttonsRT = CreateUIObject("MenuButtons", layoutRT, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            ConfigureVerticalLayout(buttonsRT, new RectOffset(0, 0, 0, 0), 32f);
            buttonsRT.GetComponent<LayoutElement>().flexibleWidth = 1f; // nested layout group: see CountsRow in CreateHud

            var playLocalButton = CreateButton("PlayLocalButton", buttonsRT, "Jugar local", 48, new Color(0.13f, 0.95f, 0.95f), Color.black, 130f);

            var menuScreen = screenRT.gameObject.AddComponent<MenuScreen>();
            SetPrivateField(menuScreen, "flow", flow);
            SetPrivateField(menuScreen, "playLocalButton", playLocalButton);
        }

        static void ConfigureVerticalLayout(RectTransform rt, RectOffset padding, float spacing)
        {
            var layout = rt.GetComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = rt.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        static Button CreateButton(string name, Transform parent, string text, int fontSize, Color background, Color textColor, float preferredHeight)
        {
            var buttonRT = CreateUIObject(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
            var image = buttonRT.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = background;

            var layoutElement = buttonRT.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;

            var labelRT = CreateUIObject("Label", buttonRT, typeof(Text));
            StretchFull(labelRT);
            var label = labelRT.GetComponent<Text>();
            ConfigureText(label, fontSize, TextAnchor.MiddleCenter, textColor);
            label.text = text;

            return buttonRT.GetComponent<Button>();
        }

        static RectTransform CreateBoardPanel(Transform parent, out CellView[] cells)
        {
            var panelRT = CreateUIObject("BoardPanel", parent, typeof(GridLayoutGroup));
            panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;
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

        static Transform CreateHud(Transform parent, MatchFlow flow)
        {
            // The parent is the GameScreen, already a SafeArea container
            // (CreateSafeAreaScreen): the HUD stays clear of notches/cutouts
            // and the gesture bar without per-device offsets.
            var hudRT = CreateUIObject("HUD", parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
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

            CreateTopBar(hudRT, flow);

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

        /// <summary>
        /// First HUD row: a small, low-contrast "Salir" button pinned to the
        /// right so it doesn't compete with the board. Only visible while
        /// Playing; in Result the panel's "Menú" button covers the same exit.
        /// </summary>
        static void CreateTopBar(Transform hud, MatchFlow flow)
        {
            var topBarRT = CreateUIObject("TopBar", hud, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var topBarLayout = topBarRT.GetComponent<HorizontalLayoutGroup>();
            topBarLayout.childAlignment = TextAnchor.UpperRight;
            topBarLayout.childControlWidth = true;
            topBarLayout.childControlHeight = true;
            topBarLayout.childForceExpandWidth = false;
            topBarLayout.childForceExpandHeight = false;
            var topBarLayoutElement = topBarRT.GetComponent<LayoutElement>();
            topBarLayoutElement.minHeight = 64f;
            topBarLayoutElement.flexibleWidth = 1f; // nested layout group: see CountsRow below

            var exitButton = CreateButton("ExitButton", topBarRT, "Salir", 28, new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.6f), 64f);
            var exitLayoutElement = exitButton.GetComponent<LayoutElement>();
            exitLayoutElement.preferredWidth = 180f;
            exitLayoutElement.flexibleWidth = 0f;

            var exitVisibility = exitButton.gameObject.AddComponent<FlowScreenVisibility>(); // RequireComponent adds its CanvasGroup
            SetPrivateField(exitVisibility, "flow", flow);
            SetPrivateField(exitVisibility, "visibleIn", new[] { FlowState.Playing });

            var exitMatchButton = exitButton.gameObject.AddComponent<ExitMatchButton>();
            SetPrivateField(exitMatchButton, "flow", flow);
            SetPrivateField(exitMatchButton, "button", exitButton);
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
