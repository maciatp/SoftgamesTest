using UnityEngine;
using TripeaksSolitaire.Core;
using TripeaksSolitaire.Gameplay;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public Board board;
    public DrawPile drawPile;
    public PlayPile playPile;

    [Header("Settings")]
    public int levelToLoad = 25;

    [Header("Game State")]
    public int moveCount = 0;
    public bool gameOver = false;
    public bool isWin = false;

    private LevelData _currentLevel;
    private List<Card> _allBombs = new List<Card>();

    void Start()
    {
        LoadAndStartLevel(levelToLoad);
    }

    public void LoadAndStartLevel(int levelNumber)
    {
        Debug.Log($"Loading level {levelNumber}...");

        _currentLevel = LevelLoader.LoadLevelByNumber(levelNumber);

        if (_currentLevel != null)
        {
            // Reset game state
            moveCount = 0;
            gameOver = false;
            isWin = false;

            // Initialize components
            board.LoadLevel(_currentLevel);
            drawPile.Initialize(_currentLevel);

            // Collect all bombs
            _allBombs = board.allCards.Where(c => c.hasBomb).ToList();
            Debug.Log($"Found {_allBombs.Count} bombs in level");

            // Draw initial card from pile
            DrawInitialCard();

            Debug.Log($"✅ Level {levelNumber} loaded successfully!");
        }
        else
        {
            Debug.LogError($"❌ Failed to load level {levelNumber}");
        }
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

        // You can add UI popup here later
    }

    void Update()
    {
        // Press R to restart level
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Restarting level...");
            LoadAndStartLevel(levelToLoad);
        }

        // Press 1-4 to load different levels
        if (Input.GetKeyDown(KeyCode.Alpha1)) LoadAndStartLevel(25);
        if (Input.GetKeyDown(KeyCode.Alpha2)) LoadAndStartLevel(31);
        if (Input.GetKeyDown(KeyCode.Alpha3)) LoadAndStartLevel(43);
        if (Input.GetKeyDown(KeyCode.Alpha4)) LoadAndStartLevel(54);
    }
}