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
            
            // Check if Final Stage boost is active
            int remainingCards = 999; // Default high value
            if (gameManager.drawPile != null && gameManager.drawPile.deck != null)
            {
                remainingCards = gameManager.drawPile.deck.Count - gameManager.drawPile.currentIndex;
            }
            
            bool hasFinalStageBoost = remainingCards <= gameManager.minimumCardsToIncreaseProbability;
            if (hasFinalStageBoost)
            {
                EditorGUILayout.LabelField($"  + Final Stage: {gameManager.finalFavorableProbability:P0} 🎯 (Cards left: {remainingCards})", EditorStyles.boldLabel);
            }
            
            // Check if Bomb boost is active by checking if there are urgent bombs
            bool hasUrgentBomb = false;
            if (gameManager.board != null)
            {
                var allCards = gameManager.board.allCards;
                if (allCards != null)
                {
                    foreach (var card in allCards)
                    {
                        if (card.isOnBoard && card.isFaceUp && card.hasBomb && 
                            card.bombModifier != null && card.bombModifier.timer <= gameManager.bombTimerToIncreaseProbability)
                        {
                            hasUrgentBomb = true;
                            break;
                        }
                    }
                }
            }
            
            if (hasUrgentBomb)
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
