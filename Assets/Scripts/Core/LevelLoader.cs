using UnityEngine;
using Newtonsoft.Json;

namespace TripeaksSolitaire.Core
{
    public class LevelLoader : MonoBehaviour
    {
        public static LevelData LoadLevel(string levelName)
        {
            // Load JSON file from Resources/Levels/
            TextAsset jsonFile = Resources.Load<TextAsset>($"Levels/{levelName}");

            if (jsonFile == null)
            {
                Debug.LogError($"Level file not found: {levelName}");
                return null;
            }

            try
            {
                // Deserialize JSON using Newtonsoft.Json
                LevelData levelData = JsonConvert.DeserializeObject<LevelData>(jsonFile.text);
                Debug.Log($"Level {levelName} loaded successfully with {levelData.cards.Count} cards");
                return levelData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing level {levelName}: {e.Message}");
                return null;
            }
        }

        // Helper method to load level by number
        public static LevelData LoadLevelByNumber(int levelNumber)
        {
            return LoadLevel($"level_{levelNumber}");
        }
    }
}