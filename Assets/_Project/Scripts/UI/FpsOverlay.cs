using UnityEngine;
using UnityEngine.UI;

namespace TicTacFade.UI
{
    /// <summary>
    /// SPIKE ONLY (feat/art-neon-spike): measures the cost of Bloom on the
    /// device. Shows the average FPS and the worst frame over the last
    /// <see cref="WindowSeconds"/>, plus a button that turns the post-process
    /// volume on and off. Remove before merging the spike (its own commit).
    /// </summary>
    public class FpsOverlay : MonoBehaviour
    {
        const float WindowSeconds = 2f;

        [SerializeField] Text label;
        [SerializeField] Button bloomToggle;
        [SerializeField] Text bloomToggleLabel;
        [SerializeField] GameObject postProcessVolume;

        float _windowStart;
        int _frames;
        float _worstFrame;

        void Awake()
        {
            bloomToggle.onClick.AddListener(ToggleBloom);
            RefreshToggleLabel();
            _windowStart = Time.unscaledTime;
        }

        void OnDestroy()
        {
            if (bloomToggle != null)
                bloomToggle.onClick.RemoveListener(ToggleBloom);
        }

        void Update()
        {
            _frames++;
            _worstFrame = Mathf.Max(_worstFrame, Time.unscaledDeltaTime);

            float elapsed = Time.unscaledTime - _windowStart;
            if (elapsed < WindowSeconds)
                return;

            label.text = $"FPS {_frames / elapsed:F1}  ·  worst {_worstFrame * 1000f:F1} ms  ·  target {Application.targetFrameRate}";
            _frames = 0;
            _worstFrame = 0f;
            _windowStart = Time.unscaledTime;
        }

        void ToggleBloom()
        {
            postProcessVolume.SetActive(!postProcessVolume.activeSelf);
            RefreshToggleLabel();
        }

        void RefreshToggleLabel() => bloomToggleLabel.text = postProcessVolume.activeSelf ? "Bloom: ON" : "Bloom: OFF";
    }
}
