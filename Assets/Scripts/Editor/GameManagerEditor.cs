using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        GameManager gameManager = (GameManager)target;
        
        // Draw separator
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        
        // Draw current probability with visual indicator
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Live Probability Monitor", EditorStyles.boldLabel);
        
        // Color code based on value
        Color barColor;
        if (gameManager.currentEffectiveProbability >= 0.75f)
            barColor = Color.green; // High boost
        else if (gameManager.currentEffectiveProbability >= 0.60f)
            barColor = Color.yellow; // Medium boost
        else
            barColor = Color.white; // Normal
        
        // Draw colored progress bar
        Rect rect = EditorGUILayout.GetControlRect(false, 20);
        EditorGUI.ProgressBar(rect, gameManager.currentEffectiveProbability, 
            $"Effective Probability: {gameManager.currentEffectiveProbability:P1}");
        
        // Color the progress bar
        GUI.color = barColor;
        EditorGUI.ProgressBar(rect, gameManager.currentEffectiveProbability, 
            $"Effective Probability: {gameManager.currentEffectiveProbability:P1}");
        GUI.color = Color.white;
        
        // Show boost breakdown
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Boost Breakdown:", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"  Base: {gameManager.favorableProbability:P0}");
            
            // Check for final stage boost
            if (gameManager.drawPile != null && gameManager.drawPile.RemainingCards() <= 2)
            {
                EditorGUILayout.LabelField($"  + Final Stage: {gameManager.finalFavorableProbability:P0} 🎯", EditorStyles.boldLabel);
            }
            
            // Check for bomb boost (simplified check)
            bool hasBombBoost = gameManager.currentEffectiveProbability > 
                               (gameManager.favorableProbability + gameManager.finalFavorableProbability + 0.01f);
            if (hasBombBoost)
            {
                EditorGUILayout.LabelField($"  + Urgent Bomb: {gameManager.bombFavorableProbability:P0} 💣", EditorStyles.boldLabel);
            }
            
            EditorGUILayout.LabelField($"  = Total: {gameManager.currentEffectiveProbability:P0}", EditorStyles.boldLabel);
            
            EditorGUILayout.EndVertical();
        }
        
        // Force repaint to update in real-time
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}
