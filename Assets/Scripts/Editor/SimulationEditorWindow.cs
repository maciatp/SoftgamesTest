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
        private int _minDeckSize = 10;
        private int _maxDeckSize = 50;
        private int _simulationsPerSize = 100;
        private float _targetCloseWinRate = 0.7f;

        // Results
        private List<DifficultyTuner.TuningResult> _results;
        private int _optimalDeckSize;
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

            EditorGUILayout.Space(5);
            DrawSeparator();

            // === SIMULATION SETTINGS ===
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("2. Simulation Settings", EditorStyles.boldLabel);

            _minDeckSize = EditorGUILayout.IntSlider("Min Deck Size", _minDeckSize, 5, 100);
            _maxDeckSize = EditorGUILayout.IntSlider("Max Deck Size", _maxDeckSize, _minDeckSize, 100);
            _simulationsPerSize = EditorGUILayout.IntSlider("Simulations Per Size", _simulationsPerSize, 10, 1000);
            _targetCloseWinRate = EditorGUILayout.Slider("Target Close Win %", _targetCloseWinRate, 0.5f, 0.9f);

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                $"Will test {_maxDeckSize - _minDeckSize + 1} deck sizes with {_simulationsPerSize} simulations each.\n" +
                $"Total simulations: {(_maxDeckSize - _minDeckSize + 1) * _simulationsPerSize}\n" +
                $"Estimated time: ~{EstimateTime()} seconds",
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

                // Optimal result box
                var optimalResult = _results.Find(r => r.deckSize == _optimalDeckSize);
                if (optimalResult != null)
                {
                    EditorGUILayout.HelpBox(
                        $"🎯 OPTIMAL DECK SIZE: {_optimalDeckSize} cards\n\n" +
                        $"Win Rate: {optimalResult.winRate:P2}\n" +
                        $"Close Win Rate: {optimalResult.closeWinRate:P2} (target: {_targetCloseWinRate:P2})\n" +
                        $"Avg Moves on Win: {optimalResult.avgMovesOnWin:F1}\n" +
                        $"Avg Cards Remaining: {optimalResult.avgCardsRemainingOnWin:F2}",
                        MessageType.None);
                }

                EditorGUILayout.Space(10);

                // Results table
                EditorGUILayout.LabelField("Detailed Results:", EditorStyles.boldLabel);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));

                // Table header
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                EditorGUILayout.LabelField("Deck Size", EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("Win Rate", EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("Close Win %", EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField("Avg Moves", EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("Avg Cards Left", EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                // Table rows
                foreach (var result in _results)
                {
                    bool isOptimal = result.deckSize == _optimalDeckSize;

                    if (isOptimal)
                    {
                        GUI.backgroundColor = Color.green;
                    }

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    EditorGUILayout.LabelField(result.deckSize.ToString(), GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{result.winRate:P1}", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{result.closeWinRate:P1}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"{result.avgMovesOnWin:F1}", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"{result.avgCardsRemainingOnWin:F2}", GUILayout.Width(100));

                    EditorGUILayout.EndHorizontal();

                    if (isOptimal)
                    {
                        GUI.backgroundColor = Color.white;
                    }
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(10);

                // Export button
                if (GUILayout.Button("Export Results to CSV", GUILayout.Height(30)))
                {
                    ExportResultsToCSV();
                }
            }
        }

        private void BrowseForJSON()
        {
            string path = EditorUtility.OpenFilePanel("Select Level JSON", Application.dataPath, "json");

            if (!string.IsNullOrEmpty(path))
            {
                _jsonFilePath = path;
                Debug.Log($"Selected JSON: {_jsonFilePath}");
            }
        }

        private int EstimateTime()
        {
            int totalSims = (_maxDeckSize - _minDeckSize + 1) * _simulationsPerSize;
            // Estimate ~0.001 seconds per simulation
            return Mathf.CeilToInt(totalSims * 0.001f);
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

            // Run simulations
            DifficultyTuner tuner = new DifficultyTuner();
            _results = new List<DifficultyTuner.TuningResult>();

            int totalDeckSizes = _maxDeckSize - _minDeckSize + 1;
            int currentDeckIndex = 0;

            for (int deckSize = _minDeckSize; deckSize <= _maxDeckSize; deckSize++)
            {
                _currentStatus = $"Testing deck size {deckSize}...";
                _simulationProgress = (float)currentDeckIndex / totalDeckSizes;
                Repaint();

                var result = RunSimulationsForDeckSize(levelData, deckSize, _simulationsPerSize);
                _results.Add(result);

                currentDeckIndex++;

                // Allow UI to update
                await System.Threading.Tasks.Task.Delay(1);
            }

            // Find optimal
            _optimalDeckSize = tuner.FindOptimalDeckSize(_results, _targetCloseWinRate);

            _currentStatus = "Complete!";
            _simulationProgress = 1f;
            _isSimulating = false;

            Repaint();

            EditorUtility.DisplayDialog(
                "Simulation Complete!",
                $"Optimal Deck Size: {_optimalDeckSize} cards\n\n" +
                $"Check the results table below for details.",
                "OK");
        }

        private LevelData LoadLevelFromFile(string filePath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                LevelData levelData = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelData>(jsonContent);
                Debug.Log($"Loaded level {levelData.settings.level_number} with {levelData.cards.Count} cards");
                return levelData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading JSON: {e.Message}");
                return null;
            }
        }

        private DifficultyTuner.TuningResult RunSimulationsForDeckSize(LevelData levelData, int deckSize, int simulations)
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

            return new DifficultyTuner.TuningResult
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

        private void ExportResultsToCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export Results", "", "simulation_results.csv", "csv");

            if (string.IsNullOrEmpty(path)) return;

            using (StreamWriter writer = new StreamWriter(path))
            {
                // Header
                writer.WriteLine("DeckSize,TotalGames,Wins,CloseWins,WinRate,CloseWinRate,AvgMovesOnWin,AvgCardsRemainingOnWin");

                // Data
                foreach (var result in _results)
                {
                    writer.WriteLine($"{result.deckSize},{result.totalGames},{result.wins},{result.closeWins}," +
                                   $"{result.winRate:F4},{result.closeWinRate:F4}," +
                                   $"{result.avgMovesOnWin:F2},{result.avgCardsRemainingOnWin:F2}");
                }
            }

            EditorUtility.DisplayDialog("Export Complete", $"Results exported to:\n{path}", "OK");
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