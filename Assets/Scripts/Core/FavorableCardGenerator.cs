using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TripeaksSolitaire.Core
{
    /// <summary>
    /// Centralized logic for favorable card generation with dynamic probability
    /// Used by both GameManager and GameSimulator to avoid code duplication
    /// </summary>
    public static class FavorableCardGenerator
    {
        public interface IGameState
        {
            int GetDrawPileRemainingCards();
            List<int> GetUrgentBombValues(); // Bombs with timer <= 3
            List<int> GetPlayableCardValues(); // Playable cards on board
            int GetCurrentPlayValue();
        }

        /// <summary>
        /// Calculate dynamic favorable probability based on game state
        /// </summary>
        public static float CalculateDynamicProbability(
            float baseProbability,
            float finalStageBonusAmount,
            float urgentBombBonusAmount,
            IGameState gameState)
        {
            float probability = baseProbability;
            
            // BOOST 1: Low cards in draw pile (<= 2 cards)
            int cardsInDraw = gameState.GetDrawPileRemainingCards();
            if (cardsInDraw <= 2)
            {
                probability += finalStageBonusAmount;
            }
            
            // BOOST 2: Urgent bomb on board (timer <= 2)
            var urgentBombs = gameState.GetUrgentBombValues();
            if (urgentBombs.Count > 0)
            {
                probability += urgentBombBonusAmount;
            }
            
            // Cap at 1.0 (100%)
            return Mathf.Min(probability, 1.0f);
        }

        /// <summary>
        /// Generate favorable card value with smart priority system
        /// Priority: 1. Adjacent to bombs, 2. Adjacent to playable cards, 3. Adjacent to play pile
        /// </summary>
        public static int GenerateFavorableCard(
            float currentProbability,
            IGameState gameState,
            System.Random rng)
        {
            // Check if we should generate favorable
            if (rng.NextDouble() > currentProbability)
            {
                return rng.Next(1, 14);
            }

            List<int> favorableValues = new List<int>();

            // PRIORITY 1: Adjacent to urgent bomb values
            var urgentBombs = gameState.GetUrgentBombValues();
            foreach (var bombValue in urgentBombs)
            {
                AddAdjacentValues(favorableValues, bombValue);
            }

            // PRIORITY 2: Adjacent to playable cards
            var playableCards = gameState.GetPlayableCardValues();
            foreach (var cardValue in playableCards)
            {
                AddAdjacentValues(favorableValues, cardValue);
            }

            // PRIORITY 3: Adjacent to current play pile
            AddAdjacentValues(favorableValues, gameState.GetCurrentPlayValue());
            
            // Pick random favorable value
            if (favorableValues.Count > 0)
            {
                return favorableValues[rng.Next(favorableValues.Count)];
            }
            
            return rng.Next(1, 14);
        }

        private static void AddAdjacentValues(List<int> list, int value)
        {
            int lowerValue = value - 1;
            int higherValue = value + 1;
            
            if (lowerValue < 1) lowerValue = 13;
            if (higherValue > 13) higherValue = 1;
            
            if (!list.Contains(lowerValue)) list.Add(lowerValue);
            if (!list.Contains(higherValue)) list.Add(higherValue);
        }
    }
}
