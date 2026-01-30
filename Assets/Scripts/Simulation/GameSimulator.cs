using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TripeaksSolitaire.Core;

namespace TripeaksSolitaire.Simulation
{
    public class GameSimulator
    {
        private LevelData _levelData;
        private List<SimCard> _boardCards;
        private List<int> _drawPile;
        private int _drawPileIndex;
        private int _currentPlayCard;
        private int _moveCount;
        private bool _gameOver;
        private bool _isWin;
        private TripeaksGameLogic _gameLogic = new TripeaksGameLogic();
        private float _favorableProbability = 0.51f;

        // Simplified card for simulation (no MonoBehaviour)
        public class SimCard
        {
            public string id;
            public int value;
            public int depth;
            public Vector2 position;
            public bool isOnBoard = true;
            public bool isFaceUp;
            public bool hasLock;
            public bool isKey;
            public bool isZap;
            public bool hasBomb;
            public int bombTimer;
            public int initialBombTimer;

            public SimCard(CardData data)
            {
                id = data.id;
                depth = data.depth;
                position = new Vector2(data.x, data.y);
                isFaceUp = data.faceUp;

                // Set value (defer if random for favorable generation)
                if (data.type == "value")
                {
                    if (data.random)
                    {
                        // If face up, generate value immediately; otherwise defer
                        if (data.faceUp)
                        {
                            System.Random rng = new System.Random(System.Guid.NewGuid().GetHashCode());
                            value = rng.Next(1, 14);
                        }
                        else
                        {
                            value = -1; // Deferred generation
                        }
                    }
                    else
                    {
                        value = data.value;
                    }
                }

                // Set special types
                hasLock = data.type == "lock";
                isKey = data.type == "key";
                isZap = data.type == "zap";

                // Check for bomb modifier
                hasBomb = false;
                if (data.modifiers != null)
                {
                    foreach (var mod in data.modifiers)
                    {
                        if (mod.type == "bomb")
                        {
                            hasBomb = true;
                            bombTimer = mod.properties.timer;
                            initialBombTimer = mod.properties.timer;
                        }
                    }
                }
            }
        }

        public class SimulationResult
        {
            public bool isWin;
            public bool isCloseWin;
            public int cardsRemainingInDraw;
            public int moveCount;
            public string lossReason;
        }

        public SimulationResult SimulateGame(LevelData levelData, int deckSize, float favorableProbability = 0.51f)
        {
            _levelData = levelData;
            _gameOver = false;
            _isWin = false;
            _moveCount = 0;
            _drawPileIndex = 0;
            _favorableProbability = favorableProbability;

            // Initialize board
            _boardCards = new List<SimCard>();
            foreach (var cardData in levelData.cards)
            {
                _boardCards.Add(new SimCard(cardData));
            }

            // Initialize draw pile with deferred generation (all -1)
            _drawPile = new List<int>();
            for (int i = 0; i < deckSize; i++)
            {
                _drawPile.Add(-1); // Will be generated on demand
            }

            // Draw initial card
            if (_drawPile.Count > 0)
            {
                _currentPlayCard = GenerateFavorableCardValue();
                _drawPile[_drawPileIndex] = _currentPlayCard;
                _drawPileIndex++;
            }

            // Play game until game over
            int maxTurns = 1000; // Safety limit
            int turnCount = 0;

            while (!_gameOver && turnCount < maxTurns)
            {
                PlayTurn();
                turnCount++;
            }

            int cardsLeft = _drawPile.Count - _drawPileIndex;

            return new SimulationResult
            {
                isWin = _isWin,
                isCloseWin = _isWin && cardsLeft <= 2,
                cardsRemainingInDraw = cardsLeft,
                moveCount = _moveCount,
                lossReason = _isWin ? "" : "No valid moves or bomb exploded"
            };
        }

