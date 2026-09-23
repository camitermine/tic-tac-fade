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
        [SerializeField] Image criticalMark;
        [SerializeField] Image ghostOverlay;

        static readonly Color XColor = new Color(0.13f, 0.95f, 0.95f);      // placeholder neon cyan
        static readonly Color OColor = new Color(1f, 0.2f, 0.6f);           // placeholder neon magenta
        static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.06f);

        const float GhostAlpha = 0.4f;
        const float GhostVictimAlpha = 0.12f;  // below the life==1 tier: the piece FIFO would remove by this specific move
        const float Life2PulseBase = 0.7f;     // matches LifeToAlpha(2)
        const float Life2PulseAmplitude = 0.15f;
        const float Life2PulseSpeed = 3f;      // rad/s

        int cellIndex;
        Button button;
        Occupant currentOccupant = Occupant.None;
        int currentLife;
        bool isGhostVictim;

        void Awake()
        {
            button = GetComponent<Button>();
            button.transition = Selectable.Transition.None; // prevents the automatic tint from overriding our colors
            button.onClick.AddListener(() => Clicked?.Invoke(cellIndex));
        }

        void Update()
        {
            if (currentOccupant == Occupant.None || currentLife != 2 || isGhostVictim)
                return;

            Color c = background.color;
            c.a = Life2PulseBase + Mathf.Sin(Time.time * Life2PulseSpeed) * Life2PulseAmplitude;
            background.color = c;
        }

        public void Init(int index)
        {
            cellIndex = index;
        }

        public void SetEmpty()
        {
            currentOccupant = Occupant.None;
            currentLife = 0;
            isGhostVictim = false;
            label.text = string.Empty;
            background.color = EmptyColor;
            SetHighlighted(false);
            if (criticalMark != null)
                criticalMark.enabled = false;
            HideGhost();
        }

        public void SetOccupant(Occupant occupant, int life)
        {
            currentOccupant = occupant;
            currentLife = life;
            isGhostVictim = false;
            label.text = occupant == Occupant.X ? "X" : "O";
            Color playerColor = occupant == Occupant.X ? XColor : OColor;
            Color c = playerColor;
            c.a = LifeToAlpha(life);
            background.color = c;
            if (criticalMark != null)
            {
                criticalMark.enabled = life == 1;
                criticalMark.color = playerColor; // tinted by the owner, not a shared fixed color
            }
            HideGhost(); // a real piece landing here invalidates any stale preview
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

        /// <summary>The new piece a ghost-piece preview would place here.</summary>
        public void ShowGhost(Occupant player)
        {
            if (ghostOverlay == null) return;
            Color c = player == Occupant.X ? XColor : OColor;
            c.a = GhostAlpha;
            ghostOverlay.color = c;
            ghostOverlay.enabled = true;
        }

        public void HideGhost()
        {
            if (ghostOverlay != null)
                ghostOverlay.enabled = false;
        }

        /// <summary>
        /// Marks this cell's own piece as the one the FIFO would remove by the
        /// currently previewed move. Distinct from the passive life==1
        /// <see cref="criticalMark"/>, which can be showing on either player's
        /// piece regardless of whose turn it is or what's being previewed.
        /// </summary>
        public void SetGhostVictim(bool active)
        {
            if (currentOccupant == Occupant.None) return;
            isGhostVictim = active;
            Color c = background.color;
            c.a = active ? GhostVictimAlpha : LifeToAlpha(currentLife);
            background.color = c;
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
