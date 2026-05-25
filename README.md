# Tactical Grid-Defense Game

A deep, tactical tower defense game built with C# and MonoGame on .NET 8.0.

## Core Features
- **Dynamic Pathfinding:** Real-time A* navigation with hazard-weighted costs and path-lock anti-juggling mechanics.
- **Logistics & Infrastructure:** 
  - **Power Grid:** Connect units to the Core or Power Plants to avoid severe regeneration penalties.
  - **Logistics Strain:** Stamina costs scale with army size; negated by building adjacent to the Core or Power Plants.
- **Advanced Thermal System:** Non-linear stamina consumption with exponential heat scaling, environmental heat soak, and natural recovery during overheat.
- **Branching Progression:** Level up nodes to Rank 2 to unlock specialized variants (Cryo for area control, Armor Piercing for heavy damage).
- **Diverse Enemy AI:** 7 unique enemy types including Support healers, Siege Strikers, and massive Harbinger Bosses with corrosive suppression auras.
- **Dynamic Maps:** 20x20 grid with randomized POIs, unbuildable obstacles, and tactical hazards (Volcanic/Corrosive).
- **Pro UI/UX:** 
  - In-game HUD with numeric real-time stats.
  - Placement preview with directional turret indicators and 90-degree sector range.
  - Interactive help system explaining all tactical layers.
  - Aspect-ratio-correct resizable scaling with letterboxing.

## Tech Stack
- **Framework:** MonoGame (.NET 8.0)
- **Rendering:** High-fidelity 2D primitives based on custom SVG asset translations.
- **Font System:** Custom 5x7 high-quality bitmapped font.
- **Architecture:** Manager-based decoupling with event-driven entity management.

## Execution
Requires .NET 8.0 SDK.

```bash
dotnet build
dotnet run
```
