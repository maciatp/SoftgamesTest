using UnityEngine;
using TripeaksSolitaire.Core;
using TripeaksSolitaire.Gameplay;
using TripeaksSolitaire.UI;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour, FavorableCardGenerator.IGameState
{
    [Header("References")]
    public Board board;
    public DrawPile drawPile;
    public PlayPile playPile;
    public GameOverUI gameOverUI;

    [Header("Level Selection")]
    [Tooltip("Drag and drop the JSON file from Resources/Levels/ here")]
    public TextAsset levelJsonFile;

    [Header("Game State")]
    public int moveCount = 0;
    public bool gameOver = false;
    public bool isWin = false;

    [Header("Favorable Randomness")]
    [Range(0f, 1f)]
    [Tooltip("Base probability of generating favorable cards")]
    public float favorableProbability = 0.51f;
    
    [Range(0f, 1f)]
    [Tooltip("Probability boost when draw pile has <= 2 cards remaining")]
    public float finalFavorableProbability = 0.25f; // +25%
    public int minimumCardsToIncreaseProbability = 5;

    [Range(0f, 1f)]
    [Tooltip("Probability boost when urgent bomb (timer <= 2) is on board")]
    public float bombFavorableProbability = 0.33f; // +33%
    public int bombTimerToIncreaseProbability = 4;
    
    [Header("Current Probability (Read Only)")]
    [Range(0f, 1f)]
    [Tooltip("Current effective probability with all boosts applied")]
    public float currentEffectiveProbability = 0.51f; // Updated in real-time

    private float _currentFavorableProbability; // Cached current probability
    private bool _hadUrgentBomb = false; // Track if we had urgent bomb
    private bool _hadFinalStageBoost = false; // Track if we had final stage boost

    [Header("Undo System")]
    public UnityEngine.UI.Button undoButton;
    UnityEngine.UI.Text undoCountText; // Optional: shows undo count
    int maxUndoSteps = 999; // Unlimited by default

    private Stack<GameState> _undoStack = new Stack<GameState>();

    private LevelData _currentLevel;
    private List<Card> _allBombs = new List<Card>();
    private TripeaksGameLogic _gameLogic = new TripeaksGameLogic();

    // Implementation of IGameState interface
    public int GetDrawPileRemainingCards() => drawPile.RemainingCards();
    
    public List<int> GetUrgentBombValues()
    {
        return _allBombs
            .Where(b => b.isOnBoard && b.isFaceUp && b.bombModifier != null && b.bombModifier.timer <= 3 && b.value != -1)
            .OrderBy(b => b.bombModifier.timer)
            .Select(b => b.value)
            .ToList();
    }
    
    public List<int> GetPlayableCardValues()
    {
        // Use Board's existing playability system
        var playableCards = board.allCards
            .Where(c => c.isOnBoard && c.isPlayable && c.value != -1 && 
                       c.cardType != Card.CardType.Key && 
                       c.cardType != Card.CardType.Zap && 
                       c.cardType != Card.CardType.Lock)
            .ToList();
        
        return playableCards.Select(c => c.value).ToList();
    }
    
    public int GetCurrentPlayValue() => playPile.currentValue;

    void Start()
    {
        LoadAndStartLevel();
        UpdateUndoButton();
    }

    void Update()
    {
        // Update current effective probability in real-time (for Inspector display)
        if (!gameOver)
        {
            CalculateDynamicFavorableProbability();
        }
        
        // Press Z to undo
        if (Input.GetKeyDown(KeyCode.Z) && _undoStack.Count > 0)
        {
            UndoLastMove();
        }

        // Press R to restart level
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Restarting level...");
            LoadAndStartLevel();
        }

        // Press L to reload level (useful after changing JSON file)
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("Reloading level...");
            LoadAndStartLevel();
        }
    }

    public void LoadAndStartLevel()
    {                
        if (levelJsonFile == null)
        {
            Debug.LogError("❌ No level JSON file assigned! Please drag a JSON file to the 'Level Json File' field in the Inspector.");
            return;
        }
        
        // Hide game over UI if showing
        if (gameOverUI != null)
        {
            gameOverUI.Hide();
        }
        
        Debug.Log($"Loading level: {levelJsonFile.name}");
        LevelData levelData = ParseLevelJSON(levelJsonFile.text, levelJsonFile.name);

        if (levelData != null)
        {
            ValidateAndStartLevel(levelData);
        }
        else
        {
            Debug.LogError($"❌ Failed to load level from {levelJsonFile.name}");
        }
    }

    private LevelData ParseLevelJSON(string jsonContent, string fileName)
    {
        try
        {
            LevelData levelData = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelData>(jsonContent);

            if (levelData == null)
            {
                Debug.LogError($"❌ Failed to deserialize JSON from: {fileName}");
                return null;
            }

            Debug.Log($"✅ Successfully parsed JSON from: {fileName}");
            return levelData;
        }
        catch (Newtonsoft.Json.JsonException e)
        {
            Debug.LogError($"❌ JSON parsing error in {fileName}:");
            Debug.LogError($"   Message: {e.Message}");
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Unexpected error parsing {fileName}: {e.Message}");
            return null;
        }
    }

    private void ValidateAndStartLevel(LevelData levelData)
    {
        Debug.Log($"=== VALIDATING LEVEL DATA ===");

        bool hasErrors = false;

        // Validate basic structure
        if (levelData.cards == null)
        {
            Debug.LogError("❌ CRITICAL: 'cards' array is null or missing!");
            hasErrors = true;
        }
        else if (levelData.cards.Count == 0)
        {
            Debug.LogError("❌ CRITICAL: 'cards' array is empty!");
            hasErrors = true;
        }
        else
        {
            Debug.Log($"✅ Found {levelData.cards.Count} cards in level");
        }

        if (levelData.settings == null)
        {
            Debug.LogError("❌ CRITICAL: 'settings' object is null or missing!");
            hasErrors = true;
        }
        else
        {
            ValidateSettings(levelData.settings, ref hasErrors);
        }

        if (levelData.cards != null)
        {
            ValidateCards(levelData.cards, ref hasErrors);
        }

        // Check for unknown properties
        if (string.IsNullOrEmpty(levelData.id))
        {
            Debug.LogWarning("⚠️ WARNING: 'id' field is missing or empty");
        }

        if (string.IsNullOrEmpty(levelData.version))
        {
            Debug.LogWarning("⚠️ WARNING: 'version' field is missing or empty");
        }

        if (hasErrors)
        {
            Debug.LogError("=== VALIDATION FAILED - CANNOT START LEVEL ===");
            return;
        }

        Debug.Log("=== VALIDATION PASSED - STARTING LEVEL ===");
        StartLevel(levelData);
    }

    private void ValidateSettings(LevelSettings settings, ref bool hasErrors)
    {
        Debug.Log("--- Validating Settings ---");

        if (settings.cards_in_stack == null)
        {
            Debug.LogError("❌ CRITICAL: 'cards_in_stack' is null or missing!");
            hasErrors = true;
        }
        else if (settings.cards_in_stack.Count == 0)
        {
            Debug.LogError("❌ CRITICAL: 'cards_in_stack' is empty!");
            hasErrors = true;
        }
        else
        {
            Debug.Log($"✅ Draw pile size: {settings.cards_in_stack.Count} cards");

            // Check for invalid values
            bool hasInvalidValues = settings.cards_in_stack.Any(v => v != -1 && (v < 1 || v > 13));
            if (hasInvalidValues)
            {
                Debug.LogError("❌ ERROR: 'cards_in_stack' contains invalid card values (must be -1 or 1-13)");
                hasErrors = true;
            }
        }

        if (settings.level_number <= 0)
        {
            Debug.LogWarning($"⚠️ WARNING: 'level_number' is {settings.level_number} (expected positive number)");
        }
        else
        {
            Debug.Log($"✅ Level number: {settings.level_number}");
        }

        if (string.IsNullOrEmpty(settings.background))
        {
            Debug.LogWarning("⚠️ WARNING: 'background' field is missing or empty");
        }
        else
        {
            Debug.Log($"✅ Background: {settings.background}");
        }

        if (settings.win_criteria == null || settings.win_criteria.Count == 0)
        {
            Debug.LogWarning("⚠️ WARNING: 'win_criteria' is missing or empty (defaulting to clear_all)");
        }
        else
        {
            Debug.Log($"✅ Win criteria: {settings.win_criteria[0].type}");
        }
    }

    private void ValidateCards(List<CardData> cards, ref bool hasErrors)
    {
        Debug.Log("--- Validating Cards ---");

        HashSet<string> cardIds = new HashSet<string>();
        int validCards = 0;
        int cardsWithErrors = 0;

        for (int i = 0; i < cards.Count; i++)
        {
            CardData card = cards[i];
            bool cardHasError = false;

            // Check required fields
            if (string.IsNullOrEmpty(card.id))
            {
                Debug.LogError($"❌ Card [{i}]: Missing 'id' field");
                cardHasError = true;
            }
            else if (cardIds.Contains(card.id))
            {
                Debug.LogError($"❌ Card [{i}]: Duplicate card ID '{card.id}'");
                cardHasError = true;
            }
            else
            {
                cardIds.Add(card.id);
            }

            // Validate type
            if (string.IsNullOrEmpty(card.type))
            {
                Debug.LogError($"❌ Card '{card.id}': Missing 'type' field");
                cardHasError = true;
            }
            else if (card.type != "value" && card.type != "lock" && card.type != "key" && card.type != "zap")
            {
                Debug.LogError($"❌ Card '{card.id}': Unknown type '{card.type}' (valid: value, lock, key, zap)");
                cardHasError = true;
            }

            // Validate depth
            if (card.depth < 0)
            {
                Debug.LogError($"❌ Card '{card.id}': Invalid depth {card.depth} (must be >= 0)");
                cardHasError = true;
            }

            // Validate value for non-random cards
            if (!card.random && card.type == "value")
            {
                if (card.value < 1 || card.value > 52)
                {
                    Debug.LogWarning($"⚠️ Card '{card.id}': Value {card.value} is outside typical range (1-52)");
                }
            }

            // Validate modifiers
            if (card.modifiers != null && card.modifiers.Count > 0)
            {
                foreach (var modifier in card.modifiers)
                {
                    if (string.IsNullOrEmpty(modifier.type))
                    {
                        Debug.LogError($"❌ Card '{card.id}': Modifier missing 'type' field");
                        cardHasError = true;
                    }
                    else if (modifier.type == "bomb")
                    {
                        if (modifier.properties == null)
                        {
                            Debug.LogError($"❌ Card '{card.id}': Bomb modifier missing 'properties'");
                            cardHasError = true;
                        }
                        else if (modifier.properties.timer <= 0)
                        {
                            Debug.LogError($"❌ Card '{card.id}': Bomb timer must be > 0 (got {modifier.properties.timer})");
                            cardHasError = true;
                        }
                        else
                        {
                            Debug.Log($"  💣 Card '{card.id}': Bomb with timer {modifier.properties.timer}");
                        }
                    }
                    else if (modifier.type != "bomb")
                    {
                        Debug.LogWarning($"⚠️ Card '{card.id}': Unknown modifier type '{modifier.type}'");
                    }
                }
            }

            if (cardHasError)
            {
                cardsWithErrors++;
                hasErrors = true;
            }
            else
            {
                validCards++;
            }
        }

        Debug.Log($"✅ Valid cards: {validCards}");
        if (cardsWithErrors > 0)
        {
            Debug.LogError($"❌ Cards with errors: {cardsWithErrors}");
        }

        // Check for depth consistency
        var depthGroups = cards.GroupBy(c => c.depth).OrderBy(g => g.Key);
    }

    private void StartLevel(LevelData levelData)
    {
        // Reset game state
        moveCount = 0;
        gameOver = false;
        isWin = false;
        
        // Clear undo stack
        _undoStack.Clear();
        UpdateUndoButton();

        // Initialize components
        board.LoadLevel(levelData);
        drawPile.Initialize(levelData);

        // Collect all bombs
        _allBombs = board.allCards.Where(c => c.hasBomb).ToList();
        if (_allBombs.Count > 0)
        {
            Debug.Log($"💣 Found {_allBombs.Count} bombs in level");
        }

        // Draw initial card from pile
        DrawInitialCard();

        Debug.Log($"=== LEVEL STARTED SUCCESSFULLY ===");
    }

    private void DrawInitialCard()
    {
        if (!drawPile.IsEmpty())
        {
            // Get initial card from draw pile
            int card = drawPile.deck[0];
            if (card == -1)
            {
                // Generate using smart favorable logic
                card = GenerateFavorableCardValue();
                drawPile.deck[0] = card;
            }
            drawPile.currentIndex = 1;
            playPile.SetCard(card);
            drawPile.UpdateVisual();
        }
    }

    public void OnCardClicked(Card card)
    {
        if (gameOver) return;

        if (!card.isPlayable)
        {
            Debug.Log($"Card {card.cardId} is not playable (covered)");
            return;
        }

        if (card.cardType == Card.CardType.Lock)
        {
            Debug.Log($"Card {card.cardId} is a lock! Use a Key to remove it.");
            return;
        }

        // Check if card can be played on current play pile card
        if (playPile.CanAcceptCard(card))
        {
            // Save state before playing
            SaveCurrentState();
            PlayCard(card);
        }
        else
        {
            Debug.Log($"Card {card.cardId} ({card.value}) cannot be played on {playPile.currentValue}");
        }
    }

    private void PlayCard(Card card)
    {

        // Handle special card types
        if (card.cardType == Card.CardType.Key)
        {
            UnlockAllLocks();
        }
        else if (card.cardType == Card.CardType.Zap)
        {
            ClearHorizontalRow(card);
        }

        // Update play pile
        playPile.SetCardFromCard(card);

        // Remove card from board
        board.RemoveCardFromBoard(card);
        card.gameObject.SetActive(false);

        // Increment move counter
        IncrementMove();

        // Check win condition
        CheckWinCondition();
    }

    public void OnDrawPileClicked()
    {
        if (gameOver) return;

        if (drawPile.IsEmpty())
        {
            Debug.Log("Draw pile is empty!");
            // Check if we have any playable moves
            if (!HasPlayableCards())
            {
                GameOver(false, "No more cards in draw pile and no valid moves!");
            }
            return;
        }

        // Generate card value FIRST if needed (before saving state)
        int card = drawPile.deck[drawPile.currentIndex];
        if (card == -1)
        {
            card = GenerateFavorableCardValue();
            drawPile.deck[drawPile.currentIndex] = card;           
        }
        else
        {
            Debug.Log($"📋 Using existing card value: {card}");
        }

        // NOW save state (after generating value)
        SaveCurrentState();

        // Draw the card
        drawPile.currentIndex++;
        drawPile.UpdateVisual();
        
        playPile.SetCard(card);

        Debug.Log($"Drew card from pile: {card} ({drawPile.RemainingCards()} cards remaining)");

        // Increment move counter (drawing also counts as a move)
        IncrementMove();
        
        // Check if this was the last card and we still can't win
        if (drawPile.IsEmpty() && !HasPlayableCards())
        {
            int cardsOnBoard = board.allCards.Count(c => c.isOnBoard);
            if (cardsOnBoard > 0)
            {
                GameOver(false, "Drew last card but no valid moves remain!");
            }
        }
    }

    private void IncrementMove()
    {
        moveCount++;
        
        // Decrement all bomb timers
        foreach (var bomb in _allBombs)
        {
            if (bomb.isOnBoard && bomb.isFaceUp)
            {
                bomb.DecrementBombTimer();

                if (bomb.bombModifier.timer <= 0)
                {
                    GameOver(false, "Bomb exploded!");
                    return;
                }
            }
        }
    }

    private void UnlockAllLocks()
    {
        var locksToRemove = _gameLogic.GetLockCards(
            board.allCards,
            c => c.isOnBoard,
            c => c.cardType == Card.CardType.Lock
        );
        
        foreach (var lockCard in locksToRemove)
        {
            board.RemoveCardFromBoard(lockCard);
            lockCard.gameObject.SetActive(false);
        }
        
        Debug.Log($"🔑 Key used! Removed {locksToRemove.Count} lock cards from board!");
    }

    private void ClearHorizontalRow(Card zapCard)
    {
        var cardsToRemove = _gameLogic.GetCardsInRow(
            zapCard,
            board.allCards,
            c => c.isOnBoard,
            c => c.boardPosition
        );

        foreach (var card in cardsToRemove)
        {
            board.RemoveCardFromBoard(card);
            card.gameObject.SetActive(false);
        }

        Debug.Log($"⚡ Zap cleared {cardsToRemove.Count} cards from row!");
    }

    private void CheckWinCondition()
    {
        // Check if all cards are cleared from board
        int cardsOnBoard = board.allCards.Count(c => c.isOnBoard);

        if (cardsOnBoard == 0)
        {
            bool isCloseWin = drawPile.IsCloseWin();
            GameOver(true, isCloseWin ? "CLOSE WIN! 🎉" : "Win!");
        }
        else if (drawPile.IsEmpty() && !HasPlayableCards())
        {
            GameOver(false, "No more moves!");
        }
    }

    private bool HasPlayableCards()
    {
        var playableCards = board.GetPlayableCards();
        return _gameLogic.HasValidMoves(
            playableCards,
            playPile.currentValue,
            c => c.value,
            c => c.cardType == Card.CardType.Key,
            c => c.cardType == Card.CardType.Zap,
            c => c.hasLock
        );
    }

    /// <summary>
    /// Calculate current favorable probability based on game state
    /// Only logs when boosts actually change
    /// </summary>
    private float CalculateDynamicFavorableProbability()
    {
        float probability = favorableProbability; // Start with base
        
        // BOOST 1: Low cards in draw pile
        int cardsInDraw = drawPile.RemainingCards();
        bool hasFinalStageBoost = cardsInDraw <= minimumCardsToIncreaseProbability;
        
        if (hasFinalStageBoost)
        {
            probability += finalFavorableProbability;
            
            // Only log when boost STARTS
            if (!_hadFinalStageBoost)
            {
                _hadFinalStageBoost = true;
                Debug.Log($"🎯 Final stage boost: +{finalFavorableProbability:P0} (Draw pile: {cardsInDraw} cards)");
            }
        }
        else if (_hadFinalStageBoost)
        {
            // Boost ended (shouldn't happen normally, but handle it)
            _hadFinalStageBoost = false;
        }
        
        // BOOST 2: Urgent bomb on board
        var urgentBombs = _allBombs
            .Where(b => b.isOnBoard && b.isFaceUp && b.bombModifier != null && b.bombModifier.timer <= bombTimerToIncreaseProbability)
            .ToList();
        
        bool hasUrgentBomb = urgentBombs.Count > 0;
        
        if (hasUrgentBomb)
        {
            probability += bombFavorableProbability;
            
            // Only log when bomb boost STARTS
            if (!_hadUrgentBomb)
            {
                _hadUrgentBomb = true;
                Debug.Log($"💣 Urgent bomb boost: +{bombFavorableProbability:P0} (Bomb timer: {urgentBombs[0].bombModifier.timer})");
            }
        }
        else if (_hadUrgentBomb)
        {
            // Bomb was cleared
            _hadUrgentBomb = false;
            Debug.Log($"✅ Bomb cleared, probability returned to normal");
        }
        
        // Cap at 1.0 (100%)
        probability = Mathf.Min(probability, 1.0f);
        
        // Update visible slider in Inspector
        currentEffectiveProbability = probability;
        
        // Only log overall probability when it changes significantly
        if (Mathf.Abs(probability - _currentFavorableProbability) > 0.01f)
        {
            _currentFavorableProbability = probability;
            Debug.Log($"📊 Current favorable probability: {probability:P1}");
        }
        
        return probability;
    }

    /// <summary>
    /// Generate a favorable card value considering current game state
    /// Priority: 1. Adjacent to bombs, 2. Adjacent to playable cards, 3. Adjacent to play pile
    /// </summary>
    public int GenerateFavorableCardValue()
    {
        System.Random rng = new System.Random(System.DateTime.Now.Millisecond + UnityEngine.Random.Range(0, 10000));
        
        // Get dynamic probability based on game state
        float currentProbability = CalculateDynamicFavorableProbability();
        
        // Check if we should generate favorable
        if (rng.NextDouble() > currentProbability)
        {
            // Random generation
            return rng.Next(1, 14);
        }

        List<int> favorableValues = new List<int>();

        // PRIORITY 1: Adjacent to visible bombs with low timers
        var urgentBombs = _allBombs
            .Where(b => b.isOnBoard && b.isFaceUp && b.bombModifier != null && b.bombModifier.timer <= bombTimerToIncreaseProbability && b.value != -1)
            .OrderBy(b => b.bombModifier.timer)
            .ToList();

        if (urgentBombs.Count > 0)
        {
            // Generate value ADJACENT to bomb (so it can be played!)
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
        }

        // PRIORITY 2: Adjacent to playable cards on board
        var playableCards = board.GetPlayableCards()
            .Where(c => c.value != -1 && c.cardType == Card.CardType.Value)
            .ToList();

        if (playableCards.Count > 0)
        {
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
        }

        // PRIORITY 3: Adjacent to current play pile
        int currentValue = playPile.currentValue;
        int lower = currentValue - 1;
        int higher = currentValue + 1;
        
        if (lower < 1) lower = 13;
        if (higher > 13) higher = 1;
        
        if (!favorableValues.Contains(lower)) favorableValues.Add(lower);
        if (!favorableValues.Contains(higher)) favorableValues.Add(higher);
        
        // Pick random favorable value
        if (favorableValues.Count > 0)
        {
            return favorableValues[rng.Next(favorableValues.Count)];
        }
        
        // Fallback to random
        return rng.Next(1, 14);
    }

    private void GameOver(bool win, string reason)
    {
        gameOver = true;
        isWin = win;

        // Start coroutine to show UI after 1 second delay
        StartCoroutine(ShowGameOverUIDelayed(win, reason));
    }

    private System.Collections.IEnumerator ShowGameOverUIDelayed(bool win, string reason)
    {
        // Wait 1 second before showing UI
        yield return new WaitForSeconds(0.5f);

        int cardsOnBoard = board.allCards.Count(c => c.isOnBoard);
        int cardsInDraw = drawPile.RemainingCards();
        bool isCloseWin = win && cardsInDraw <= 2;

        // Show UI message
        if (gameOverUI != null)
        {
            if (win)
            {
                gameOverUI.ShowVictory(isCloseWin);
            }
            else
            {
                gameOverUI.ShowDefeat(reason);
            }
        }

        // Log to console
        string message = new string("");
        
        if (win)
        {
            message += $"🎉 VICTORY! {reason}\n";
            message += new string('=', 50) + "\n";
            message += $"Final Stats:\n";
            message += $"  • Total Moves: {moveCount}\n";
            message += $"  • Cards Remaining in Draw Pile: {cardsInDraw}\n";
            
            if (isCloseWin)
            {
                message += $"  • 🌟 CLOSE WIN ACHIEVED! ({cardsInDraw} cards left)\n";
            }
        }
        else
        {
            message += $"💀 GAME OVER\n";
            message += new string('=', 50) + "\n";
            message += $"Defeat Reason: {reason}\n";
            message += $"\n";
            message += $"Final Stats:\n";
            message += $"  • Total Moves: {moveCount}\n";
            message += $"  • Cards Left on Board: {cardsOnBoard}\n";
            message += $"  • Cards Remaining in Draw Pile: {cardsInDraw}\n";
        }
        
        message += new string('=', 50);

        Debug.Log(message);
    }

    private void SaveCurrentState()
    {
        GameState state = new GameState(playPile, drawPile, board, moveCount);
        _undoStack.Push(state);
        
        // Limit stack size if needed
        if (_undoStack.Count > maxUndoSteps)
        {
            // Remove oldest state (bottom of stack)
            var tempList = _undoStack.ToList();
            tempList.RemoveAt(tempList.Count - 1);
            _undoStack = new Stack<GameState>(tempList.Reverse<GameState>());
        }
        
        UpdateUndoButton();
    }

    public void UndoLastMove()
    {
        if (_undoStack.Count == 0)
        {
            Debug.Log("⚠️ No moves to undo");
            return;
        }

        Debug.Log($"↩️ Undoing move... ({_undoStack.Count - 1} undos remaining)");
        
        // Pop and restore state
        GameState previousState = _undoStack.Pop();
        previousState.RestoreState(playPile, drawPile, board, moveCount);
        
        // Restore move count
        moveCount = previousState.GetMoveCount();
        
        UpdateUndoButton();
        Debug.Log($"✅ Move undone! ({_undoStack.Count} undos available)");
    }

    private void UpdateUndoButton()
    {
        bool canUndo = _undoStack.Count > 0;
        
        if (undoButton != null)
        {
            undoButton.interactable = canUndo;
        }
        
        if (undoCountText != null)
        {
            undoCountText.text = _undoStack.Count > 0 ? $"Undo ({_undoStack.Count})" : "Undo";
        }
    }
}
