using UnityEngine;
using System.Collections.Generic;
using TripeaksSolitaire.Core;
using TMPro;

public class Card : MonoBehaviour
{
    [Header("Card Data")]
    public string cardId;
    public CardType cardType;
    public int value; // 1-13 (Ace to King)
    public int depth;
    public bool isFaceUp;
    public bool isPlayable;

    [Header("Modifiers")]
    public bool hasLock;
    public bool isKey;
    public bool isZap;
    public bool hasBomb;
    public BombModifier bombModifier;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public TextMeshPro valueText;
    public GameObject lockIcon;
    public GameObject bombIcon;
    public TextMeshPro bombTimerText;

    [Header("State")]
    public bool isOnBoard = true;
    public Vector2 boardPosition;

    private CardData _sourceData;

    public enum CardType
    {
        Value,
        Lock,
        Key,
        Zap,
        Bomb
    }

    public void Initialize(CardData data)
    {
        _sourceData = data;
        cardId = data.id;
        depth = data.depth;
        isFaceUp = data.faceUp;
        boardPosition = new Vector2(data.x, data.y);

        // Set card type
        switch (data.type)
        {
            case "value":
                cardType = CardType.Value;
                break;
            case "lock":
                cardType = CardType.Lock;
                hasLock = true;
                break;
            case "key":
                cardType = CardType.Key;
                isKey = true;
                break;
            case "zap":
                cardType = CardType.Zap;
                isZap = true;
                break;
            case "bomb":
                cardType = CardType.Bomb;
                break;
        }

        // Set value (random or fixed)
        if (cardType == CardType.Value)
        {
            if (data.random)
            {
                // If face up, generate value immediately; otherwise defer
                if (data.faceUp)
                {
                    // Generate value immediately for face-up cards
                    System.Random rng = new System.Random(System.DateTime.Now.Millisecond + GetInstanceID() + (int)(data.x * 1000 + data.y));
                    value = rng.Next(1, 14);
                }
                else
                {
                    // Defer value generation until card is revealed (favorable randomness)
                    value = -1; // -1 means not yet assigned
                }
            }
            else
            {
                value = data.value;
            }
        }

        // Handle modifiers - CHECK IF BOMB EXISTS
        hasBomb = false;
        if (data.modifiers != null && data.modifiers.Count > 0)
        {
            foreach (var mod in data.modifiers)
            {
                if (mod.type == "bomb")
                {
                    hasBomb = true;
                    bombModifier = new BombModifier
                    {
                        timer = mod.properties.timer,
                        initialTimer = mod.properties.timer
                    };
                    Debug.Log($"💣 Card {cardId} has bomb with timer {bombModifier.timer}");
                }
            }
        }

        // Force ensure valueText GameObject is active before calling UpdateVisual
        if (valueText != null && valueText.gameObject != null)
        {
            valueText.gameObject.SetActive(true);
        }

        // Call UpdateVisual in next frame to ensure Unity hierarchy is ready
        StartCoroutine(UpdateVisualNextFrame());
    }
    
    private System.Collections.IEnumerator UpdateVisualNextFrame()
    {
        yield return null; // Wait one frame
        UpdateVisual();
    }

    public void UpdateVisual()
    {
        // Update sprite color based on face up/down
        if (spriteRenderer != null)
        {
            if (isFaceUp)
            {
                spriteRenderer.color = Color.white;
            }
            else
            {
                // Face down - darker blue
                spriteRenderer.color = new Color(0.3f, 0.3f, 0.6f);
            }
        }

        // Update value text
        if (valueText != null)
        {
            if (!isFaceUp)
            {
                // Card is face down - show nothing
                valueText.text = "";
                valueText.gameObject.SetActive(false);
            }
            else if (cardType == CardType.Value)
            {
                valueText.text = GetCardValueString();
                valueText.gameObject.SetActive(true);
            }
            else if (cardType == CardType.Lock)
            {
                valueText.text = "LOCK";
                valueText.gameObject.SetActive(true);
            }
            else if (cardType == CardType.Key)
            {
                valueText.text = "KEY";
                valueText.gameObject.SetActive(true);
            }
            else if (cardType == CardType.Zap)
            {
                valueText.text = "ZAP";
                valueText.gameObject.SetActive(true);
            }
            else
            {
                valueText.gameObject.SetActive(false);
            }
        }

        // Update bomb timer - ONLY IF BOMB EXISTS
        if (hasBomb && bombModifier != null && isFaceUp)
        {
            if (bombTimerText != null)
            {
                bombTimerText.text = bombModifier.timer.ToString();
                bombTimerText.gameObject.SetActive(true);
            }
            if (bombIcon != null)
            {
                bombIcon.SetActive(true);
            }
        }
        else
        {
            // Hide bomb visuals if no bomb
            if (bombTimerText != null)
            {
                bombTimerText.gameObject.SetActive(false);
            }
            if (bombIcon != null)
            {
                bombIcon.SetActive(false);
            }
        }

        // Update lock icon
        if (lockIcon != null)
        {
            lockIcon.SetActive(hasLock);
        }
    }

