using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TicTacFade.Game;
using TicTacFade.Net;
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
        const string AITableAssetPath = SolverTableGenerator.AssetPath;

        // Neon art pass (GDD §6): sprites are the SVG sources imported as Textured
        // Sprites; the material lifts them above 1 so URP Bloom only catches them.
        const string NeonFolder = "Assets/_Project/Art/Neon";
        const string NeonXSpritePath = NeonFolder + "/Source/neon_x.svg";
        const string NeonOSpritePath = NeonFolder + "/Source/neon_o.svg";
        const string NeonGridLineSpritePath = NeonFolder + "/Source/neon_grid_line.svg";
        const string NeonShaderPath = NeonFolder + "/UI-Neon.shader";
        const string NeonMaterialPath = NeonFolder + "/NeonUI.mat";
        const string NeonVolumeProfilePath = NeonFolder + "/NeonVolumeProfile.asset";
        static readonly Color NeonBackground = new Color32(0x0B, 0x10, 0x26, 0xFF); // #0B1026
        static readonly Color NeonGridColor = new Color32(0x22, 0xE6, 0xF0, 0xFF);  // the grid glows cyan, like the X

        [MenuItem("Tic-Tac-Fade/Build Placeholder Scene (1v1)")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mainCamera = CreateMainCamera();
            CreatePostProcessVolume();

            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemGO.AddComponent<StandaloneInputModule>();
#endif

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            // Screen Space - Camera, not Overlay: URP post-processing (Bloom) only
            // reaches a canvas the camera renders (GDD §6). The GraphicRaycaster
            // uses this camera as its event camera.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCamera;
            canvas.planeDistance = 10f;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var gameManagerGO = new GameObject("GameManager");
            var gameManager = gameManagerGO.AddComponent<GameManager>();
            SetPrivateField(gameManager, "config", GetOrCreateGameConfigAsset());

            var networkManager = CreateNetworkManager();
            var sessionService = new GameObject("SessionService").AddComponent<UgsSessionService>();
            var matchTransport = new GameObject("MatchTransport").AddComponent<NgoMatchTransport>();
            SetPrivateField(matchTransport, "networkManager", networkManager);

            var matchFlowGO = new GameObject("MatchFlow");
            var matchFlow = matchFlowGO.AddComponent<MatchFlow>();
            SetPrivateField(matchFlow, "gameManager", gameManager);
            SetPrivateField(matchFlow, "sessionService", sessionService);
            SetPrivateField(matchFlow, "matchTransport", matchTransport);
            var aiTable = AssetDatabase.LoadAssetAtPath<TextAsset>(AITableAssetPath);
            if (aiTable == null)
                Debug.LogError($"Tic-Tac-Fade: {AITableAssetPath} not found. Run Tic-Tac-Fade/Solver/Generate AI table, then rebuild the scene.");
            SetPrivateField(matchFlow, "aiTable", aiTable);

            // Sibling order = draw order. Hidden screens don't block input
            // (FlowScreenVisibility turns off blocksRaycasts).
            CreateGameScreen(canvasGO.transform, gameManager, matchFlow);
            CreateResultScreen(canvasGO.transform, matchFlow);
            CreateMenuScreen(canvasGO.transform, matchFlow);
            CreateLobbyScreen(canvasGO.transform, matchFlow);
            CreateJoinByCodeScreen(canvasGO.transform, matchFlow);
            CreateAILevelScreen(canvasGO.transform, matchFlow);

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

        static Camera CreateMainCamera()
        {
            var cameraGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGO.tag = "MainCamera";

            var camera = cameraGO.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NeonBackground;
            camera.allowHDR = true; // values above 1 from the neon material must survive until Bloom

            // The Canvas renders through this camera (Screen Space - Camera), so
            // its post-processing is what applies Bloom to the UI.
            var cameraData = cameraGO.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            return camera;
        }

        /// <summary>
        /// Global Volume with Bloom. Threshold 1: only the neon material goes
        /// above 1, so white text and buttons stay out of the glow (raise it a
        /// little, e.g. 1.1, if they ever bleed; don't darken the UI). The
        /// profile asset is created once and then kept, so tuning it in the
        /// Inspector survives a scene rebuild.
        /// </summary>
        static void CreatePostProcessVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NeonVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, NeonVolumeProfilePath);
                var bloom = profile.Add<Bloom>(overrides: true);
                bloom.threshold.value = 1f;
                bloom.intensity.value = 1.5f;
                bloom.scatter.value = 0.7f;
                bloom.highQualityFiltering.value = false; // mobile
                bloom.name = "Bloom";
                AssetDatabase.AddObjectToAsset(bloom, profile);
                AssetDatabase.SaveAssets();
            }

            var volumeGO = new GameObject("PostProcessVolume");
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static Material GetOrCreateNeonMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(NeonMaterialPath);
            if (existing != null)
                return existing;

            var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(NeonShaderPath));
            material.SetFloat("_Intensity", 2.5f);
            AssetDatabase.CreateAsset(material, NeonMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        // The sprite generated by the SVG importer (Textured Sprite) inside the .svg asset.
        static Sprite LoadNeonSprite(string svgPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(svgPath))
            {
                if (asset is Sprite sprite)
                    return sprite;
            }
            Debug.LogError($"Tic-Tac-Fade: no sprite in {svgPath}; it must be imported as a Textured Sprite.");
            return null;
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
            SetPrivateField(gameHud, "turnTimerLabel", hud.Find("TurnTimerLabel").GetComponent<Text>());
            SetPrivateField(gameHud, "flow", flow);
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

            // Online: "Esperando al rival..." after asking for a rematch.
            var rematchStatusLabel = CreateLabel("RematchStatus", panelRT, string.Empty, 32, new Color(0.13f, 0.95f, 0.95f), 50f);

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
            SetPrivateField(resultScreen, "rematchStatusLabel", rematchStatusLabel);
            SetPrivateField(resultScreen, "rematchButton", rematchButton);
            SetPrivateField(resultScreen, "menuButton", menuButton);
        }

        static void CreateMenuScreen(Transform parent, MatchFlow flow)
        {
            var screenRT = CreateSafeAreaScreen("MenuScreen", parent, flow, FlowState.Menu);

            var layoutRT = CreateCenteredColumn("MenuLayout", screenRT, 96f);
            CreateLabel("Title", layoutRT, "Tic-Tac-Fade", 96, Color.white, 160f);

            var buttonsRT = CreateUIObject("MenuButtons", layoutRT, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            ConfigureVerticalLayout(buttonsRT, new RectOffset(0, 0, 0, 0), 32f);
            buttonsRT.GetComponent<LayoutElement>().flexibleWidth = 1f; // nested layout group: see CountsRow in CreateHud

            var playLocalButton = CreateButton("PlayLocalButton", buttonsRT, "Jugar local", 48, new Color(0.13f, 0.95f, 0.95f), Color.black, 130f);
            var playVsAIButton = CreateButton("PlayVsAIButton", buttonsRT, "Jugar contra la IA", 48, new Color(0.6f, 0.45f, 1f), Color.black, 130f);
            var createRoomButton = CreateButton("CreateRoomButton", buttonsRT, "Crear sala", 48, new Color(1f, 0.2f, 0.6f), Color.black, 130f);
            var joinByCodeButton = CreateButton("JoinByCodeButton", buttonsRT, "Unirse con código", 48, new Color(1f, 1f, 1f, 0.15f), Color.white, 130f);

            // "Conectando..." while creating a room, or the last error.
            var statusLabel = CreateLabel("MenuStatus", layoutRT, string.Empty, 32, new Color(1f, 0.55f, 0.15f), 100f);

            var menuScreen = screenRT.gameObject.AddComponent<MenuScreen>();
            SetPrivateField(menuScreen, "flow", flow);
            SetPrivateField(menuScreen, "playLocalButton", playLocalButton);
            SetPrivateField(menuScreen, "playVsAIButton", playVsAIButton);
            SetPrivateField(menuScreen, "createRoomButton", createRoomButton);
            SetPrivateField(menuScreen, "joinByCodeButton", joinByCodeButton);
            SetPrivateField(menuScreen, "statusLabel", statusLabel);
        }

        static void CreateLobbyScreen(Transform parent, MatchFlow flow)
        {
            var screenRT = CreateSafeAreaScreen("LobbyScreen", parent, flow, FlowState.Lobby);
            var layoutRT = CreateCenteredColumn("LobbyLayout", screenRT, 48f);

            CreateLabel("LobbyTitle", layoutRT, "Código de la sala", 48, new Color(1f, 1f, 1f, 0.7f), 80f);
            // Large and high-contrast: if the clipboard fails on a device the
            // code still has to be easy to read and copy by hand.
            var codeLabel = CreateLabel("CodeLabel", layoutRT, string.Empty, 120, Color.white, 180f);
            var copyButton = CreateButton("CopyButton", layoutRT, "Copiar", 40, new Color(1f, 1f, 1f, 0.15f), Color.white, 110f);
            var statusLabel = CreateLabel("LobbyStatus", layoutRT, string.Empty, 40, new Color(0.13f, 0.95f, 0.95f), 130f);
            var cancelButton = CreateButton("LobbyCancelButton", layoutRT, "Cancelar", 40, new Color(1f, 1f, 1f, 0.15f), Color.white, 110f);

            var lobbyScreen = screenRT.gameObject.AddComponent<LobbyScreen>();
            SetPrivateField(lobbyScreen, "flow", flow);
            SetPrivateField(lobbyScreen, "codeLabel", codeLabel);
            SetPrivateField(lobbyScreen, "copyButton", copyButton);
            SetPrivateField(lobbyScreen, "statusLabel", statusLabel);
            SetPrivateField(lobbyScreen, "cancelButton", cancelButton);
        }

        static void CreateJoinByCodeScreen(Transform parent, MatchFlow flow)
        {
            var screenRT = CreateSafeAreaScreen("JoinByCodeScreen", parent, flow, FlowState.JoinByCode);
            var layoutRT = CreateCenteredColumn("JoinLayout", screenRT, 48f);

            CreateLabel("JoinTitle", layoutRT, "Unirse con código", 56, Color.white, 100f);
            var codeInput = CreateCodeInput(layoutRT);
            var statusLabel = CreateLabel("JoinStatus", layoutRT, string.Empty, 32, new Color(1f, 0.55f, 0.15f), 100f);
            var joinButton = CreateButton("JoinButton", layoutRT, "Unirse", 48, new Color(0.13f, 0.95f, 0.95f), Color.black, 130f);
            var cancelButton = CreateButton("JoinCancelButton", layoutRT, "Cancelar", 40, new Color(1f, 1f, 1f, 0.15f), Color.white, 110f);

            var joinScreen = screenRT.gameObject.AddComponent<JoinByCodeScreen>();
            SetPrivateField(joinScreen, "flow", flow);
            SetPrivateField(joinScreen, "codeInput", codeInput);
            SetPrivateField(joinScreen, "joinButton", joinButton);
            SetPrivateField(joinScreen, "cancelButton", cancelButton);
            SetPrivateField(joinScreen, "statusLabel", statusLabel);
        }

        static void CreateAILevelScreen(Transform parent, MatchFlow flow)
        {
            var screenRT = CreateSafeAreaScreen("AILevelScreen", parent, flow, FlowState.AILevelSelect);
            var layoutRT = CreateCenteredColumn("AILevelLayout", screenRT, 48f);

            CreateLabel("AILevelTitle", layoutRT, "Jugar contra la IA", 56, Color.white, 100f);
            CreateLabel("AILevelSubtitle", layoutRT, "Elegí la dificultad", 40, new Color(1f, 1f, 1f, 0.7f), 70f);
            var easyButton = CreateButton("AIEasyButton", layoutRT, "Fácil", 48, new Color(0.13f, 0.95f, 0.95f), Color.black, 130f);
            var normalButton = CreateButton("AINormalButton", layoutRT, "Normal", 48, new Color(0.6f, 0.45f, 1f), Color.black, 130f);
            var perfectButton = CreateButton("AIPerfectButton", layoutRT, "Perfecta", 48, new Color(1f, 0.2f, 0.6f), Color.black, 130f);
            var backButton = CreateButton("AILevelBackButton", layoutRT, "Volver", 40, new Color(1f, 1f, 1f, 0.15f), Color.white, 110f);

            var levelScreen = screenRT.gameObject.AddComponent<AILevelScreen>();
            SetPrivateField(levelScreen, "flow", flow);
            SetPrivateField(levelScreen, "easyButton", easyButton);
            SetPrivateField(levelScreen, "normalButton", normalButton);
            SetPrivateField(levelScreen, "perfectButton", perfectButton);
            SetPrivateField(levelScreen, "backButton", backButton);
        }

        static InputField CreateCodeInput(Transform parent)
        {
            var inputRT = CreateUIObject("CodeInput", parent, typeof(Image), typeof(InputField), typeof(LayoutElement));
            var background = inputRT.GetComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            background.color = new Color(1f, 1f, 1f, 0.12f);
            var layoutElement = inputRT.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 150f;
            layoutElement.flexibleWidth = 1f;

            var textRT = CreateUIObject("Text", inputRT, typeof(Text));
            StretchFull(textRT);
            var text = textRT.GetComponent<Text>();
            ConfigureText(text, 80, TextAnchor.MiddleCenter, Color.white);
            text.supportRichText = false;

            var placeholderRT = CreateUIObject("Placeholder", inputRT, typeof(Text));
            StretchFull(placeholderRT);
            var placeholder = placeholderRT.GetComponent<Text>();
            ConfigureText(placeholder, 48, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.35f));
            placeholder.text = "Código";

            var input = inputRT.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterValidation = InputField.CharacterValidation.Alphanumeric;
            input.characterLimit = 12;
            return input;
        }

        /// <summary>
        /// Opaque full-screen background plus a vertically centered,
        /// full-width column (VLG + ContentSizeFitter) for a screen's content.
        /// </summary>
        static RectTransform CreateCenteredColumn(string name, RectTransform screen, float spacing)
        {
            var backgroundRT = CreateUIObject("Background", screen, typeof(Image));
            StretchFull(backgroundRT);
            backgroundRT.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f);

            var columnRT = CreateUIObject(name, screen, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            columnRT.anchorMin = new Vector2(0f, 0.5f);
            columnRT.anchorMax = new Vector2(1f, 0.5f);
            columnRT.pivot = new Vector2(0.5f, 0.5f);
            columnRT.anchoredPosition = Vector2.zero;
            columnRT.sizeDelta = new Vector2(0f, columnRT.sizeDelta.y);
            ConfigureVerticalLayout(columnRT, new RectOffset(96, 96, 0, 0), spacing);
            return columnRT;
        }

        static Text CreateLabel(string name, Transform parent, string text, int fontSize, Color color, float preferredHeight)
        {
            var labelRT = CreateUIObject(name, parent, typeof(Text), typeof(LayoutElement));
            var label = labelRT.GetComponent<Text>();
            ConfigureText(label, fontSize, TextAnchor.MiddleCenter, color);
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var layoutElement = labelRT.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
            return label;
        }

        /// <summary>
        /// NetworkManager + UnityTransport: Multiplayer Services' Relay
        /// network starts the Netcode host/client on this object, and fails
        /// if NetworkManager.Singleton isn't set.
        /// </summary>
        static Unity.Netcode.NetworkManager CreateNetworkManager()
        {
            var networkGO = new GameObject("NetworkManager");
            var transport = networkGO.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            var networkManager = networkGO.AddComponent<Unity.Netcode.NetworkManager>();
            networkManager.NetworkConfig.NetworkTransport = transport;
            return networkManager;
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

            var neonMaterial = GetOrCreateNeonMaterial();
            CreateGridLines(panelRT, neonMaterial, cellSize: 150f, spacing: 10f);

            var xSprite = LoadNeonSprite(NeonXSpritePath);
            var oSprite = LoadNeonSprite(NeonOSpritePath);
            cells = new CellView[9];
            for (int i = 0; i < 9; i++)
                cells[i] = CreateCell(panelRT, i, neonMaterial, xSprite, oSprite);

            return panelRT;
        }

        /// <summary>
        /// The 4 neon lines between the cells, created first so they draw under
        /// the pieces. They ignore the GridLayoutGroup and sit on the cell
        /// boundaries. 9-sliced: the left/right borders hold the round caps, and
        /// pixelsPerUnitMultiplier scales the borders with the thickness so the
        /// caps stay round on a line thinner than the sprite.
        /// </summary>
        static void CreateGridLines(RectTransform board, Material neonMaterial, float cellSize, float spacing)
        {
            var sprite = LoadNeonSprite(NeonGridLineSpritePath);
            const float thickness = 32f; // rect height; the stroke inside is a quarter of it
            float length = 3 * cellSize + 2 * spacing;
            float offset = (cellSize + spacing) / 2f;

            var positions = new[] { -offset, offset };
            for (int i = 0; i < 4; i++)
            {
                bool vertical = i >= 2;
                var rt = CreateUIObject(vertical ? $"GridLineV_{i - 2}" : $"GridLineH_{i}", board, typeof(Image), typeof(LayoutElement));
                rt.GetComponent<LayoutElement>().ignoreLayout = true;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(length, thickness);
                rt.anchoredPosition = vertical ? new Vector2(positions[i - 2], 0f) : new Vector2(0f, positions[i]);
                rt.localRotation = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;

                var image = rt.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = sprite.rect.height / thickness;
                image.material = neonMaterial;
                image.color = NeonGridColor;
                image.raycastTarget = false;
            }
        }

        static CellView CreateCell(Transform parent, int index, Material neonMaterial, Sprite xSprite, Sprite oSprite)
        {
            var rt = CreateUIObject($"Cell_{index}", parent, typeof(Image), typeof(Button));
            var background = rt.GetComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            background.color = new Color(1f, 1f, 1f, 0f); // invisible: the grid lines frame the cell, this only catches taps

            var pieceRT = CreateUIObject("Piece", rt, typeof(Image));
            StretchFull(pieceRT);
            var piece = pieceRT.GetComponent<Image>();
            piece.material = neonMaterial;
            piece.preserveAspect = true;
            piece.raycastTarget = false;
            piece.enabled = false; // sprite and tint set by CellView.SetOccupant()

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
            ghostOverlay.material = neonMaterial;
            ghostOverlay.preserveAspect = true;
            ghostOverlay.color = new Color(1f, 1f, 1f, 0f); // real sprite/color/alpha set by ShowGhost()
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
            SetPrivateField(cellView, "piece", piece);
            SetPrivateField(cellView, "xSprite", xSprite);
            SetPrivateField(cellView, "oSprite", oSprite);
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

            // Online turn countdown ("0:23"); empty offline.
            CreateLabel("TurnTimerLabel", hudRT, string.Empty, 44, Color.white, 60f);

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
