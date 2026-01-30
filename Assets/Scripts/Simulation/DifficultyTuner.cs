using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using TripeaksSolitaire.Core;

namespace TripeaksSolitaire.Simulation
{
    public class DifficultyTuner
    {
        public class OptimalDeckResult
        {
            public int optimalDeckSize;      // Average of the range
            public int minDeckSize;          // Minimum of range
            public int maxDeckSize;          // Maximum of range
            public List<int> qualifyingDeckSizes; // All deck sizes in range
            public TuningResult optimalResult;    // Result for the optimal (average) deck size
        }

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
            Debug.Log($"GOAL: Find deck size with HIGHEST avg cards remaining that meets target\n");
            
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
        
        public OptimalDeckResult FindOptimalDeckSize(List<TuningResult> results, float targetCloseWinRate)
        {
            Debug.Log($"\n=== FINDING OPTIMAL DECK SIZE ===");
            Debug.Log($"Requirement 1: Close Win Rate ≥ {targetCloseWinRate:P0}");
            Debug.Log($"Requirement 2: Find RANGE of qualifying deck sizes");
            Debug.Log($"Requirement 3: Select AVERAGE of range as optimal");
            Debug.Log($"Definition: Close Win = winning with ≤2 cards left in draw pile\n");
            
            // Find all deck sizes that meet the close win rate target
            var qualifyingResults = results
                .Where(r => r.meetsTarget)
                .OrderBy(r => r.deckSize)
                .ToList();
            
            if (qualifyingResults.Count == 0)
            {
                Debug.LogWarning("⚠️ WARNING: No deck size achieved the target!");
                Debug.LogWarning($"No deck size had ≥{targetCloseWinRate:P0} close win rate.");
                Debug.LogWarning("Falling back to best available option...\n");
                
                // Fallback: Use single best result
                var fallback = results
                    .Where(r => r.wins >= 5)
                    .OrderByDescending(r => r.closeWinRate)
                    .FirstOrDefault();
                
                if (fallback != null)
                {
                    Debug.LogWarning($"📊 FALLBACK SELECTION: Deck Size {fallback.deckSize}");
                    return new OptimalDeckResult
                    {
                        optimalDeckSize = fallback.deckSize,
                        minDeckSize = fallback.deckSize,
                        maxDeckSize = fallback.deckSize,
                        qualifyingDeckSizes = new List<int> { fallback.deckSize },
                        optimalResult = fallback
                    };
                }
                
                Debug.LogError("❌ ERROR: No viable deck size found!");
                var first = results.First();
                return new OptimalDeckResult
                {
                    optimalDeckSize = first.deckSize,
                    minDeckSize = first.deckSize,
                    maxDeckSize = first.deckSize,
                    qualifyingDeckSizes = new List<int> { first.deckSize },
                    optimalResult = first
                };
            }
            
            // Extract qualifying deck sizes
            var qualifyingSizes = qualifyingResults.Select(r => r.deckSize).ToList();
            int minSize = qualifyingSizes.Min();
            int maxSize = qualifyingSizes.Max();
            int avgSize = (int)Math.Round(qualifyingSizes.Average());
            
            // Get the result for the average deck size (or closest)
            var optimalResult = qualifyingResults
                .OrderBy(r => Math.Abs(r.deckSize - avgSize))
                .First();
            
            Debug.Log($"✅ SUCCESS: Optimal deck range found!");
            Debug.Log($"\n📊 QUALIFYING RANGE: {minSize} to {maxSize} cards ({qualifyingResults.Count} deck sizes)");
            Debug.Log($"   Deck sizes: {string.Join(", ", qualifyingSizes)}");
            Debug.Log($"\n🎯 RECOMMENDED DECK SIZE: {optimalResult.deckSize} (average of range)");
            Debug.Log($"   Win Rate: {optimalResult.winRate:P1} ({optimalResult.wins}/{optimalResult.totalGames} games)");
            Debug.Log($"   Close Win Rate: {optimalResult.closeWinRate:P1} ({optimalResult.closeWins}/{optimalResult.wins} wins) ✅");
            Debug.Log($"   Avg Cards Remaining: {optimalResult.avgCardsRemainingOnWin:F2}");
            Debug.Log($"   Avg Moves per Win: {optimalResult.avgMovesOnWin:F1}");
            
            // Show range statistics
            Debug.Log($"\n📈 RANGE STATISTICS:");
            Debug.Log($"   Min Deck: {minSize} - Win Rate: {qualifyingResults.First().winRate:P1}");
            Debug.Log($"   Max Deck: {maxSize} - Win Rate: {qualifyingResults.Last().winRate:P1}");
            Debug.Log($"   Range Size: {maxSize - minSize} cards");
            
            return new OptimalDeckResult
            {
                optimalDeckSize = optimalResult.deckSize,
                minDeckSize = minSize,
                maxDeckSize = maxSize,
                qualifyingDeckSizes = qualifyingSizes,
                optimalResult = optimalResult
            };
        }
    }
}
