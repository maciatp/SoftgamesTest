using UnityEngine;
using UnityEditor;
using TripeaksSolitaire.Core;
using TripeaksSolitaire.Simulation;
using System.Collections.Generic;
using System.IO;

namespace TripeaksSolitaire.Editor
{
    public class SimulationEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Tripeaks/Difficulty Tuner")]
        public static void ShowWindow()
        {
            SimulationEditorWindow window = GetWindow<SimulationEditorWindow>("Difficulty Tuner");
            window.minSize = new Vector2(600, 700);
        }

        // Settings
        private string _jsonFilePath = "";
        private int _loadedLevelDeckSize = 0; // Store the deck size from loaded level
        private int _minDeckSize = 10;
        private int _maxDeckSize = 50;
        private int _simulationsPerSize = 500;
        private float _targetCloseWinRate = 0.7f;
        private float _favorableProbability = 0.51f;
        private float _finalFavorableProbability = 0.25f;
        private float _bombFavorableProbability = 0.33f;
        private int _minimumCardsToIncreaseProbability = 2;
        private int _bombTimerToIncreaseProbability = 3;

        // Results
        private List<DifficultyTuner.TuningResult> _results;
        private DifficultyTuner.OptimalDeckResult _optimalDeckResult;
        private bool _isSimulating = false;
        private float _simulationProgress = 0f;
        private string _currentStatus = "Ready";