    /// <summary>
    /// Generate card value with configurable probability, considering multiple target values
    /// </summary>
    public void GenerateFavorableValueSmart(List<int> targetValues, float favorableProbability = 0.51f)
    {
        // Only generate if not already assigned
        if (value != -1) return;

        System.Random rng = new System.Random(System.DateTime.Now.Millisecond + GetInstanceID());
        
        // Configurable chance of favorable (adjacent to any target)
        if (rng.NextDouble() <= favorableProbability && targetValues.Count > 0)
        {
            // Generate value adjacent to ANY of the target values
            List<int> adjacentValues = new List<int>();
            
            foreach (int targetValue in targetValues)
            {
                int lowerValue = targetValue - 1;
                int higherValue = targetValue + 1;
                
                // Handle wrapping
                if (lowerValue < 1) lowerValue = 13;
                if (higherValue > 13) higherValue = 1;
                
                if (!adjacentValues.Contains(lowerValue))
                    adjacentValues.Add(lowerValue);
                if (!adjacentValues.Contains(higherValue))
                    adjacentValues.Add(higherValue);
            }
            
            // Pick random adjacent value
            if (adjacentValues.Count > 0)
            {
                value = adjacentValues[rng.Next(adjacentValues.Count)];
            }
            else
            {
                value = rng.Next(1, 14);
            }
        }
        else
        {
            // Random value
            value = rng.Next(1, 14);
        }
        
        UpdateVisual();
    }

    /// <summary>
    /// Generate card value with configurable probability of being favorable (adjacent to target)
    /// </summary>
    public void GenerateFavorableValue(int targetValue, float favorableProbability = 0.51f)
    {
        // Only generate if not already assigned
        if (value != -1) return;

        System.Random rng = new System.Random(System.DateTime.Now.Millisecond + GetInstanceID());
        
        // Configurable chance of favorable (adjacent card)
        if (rng.NextDouble() <= favorableProbability)
        {
            // Generate adjacent value
            List<int> adjacentValues = new List<int>();
            
            // Add adjacent values
            int lowerValue = targetValue - 1;
            int higherValue = targetValue + 1;
            
            // Handle wrapping
            if (lowerValue < 1) lowerValue = 13; // Ace wraps to King
            if (higherValue > 13) higherValue = 1; // King wraps to Ace
            
            adjacentValues.Add(lowerValue);
            adjacentValues.Add(higherValue);
            
            // Pick random adjacent value
            value = adjacentValues[rng.Next(adjacentValues.Count)];
        }
        else
        {
            // Random value
            value = rng.Next(1, 14);
        }
        
        UpdateVisual();
    }

    private string GetCardValueString()
    {
        switch (value)
        {
            case 1: return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            default: return value.ToString();
        }
    }

    public bool CanPlayOn(Card targetCard)
    {
        if (!isPlayable) return false;
        if (cardType == CardType.Lock && hasLock) return false;

        // Keys and Zaps can always be played
        if (cardType == CardType.Key || cardType == CardType.Zap) return true;

        // Value cards: check if adjacent value
        if (cardType == CardType.Value && targetCard.cardType == CardType.Value)
        {
            int diff = Mathf.Abs(value - targetCard.value);
            return diff == 1 || diff == 12; // Adjacent or Ace-King wrap
        }

        return false;
    }

    public void DecrementBombTimer()
    {
        // ONLY decrement if this card HAS a bomb
        if (!hasBomb || bombModifier == null) return;

        bombModifier.timer--;
        UpdateVisual();

        if (bombModifier.timer <= 0)
        {
            // Bomb exploded! Game over
            Debug.LogWarning($"💣💥 Bomb {cardId} exploded!");
        }
    }

    private void OnMouseDown()
    {
        if (!isOnBoard) return;

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.OnCardClicked(this);
        }
    }
}

[System.Serializable]
public class BombModifier
{
    public int timer;
    public int initialTimer;
}
