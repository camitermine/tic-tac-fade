using System;
using UnityEngine;
using UnityEngine.UI;
using TicTacFade.Core;

namespace TicTacFade.UI
{
    [RequireComponent(typeof(Button))]
    public class CellView : MonoBehaviour
    {
        public event Action<int> Clicked;

        [SerializeField] Text label;
        [SerializeField] Image background;
        [SerializeField] Image highlightBorder;

        static readonly Color XColor = new Color(0.13f, 0.95f, 0.95f);      // placeholder neon cyan
        static readonly Color OColor = new Color(1f, 0.2f, 0.6f);           // placeholder neon magenta
        static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.06f);

        int cellIndex;
        Button button;

        void Awake()
        {
            button = GetComponent<Button>();
            button.transition = Selectable.Transition.None; // prevents the automatic tint from overriding our colors
            button.onClick.AddListener(() => Clicked?.Invoke(cellIndex));
        }

        public void Init(int index)
        {
            cellIndex = index;
        }

        public void SetEmpty()
        {
            label.text = string.Empty;
            background.color = EmptyColor;
            SetHighlighted(false);
        }

        public void SetOccupant(Occupant occupant, int life)
        {
            label.text = occupant == Occupant.X ? "X" : "O";
            Color c = occupant == Occupant.X ? XColor : OColor;
            c.a = LifeToAlpha(life);
            background.color = c;
        }

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
        }

        public void SetHighlighted(bool on)
        {
            if (highlightBorder != null)
                highlightBorder.enabled = on;
        }

        static float LifeToAlpha(int life)
        {
            switch (life)
            {
                case 3: return 1.0f;
                case 2: return 0.7f;
                case 1: return 0.35f;
                default: return 0f;
            }
        }
    }
}
