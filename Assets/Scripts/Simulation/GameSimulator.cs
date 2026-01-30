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
        private float _finalFavorableProbability = 0.25f;
        private float _bombFavorableProbability = 0.33f;

        // Adapter for FavorableCardGenerator
        private class SimulatorGameState : FavorableCardGenerator.IGameState
        {
            private GameSimulator _simulator;
            
            public SimulatorGameState(GameSimulator simulator)
            {
                _simulator = simulator;
            }
            
            public int GetDrawPileRemainingCards()
            {
                return _simulator._drawPile.Count - _simulator._drawPileIndex;
            }
            
            public List<int> GetUrgentBombValues()
            {
                return _simulator._boardCards
                    .Where(b => b.isOnBoard && b.isFaceUp && b.hasBomb && b.bombTimer <= 3 && b.value != -1)
                    .OrderBy(b => b.bombTimer)
                    .Select(b => b.value)
                    .ToList();
            }
            
            public List<int> GetPlayableCardValues()
            {
                var playableCards = _simulator.GetPlayableCards();
                return playableCards
                    .Where(c => c.value != -1 && !c.isKey && !c.isZap && !c.hasLock)
                    .Select(c => c.value)
                    .ToList();
            }
            
            public int GetCurrentPlayValue()
            {
                return _simulator._currentPlayCard;
            }
        }

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

        public SimulationResult SimulateGame(LevelData levelData, int deckSize, float favorableProbability = 0.51f, float finalFavorableProbability = 0.25f, float bombFavorableProbability = 0.33f)
        {
            _levelData = levelData;
            _gameOver = false;
            _isWin = false;
            _moveCount = 0;
            _drawPileIndex = 0;
            _favorableProbability = favorableProbability;
            _finalFavorableProbability = finalFavorableProbability;
            _bombFavorableProbability = bombFavorableProbability;

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
        /// Generate favorable card value using centralized logic
        /// </summary>
        private int GenerateFavorableCardValue()
        {
            System.Random rng = new System.Random(System.Guid.NewGuid().GetHashCode());
            
            // Use centralized probability calculation
            var gameState = new SimulatorGameState(this);
            float currentProbability = FavorableCardGenerator.CalculateDynamicProbability(
                _favorableProbability,
                _finalFavorableProbability,
                _bombFavorableProbability,
                gameState
            );
            
            // Use centralized card generation
            return FavorableCardGenerator.GenerateFavorableCard(currentProbability, gameState, rng);
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
                    _moveCount++;

                    // Decrement all bomb timers
                    DecrementAllBombTimers();
                }
                else
                {
                    // No cards left to draw
                    _gameOver = true;
                    _isWin = false;
                }
            }
        }

        private void PlayCard(SimCard card)
        {
            // Handle special effects
            if (card.isKey)
            {
                UnlockAllLocks();
            }
            else if (card.isZap)
            {
                ClearHorizontalRow(card);
            }

            // Set current play card
            _currentPlayCard = card.value;

            // Remove card from board
            card.isOnBoard = false;

            _moveCount++;

            // Reveal newly uncovered cards
            RevealUncoveredCards();

            // Decrement all bomb timers
            DecrementAllBombTimers();

            // Check for win
            if (AllCardsCleared())
            {
                _gameOver = true;
                _isWin = true;
            }
        }

        private List<SimCard> GetPlayableCards()
        {
            // A card is playable if it's on the board, face up, and not covered by other cards
            var playable = new List<SimCard>();
            
            foreach (var card in _boardCards.Where(c => c.isOnBoard && c.isFaceUp))
            {
                // Check if any other card is covering this one
                bool isCovered = false;
                foreach (var other in _boardCards.Where(c => c.isOnBoard && c.id != card.id))
                {
                    // Check if other card overlaps and is lower depth (covers this card)
                    if (other.depth < card.depth)
                    {
                        float distance = Vector2.Distance(card.position, other.position);
                        if (distance < 1.5f) // Cards overlap
                        {
                            isCovered = true;
                            break;
                        }
                    }
                }
                
                if (!isCovered)
                {
                    playable.Add(card);
                }
            }
            
            return playable;
        }

        private bool CanPlayCard(SimCard card)
        {
            if (card.hasLock) return false;
            if (card.isKey) return true;
            if (card.isZap) return true;
            
            int diff = Mathf.Abs(card.value - _currentPlayCard);
            return diff == 1 || diff == 12; // Adjacent or wrap (Ace-King)
        }

        private SimCard ChooseBestCard(List<SimCard> validCards)
        {
            // Prioritize: Keys, Zaps, Cards with bombs, then random
            var keyCard = validCards.FirstOrDefault(c => c.isKey);
            if (keyCard != null) return keyCard;

            var zapCard = validCards.FirstOrDefault(c => c.isZap);
            if (zapCard != null) return zapCard;

            var bombCard = validCards.FirstOrDefault(c => c.hasBomb && c.bombTimer <= 2);
            if (bombCard != null) return bombCard;

            return validCards[0]; // Just pick first
        }

        private void UnlockAllLocks()
        {
            foreach (var card in _boardCards.Where(c => c.hasLock && c.isOnBoard))
            {
                card.hasLock = false;
            }
        }

        private void ClearHorizontalRow(SimCard zapCard)
        {
            float y = zapCard.position.y;
            foreach (var card in _boardCards.Where(c => c.isOnBoard && Mathf.Approximately(c.position.y, y) && c.id != zapCard.id))
            {
                card.isOnBoard = false;
            }
        }

        private void RevealUncoveredCards()
        {
            // Reveal all uncovered cards
            foreach (var card in _boardCards.Where(c => c.isOnBoard && !c.isFaceUp))
            {
                // Check if any other card is covering this one
                bool isCovered = false;
                foreach (var other in _boardCards.Where(c => c.isOnBoard && c.id != card.id))
                {
                    if (other.depth < card.depth)
                    {
                        float distance = Vector2.Distance(card.position, other.position);
                        if (distance < 1.5f)
                        {
                            isCovered = true;
                            break;
                        }
                    }
                }
                
                if (!isCovered)
                {
                    card.isFaceUp = true;
                    // Generate favorable value if not assigned
                    if (card.value == -1)
                    {
                        card.value = GenerateFavorableCardValue();
                    }
                }
            }
        }

        private void DecrementAllBombTimers()
        {
            foreach (var bomb in _boardCards.Where(b => b.isOnBoard && b.hasBomb))
            {
                bomb.bombTimer--;
                if (bomb.bombTimer <= 0)
                {
                    _gameOver = true;
                    _isWin = false;
                }
            }
        }

        private bool AllCardsCleared()
        {
            return _boardCards.All(c => !c.isOnBoard);
        }
    }
}
