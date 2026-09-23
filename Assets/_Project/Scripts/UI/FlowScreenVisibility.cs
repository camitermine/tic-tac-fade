using System;
using UnityEngine;
using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>
    /// Shows this object only in the given <see cref="FlowState"/>s.
    /// Uses a CanvasGroup instead of SetActive on purpose: every screen stays
    /// active, so BoardView/GameHud run Awake (and subscribe to GameManager)
    /// at scene load regardless of which screen is visible first.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FlowScreenVisibility : MonoBehaviour
    {
        [SerializeField] MatchFlow flow;
        [SerializeField] FlowState[] visibleIn = Array.Empty<FlowState>();

        CanvasGroup _canvasGroup;

        public bool IsVisible => _canvasGroup.alpha > 0f;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            flow.StateChanged += OnFlowStateChanged;
            Apply(flow.State);
        }

        void OnDestroy()
        {
            if (flow != null)
                flow.StateChanged -= OnFlowStateChanged;
        }

        void OnFlowStateChanged(FlowState state) => Apply(state);

        void Apply(FlowState state)
        {
            bool visible = Array.IndexOf(visibleIn, state) >= 0;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
