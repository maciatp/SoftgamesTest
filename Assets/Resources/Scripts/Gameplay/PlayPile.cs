using UnityEngine;
using TMPro;

namespace TripeaksSolitaire.Gameplay
{
    public class PlayPile : MonoBehaviour
    {
        [Header("Settings")]
        public Vector3 pilePosition = new Vector3(-3f, -3f, 0f);

        [Header("Visual")]
        public SpriteRenderer cardSprite;
        public TextMeshPro valueText;

        [Header("Current Card")]
        public int currentValue;
        public Card.CardType currentType;

        private void Start()
        {
            transform.position = pilePosition;
        }

        public void SetCard(int value, Card.CardType type = Card.CardType.Value)
        {
            currentValue = value;
            currentType = type;
            UpdateVisual();

            Debug.Log($"Play pile now shows: {GetCardString(currentValue)}");
        }

        public void SetCardFromCard(Card card)
        {
            currentValue = card.value;
            currentType = card.cardType;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (cardSprite != null)
            {
                cardSprite.color = Color.white;
            }

            if (valueText != null)
            {
                valueText.text = GetCardString(currentValue);
            }
        }

        public bool CanAcceptCard(Card card)
        {
            // Keys and Zaps can always be played
            if (card.cardType == Card.CardType.Key || card.cardType == Card.CardType.Zap)
            {
                return true;
            }

            // Check if card value is adjacent
            if (card.cardType == Card.CardType.Value && currentType == Card.CardType.Value)
            {
                int diff = Mathf.Abs(card.value - currentValue);
                return diff == 1 || diff == 12; // Adjacent or Ace-King wrap
            }

            return false;
        }

        private string GetCardString(int value)
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
    }
}