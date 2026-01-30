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
        private float _favorableProbability = 0.15f;
        private float _finalFavorableProbability = 0.66f;
        private float _bombFavorableProbability = 0.33f;
        private int _minimumCardsToIncreaseProbability = 2;
        private int _bombTimerToIncreaseProbability = 3;

        // Adapter for FavorableCardGenerator - OPTIMIZED with caching
        private class SimulatorGameState : FavorableCardGenerator.IGameState
        {
            private GameSimulator _simulator;
            private List<int> _urgentBombCache = new List<int>();
            private List<int> _playableValuesCache = new List<int>();
            
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
                _urgentBombCache.Clear();
                
                // Direct loop instead of LINQ for performance
                for (int i = 0; i < _simulator._boardCards.Count; i++)
                {
                    var b = _simulator._boardCards[i];
                    if (b.isOnBoard && b.isFaceUp && b.hasBomb && b.bombTimer <= _simulator._bombTimerToIncreaseProbability && b.value != -1)
                    {
                        _urgentBombCache.Add(b.value);
                    }
                }
                
                return _urgentBombCache;
            }
            
            public List<int> GetPlayableCardValues()
            {
                _playableValuesCache.Clear();
                var playableCards = _simulator.GetPlayableCards();
                
                // Direct loop instead of LINQ
                for (int i = 0; i < playableCards.Count; i++)
                {
                    var c = playableCards[i];
                    if (c.value != -1 && !c.isKey && !c.isZap && !c.hasLock)
                    {
                        _playableValuesCache.Add(c.value);
                    }
                }
                
                return _playableValuesCache;
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

        public SimulationResult SimulateGame(LevelData levelData, int deckSize, float favorableProbability = 0.51f, float finalFavorableProbability = 0.25f, float bombFavorableProbability = 0.33f, int minimumCardsToIncreaseProbability = 2, int bombTimerToIncreaseProbability = 3)
        {
            _levelData = levelData;
            _gameOver = false;
            _isWin = false;
            _moveCount = 0;
            _drawPileIndex = 0;
            _favorableProbability = favorableProbability;
            _finalFavorableProbability = finalFavorableProbability;
            _bombFavorableProbability = bombFavorableProbability;
            _minimumCardsToIncreaseProbability = minimumCardsToIncreaseProbability;
            _bombTimerToIncreaseProbability = bombTimerToIncreaseProbability;

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

        private List<SimCard> _validMovesCache = new List<SimCard>();
        
        private void PlayTurn()
        {
            // Get playable cards
            List<SimCard> playableCards = GetPlayableCards();
            
            // Filter valid moves (reuse cache)
            _validMovesCache.Clear();
            for (int i = 0; i < playableCards.Count; i++)
            {
                if (CanPlayCard(playableCards[i]))
                {
                    _validMovesCache.Add(playableCards[i]);
                }
            }

            if (_validMovesCache.Count > 0)
            {
                // We have valid cards to play - pick best one
                SimCard cardToPlay = ChooseBestCard(_validMovesCache);
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

        private List<SimCard> _playableCardsCache = new List<SimCard>();
        
        private List<SimCard> GetPlayableCards()
        {
            _playableCardsCache.Clear();
            
            // A card is playable if it's on the board, face up, and not covered by other cards
            for (int i = 0; i < _boardCards.Count; i++)
            {
                var card = _boardCards[i];
                if (!card.isOnBoard || !card.isFaceUp) continue;
                
                // Check if any other card is covering this one
                bool isCovered = false;
                for (int j = 0; j < _boardCards.Count; j++)
                {
                    if (j == i) continue;
                    
                    var other = _boardCards[j];
                    if (!other.isOnBoard) continue;
                    
                    // Check if other card overlaps and is lower depth (covers this card)
                    if (other.depth < card.depth)
                    {
                        float dx = card.position.x - other.position.x;
                        float dy = card.position.y - other.position.y;
                        float distanceSq = dx * dx + dy * dy; // Avoid sqrt
                        if (distanceSq < 2.25f) // 1.5 * 1.5 = 2.25
                        {
                            isCovered = true;
                            break;
                        }
                    }
                }
                
                if (!isCovered)
                {
                    _playableCardsCache.Add(card);
                }
            }
            
            return _playableCardsCache;
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
            // Use direct loop for performance
            for (int i = 0; i < validCards.Count; i++)
            {
                if (validCards[i].isKey) return validCards[i];
            }
            
            for (int i = 0; i < validCards.Count; i++)
            {
                if (validCards[i].isZap) return validCards[i];
            }
            
            for (int i = 0; i < validCards.Count; i++)
            {
                if (validCards[i].hasBomb && validCards[i].bombTimer <= 2)
                    return validCards[i];
            }

            return validCards[0]; // Just pick first
        }

        private void UnlockAllLocks()
        {
            for (int i = 0; i < _boardCards.Count; i++)
            {
                var card = _boardCards[i];
                if (card.hasLock && card.isOnBoard)
                {
                    card.hasLock = false;
                }
            }
        }

        private void ClearHorizontalRow(SimCard zapCard)
        {
            float y = zapCard.position.y;
            for (int i = 0; i < _boardCards.Count; i++)
            {
                var card = _boardCards[i];
                if (card.isOnBoard && card.id != zapCard.id && Mathf.Abs(card.position.y - y) < 0.01f)
                {
                    card.isOnBoard = false;
                }
            }
        }

        private void RevealUncoveredCards()
        {
            // Reveal all uncovered cards
            for (int i = 0; i < _boardCards.Count; i++)
            {
                var card = _boardCards[i];
                if (!card.isOnBoard || card.isFaceUp) continue;
                
                // Check if any other card is covering this one
                bool isCovered = false;
                for (int j = 0; j < _boardCards.Count; j++)
                {
                    if (j == i) continue;
                    
                    var other = _boardCards[j];
                    if (!other.isOnBoard) continue;
                    
                    if (other.depth < card.depth)
                    {
                        float dx = card.position.x - other.position.x;
                        float dy = card.position.y - other.position.y;
                        float distanceSq = dx * dx + dy * dy;
                        if (distanceSq < 2.25f)
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
            for (int i = 0; i < _boardCards.Count; i++)
            {
                var bomb = _boardCards[i];
                if (bomb.isOnBoard && bomb.hasBomb)
                {
                    bomb.bombTimer--;
                    if (bomb.bombTimer <= 0)
                    {
                        _gameOver = true;
                        _isWin = false;
                        return; // Early exit
                    }
                }
            }
        }

        private bool AllCardsCleared()
        {
            for (int i = 0; i < _boardCards.Count; i++)
            {
                if (_boardCards[i].isOnBoard)
                    return false;
            }
            return true;
        }
    }
}
