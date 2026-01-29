using UnityEngine;
using TripeaksSolitaire.Core;
using TripeaksSolitaire.Gameplay;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public Board board;
    public DrawPile drawPile;
    public PlayPile playPile;

    [Header("Level Selection")]
    [Tooltip("Path to the JSON level file. Can be relative to Resources/Levels/ or absolute path")]
    public string levelFilePath = "level_25.json";
    public bool loadFromResources = true; // If false, will load from absolute path

    [Header("Game State")]
    public int moveCount = 0;
    public bool gameOver = false;
    public bool isWin = false;

    private LevelData _currentLevel;
    private List<Card> _allBombs = new List<Card>();

    void Start()
    {
        LoadAndStartLevel();
    }

    public void LoadAndStartLevel()
    {
        Debug.Log($"=== LOADING LEVEL ===");
        Debug.Log($"File: {levelFilePath}");
        Debug.Log($"Load from Resources: {loadFromResources}");

        _currentLevel = loadFromResources ?
            LoadLevelFromResources(levelFilePath) :
            LoadLevelFromAbsolutePath(levelFilePath);

        if (_currentLevel != null)
        {
            ValidateAndStartLevel(_currentLevel);
        }
        else
        {
            Debug.LogError($"❌ Failed to load level from: {levelFilePath}");
        }
    }

    private LevelData LoadLevelFromResources(string fileName)
    {
        // Remove .json extension if present
        string resourcePath = fileName.Replace(".json", "");

        // Try loading from Resources/Levels/
        TextAsset jsonFile = Resources.Load<TextAsset>($"Levels/{resourcePath}");

        if (jsonFile == null)
        {
            Debug.LogError($"❌ Level file not found in Resources/Levels/: {fileName}");
            Debug.LogError($"Make sure the file is located at: Assets/Resources/Levels/{fileName}");
            return null;
        }

        return ParseLevelJSON(jsonFile.text, fileName);
    }

    private LevelData LoadLevelFromAbsolutePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"❌ Level file not found at path: {filePath}");
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(filePath);
            return ParseLevelJSON(jsonContent, Path.GetFileName(filePath));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error reading file: {e.Message}");
            return null;
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
            //Debug.LogError($"   Line: {e.LineNumber}, Position: {e.LinePosition}");
            //Debug.LogError($"   Path: {e.Path}");
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

        // Check for unknown properties (this is tricky with JSON, but we can check common mistakes)
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

        // Validate star thresholds
        if (settings.star_1 <= 0 || settings.star_2 <= 0 || settings.star_3 <= 0)
        {
            Debug.LogWarning($"⚠️ WARNING: Star thresholds seem invalid: {settings.star_1}, {settings.star_2}, {settings.star_3}");
        }
        else
        {
            Debug.Log($"✅ Star thresholds: {settings.star_1} / {settings.star_2} / {settings.star_3}");
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
        Debug.Log($"Depth distribution:");
        foreach (var group in depthGroups)
        {
            Debug.Log($"  Depth {group.Key}: {group.Count()} cards");
        }
    }

    private void StartLevel(LevelData levelData)
    {
        // Reset game state
        moveCount = 0;
        gameOver = false;
        isWin = false;

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
            int card = drawPile.DrawCard();
            playPile.SetCard(card);
        }
    }

    public void OnCardClicked(Card card)
    {
        if (gameOver) return;

        if (!card.isPlayable)
        {
            Debug.Log($"Card {card.cardId} is not playable (covered or locked)");
            return;
        }

        if (card.cardType == Card.CardType.Lock && card.hasLock)
        {
            Debug.Log($"Card {card.cardId} is locked!");
            return;
        }

        // Check if card can be played on current play pile card
        if (playPile.CanAcceptCard(card))
        {
            PlayCard(card);
        }
        else
        {
            Debug.Log($"Card {card.cardId} ({card.value}) cannot be played on {playPile.currentValue}");
        }
    }

    private void PlayCard(Card card)
    {
        Debug.Log($"Playing card {card.cardId} - {card.cardType}");

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
            return;
        }

        int newCard = drawPile.DrawCard();
        playPile.SetCard(newCard);

        Debug.Log($"Drew new card from pile: {newCard}");

        // Increment move counter (drawing also counts as a move)
        IncrementMove();
    }

    private void IncrementMove()
    {
        moveCount++;
        Debug.Log($"Move #{moveCount}");

        // Decrement all bomb timers
        foreach (var bomb in _allBombs)
        {
            if (bomb.isOnBoard)
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
        int unlockedCount = 0;
        foreach (var card in board.allCards)
        {
            if (card.hasLock && card.isOnBoard)
            {
                card.hasLock = false;
                card.UpdateVisual();
                unlockedCount++;
            }
        }
        Debug.Log($"🔓 Unlocked {unlockedCount} locks!");
        board.UpdateAllCardsPlayability();
    }

    private void ClearHorizontalRow(Card zapCard)
    {
        // Find all cards in the same horizontal row (similar Y position)
        float yThreshold = 30f; // Adjust based on your coordinate system
        int clearedCount = 0;

        List<Card> cardsToRemove = new List<Card>();

        foreach (var card in board.allCards)
        {
            if (!card.isOnBoard) continue;
            if (card == zapCard) continue;

            float yDiff = Mathf.Abs(card.boardPosition.y - zapCard.boardPosition.y);
            if (yDiff < yThreshold)
            {
                cardsToRemove.Add(card);
            }
        }

        foreach (var card in cardsToRemove)
        {
            board.RemoveCardFromBoard(card);
            card.gameObject.SetActive(false);
            clearedCount++;
        }

        Debug.Log($"⚡ Zap cleared {clearedCount} cards from row!");
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
        foreach (var card in board.GetPlayableCards())
        {
            if (playPile.CanAcceptCard(card))
            {
                return true;
            }
        }
        return false;
    }

    private void GameOver(bool win, string reason)
    {
        gameOver = true;
        isWin = win;

        string message = win ? $"🎉 YOU WIN! {reason}" : $"💀 GAME OVER: {reason}";
        message += $"\nMoves: {moveCount}";
        message += $"\nCards remaining in draw pile: {drawPile.RemainingCards()}";

        Debug.Log(message);
    }

    void Update()
    {
        // Press R to restart level
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Restarting level...");
            LoadAndStartLevel();
        }

        // Press L to reload level (useful after changing the file path in inspector)
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("Reloading level from file...");
            LoadAndStartLevel();
        }
    }
}