        // UI State
        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Tripeaks Solitaire - Difficulty Tuner", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Find optimal deck size for 'Close Win' achievement", EditorStyles.miniLabel);

            EditorGUILayout.Space(10);
            DrawSeparator();

            // === LEVEL SELECTION ===
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("1. Select Level JSON", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("JSON File:", GUILayout.Width(100));
            EditorGUILayout.LabelField(_jsonFilePath == "" ? "No file selected" : Path.GetFileName(_jsonFilePath), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Browse JSON File...", GUILayout.Width(150)))
            {
                BrowseForJSON();
            }
            EditorGUILayout.EndHorizontal();
            
            // Show deck size right after the button if level is loaded
            if (_loadedLevelDeckSize > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Current DrawPile in loaded level: {_loadedLevelDeckSize} cards", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(5);
            DrawSeparator();

            // === SIMULATION SETTINGS ===
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("2. Simulation Settings", EditorStyles.boldLabel);

            _minDeckSize = EditorGUILayout.IntSlider("Min Deck Size", _minDeckSize, 5, 10);
            _maxDeckSize = EditorGUILayout.IntSlider("Max Deck Size", _maxDeckSize, _minDeckSize, 40);
            _simulationsPerSize = EditorGUILayout.IntSlider("Simulations Per Size", _simulationsPerSize, 500, 5000);
            _targetCloseWinRate = EditorGUILayout.Slider("Target Close Win %", _targetCloseWinRate, 0.5f, 0.9f);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Dynamic Probability System:", EditorStyles.boldLabel);
            _favorableProbability = EditorGUILayout.Slider("Base Favorable Probability", _favorableProbability, 0f, 1f);
            _finalFavorableProbability = EditorGUILayout.Slider("Final Stage Boost", _finalFavorableProbability, 0f, 1f);
            _bombFavorableProbability = EditorGUILayout.Slider("Urgent Bomb Boost", _bombFavorableProbability, 0f, 1f);

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                $"Will test {_maxDeckSize - _minDeckSize + 1} deck sizes with {_simulationsPerSize} simulations each.\n" +
                $"Total simulations: {(_maxDeckSize - _minDeckSize + 1) * _simulationsPerSize}\n" +
                $"Target: {_targetCloseWinRate:P0} of wins must have ≤2 cards remaining\n" +
                $"Goal: Find range of deck sizes that meets this target",
                MessageType.Info);

            EditorGUILayout.Space(5);
            DrawSeparator();

            // === RUN SIMULATION ===
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("3. Run Simulation", EditorStyles.boldLabel);

            GUI.enabled = !_isSimulating && _jsonFilePath != "";

            if (GUILayout.Button(_isSimulating ? "Simulating..." : "▶ START SIMULATION", GUILayout.Height(40)))
            {
                StartSimulation();
            }

            GUI.enabled = true;

            // Progress bar
            if (_isSimulating)
            {
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 20),
                    _simulationProgress,
                    $"{_currentStatus} ({(_simulationProgress * 100):F1}%)"
                );
            }

            EditorGUILayout.Space(5);
            DrawSeparator();

            // === RESULTS ===
            if (_results != null && _results.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("4. Results", EditorStyles.boldLabel);

                // Optimal range result box (only show when simulation is complete)
                if (_optimalDeckResult != null)
                {
                    var optimalResult = _optimalDeckResult.optimalResult;
                    if (optimalResult != null)
                    {
                        string statusIcon = optimalResult.meetsTarget ? "✅" : "⚠️";
                        MessageType msgType = optimalResult.meetsTarget ? MessageType.None : MessageType.Warning;
                        
                        EditorGUILayout.HelpBox(
                            $"{statusIcon} OPTIMAL DECK RANGE: {_optimalDeckResult.minDeckSize} to {_optimalDeckResult.maxDeckSize} cards\n" +
                            $"Qualifying deck sizes: {string.Join(", ", _optimalDeckResult.qualifyingDeckSizes)}\n\n" +
                            $"🎯 RECOMMENDED: {_optimalDeckResult.optimalDeckSize} cards (average of range)\n\n" +
                            $"Win Rate: {optimalResult.winRate:P2} ({optimalResult.wins}/{optimalResult.totalGames} games)\n" +
                            $"Close Win Rate: {optimalResult.closeWinRate:P2} (target: ≥{_targetCloseWinRate:P0})\n" +
                            $"Meets Target: {(optimalResult.meetsTarget ? "YES ✅" : "NO - Consider adjusting settings")}\n" +
                            $"Avg Moves on Win: {optimalResult.avgMovesOnWin:F1}\n" +
                            $"Avg Cards Remaining: {optimalResult.avgCardsRemainingOnWin:F2}",
                            msgType);
                    }

                    EditorGUILayout.Space(10);
                }

                // Results table
                EditorGUILayout.LabelField("Detailed Results:", EditorStyles.boldLabel);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));

                // Table header
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                EditorGUILayout.LabelField("Deck", EditorStyles.boldLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField("Wins", EditorStyles.boldLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField("Win %", EditorStyles.boldLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField("Close Win %", EditorStyles.boldLabel, GUILayout.Width(90));
                EditorGUILayout.LabelField("Meets Target", EditorStyles.boldLabel, GUILayout.Width(90));
                EditorGUILayout.LabelField("Avg Cards", EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();

                // Table rows
                foreach (var result in _results)
                {
                    bool isOptimal = false;
                    bool isInRange = false;
                    
                    // Only check optimal/range if simulation is complete
                    if (_optimalDeckResult != null)
                    {
                        isOptimal = result.deckSize == _optimalDeckResult.optimalDeckSize;
                        isInRange = _optimalDeckResult.qualifyingDeckSizes != null && 
                                    ((System.Collections.Generic.List<int>)_optimalDeckResult.qualifyingDeckSizes).Contains(result.deckSize);
                    }

                    if (isOptimal)
                    {
                        GUI.backgroundColor = new Color(0f, 0.86f, 1f); // Cyan bright for optimal
                    }
                    else if (isInRange)
                    {
                        GUI.backgroundColor = new Color(0.5f, 0.85f, 0.5f); // Light green for range
                    }
                    else if (result.meetsTarget)
                    {
                        GUI.backgroundColor = new Color(0.75f, 0.95f, 0.75f); // Pale green for others meeting target
                    }

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    EditorGUILayout.LabelField(result.deckSize.ToString(), GUILayout.Width(50));
                    EditorGUILayout.LabelField($"{result.wins}/{result.totalGames}", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"{result.winRate:P1}", GUILayout.Width(60));
                    EditorGUILayout.LabelField($"{result.closeWinRate:P1}", GUILayout.Width(90));
                    EditorGUILayout.LabelField(result.meetsTarget ? "✅" : "❌", GUILayout.Width(90));
                    
                    // Avg Cards with optional OPTIMAL indicator at the end
                    string avgCardsLabel = $"{result.avgCardsRemainingOnWin:F2}";
                    if (isOptimal && !_isSimulating) // Only show OPTIMAL after simulation completes
                    {
                        avgCardsLabel += " ★ OPTIMAL DECK SIZE";
                    }
                    EditorGUILayout.LabelField(avgCardsLabel, GUILayout.Width(200));

                    EditorGUILayout.EndHorizontal();

                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(10);

                // Export buttons
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Export Results to CSV", GUILayout.Height(30)))
                {
                    ExportResultsToCSV();
                }
                
                GUI.enabled = _optimalDeckResult != null;
                if (GUILayout.Button("Create Optimized Level JSON", GUILayout.Height(30)))
                {
                    CreateOptimizedLevelJSON();
                }
                GUI.enabled = true;
                
                EditorGUILayout.EndHorizontal();
            }
        }

        private void BrowseForJSON()
        {
            string path = EditorUtility.OpenFilePanel("Select Level JSON", Application.dataPath, "json");

            if (!string.IsNullOrEmpty(path))
            {
                _jsonFilePath = path;
                
                // Load the level and get deck size
                LevelData levelData = LoadLevelFromFile(path);
                if (levelData != null)
                {
                    // cards_in_stack is a List - get its count for the deck size
                    if (levelData.settings.cards_in_stack != null)
                    {
                        _loadedLevelDeckSize = levelData.settings.cards_in_stack.Count;
                    }
                    else
                    {
                        _loadedLevelDeckSize = 0;
                    }
                    // Debug.Log removed - info shown in UI
                }
                else
                {
                    _loadedLevelDeckSize = 0;
                    Debug.LogError($"Failed to load level from: {path}");
                }
            }
        }

        private async void StartSimulation()
        {
            if (string.IsNullOrEmpty(_jsonFilePath))
            {
                EditorUtility.DisplayDialog("Error", "Please select a JSON file first!", "OK");
                return;
            }

            _isSimulating = true;
            _simulationProgress = 0f;
            _currentStatus = "Loading level...";
            Repaint();

            // Load JSON
            LevelData levelData = LoadLevelFromFile(_jsonFilePath);

            if (levelData == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to load JSON file!", "OK");
                _isSimulating = false;
                return;
            }

            _currentStatus = "Running simulations...";
            Repaint();

            // Run simulations using DifficultyTuner
            DifficultyTuner tuner = new DifficultyTuner();
            
            // We'll run simulations manually to show progress
            _results = new List<DifficultyTuner.TuningResult>();
            int totalDeckSizes = _maxDeckSize - _minDeckSize + 1;

            for (int deckSize = _minDeckSize; deckSize <= _maxDeckSize; deckSize++)
            {
                int currentIndex = deckSize - _minDeckSize;
                _currentStatus = $"Testing deck size {deckSize}...";
                _simulationProgress = (float)currentIndex / totalDeckSizes;
                Repaint();

                var result = RunSimulationsForDeckSize(levelData, deckSize, _simulationsPerSize, _favorableProbability, _finalFavorableProbability, _bombFavorableProbability);
                _results.Add(result);

                // Allow UI to update
                await System.Threading.Tasks.Task.Delay(1);
            }

            // Find optimal using DifficultyTuner logic
            _currentStatus = "Calculating optimal deck size...";
            _simulationProgress = 0.95f; // Show near completion
            Repaint();
            
            _optimalDeckResult = tuner.FindOptimalDeckSize(_results, _targetCloseWinRate);

            // Update to 100% BEFORE showing popup
            _currentStatus = "Complete!";
            _simulationProgress = 1f;
            _isSimulating = false;
            Repaint();
            
            // Give UI time to update to 100%
            await System.Threading.Tasks.Task.Delay(200);

            // NOW show the popup
            var optimal = _optimalDeckResult.optimalResult;

            string title = optimal.meetsTarget ? "✅ Simulation Complete!" : "⚠️ Simulation Complete";
            string icon = optimal.meetsTarget ? "✅" : "⚠️";

            string message = $"{icon} Recommended Deck Size: {_optimalDeckResult.optimalDeckSize} cards\n\n";
            message += $"Win Rate: {optimal.winRate:P1}\n";
            message += $"Close Win Rate: {optimal.closeWinRate:P1}\n";
            
            if (optimal.meetsTarget)
            {
                message += $"\n✅ Meets target of ≥{_targetCloseWinRate:P0}!";
            }
            else
            {
                message += $"\n⚠️ Does not meet {_targetCloseWinRate:P0} target.\nConsider adjusting settings.";
            }

            EditorUtility.DisplayDialog(title, message, "OK");
        }

        private LevelData LoadLevelFromFile(string filePath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                LevelData levelData = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelData>(jsonContent);
                return levelData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading JSON: {e.Message}");
                return null;
            }
        }

        private DifficultyTuner.TuningResult RunSimulationsForDeckSize(LevelData levelData, int deckSize, int simulations, float favorableProbability, float finalFavorableProbability, float bombFavorableProbability)
        {
            GameSimulator simulator = new GameSimulator();

            int wins = 0;
            int closeWins = 0;
            float totalMoves = 0;
            float totalCardsRemaining = 0;

            for (int i = 0; i < simulations; i++)
            {
                var result = simulator.SimulateGame(levelData, deckSize, favorableProbability, finalFavorableProbability, bombFavorableProbability, _minimumCardsToIncreaseProbability, _bombTimerToIncreaseProbability);

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

            float closeWinRate = wins > 0 ? (float)closeWins / wins : 0f;
            bool meetsTarget = wins > 0 && closeWinRate >= _targetCloseWinRate;

            return new DifficultyTuner.TuningResult
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

        private void ExportResultsToCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export Results", "", "simulation_results.csv", "csv");

            if (string.IsNullOrEmpty(path)) return;

            using (StreamWriter writer = new StreamWriter(path))
            {
                // Metadata header with ALL probability values
                writer.WriteLine($"# Simulation Metadata");
                writer.WriteLine($"# Base Favorable Probability: {_favorableProbability.ToString("F2").Replace(".", ",")}");
                writer.WriteLine($"# Final Stage Boost: {_finalFavorableProbability.ToString("F2").Replace(".", ",")}");
                writer.WriteLine($"# Urgent Bomb Boost: {_bombFavorableProbability.ToString("F2").Replace(".", ",")}");
                writer.WriteLine($"# Target Close Win Rate: {_targetCloseWinRate:P0}");
                writer.WriteLine($"# Simulations Per Size: {_simulationsPerSize}");
                
                // Add optimal deck info
                if (_optimalDeckResult != null)
                {
                    writer.WriteLine($"# Optimal Deck Range: {_optimalDeckResult.minDeckSize} to {_optimalDeckResult.maxDeckSize}");
                    writer.WriteLine($"# Recommended Deck Size: {_optimalDeckResult.optimalDeckSize} (average of range)");
                }
                
                writer.WriteLine($"# Export Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();
                
                // Header with TAB separator
                writer.WriteLine("DeckSize\tWins\tCloseWins\tWinRate\tCloseWinRate\tAvgMovesOnWin\tAvgCardsRemainingOnWin\tMeetsTarget\tIsOptimal");

                // Data with TAB separator and comma for decimals
                foreach (var result in _results)
                {
                    bool isOptimal = _optimalDeckResult != null && result.deckSize == _optimalDeckResult.optimalDeckSize;
                    
                    writer.WriteLine($"{result.deckSize}\t{result.wins}\t{result.closeWins}\t" +
                                   $"{result.winRate.ToString("F4").Replace(".", ",")}\t{result.closeWinRate.ToString("F4").Replace(".", ",")}\t" +
                                   $"{result.avgMovesOnWin.ToString("F2").Replace(".", ",")}\t{result.avgCardsRemainingOnWin.ToString("F2").Replace(".", ",")}\t{result.meetsTarget}\t{isOptimal}");
                }
            }

            EditorUtility.DisplayDialog("Export Complete", $"Results exported to:\n{path}", "OK");
        }

        private void CreateOptimizedLevelJSON()
        {
            if (_optimalDeckResult == null || string.IsNullOrEmpty(_jsonFilePath))
            {
                EditorUtility.DisplayDialog("Error", "No optimal result available!", "OK");
                return;
            }

            // Load the original level
            LevelData originalLevel = LoadLevelFromFile(_jsonFilePath);
            if (originalLevel == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to load original level!", "OK");
                return;
            }

            int optimalDeckSize = _optimalDeckResult.optimalDeckSize;
            int originalDeckSize = originalLevel.settings.cards_in_stack.Count;

            // Create new cards_in_stack array with optimal size
            List<int> newCardsInStack = new List<int>();
            for (int i = 0; i < optimalDeckSize; i++)
            {
                newCardsInStack.Add(-1); // -1 means random card
            }

            // Update the level data
            originalLevel.settings.cards_in_stack = newCardsInStack;

            // Generate default filename
            string originalFileName = Path.GetFileNameWithoutExtension(_jsonFilePath);
            string defaultFileName = $"{originalFileName}_optimized_{optimalDeckSize}cards.json";

            // Ask user where to save
            string savePath = EditorUtility.SaveFilePanel(
                "Save Optimized Level",
                Path.GetDirectoryName(_jsonFilePath),
                defaultFileName,
                "json"
            );

            if (string.IsNullOrEmpty(savePath)) return;

            // Serialize and save
            try
            {
                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(originalLevel, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(savePath, jsonContent);

                string message = $"Optimized level created!\n\n";
                message += $"Original deck size: {originalDeckSize} cards\n";
                message += $"Optimized deck size: {optimalDeckSize} cards\n";
                message += $"Change: {optimalDeckSize - originalDeckSize:+0;-0} cards\n\n";
                message += $"File saved to:\n{savePath}";

                EditorUtility.DisplayDialog("Success", message, "OK");
                
                Debug.Log($"✅ Optimized level created: {Path.GetFileName(savePath)}");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save level:\n{e.Message}", "OK");
                Debug.LogError($"Error saving optimized level: {e.Message}");
            }
        }

        private void DrawSeparator()
        {
            EditorGUILayout.Space(5);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space(5);
        }
    }
}
