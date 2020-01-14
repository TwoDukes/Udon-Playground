TwoDukes aka (Dustin-the-wind) Checkpoints v1

Package comes with one example scene.


Checkpoints:
-Heirarchy order is key to using the checkpoints. 
-All checkpoints must be under the checkpoint manager. 
-the first child of every checkpoint will automatically be enabled/disabled depending on checkpoint state. This can be used for adding graphics to show wheather the checkpoint is active or not. (Check example scene)
-You can duplicate checkpoints and place as many as you would like as long as they remain children of the checkpoint manager. All checkpoints are accounted for on scene start.

Respawning:
-Respawns are handled through the relocation of the world spawn position. 
-The world spawn position must be assigned to the checkpoint manager (check example scene).

Testing:
-Turning on testing mode on the CheckpointManager will allow any object dynamic collider to activate a checkpoint (Testing capsule in example scene). When testing mode is off it requires the local player to activate the checkpoint and will throw errors in editor.

issues:
-Since the world spawn position is moved when checkpoints are actived, there is no way respawn to the beginning of the world without an outside solution or just reloading the entire world.

