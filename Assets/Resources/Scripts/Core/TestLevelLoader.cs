using UnityEngine;
using TripeaksSolitaire.Core;

public class TestLevelLoader : MonoBehaviour
{
    void Start()
    {
        // Test loading all 4 levels
        TestLoadLevel(25);
        TestLoadLevel(31);
        TestLoadLevel(43);
        TestLoadLevel(54);
    }

    void TestLoadLevel(int levelNumber)
    {
        LevelData level = LevelLoader.LoadLevelByNumber(levelNumber);

        if (level != null)
        {
            Debug.Log($"✅ Level {levelNumber} loaded:");
            Debug.Log($"  - Cards: {level.cards.Count}");
            Debug.Log($"  - Draw pile size: {level.settings.cards_in_stack.Count}");
            Debug.Log($"  - Background: {level.settings.background}");

            // Test first card data
            if (level.cards.Count > 0)
            {
                CardData firstCard = level.cards[0];
                Debug.Log($"  - First card: {firstCard.id}, type: {firstCard.type}, depth: {firstCard.depth}, pos: ({firstCard.x}, {firstCard.y})");
            }
        }
        else
        {
            Debug.LogError($"❌ Failed to load level {levelNumber}");
        }
    }
}