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
            public float closeWinRate; // Percentage of WINS that are close wins
            public float avgMovesOnWin;
            public float avgCardsRemainingOnWin;
            public bool meetsTarget; // Does this meet the 70% close win target?
        }
        
        public List<TuningResult> RunTuningSimulations(LevelData levelData, int minDeckSize = 10, int maxDeckSize = 50, int simulationsPerSize = 100, float targetCloseWinRate = 0.7f, float favorableProbability = 0.51f, float finalFavorableProbability = 0.25f, float bombFavorableProbability = 0.33f)
        {
            List<TuningResult> results = new List<TuningResult>();
            
            Debug.Log($"=== DIFFICULTY TUNER SIMULATION ===");
            Debug.Log($"Level: {levelData.settings.level_number}");
            Debug.Log($"Testing deck sizes: {minDeckSize} to {maxDeckSize}");
            Debug.Log($"Simulations per size: {simulationsPerSize}");
            Debug.Log($"Favorable Probability: {favorableProbability:P1}");
            Debug.Log($"TARGET: {targetCloseWinRate:P0} of wins must have ≤2 cards remaining");
            Debug.Log($"GOAL: Find SMALLEST deck size that meets this target\n");
            
            for (int deckSize = minDeckSize; deckSize <= maxDeckSize; deckSize++)
            {
                TuningResult result = RunSimulationsForDeckSize(levelData, deckSize, simulationsPerSize, targetCloseWinRate, favorableProbability, finalFavorableProbability, bombFavorableProbability);
                results.Add(result);
                
                string status = result.meetsTarget ? "✅" : "  ";
                string winInfo = $"Wins: {result.wins}/{result.totalGames} ({result.winRate:P1})";
                string closeInfo = result.wins > 0 ? $"Close Wins: {result.closeWins}/{result.wins} ({result.closeWinRate:P1})" : "Close Wins: N/A";
                
                Debug.Log($"{status} Deck {deckSize,3}: {winInfo,-25} {closeInfo}");
            }
            
            return results;
        }
        
        private TuningResult RunSimulationsForDeckSize(LevelData levelData, int deckSize, int simulations, float targetCloseWinRate, float favorableProbability, float finalFavorableProbability, float bombFavorableProbability)
        {
            GameSimulator simulator = new GameSimulator();
            
            int wins = 0;
            int closeWins = 0;
            float totalMoves = 0;
            float totalCardsRemaining = 0;
            
            for (int i = 0; i < simulations; i++)
            {
                var result = simulator.SimulateGame(levelData, deckSize, favorableProbability, finalFavorableProbability, bombFavorableProbability);
                
                if (result.isWin)
                {
                    wins++;
                    totalMoves += result.moveCount;
                    totalCardsRemaining += result.cardsRemainingInDraw;
                    
                    if (result.isCloseWin) // isCloseWin = win with ≤2 cards remaining
                    {
                        closeWins++;
                    }
                }
            }
            
            float closeWinRate = wins > 0 ? (float)closeWins / wins : 0f;
            bool meetsTarget = wins > 0 && closeWinRate >= targetCloseWinRate;
            
            return new TuningResult
            {
                deckSize = deckSize,
                totalGames = simulations,
                wins = wins,
                closeWins = closeWins,
                winRate = (float)wins / simulations,
                closeWinRate = closeWinRate,
                avgMovesOnWin = wins > 0 ? totalMoves / wins : 0f,
                avgCardsRemainingOnWin = wins > 0 ? totalCardsRemaining / wins : 0f,
                meetsTarget = meetsTarget
            };
        }
        
        public int FindOptimalDeckSize(List<TuningResult> results, float targetCloseWinRate)
        {
            Debug.Log($"\n=== FINDING OPTIMAL DECK SIZE ===");
            Debug.Log($"Requirement 1: Close Win Rate ≥ {targetCloseWinRate:P0}");
            Debug.Log($"Requirement 2: If multiple qualify, choose SMALLEST deck size");
            Debug.Log($"Definition: Close Win = winning with ≤2 cards left in draw pile\n");
            
            // Find all deck sizes that meet the close win rate target
            var qualifyingResults = results
                .Where(r => r.meetsTarget)
                .OrderBy(r => r.deckSize) // Sort by deck size (ascending)
                .ToList();
            
            if (qualifyingResults.Count == 0)
            {
                Debug.LogWarning("⚠️ WARNING: No deck size achieved the target!");
                Debug.LogWarning($"No deck size had ≥{targetCloseWinRate:P0} close win rate.");
                Debug.LogWarning("Possible solutions:");
                Debug.LogWarning("  1. Increase maxDeckSize parameter");
                Debug.LogWarning("  2. Run more simulations per size");
                Debug.LogWarning("  3. Adjust level design (reduce difficulty)");
                Debug.LogWarning("\nFalling back to best available option...\n");
                
                // Fallback: Choose the deck size with highest close win rate
                var fallback = results
                    .Where(r => r.wins >= 5) // Must have at least 5 wins
                    .OrderByDescending(r => r.closeWinRate) // Highest close win rate
                    .ThenBy(r => r.deckSize) // If tied, prefer smaller
                    .FirstOrDefault();
                
                if (fallback != null)
                {
                    Debug.LogWarning($"📊 FALLBACK SELECTION:");
                    Debug.LogWarning($"   Deck Size: {fallback.deckSize}");
                    Debug.LogWarning($"   Win Rate: {fallback.winRate:P1} ({fallback.wins}/{fallback.totalGames})");
                    Debug.LogWarning($"   Close Win Rate: {fallback.closeWinRate:P1} ({fallback.closeWins}/{fallback.wins})");
                    Debug.LogWarning($"   ⚠️ Below {targetCloseWinRate:P0} target\n");
                    return fallback.deckSize;
                }
                
                Debug.LogError("❌ ERROR: No viable deck size found!");
                return results.OrderBy(r => r.deckSize).First().deckSize;
            }
            
            // Success! Select the SMALLEST qualifying deck size
            var optimal = qualifyingResults.First();
            
            Debug.Log($"✅ SUCCESS: Optimal deck size found!");
            Debug.Log($"\n📊 RECOMMENDED DECK SIZE: {optimal.deckSize}");
            Debug.Log($"   Win Rate: {optimal.winRate:P1} ({optimal.wins}/{optimal.totalGames} games)");
            Debug.Log($"   Close Win Rate: {optimal.closeWinRate:P1} ({optimal.closeWins}/{optimal.wins} wins) ✅");
            Debug.Log($"   Avg Cards Remaining: {optimal.avgCardsRemainingOnWin:F2}");
            Debug.Log($"   Avg Moves per Win: {optimal.avgMovesOnWin:F1}");
            
            // Show alternatives
            if (qualifyingResults.Count > 1)
            {
                Debug.Log($"\n   Alternative deck sizes (also meet target):");
                foreach (var alt in qualifyingResults.Skip(1).Take(4))
                {
                    Debug.Log($"      • Deck {alt.deckSize}: Win {alt.winRate:P1}, Close Win {alt.closeWinRate:P1}");
                }
                Debug.Log($"   (Smaller deck = harder but achieves target)");
            }
            
            Debug.Log($"\n=== TUNING COMPLETE ===\n");
            return optimal.deckSize;
        }
    }
}
