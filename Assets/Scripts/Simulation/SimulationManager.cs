using UnityEngine;
using TripeaksSolitaire.Core;
using System.Collections.Generic;

namespace TripeaksSolitaire.Simulation
{
    public class SimulationManager : MonoBehaviour
    {
        [Header("Settings")]
        public int levelToSimulate = 25;
        public int minDeckSize = 10;
        public int maxDeckSize = 50;
        public int simulationsPerSize = 100;

        [Header("Results")]
        public List<DifficultyTuner.TuningResult> lastResults;
        public int optimalDeckSize;

        private bool _isSimulating = false;

        public void StartSimulation()
        {
            if (_isSimulating)
            {
                Debug.LogWarning("Simulation already running!");
                return;
            }

            StartCoroutine(RunSimulation());
        }

        private System.Collections.IEnumerator RunSimulation()
        {
            _isSimulating = true;

            Debug.Log($"🚀 Starting simulation for level {levelToSimulate}");

            // Load level
            LevelData levelData = LevelLoader.LoadLevelByNumber(levelToSimulate);

            if (levelData == null)
            {
                Debug.LogError($"Failed to load level {levelToSimulate}");
                _isSimulating = false;
                yield break;
            }

            // Run tuning
            DifficultyTuner tuner = new DifficultyTuner();
            lastResults = tuner.RunTuningSimulations(levelData, minDeckSize, maxDeckSize, simulationsPerSize);

            yield return null;

            // Find optimal
            optimalDeckSize = tuner.FindOptimalDeckSize(lastResults);

            Debug.Log($"✅ Simulation complete! Optimal deck size: {optimalDeckSize}");

            _isSimulating = false;
        }

        void Update()
        {
            // Press S to start simulation
            if (Input.GetKeyDown(KeyCode.S) && !_isSimulating)
            {
                StartSimulation();
            }
        }
    }
}