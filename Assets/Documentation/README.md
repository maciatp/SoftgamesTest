# **Setup**

1. Open the project in Unity
2. Open the scene: Assets/Scenes/GameScene.unity
3. In GameManager Inspector, assign a level JSON file from Resources/Levels/
4. Configure probability settings in Inspector if desired
5. Press Play to start the game

# 

# 

# **Using the Difficulty Tuner**

1. Open the tool: Tools → Tripeaks → Difficulty Tuner
2. Select a level JSON file
3. Configure simulation parameters:
4. Min/Max Deck Size: Range to test
5. Simulations Per Size: Number of games per deck size (500-5000)
6. Target Close Win %: Desired percentage of wins that are "close" (≤2 cards)
7. Probability Settings: Match your game settings.

   ·Favorable Probability: Chance of getting the needed card.
   ·Final Favorable Probability: Chance Increment of getting the needed card when below "Minimum Cards to Increase Probability"
   ·Minimum Cards To Increase Probability: Range where the Final Favorable Probability chance increases the Favorable Probability.
   ·Bomb Favorable Probability: Chance increment applied to favorable Probability to get the needed card to defuse a Bomb Card.
   ·Bomb Timer to Increase Probability: Number of movements that the bomb has when the Bomb Favorable Probability increases the General Favorable Probability.

8. Click START SIMULATION
9. Review results and export to CSV if needed. Import as TSV!!!Required for decimals correct visualization.
10. A copy of the level can be saved with the optimal deck resulting from the simulation. Click on “Create Optimized Level JSON”, and choose a location to save it.


Created by Macià Torrens for SOFTGAMES - TECHNICAL GAME DESIGNER TEST 2026



