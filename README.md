# Tactical Grid-Defense Game

A grid-based tower defense game built with C# and MonoGame.

## Features
- **A\* Pathfinding:** Enemy navigation using a binary heap priority queue.
- **Stamina System:** Nodes consume stamina to fire and enter a cooldown state when depleted.
- **Synergy Links:** Adjacent nodes establish links that modify stamina recovery and attack speed.
- **Object Pooling:** Used for projectiles and enemies to manage memory allocation.
- **Primitive Rendering:** Graphics are drawn using primitive shapes.

## Tech Stack
- **Framework:** MonoGame (.NET 6.0)
- **Architecture:** Manager-based pattern
- **Algorithms:** A* Pathfinding, Binary Heap

## Project Structure
- `Core/`: Game loop and entry point.
- `Managers/`: Logic for grid, entities, resources, and waves.
- `Entities/`: Base and concrete classes for game objects.
- `Models/`: Data structures and enums.
- `Utils/`: Utility classes including object pools and priority queues.

## Execution
Requires .NET 6.0 SDK.

```bash
dotnet build
dotnet run
```
