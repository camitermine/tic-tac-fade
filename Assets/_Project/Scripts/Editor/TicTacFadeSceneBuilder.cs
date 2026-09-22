using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
        const string ScenePath = "Assets/Scenes/TicTacFadeGame.unity";

        [MenuItem("Tic-Tac-Fade/Build Placeholder Scene (1v1)")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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
            SetPrivateField(gameHud, "countLabelX", hud.Find("CountX").GetComponent<Text>());
            SetPrivateField(gameHud, "countLabelO", hud.Find("CountO").GetComponent<Text>());
            SetPrivateField(gameHud, "gameEndedBanner", winBanner.gameObject);
            SetPrivateField(gameHud, "gameEndedLabel", winLabel);

            var gameManagerGO = new GameObject("GameManager");
            var gameManager = gameManagerGO.AddComponent<GameManager>();
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
            return cellView;
        }

        static Transform CreateHud(Transform parent)
        {
            var hudRT = CreateUIObject("HUD", parent);
            hudRT.anchorMin = new Vector2(0f, 1f);
            hudRT.anchorMax = new Vector2(1f, 1f);
            hudRT.pivot = new Vector2(0.5f, 1f);
            hudRT.sizeDelta = new Vector2(0f, 220f);
            hudRT.anchoredPosition = Vector2.zero;

            var turnRT = CreateUIObject("TurnLabel", hudRT, typeof(Text));
            turnRT.anchorMin = new Vector2(0f, 1f);
            turnRT.anchorMax = new Vector2(1f, 1f);
            turnRT.pivot = new Vector2(0.5f, 1f);
            turnRT.sizeDelta = new Vector2(0f, 80f);
            turnRT.anchoredPosition = new Vector2(0f, -20f);
            ConfigureText(turnRT.GetComponent<Text>(), 48, TextAnchor.MiddleCenter, Color.white);

            var countXRT = CreateUIObject("CountX", hudRT, typeof(Text));
            countXRT.anchorMin = new Vector2(0f, 1f);
            countXRT.anchorMax = new Vector2(0.5f, 1f);
            countXRT.pivot = new Vector2(0f, 1f);
            countXRT.sizeDelta = new Vector2(0f, 60f);
            countXRT.anchoredPosition = new Vector2(20f, -110f);
            var countX = countXRT.GetComponent<Text>();
            ConfigureText(countX, 32, TextAnchor.MiddleLeft, new Color(0.13f, 0.95f, 0.95f));

            var countORT = CreateUIObject("CountO", hudRT, typeof(Text));
            countORT.anchorMin = new Vector2(0.5f, 1f);
            countORT.anchorMax = new Vector2(1f, 1f);
            countORT.pivot = new Vector2(1f, 1f);
            countORT.sizeDelta = new Vector2(0f, 60f);
            countORT.anchoredPosition = new Vector2(-20f, -110f);
            var countO = countORT.GetComponent<Text>();
            ConfigureText(countO, 32, TextAnchor.MiddleRight, new Color(1f, 0.2f, 0.6f));

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
