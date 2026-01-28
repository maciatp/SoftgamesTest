using System.Collections.Generic;
using UnityEngine;
using TripeaksSolitaire.Core;
using System.Linq;

namespace TripeaksSolitaire.Gameplay
{
    public class Board : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject cardPrefab;

        [Header("Settings")]
        public float cardWidth = 1.5f;
        public float cardHeight = 2f;
        public float depthSpacing = 0.1f;
        public float worldScale = 0.01f; // Scale factor for JSON coordinates

        [Header("Runtime")]
        public List<Card> allCards = new List<Card>();
        public Dictionary<string, Card> cardDictionary = new Dictionary<string, Card>();

        private LevelData _currentLevel;

        public void LoadLevel(LevelData levelData)
        {
            _currentLevel = levelData;
            ClearBoard();
            GenerateBoard();
        }

        private void ClearBoard()
        {
            foreach (var card in allCards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }
            allCards.Clear();
            cardDictionary.Clear();
        }

        private void GenerateBoard()
        {
            if (_currentLevel == null || _currentLevel.cards == null)
            {
                Debug.LogError("No level data to generate board");
                return;
            }

            Debug.Log($"Generating board with {_currentLevel.cards.Count} cards");

            foreach (var cardData in _currentLevel.cards)
            {
                CreateCard(cardData);
            }

            // Update playability after all cards are created
            UpdateAllCardsPlayability();

            Debug.Log($"Board generated successfully with {allCards.Count} cards");
        }

        private void CreateCard(CardData data)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("Card prefab is not assigned!");
                return;
            }

            GameObject cardObj = Instantiate(cardPrefab, transform);
            Card card = cardObj.GetComponent<Card>();

            if (card == null)
            {
                Debug.LogError("Card prefab doesn't have Card component!");
                Destroy(cardObj);
                return;
            }

            // Initialize card data
            card.Initialize(data);

            // Set position (convert JSON coordinates to world space)
            Vector3 worldPos = new Vector3(
                data.x * worldScale,
                data.y * worldScale,
                -data.depth * depthSpacing // Use depth for Z-sorting
            );
            cardObj.transform.position = worldPos;

            // Set rotation
            cardObj.transform.rotation = Quaternion.Euler(0, 0, data.angle);

            // Set name for debugging
            cardObj.name = $"Card_{data.id}_Depth{data.depth}";

            allCards.Add(card);
            cardDictionary[data.id] = card;
        }

        public void UpdateAllCardsPlayability()
        {
            foreach (var card in allCards)
            {
                if (card.isOnBoard)
                {
                    card.isPlayable = !IsCovered(card);
                }
            }
        }

        public bool IsCovered(Card card)
        {
            // A card is covered if any other card with higher depth overlaps it
            foreach (var otherCard in allCards)
            {
                if (!otherCard.isOnBoard) continue;
                if (otherCard == card) continue;
                if (otherCard.depth <= card.depth) continue;

                // Check if cards overlap (simple circle collision)
                float distance = Vector2.Distance(card.boardPosition, otherCard.boardPosition);
                float overlapThreshold = 50f; // Adjust based on your coordinate system

                if (distance < overlapThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        public List<Card> GetPlayableCards()
        {
            return allCards.Where(c => c.isOnBoard && c.isPlayable).ToList();
        }

        public void RemoveCardFromBoard(Card card)
        {
            card.isOnBoard = false;
            card.isPlayable = false;
            UpdateAllCardsPlayability();
            RevealUncoveredCards(); 
        }
        private void RevealUncoveredCards()
        {
            // Check all face-down cards and reveal them if they're no longer covered
            foreach (var card in allCards)
            {
                if (!card.isOnBoard) continue;
                if (card.isFaceUp) continue; // Skip already face-up cards

                // If card is not covered, reveal it
                if (!IsCovered(card))
                {
                    card.isFaceUp = true;
                    card.UpdateVisual();
                    Debug.Log($"🔍 Revealed card {card.cardId}");
                }
            }
        }
    }
}