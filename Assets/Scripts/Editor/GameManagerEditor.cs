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
            
            // Calculate expected base
            float baseProbability = gameManager.favorableProbability;
            float totalExpected = baseProbability;
            
            // Check if Final Stage boost should be active
            int remainingCards = 999; // Default high value
            if (gameManager.drawPile != null && gameManager.drawPile.deck != null)
            {
                remainingCards = gameManager.drawPile.deck.Count - gameManager.drawPile.currentIndex;
            }
            
            if (remainingCards <= 2)
            {
                EditorGUILayout.LabelField($"  + Final Stage: {gameManager.finalFavorableProbability:P0} 🎯 (Cards left: {remainingCards})", EditorStyles.boldLabel);
                totalExpected += gameManager.finalFavorableProbability;
            }
            
            // Check if Bomb boost should be active
            if (gameManager.currentEffectiveProbability > totalExpected + 0.05f)
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