        /// <summary>
        /// Generate favorable card value with smart priority system
        /// Priority: 1. Adjacent to bombs, 2. Adjacent to playable cards, 3. Adjacent to play pile
        /// </summary>
        private int GenerateFavorableCardValue()
        {
            System.Random rng = new System.Random(System.Guid.NewGuid().GetHashCode());
            
            // Check if we should generate favorable
            if (rng.NextDouble() > _favorableProbability)
            {
                return rng.Next(1, 14);
            }

            List<int> favorableValues = new List<int>();

            // PRIORITY 1: Adjacent to visible bombs with low timers
            var urgentBombs = _boardCards
                .Where(b => b.isOnBoard && b.isFaceUp && b.hasBomb && b.bombTimer <= 3 && b.value != -1)
                .OrderBy(b => b.bombTimer)
                .ToList();

            foreach (var bomb in urgentBombs)
            {
                int bombValue = bomb.value;
                int lowerValue = bombValue - 1;
                int higherValue = bombValue + 1;
                
                if (lowerValue < 1) lowerValue = 13;
                if (higherValue > 13) higherValue = 1;
                
                if (!favorableValues.Contains(lowerValue)) favorableValues.Add(lowerValue);
                if (!favorableValues.Contains(higherValue)) favorableValues.Add(higherValue);
            }

            // PRIORITY 2: Adjacent to playable cards on board
            var playableCards = GetPlayableCards()
                .Where(c => c.value != -1 && !c.isKey && !c.isZap && !c.hasLock)
                .ToList();

            foreach (var card in playableCards)
            {
                int cardValue = card.value;
                int lowerValue = cardValue - 1;
                int higherValue = cardValue + 1;
                
                if (lowerValue < 1) lowerValue = 13;
                if (higherValue > 13) higherValue = 1;
                
                if (!favorableValues.Contains(lowerValue)) favorableValues.Add(lowerValue);
                if (!favorableValues.Contains(higherValue)) favorableValues.Add(higherValue);
            }

            // PRIORITY 3: Adjacent to current play pile
            int lower = _currentPlayCard - 1;
            int higher = _currentPlayCard + 1;
            
            if (lower < 1) lower = 13;
            if (higher > 13) higher = 1;
            
            if (!favorableValues.Contains(lower)) favorableValues.Add(lower);
            if (!favorableValues.Contains(higher)) favorableValues.Add(higher);
            
            // Pick random favorable value
            if (favorableValues.Count > 0)
            {
                return favorableValues[rng.Next(favorableValues.Count)];
            }
            
            return rng.Next(1, 14);
        }

        private void PlayTurn()
        {
            // Get playable cards
            List<SimCard> playableCards = GetPlayableCards();
            List<SimCard> validMoves = playableCards.Where(c => CanPlayCard(c)).ToList();

            if (validMoves.Count > 0)
            {
                // We have valid cards to play - pick best one
                SimCard cardToPlay = ChooseBestCard(validMoves);
                PlayCard(cardToPlay);
            }
            else
            {
                // No valid cards - must draw from pile
                if (_drawPileIndex < _drawPile.Count)
                {
                    int card = _drawPile[_drawPileIndex];
                    // Generate favorable value if not assigned
                    if (card == -1)
                    {
                        card = GenerateFavorableCardValue();
                        _drawPile[_drawPileIndex] = card;
                    }
                    _currentPlayCard = card;
                    _drawPileIndex++;
                    IncrementMove();
                }
                else
                {
                    // No cards in draw pile and no valid moves - game over
                    GameOver(false);
                }
            }
        }

        private SimCard ChooseBestCard(List<SimCard> validCards)
        {
            // Advanced AI Strategy:
            // 1. URGENT: Play bombs about to explode (timer <= 2)
            // 2. PRIORITY: Play keys if locks exist
            // 3. STRATEGIC: Prefer cards that uncover the most other cards
            // 4. DEPTH: Prefer higher depth cards (they're on top, easier to access)
            // 5. AVOID: Don't unnecessarily draw if we have valid moves

            // 1. Check for urgent bombs
            var urgentBombs = validCards.Where(c => c.hasBomb && c.bombTimer <= 2).ToList();
            if (urgentBombs.Count > 0)
            {
                // Play the most urgent bomb
                return urgentBombs.OrderBy(c => c.bombTimer).First();
            }

            // 2. Check for keys if locks exist
            bool hasLockedCards = _boardCards.Any(c => c.isOnBoard && c.hasLock);
            if (hasLockedCards)
            {
                var keys = validCards.Where(c => c.isKey).ToList();
                if (keys.Count > 0)
                {
                    return keys.First();
                }
            }

            // 3. Check for zaps - only use if they clear multiple cards
            var zaps = validCards.Where(c => c.isZap).ToList();
            if (zaps.Count > 0)
            {
                // Count how many cards each zap would clear
                var bestZap = zaps.OrderByDescending(z => CountCardsInRow(z)).First();
                if (CountCardsInRow(bestZap) >= 3) // Only use if clears 3+ cards
                {
                    return bestZap;
                }
            }

            // 4. Score each card based on strategic value
            var scoredCards = validCards.Select(card => new
            {
                card = card,
                score = CalculateCardScore(card)
            }).OrderByDescending(x => x.score).ToList();

            // Pick the best scored card
            return scoredCards.First().card;
        }

