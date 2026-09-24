# Star Seed Protocol — Code Samples

Selected C# files from *Star Seed Protocol*, a 2D horde survival game I’m developing in Unity.

These files come from a larger Unity project. They reference project-specific types, services, and assets that are not included here, so this repository is intended for code review rather than as a standalone build.

## UI and save slots

- `AbstractUI.cs` - base class for UI visibility and input context handling.
- `DialogSaveSlots.cs` - save slot dialog, navigation, selection, and deletion flow.
- `SaveSlotsViewModel.cs` - save slot data, localization, selection, and application logic.

## Gameplay

- `AbilityCaster.cs` - handles attacks for the player, turrets, and some enemies, including projectiles, beams, and auras. Uses ability data supplied by the game's models.
- `StatEntity.cs` - manages entity stats and recalculates them when modifiers change.
- `EntitiesLifecycleSystem.cs` - coordinates entity spawning, movement, targeting, and death with other gameplay systems.

## Physics

- `PhysicsWorld2d.cs` - resolves collisions between the player, AI entities, and static objects; uses Unity Jobs and Burst for AI separation.
