# colored_tiles (Match 3 game)

Quick implementation of a Match 3 game in Unity.

Tap on a cell then tap on a neighboring cell to swap their positions. 
A connection of 3 or more horizontally and vertically will destroy the blocks.

Includes a simulation function that will simulate user actions in the game (although not too smart, it will try to tap on the neighboring cell of the initially chosen cell).

Game Config can be set in the GridManager's inspector in the scene.
You can set the number of rows and columns and the colors to be used in the game.
You can also set if the game allows the user to swap cells even if the action won't result in a match (`DisallowLooseSwap` property).



https://github.com/remingtonchan/colored_tiles/assets/8456890/d03e8cf7-df80-4295-b8ae-0c44069dc1fd

