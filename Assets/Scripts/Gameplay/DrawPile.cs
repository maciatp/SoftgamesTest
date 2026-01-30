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

            // All random cards will be -1 (deferred generation)
            foreach (int cardValue in levelData.settings.cards_in_stack)
            {
                deck.Add(cardValue); // Keep -1 for random, specific values for fixed
            }

            transform.position = pilePosition;
            UpdateVisual();

            Debug.Log($"Draw pile initialized with {deck.Count} cards (deferred generation)");
        }

        public int DrawCard(int currentPlayValue, List<int> targetValues, float favorableProbability = 0.51f)
        {
            if (IsEmpty())
            {
                Debug.LogWarning("Draw pile is empty!");
                return -1;
            }

            int card = deck[currentIndex];
            
            // If card is -1 (random), generate favorable value
            if (card == -1)
            {
                card = GenerateFavorableValueSmart(targetValues, favorableProbability);
                deck[currentIndex] = card; // Store generated value
            }
            
            currentIndex++;
            UpdateVisual();

            Debug.Log($"Drew card: {GetCardString(card)} ({RemainingCards()} cards left)");
            return card;
        }
        
        private int GenerateFavorableValueSmart(List<int> targetValues, float favorableProbability)
        {
            // Configurable chance of favorable (adjacent to any target)
            if (_rng.NextDouble() <= favorableProbability && targetValues.Count > 0)
            {
                // Generate value adjacent to ANY of the target values
                List<int> adjacentValues = new List<int>();
                
                foreach (int targetValue in targetValues)
                {
                    int lowerValue = targetValue - 1;
                    int higherValue = targetValue + 1;
                    
                    // Handle wrapping
                    if (lowerValue < 1) lowerValue = 13;
                    if (higherValue > 13) higherValue = 1;
                    
                    if (!adjacentValues.Contains(lowerValue))
                        adjacentValues.Add(lowerValue);
                    if (!adjacentValues.Contains(higherValue))
                        adjacentValues.Add(higherValue);
                }
                
                // Pick random adjacent value
                if (adjacentValues.Count > 0)
                {
                    return adjacentValues[_rng.Next(adjacentValues.Count)];
                }
            }
            
            // Random value
            return _rng.Next(1, 14);
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

        public void UpdateVisual()
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
