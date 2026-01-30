using System.Collections.Generic;
using UnityEngine;
using TripeaksSolitaire.Gameplay;

namespace TripeaksSolitaire.Core
{
    /// <summary>
    /// Represents a snapshot of the game state for undo functionality
    /// </summary>
    [System.Serializable]
    public class GameState
    {
        // Play pile state
        public int playPileValue;

        // Draw pile state
        public int drawPileIndex;
        public List<int> drawPileDeck;

        // Board cards state
        public List<CardState> boardCards;

        // Move count
        public int moveCount;

        [System.Serializable]
        public class CardState
        {
            public string id;
            public int value;
            public bool isFaceUp;
            public bool isOnBoard;
            public int bombTimer;

            public CardState(Card card)
            {
                id = card.cardId;
                value = card.value;
                isFaceUp = card.isFaceUp;
                isOnBoard = card.isOnBoard;
                bombTimer = card.hasBomb && card.bombModifier != null ? card.bombModifier.timer : -1;
            }
        }

        public GameState(PlayPile playPile, DrawPile drawPile, Board board, int currentMoveCount)
        {
            // Save play pile
            playPileValue = playPile.currentValue;

            // Save draw pile
            drawPileIndex = drawPile.currentIndex;
            drawPileDeck = new List<int>(drawPile.deck); // Deep copy

            // Save board cards
            boardCards = new List<CardState>();
            foreach (var card in board.allCards)
            {
                boardCards.Add(new CardState(card));
            }

            // Save move count
            moveCount = currentMoveCount;
        }

        public void RestoreState(PlayPile playPile, DrawPile drawPile, Board board, int currentMoveCount)
        {
            // Restore play pile
            playPile.SetCard(playPileValue);

            // Restore draw pile
            drawPile.currentIndex = drawPileIndex;
            drawPile.deck = new List<int>(drawPileDeck); // Restore deck
            drawPile.UpdateVisual();

            // Restore board cards
            foreach (var cardState in boardCards)
            {
                Card card = board.cardDictionary[cardState.id];
                card.value = cardState.value;
                card.isFaceUp = cardState.isFaceUp;
                card.isOnBoard = cardState.isOnBoard;
                
                // Restore bomb timer
                if (card.hasBomb && card.bombModifier != null && cardState.bombTimer != -1)
                {
                    card.bombModifier.timer = cardState.bombTimer;
                }

                // Update visuals
                card.gameObject.SetActive(cardState.isOnBoard);
                card.UpdateVisual();
            }

            // Update board playability
            board.UpdateAllCardsPlayability();
        }

        public int GetMoveCount()
        {
            return moveCount;
        }
    }
}