        private int CalculateCardScore(SimCard card)
        {
            int score = 0;

            // Higher depth = higher priority (cards on top)
            score += card.depth * 10;

            // Cards that uncover more cards are better
            int uncoversCount = CountCardsThisWouldUncover(card);
            score += uncoversCount * 20;

            // Bombs get priority based on timer
            if (card.hasBomb)
            {
                score += (card.initialBombTimer - card.bombTimer) * 5;
            }

            // Keys are valuable if locks exist
            if (card.isKey && _boardCards.Any(c => c.isOnBoard && c.hasLock))
            {
                score += 50;
            }

            return score;
        }

        private int CountCardsThisWouldUncover(SimCard card)
        {
            return _gameLogic.CountCardsUncovered(
                card,
                _boardCards,
                c => c.isOnBoard,
                c => c.depth,
                c => c.position
            );
        }

        private int CountCardsInRow(SimCard zapCard)
        {
            var cardsInRow = _gameLogic.GetCardsInRow(
                zapCard,
                _boardCards,
                c => c.isOnBoard,
                c => c.position
            );
            return cardsInRow.Count;
        }

        private bool CanPlayCard(SimCard card)
        {
            if (card.hasLock) return false;
            if (card.isKey || card.isZap) return true;

            // Check if value is adjacent
            int diff = Mathf.Abs(card.value - _currentPlayCard);
            return diff == 1 || diff == 12;
        }

        private void PlayCard(SimCard card)
        {
            // Handle special cards
            if (card.isKey)
            {
                UnlockAllLocks();
            }
            else if (card.isZap)
            {
                ClearRow(card);
            }

            // Update play pile ONLY for value cards
            // Keys and Zaps don't change the current play card value
            if (!card.isKey && !card.isZap)
            {
                _currentPlayCard = card.value;
            }

            // Remove from board
            card.isOnBoard = false;

            // Reveal any cards that are now uncovered
            RevealUncoveredCards();

            IncrementMove();
            CheckWinCondition();
        }

        private void RevealUncoveredCards()
        {
            _gameLogic.RevealUncoveredCards(
                _boardCards,
                c => c.isOnBoard,
                c => c.isFaceUp,
                c => {
                    c.isFaceUp = true;
                    // Generate favorable value if not assigned
                    if (c.value == -1)
                    {
                        c.value = GenerateFavorableCardValue();
                    }
                },
                c => c.depth,
                c => c.position
            );
        }

        private void IncrementMove()
        {
            _moveCount++;

            // Decrement bomb timers ONLY for face-up bombs
            foreach (var card in _boardCards)
            {
                if (card.hasBomb && card.isOnBoard && card.isFaceUp)
                {
                    card.bombTimer--;
                    if (card.bombTimer <= 0)
                    {
                        GameOver(false);
                        return;
                    }
                }
            }
        }

        private void UnlockAllLocks()
        {
            // Remove all lock cards from the board
            foreach (var card in _boardCards)
            {
                if (card.hasLock && card.isOnBoard)
                {
                    card.isOnBoard = false;
                }
            }
        }

        private void ClearRow(SimCard zapCard)
        {
            float yThreshold = 30f;
            foreach (var card in _boardCards)
            {
                if (!card.isOnBoard) continue;
                if (card == zapCard) continue;

                float yDiff = Mathf.Abs(card.position.y - zapCard.position.y);
                if (yDiff < yThreshold)
                {
                    card.isOnBoard = false;
                }
            }
        }

        private List<SimCard> GetPlayableCards()
        {
            List<SimCard> playable = new List<SimCard>();

            foreach (var card in _boardCards)
            {
                if (!card.isOnBoard) continue;
                if (!card.isFaceUp) continue; // Only face-up cards are playable!
                if (!IsCovered(card))
                {
                    playable.Add(card);
                }
            }

            return playable;
        }

        private bool IsCovered(SimCard card)
        {
            foreach (var other in _boardCards)
            {
                if (!other.isOnBoard) continue;
                if (other == card) continue;
                if (other.depth <= card.depth) continue;

                float xDiff = Mathf.Abs(card.position.x - other.position.x);
                float yDiff = Mathf.Abs(card.position.y - other.position.y);

                // Updated to match Board.cs threshold
                if (xDiff < 150f && yDiff < 150f)
                {
                    return true;
                }
            }
            return false;
        }

        private void CheckWinCondition()
        {
            int cardsOnBoard = _boardCards.Count(c => c.isOnBoard);

            if (cardsOnBoard == 0)
            {
                GameOver(true);
            }
        }

        private void GameOver(bool win)
        {
            _gameOver = true;
            _isWin = win;
        }
    }
}