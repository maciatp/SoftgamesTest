using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TripeaksSolitaire.Core;

namespace TripeaksSolitaire.Simulation
{
    public class DifficultyTuner
    {
        public class TuningResult
        {
            public int deckSize;
            public int totalGames;
            public int wins;
            public int closeWins;
            public float winRate;
            public float closeWinRate;
            public float avgMovesOnWin;
            public float avgCardsRemainingOnWin;
        }

        public List<TuningResult> RunTuningSimulations(LevelData levelData, int minDeckSize = 10, int maxDeckSize = 50, int simulationsPerSize = 100)
        {
            List<TuningResult> results = new List<TuningResult>();

            Debug.Log($"🎲 Starting tuning simulations for level {levelData.settings.level_number}");
            Debug.Log($"Testing deck sizes from {minDeckSize} to {maxDeckSize} with {simulationsPerSize} simulations each");

            for (int deckSize = minDeckSize; deckSize <= maxDeckSize; deckSize++)
            {
                TuningResult result = RunSimulationsForDeckSize(levelData, deckSize, simulationsPerSize);
                results.Add(result);

                Debug.Log($"Deck Size {deckSize}: Win Rate = {result.winRate:P2}, Close Win Rate = {result.closeWinRate:P2}");
            }

            return results;
        }

        private TuningResult RunSimulationsForDeckSize(LevelData levelData, int deckSize, int simulations)
        {
            GameSimulator simulator = new GameSimulator();

            int wins = 0;
            int closeWins = 0;
            float totalMoves = 0;
            float totalCardsRemaining = 0;

            for (int i = 0; i < simulations; i++)
            {
                var result = simulator.SimulateGame(levelData, deckSize);

                if (result.isWin)
                {
                    wins++;
                    totalMoves += result.moveCount;
                    totalCardsRemaining += result.cardsRemainingInDraw;

                    if (result.isCloseWin)
                    {
                        closeWins++;
                    }
                }
            }

            return new TuningResult
            {
                deckSize = deckSize,
                totalGames = simulations,
                wins = wins,
                closeWins = closeWins,
                winRate = (float)wins / simulations,
                closeWinRate = wins > 0 ? (float)closeWins / wins : 0f,
                avgMovesOnWin = wins > 0 ? totalMoves / wins : 0f,
                avgCardsRemainingOnWin = wins > 0 ? totalCardsRemaining / wins : 0f
            };
        }

        public int FindOptimalDeckSize(List<TuningResult> results, float targetCloseWinRate = 0.7f)
        {
            // Filter for results with decent win rate (at least 30%)
            var viableResults = results.Where(r => r.winRate >= 0.3f).ToList();

            if (viableResults.Count == 0)
            {
                Debug.LogWarning("No deck size achieved 30%+ win rate! Using all results.");
                viableResults = results.Where(r => r.winRate > 0).ToList();
            }

            if (viableResults.Count == 0)
            {
                Debug.LogError("No viable results found!");
                return results.First().deckSize;
            }

            // Find the one closest to target close win rate
            var optimal = viableResults
                .OrderBy(r => Mathf.Abs(r.closeWinRate - targetCloseWinRate))
                .First();

            Debug.Log($"🎯 OPTIMAL DECK SIZE FOUND: {optimal.deckSize}");
            Debug.Log($"   Overall Win Rate: {optimal.winRate:P2} ({optimal.wins}/{optimal.totalGames} games)");
            Debug.Log($"   Close Win Rate: {optimal.closeWinRate:P2} ({optimal.closeWins}/{optimal.wins} wins)");
            Debug.Log($"   Target was: {targetCloseWinRate:P2}");
            Debug.Log($"   Avg Moves on Win: {optimal.avgMovesOnWin:F1}");
            Debug.Log($"   Avg Cards Remaining on Win: {optimal.avgCardsRemainingOnWin:F2}");

            return optimal.deckSize;
        }
    }
}