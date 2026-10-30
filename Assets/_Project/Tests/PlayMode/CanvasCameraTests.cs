using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// The Canvas renders through the camera (Screen Space - Camera, GDD §6),
    /// so taps go through the GraphicRaycaster's event camera. The flow tests
    /// call onClick directly and would not notice a broken event camera: these
    /// raycast from each button's on-screen position like a real tap, and
    /// check that every visible screen still fills the safe area.
    /// </summary>
    public class CanvasCameraTests
    {
        const string ScenePath = "Assets/_Project/Scenes/TicTacFadeGame.unity";
        const float TimeoutSeconds = 5f;

        MatchFlow _flow;
        Canvas _canvas;

        [UnityTearDown]
        public IEnumerator TearDown() => NetworkTestCleanup.DestroyAllNetworkManagers();

        [UnityTest]
        public IEnumerator Canvas_RendersThroughTheCamera_WithinItsClipRange()
        {
            yield return LoadScene();

            Assert.AreEqual(RenderMode.ScreenSpaceCamera, _canvas.renderMode);
            var camera = _canvas.worldCamera;
            Assert.IsNotNull(camera, "The canvas needs its camera, or the UI isn't drawn and taps don't land.");
            Assert.IsTrue(camera.allowHDR, "HDR keeps the neon values above 1 until Bloom.");
            Assert.That(_canvas.planeDistance, Is.InRange(camera.nearClipPlane, camera.farClipPlane), "The canvas plane must be inside the camera's clip range.");
        }

        [UnityTest]
        public IEnumerator EveryScreen_ButtonsReceiveRealTaps_AndFillTheSafeArea()
        {
            yield return LoadScene();

            yield return AssertTapsReach("MenuScreen", "PlayLocalButton", "PlayVsAIButton", "CreateRoomButton", "JoinByCodeButton");

            Press("PlayVsAIButton");
            yield return WaitForState(FlowState.AILevelSelect);
            yield return AssertTapsReach("AILevelScreen", "AIEasyButton", "AINormalButton", "AIPerfectButton", "AILevelBackButton");
            Press("AILevelBackButton");
            yield return WaitForState(FlowState.Menu);

            Press("PlayLocalButton");
            yield return WaitForState(FlowState.Playing);
            yield return AssertTapsReach("GameScreen", "Cell_0", "Cell_4", "Cell_8", "ExitButton");

            // Top row for X: the match ends and the result panel shows.
            foreach (int cell in new[] { 0, 3, 1, 4, 2 })
            {
                Press($"Cell_{cell}");
                yield return null;
                Press($"Cell_{cell}");
                yield return null;
            }
            yield return WaitForState(FlowState.Result);
            yield return AssertTapsReach("ResultScreen", "RematchButton", "MenuButton");
        }

        IEnumerator AssertTapsReach(string screenName, params string[] buttonNames)
        {
            yield return null; // let the visibility change apply
            AssertFillsSafeArea(screenName);
            foreach (var name in buttonNames)
            {
                var target = Find(name);
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, target.transform.position);
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPoint }, hits);

                Assert.IsNotEmpty(hits, $"A tap on '{name}' hit nothing ({screenName}).");
                var top = hits[0].gameObject.transform;
                Assert.IsTrue(top == target.transform || top.IsChildOf(target.transform),
                    $"A tap on '{name}' landed on '{top.name}' ({screenName}).");
            }
        }

        void AssertFillsSafeArea(string screenName)
        {
            var rt = (RectTransform)Find(screenName).transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, corners[2]);
            Rect safe = Screen.safeArea;

            const float tolerance = 1.5f; // pixels
            Assert.AreEqual(safe.xMin, min.x, tolerance, $"{screenName} left edge vs. safe area.");
            Assert.AreEqual(safe.yMin, min.y, tolerance, $"{screenName} bottom edge vs. safe area.");
            Assert.AreEqual(safe.xMax, max.x, tolerance, $"{screenName} right edge vs. safe area.");
            Assert.AreEqual(safe.yMax, max.y, tolerance, $"{screenName} top edge vs. safe area.");
        }

        IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while ((_flow = Object.FindAnyObjectByType<MatchFlow>()) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(_flow, "MatchFlow not found after loading the scene.");
            yield return null; // every Start, MatchFlow's first StateChanged included
            yield return null; // and the first layout pass
            _canvas = Object.FindAnyObjectByType<Canvas>();
        }

        IEnumerator WaitForState(FlowState expected)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (_flow.State != expected && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(expected, _flow.State, $"Flow did not reach {expected}.");
        }

        // Scene objects are looked up by the names the scene builder assigns:
        // a string lookup, acceptable in a test (not in game code).
        static void Press(string name) => Find(name).GetComponent<Button>().onClick.Invoke();

        static GameObject Find(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, $"'{name}' not found in scene.");
            return go;
        }
    }
}
