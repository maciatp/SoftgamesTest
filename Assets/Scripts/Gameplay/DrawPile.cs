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
        private System.Random _rng;

        public void Initialize(LevelData levelData)
        {
            _levelData = levelData;
            deck.Clear();
            currentIndex = 0;

            // Initialize random number generator with truly unique seed
            _rng = new System.Random(System.Environment.TickCount + System.DateTime.Now.Millisecond * 1000 + GetInstanceID());

            // Count how many random cards we need
            int randomCardsNeeded = 0;
            List<int> fixedCards = new List<int>();
            
            foreach (int cardValue in levelData.settings.cards_in_stack)
            {
                if (cardValue == -1)
                {
                    randomCardsNeeded++;
                }
                else
                {
                    fixedCards.Add(cardValue);
                }
            }

            // Generate a balanced deck for random cards
            List<int> randomCards = GenerateBalancedDeck(randomCardsNeeded);

            // Rebuild the deck maintaining the original structure but with balanced random cards
            int randomCardIndex = 0;
            foreach (int cardValue in levelData.settings.cards_in_stack)
            {
                if (cardValue == -1)
                {
                    deck.Add(randomCards[randomCardIndex]);
                    randomCardIndex++;
                }
                else
                {
                    deck.Add(cardValue);
                }
            }

            // Final shuffle for good measure
            ShuffleDeck();

            transform.position = pilePosition;
            UpdateVisual();

            Debug.Log($"Draw pile initialized with {deck.Count} cards");
            
            // Debug: Show distribution
            var distribution = new Dictionary<int, int>();
            foreach (int card in deck)
            {
                if (!distribution.ContainsKey(card))
                    distribution[card] = 0;
                distribution[card]++;
            }
            
            string distStr = "Card distribution: ";
            for (int i = 1; i <= 13; i++)
            {
                if (distribution.ContainsKey(i))
                    distStr += $"{GetCardString(i)}:{distribution[i]} ";
            }
            Debug.Log(distStr);
        }

        private List<int> GenerateBalancedDeck(int deckSize)
        {
            List<int> balancedDeck = new List<int>();

            // Calculate how many complete sets of 13 cards we can fit
            int completeSets = deckSize / 13;
            int remainingCards = deckSize % 13;

            // Add complete sets (1-13, 1-13, ...)
            for (int set = 0; set < completeSets; set++)
            {
                for (int value = 1; value <= 13; value++)
                {
                    balancedDeck.Add(value);
                }
            }

            // Add remaining cards randomly but without immediate repeats
            if (remainingCards > 0)
            {
                List<int> availableValues = new List<int>();
                for (int i = 1; i <= 13; i++)
                {
                    availableValues.Add(i);
                }

                // Shuffle available values
                for (int i = availableValues.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(0, i + 1);
                    int temp = availableValues[i];
                    availableValues[i] = availableValues[j];
                    availableValues[j] = temp;
                }

                // Take first N values
                for (int i = 0; i < remainingCards; i++)
                {
                    balancedDeck.Add(availableValues[i]);
                }
            }

            // Shuffle the balanced deck
            for (int i = balancedDeck.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(0, i + 1);
                int temp = balancedDeck[i];
                balancedDeck[i] = balancedDeck[j];
                balancedDeck[j] = temp;
            }

            return balancedDeck;
        }

        private void ShuffleDeck()
        {
            // Fisher-Yates shuffle algorithm
            int n = deck.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = _rng.Next(0, i + 1);
                // Swap
                int temp = deck[i];
                deck[i] = deck[j];
                deck[j] = temp;
            }
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
