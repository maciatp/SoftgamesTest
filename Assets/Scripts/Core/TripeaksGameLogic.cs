using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TripeaksSolitaire.Core
{
    /// <summary>
    /// Shared game logic for Tripeaks Solitaire.
    /// Used by both GameManager (real gameplay) and GameSimulator (AI simulation).
    /// </summary>
    public class TripeaksGameLogic
    {
        // Card overlap detection threshold
        private const float OVERLAP_THRESHOLD = 150f;
        private const float ZAP_ROW_THRESHOLD = 30f;

        /// <summary>
        /// Get optimal target values for favorable card generation based on playable cards and current play value
        /// </summary>
        public List<int> GetOptimalTargetValues<T>(List<T> playableCards, int currentPlayValue,
                                                   System.Func<T, int> getValue, System.Func<T, bool> hasBomb,
                                                   System.Func<T, bool> isKey, System.Func<T, bool> isZap) where T : class
        {
            List<int> targetValues = new List<int>();
            
            // Priority 1: Bomb cards (most urgent!)
            var bombCards = playableCards.Where(c => hasBomb(c) && getValue(c) != -1).ToList();
            if (bombCards.Count > 0)
            {
                foreach (var bomb in bombCards)
                {
                    targetValues.Add(getValue(bomb));
                }
            }
            
            // Priority 2: Other playable value cards
            var valueCards = playableCards.Where(c => !hasBomb(c) && !isKey(c) && !isZap(c) && getValue(c) != -1).ToList();
            foreach (var card in valueCards)
            {
                targetValues.Add(getValue(card));
            }
            
            // Priority 3: Current play value (fallback)
            targetValues.Add(currentPlayValue);
            
            return targetValues;
        }

        /// <summary>
        /// Check if a card can be played on the current play card value
        /// </summary>
        public bool CanPlayCard(int cardValue, bool isKey, bool isZap, bool hasLock, int currentPlayValue)
        {
            // Locked cards cannot be played
            if (hasLock) return false;
            
            // Keys and Zaps can always be played
            if (isKey || isZap) return true;

            // Check if value is adjacent (or wraps Ace-King)
            int diff = Mathf.Abs(cardValue - currentPlayValue);
            return diff == 1 || diff == 12;
        }

        /// <summary>
        /// Check if a card is covered by any other card
        /// </summary>
        public bool IsCovered<T>(T card, List<T> allCards, System.Func<T, bool> isOnBoard, 
                                  System.Func<T, int> getDepth, System.Func<T, Vector2> getPosition) where T : class
        {
            Vector2 cardPos = getPosition(card);
            int cardDepth = getDepth(card);

            foreach (var other in allCards)
            {
                if (!isOnBoard(other)) continue;
                if (other == card) continue;
                if (getDepth(other) <= cardDepth) continue;

                Vector2 otherPos = getPosition(other);
                float xDiff = Mathf.Abs(cardPos.x - otherPos.x);
                float yDiff = Mathf.Abs(cardPos.y - otherPos.y);

                if (xDiff < OVERLAP_THRESHOLD && yDiff < OVERLAP_THRESHOLD)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get all playable cards (face-up and not covered)
        /// </summary>
        public List<T> GetPlayableCards<T>(List<T> allCards, System.Func<T, bool> isOnBoard, 
                                           System.Func<T, bool> isFaceUp, System.Func<T, int> getDepth, 
                                           System.Func<T, Vector2> getPosition) where T : class
        {
            List<T> playable = new List<T>();

            foreach (var card in allCards)
            {
                if (!isOnBoard(card)) continue;
                if (!isFaceUp(card)) continue; // Only face-up cards are playable
                
                if (!IsCovered(card, allCards, isOnBoard, getDepth, getPosition))
                {
                    playable.Add(card);
                }
            }

            return playable;
        }

        /// <summary>
        /// Reveal any cards that are now uncovered
        /// </summary>
        public void RevealUncoveredCards<T>(List<T> allCards, System.Func<T, bool> isOnBoard, 
                                            System.Func<T, bool> isFaceUp, System.Action<T> setFaceUp,
                                            System.Func<T, int> getDepth, System.Func<T, Vector2> getPosition) where T : class
        {
            foreach (var card in allCards)
            {
                if (!isOnBoard(card)) continue;
                if (isFaceUp(card)) continue; // Skip already face-up cards

                // If card is not covered, reveal it
                if (!IsCovered(card, allCards, isOnBoard, getDepth, getPosition))
                {
                    setFaceUp(card);
                }
            }
        }

        /// <summary>
        /// Get all cards in the same horizontal row as the zap card
        /// </summary>
        public List<T> GetCardsInRow<T>(T zapCard, List<T> allCards, System.Func<T, bool> isOnBoard, 
                                        System.Func<T, Vector2> getPosition) where T : class
        {
            List<T> cardsInRow = new List<T>();
            Vector2 zapPos = getPosition(zapCard);

            foreach (var card in allCards)
            {
                if (!isOnBoard(card)) continue;
                if (card == zapCard) continue;

                Vector2 cardPos = getPosition(card);
                float yDiff = Mathf.Abs(cardPos.y - zapPos.y);
                
                if (yDiff < ZAP_ROW_THRESHOLD)
                {
                    cardsInRow.Add(card);
                }
            }

            return cardsInRow;
        }

        /// <summary>
        /// Get all lock cards on the board
        /// </summary>
        public List<T> GetLockCards<T>(List<T> allCards, System.Func<T, bool> isOnBoard, 
                                       System.Func<T, bool> isLock) where T : class
        {
            List<T> lockCards = new List<T>();

            foreach (var card in allCards)
            {
                if (!isOnBoard(card)) continue;
                if (isLock(card))
                {
                    lockCards.Add(card);
                }
            }

            return lockCards;
        }

        /// <summary>
        /// Check if there are any valid moves available
        /// </summary>
        public bool HasValidMoves<T>(List<T> playableCards, int currentPlayValue, 
                                     System.Func<T, int> getValue, System.Func<T, bool> isKey, 
                                     System.Func<T, bool> isZap, System.Func<T, bool> hasLock) where T : class
        {
            foreach (var card in playableCards)
            {
                if (CanPlayCard(getValue(card), isKey(card), isZap(card), hasLock(card), currentPlayValue))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Count how many cards would be uncovered by removing a specific card
        /// </summary>
        public int CountCardsUncovered<T>(T cardToRemove, List<T> allCards, System.Func<T, bool> isOnBoard, 
                                          System.Func<T, int> getDepth, System.Func<T, Vector2> getPosition) where T : class
        {
            int count = 0;

            foreach (var otherCard in allCards)
            {
                if (!isOnBoard(otherCard)) continue;
                if (otherCard == cardToRemove) continue;

                // Check if this card is covered ONLY by cardToRemove
                if (IsCoveredByOnly(otherCard, cardToRemove, allCards, isOnBoard, getDepth, getPosition))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsCoveredByOnly<T>(T card, T coveringCard, List<T> allCards, System.Func<T, bool> isOnBoard,
                                        System.Func<T, int> getDepth, System.Func<T, Vector2> getPosition) where T : class
        {
            Vector2 cardPos = getPosition(card);
            int cardDepth = getDepth(card);
            int coverCount = 0;

            foreach (var other in allCards)
            {
                if (!isOnBoard(other)) continue;
                if (other == card) continue;
                if (getDepth(other) <= cardDepth) continue;

                Vector2 otherPos = getPosition(other);
                float xDiff = Mathf.Abs(cardPos.x - otherPos.x);
                float yDiff = Mathf.Abs(cardPos.y - otherPos.y);

                if (xDiff < OVERLAP_THRESHOLD && yDiff < OVERLAP_THRESHOLD)
                {
                    coverCount++;
                }
            }

            return coverCount == 1;
        }
    }
}
