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
                value = Random.Range(1, 14); // 1-13
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

        UpdateVisual();
    }

    public void UpdateVisual()
    {
        // Update sprite color based on face up/down
        if (spriteRenderer != null)
        {
            spriteRenderer.color = isFaceUp ? Color.white : new Color(0.5f, 0.5f, 0.8f);
        }

        // Update value text
        if (valueText != null)
        {
            if (isFaceUp && cardType == CardType.Value)
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
        if (cardType == CardType.Bomb && hasBomb && bombModifier != null)
        {
            if (bombTimerText != null)
            {
                bombTimerText.text = "💣" + bombModifier.timer.ToString();
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