INSTRUCTIONS TO SET UP THE PROJECT.
1. Download the project as a .ZIP or through Sourcetree or Github Desktop.
2. Unzip it.
3. Install Unity Hub. (https://unity.com/es/download)
4. In Unity Hub, select "ADD", then click "ADD PROJECT FROM DISK".
5. Navigate into the folder you just unzipped. It should be named "Softgames Test"
6. Click "Open" on the explorer window.
7. The project is now added to Unity Hub.
8. Unity Hub should inform you to install the proper version of the engine. Follow instructions to proceed, or:
9. To manually install the proper version of Unity, go to "INSTALLS" in Unity Hub, and then click on "INSTALL EDITOR", and select Unity version 6000.3.5f2 (LTS).
10. Now you should be able to open the project.
11. Open the SolitaireTripeaks scene, in "Scenes".
12. Select the GameManager gameobject, and in the Inspector, assign a level JSON file from Resources/Levels/
13. Press the PLAY button to run the game.


USING THE BALANCING TOOL
1. Open the tool: Tools → Tripeaks → Difficulty Tuner.
2. Select a level JSON file.
3. Configure simulation parameters:
  a. Min/Max Deck Size: Range to test.
  b. Simulations Per Size: Number of games per deck size (500-5000).
  c. Target Close Win %: Desired percentage of wins that are "close" (≤2 cards).
  d. Probability Settings: Match your game settings.
4. Click START SIMULATION
5. Review results and export to CSV if needed. Import as TSV!!!Required for decimals correct visualization.
6. A copy of the level can be saved with the optimal deck resulting from the simulation. Click on “Create Optimized Level JSON”, and choose a location to save it.

Created by Macià Torrens for SOFTGAMES - TECHNICAL GAME DESIGNER TEST 2026
