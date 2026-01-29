using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TripeaksSolitaire.Core;

namespace TripeaksSolitaire.Gameplay
{
    public class DrawPile : MonoBehaviour
    {
        [Header("Settings")]
        public Vector3 pilePosition = new Vector3(-6f, -3f, 0f);

        [Header("Visual")]
        public SpriteRenderer pileSprite;
        public TMPro.TextMeshPro countText;

        [Header("Runtime")]
        public int totalCards; // Total cards available
        public int cardsDrawn = 0; // How many have been drawn

        private System.Random _rng;

        public void Initialize(LevelData levelData)
        {
            cardsDrawn = 0;

            // Initialize random number generator with unique seed
            _rng = new System.Random(System.Environment.TickCount + System.DateTime.Now.Millisecond * 1000 + GetInstanceID());

            // Always use on-demand generation now
            totalCards = levelData.settings.cards_in_stack.Count;
            
            transform.position = pilePosition;
            UpdateVisual();

            Debug.Log($"Draw pile initialized: {totalCards} cards (ON-DEMAND GENERATION)");
        }

        public int DrawCard()
        {
            if (IsEmpty())
            {
                Debug.LogWarning("Draw pile is empty!");
                return -1;
            }

            // Generate random card on demand (1-13)
            int card = _rng.Next(1, 14);
            
            cardsDrawn++;
            UpdateVisual();

            Debug.Log($"Drew card: {GetCardString(card)} ({RemainingCards()} cards left)");
            return card;
        }

        public bool IsEmpty()
        {
            return cardsDrawn >= totalCards;
        }

        public int RemainingCards()
        {
            return totalCards - cardsDrawn;
        }

        public bool IsCloseWin()
        {
            return RemainingCards() <= 2;
        }

        private void UpdateVisual()
        {
            if (pileSprite != null)
            {
                pileSprite.color = IsEmpty() ? Color.gray : Color.cyan;
            }

            if (countText != null)
            {
                countText.text = RemainingCards().ToString();
            }
        }

        private string GetCardString(int value)
        {
            switch (value)
            {
                case 1: return "A";
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                default: return value.ToString();
            }
        }

        private void OnMouseDown()
        {
            // This will be handled by GameManager
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                gm.OnDrawPileClicked();
            }
        }
    }
}
