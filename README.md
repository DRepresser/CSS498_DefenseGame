# Tactical Grid-Defense
High-fidelity tower defense built with MonoGame on .NET 8.0.

## Overview
- **Visuals:** Pure PNG assets at unified 1.25x scale. Custom 5x7 bitmapped font.
- **Resolution:** 960x1060 virtual res (720x795 window) with letterboxed scaling.
- **Grid:** 20x20 layout (48px cells) with randomized hazards (Volcanic, Corrosive, Obstacles).

## Key Systems
- **Logistics:** Units need proximity to Core/Power Plants to negate Logistics Strain (+2% cost/unit) and Power Penalties (80% regen drop).
- **Combat:** Units attack in 90-degree front arcs. Rank 2 units specialize into **Cryo** (Slow) or **AP** (Piercing).
- **Enemies:** 7 types (Support healers, Siege Strikers, Harbinger Bosses) with dynamic A* pathfinding and boss suppression auras.
- **Economy:** Dual-resource (Energy for placement, Scrap for specialization).

## Build & Run
```bash
dotnet build
dotnet run
```
