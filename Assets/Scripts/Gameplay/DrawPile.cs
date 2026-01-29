using System.Collections.Generic;
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
        public List<int> deck = new List<int>();
        public int currentIndex = 0;

        private LevelData _levelData;

        public void Initialize(LevelData levelData)
        {
            _levelData = levelData;
            deck.Clear();
            currentIndex = 0;

            // Load deck from level settings
            foreach (int cardValue in levelData.settings.cards_in_stack)
            {
                // -1 means random card (1-13)
                if (cardValue == -1)
                {
                    deck.Add(Random.Range(1, 14));
                }
                else
                {
                    deck.Add(cardValue);
                }
            }

            transform.position = pilePosition;
            UpdateVisual();

            Debug.Log($"Draw pile initialized with {deck.Count} cards");
        }

        public int DrawCard()
        {
            if (IsEmpty())
            {
                Debug.LogWarning("Draw pile is empty!");
                return -1;
            }

            int card = deck[currentIndex];
            currentIndex++;
            UpdateVisual();

            Debug.Log($"Drew card: {GetCardString(card)} ({RemainingCards()} cards left)");
            return card;
        }

        public bool IsEmpty()
        {
            return currentIndex >= deck.Count;
        }

        public int RemainingCards()
        {
            return deck.Count - currentIndex;
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