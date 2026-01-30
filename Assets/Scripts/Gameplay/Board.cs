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
        private TripeaksGameLogic _gameLogic = new TripeaksGameLogic();

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

            foreach (var cardData in _currentLevel.cards)
            {
                CreateCard(cardData);
            }

            // Update playability after all cards are created
            UpdateAllCardsPlayability();
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
            return _gameLogic.IsCovered(
                card,
                allCards,
                c => c.isOnBoard,
                c => c.depth,
                c => c.boardPosition
            );
        }

        public List<Card> GetPlayableCards()
        {
            return _gameLogic.GetPlayableCards(
                allCards,
                c => c.isOnBoard,
                c => c.isFaceUp,
                c => c.depth,
                c => c.boardPosition
            );
        }

        public void RemoveCardFromBoard(Card card)
        {
            // IMPORTANT: Update playability BEFORE marking card as removed
            // This ensures we check coverage with the card still present
            card.isOnBoard = false;
            
            // Now update playability for remaining cards
            UpdateAllCardsPlayability();
            
            // Finally, reveal any cards that are now uncovered
            RevealUncoveredCards();
        }

        private void RevealUncoveredCards()
        {
            // Get GameManager reference
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm == null) return;
            
            _gameLogic.RevealUncoveredCards(
                allCards,
                c => c.isOnBoard,
                c => c.isFaceUp,
                c => { 
                    c.isFaceUp = true;
                    // Generate favorable value if not assigned using GameManager's smart logic
                    if (c.value == -1)
                    {
                        c.value = gm.GenerateFavorableCardValue();
                    }
                    c.UpdateVisual();
                },
                c => c.depth,
                c => c.boardPosition
            );
        }
    }
}
