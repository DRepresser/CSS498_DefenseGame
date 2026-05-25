using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TacticalDefenseGame.Models;
using TacticalDefenseGame.Entities;
using TacticalDefenseGame.Utils;

namespace TacticalDefenseGame.Managers
{
    public class GridManager
    {
        public const int GridSize = 15;
        public const int CellSize = 40;
        private Cell[,] _grid;

        // Anti-Juggling
        private float _pathLockTimer = 0f;
        public const float PathLockDuration = 2.5f;
        public bool IsPathLocked => _pathLockTimer > 0;

        // Events for Decoupling
        public event Action<Node> NodePlaced;
        public event Action<Point> NodeRemoved;

        public GridManager()
        {
            _grid = new Cell[GridSize, GridSize];
            Random rng = new Random();

            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    _grid[x, y] = new Cell(x, y);

                    // Randomly assign hazards (excluding spawn/core rows roughly)
                    if (x > 1 && x < GridSize - 2)
                    {
                        double roll = rng.NextDouble();
                        if (roll < 0.05) 
                        {
                            _grid[x, y].Type = CellType.Obstacle;
                            _grid[x, y].IsWalkable = false;
                        }
                        else if (roll < 0.12) _grid[x, y].Type = CellType.Volcanic;
                        else if (roll < 0.20) _grid[x, y].Type = CellType.Corrosive;
                    }
                }
            }
        }

        public (Point spawn, Point core) GenerateRandomPoints()
        {
            Random rng = new Random();
            Point spawn, core;
            List<Point> path = null;

            int attempts = 0;
            do
            {
                // Spawn on left side, Core on right side
                spawn = new Point(0, rng.Next(0, GridSize));
                core = new Point(GridSize - 1, rng.Next(0, GridSize));

                // Clear obstacles at spawn/core
                GetCell(spawn.X, spawn.Y).Type = CellType.Standard;
                GetCell(spawn.X, spawn.Y).IsWalkable = true;
                GetCell(core.X, core.Y).Type = CellType.Standard;
                GetCell(core.X, core.Y).IsWalkable = true;

                path = FindPath(spawn, core);
                attempts++;
            } while (path == null && attempts < 100);

            return (spawn, core);
        }

        public void Update(GameTime gameTime)
        {
            if (_pathLockTimer > 0)
            {
                _pathLockTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
        }

        public Cell GetCell(int x, int y)
        {
            if (x < 0 || x >= GridSize || y < 0 || y >= GridSize) return null;
            return _grid[x, y];
        }

        public void SetWalkable(int x, int y, bool isWalkable)
        {
            var cell = GetCell(x, y);
            if (cell != null) cell.IsWalkable = isWalkable;
        }

        public List<Point> FindPath(Point start, Point end)
        {
            var openSet = new PriorityQueue<PathNode>();
            var closedSet = new HashSet<Point>();
            var startNode = new PathNode(start, 0, GetDistance(start, end));
            openSet.Enqueue(startNode);

            while (openSet.Count > 0)
            {
                var currentNode = openSet.Dequeue();
                
                if (closedSet.Contains(currentNode.Position)) continue;
                closedSet.Add(currentNode.Position);

                if (currentNode.Position == end)
                {
                    return RetracePath(currentNode);
                }

                foreach (var neighborPos in GetNeighbors(currentNode.Position))
                {
                    if (closedSet.Contains(neighborPos)) continue;

                    var neighborCell = GetCell(neighborPos.X, neighborPos.Y);
                    if (neighborCell == null || !neighborCell.IsWalkable) continue;

                    int newCostToNeighbor = currentNode.G + 10;
                    
                    // Pathfinding Cost Penalty for Hazards? 
                    // Let's make enemies prefer not walking on Corrosive if possible
                    if (neighborCell.Type == CellType.Corrosive) newCostToNeighbor += 20;

                    var neighborNode = new PathNode(neighborPos, newCostToNeighbor, GetDistance(neighborPos, end), currentNode);
                    openSet.Enqueue(neighborNode);
                }
            }

            return null; // No path found
        }

        private List<Point> GetNeighbors(Point p)
        {
            var neighbors = new List<Point>();
            Point[] directions = { new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0) };
            foreach (var dir in directions)
            {
                var next = new Point(p.X + dir.X, p.Y + dir.Y);
                if (next.X >= 0 && next.X < GridSize && next.Y >= 0 && next.Y < GridSize)
                    neighbors.Add(next);
            }
            return neighbors;
        }

        private int GetDistance(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); // Manhattan distance
        }

        private List<Point> RetracePath(PathNode endNode)
        {
            var path = new List<Point>();
            var curr = endNode;
            while (curr != null)
            {
                path.Add(curr.Position);
                curr = curr.Parent;
            }
            path.Reverse();
            return path;
        }

        private class PathNode : IComparable<PathNode>
        {
            public Point Position;
            public int G; // Cost from start
            public int H; // Heuristic cost to end
            public int F => G + H;
            public PathNode Parent;

            public PathNode(Point pos, int g, int h, PathNode parent = null)
            {
                Position = pos;
                G = g;
                H = h;
                Parent = parent;
            }

            public int CompareTo(PathNode other)
            {
                int result = F.CompareTo(other.F);
                if (result == 0) result = H.CompareTo(other.H);
                return result;
            }
        }

        public bool CanPlaceNode(int x, int y, Point spawn, Point core, IEnumerable<Enemy> activeEnemies)
        {
            if (IsPathLocked) return false;

            // Restricted Locations: Cannot place on Spawn or Core
            if (new Point(x, y) == spawn || new Point(x, y) == core) return false;

            var cell = GetCell(x, y);
            if (cell == null || !cell.IsWalkable || cell.OccupyingNode != null) return false;

            // Check if any enemy is currently in this cell
            foreach (var enemy in activeEnemies)
            {
                if (enemy.IsActive && enemy.GetCurrentGridPosition() == new Point(x, y))
                    return false;
            }

            // Temporarily block and check path
            cell.IsWalkable = false;
            var path = FindPath(spawn, core);
            cell.IsWalkable = true;

            return path != null;
        }

        public void PlaceNode(int x, int y, Node node, Point core)
        {
            var cell = GetCell(x, y);
            if (cell != null)
            {
                cell.IsWalkable = false;
                cell.OccupyingNode = node;
                UpdateAllSynergies(core);
                NodePlaced?.Invoke(node);
            }
        }

        public void RemoveNode(int x, int y, Point spawn, Point core)
        {
            var cell = GetCell(x, y);
            if (cell != null && cell.OccupyingNode != null)
            {
                Point gridPos = cell.OccupyingNode.GridPosition;

                // Check for path alteration
                var oldPath = FindPath(spawn, core);
                
                cell.IsWalkable = true;
                cell.OccupyingNode = null;
                UpdateAllSynergies(core);

                var newPath = FindPath(spawn, core);

                // If path changed, trigger lock
                if (oldPath != null && newPath != null && oldPath.Count != newPath.Count)
                {
                    _pathLockTimer = PathLockDuration;
                }

                NodeRemoved?.Invoke(gridPos);
            }
        }

        public void UpdateAllSynergies(Point core)
        {
            // 1. Reset all power statuses
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    if (_grid[x, y].OccupyingNode != null)
                        _grid[x, y].OccupyingNode.IsPowered = false;
                }
            }

            // 2. BFS from Core to find powered nodes
            Queue<Point> queue = new Queue<Point>();
            HashSet<Point> visited = new HashSet<Point>();
            
            // Start BFS from nodes adjacent to the core
            foreach (var neighbor in GetNeighbors(core))
            {
                if (_grid[neighbor.X, neighbor.Y].OccupyingNode != null)
                {
                    queue.Enqueue(neighbor);
                    visited.Add(neighbor);
                }
            }

            while (queue.Count > 0)
            {
                Point curr = queue.Dequeue();
                _grid[curr.X, curr.Y].OccupyingNode.IsPowered = true;

                foreach (var neighbor in GetNeighbors(curr))
                {
                    if (!visited.Contains(neighbor) && _grid[neighbor.X, neighbor.Y].OccupyingNode != null)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // 3. Calculate synergy bonuses
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    Node node = _grid[x, y].OccupyingNode;
                    if (node != null)
                    {
                        int neighborCount = 0;
                        foreach (var neighborPos in GetNeighbors(new Point(x, y)))
                        {
                            if (_grid[neighborPos.X, neighborPos.Y].OccupyingNode != null)
                            {
                                neighborCount++;
                            }
                        }

                        // Tiered Synergy Bonuses
                        node.SynergyBonus = neighborCount >= 1 ? 5.0f : 0f;
                        node.SynergyRangeBonus = neighborCount >= 2 ? 30.0f : 0f;
                        node.SynergyCostMultiplier = neighborCount >= 3 ? 0.75f : 1.0f;
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, Point hoveredCell, IReadOnlyList<Enemy> activeEnemies)
        {
            // 1. Draw Base Grid
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    Rectangle rect = new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize);
                    Cell cell = _grid[x, y];
                    
                    Color cellColor = cell.Type switch
                    {
                        CellType.Obstacle => Color.Black,
                        CellType.Volcanic => Color.OrangeRed * 0.4f,
                        CellType.Corrosive => Color.DarkGreen * 0.4f,
                        _ => cell.IsWalkable ? 
                            ((x + y) % 2 == 0 ? Color.DarkSlateGray : Color.SlateGray) : 
                            Color.DarkRed
                    };
                    
                    spriteBatch.Draw(pixelTexture, rect, cellColor);
                    DrawBorder(spriteBatch, pixelTexture, rect, 1, Color.Black * 0.5f);
                }
            }

            // Path Lock Overlay
            if (IsPathLocked)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, GridSize * CellSize, GridSize * CellSize), Color.Red * 0.15f);
                DrawBorder(spriteBatch, pixelTexture, new Rectangle(0, 0, GridSize * CellSize, GridSize * CellSize), 4, Color.Red * 0.5f);
            }

            // 2. Draw Range Highlights
            Node focusedNode = GetCell(hoveredCell.X, hoveredCell.Y)?.OccupyingNode;
            if (focusedNode != null)
            {
                float effectiveRange = focusedNode.Range + focusedNode.SynergyRangeBonus;
                DrawRange(spriteBatch, pixelTexture, focusedNode.Position, effectiveRange, Color.Cyan * 0.2f, focusedNode.Facing);
            }

            // 3. Draw Enemy Danger Paths
            foreach (var enemy in activeEnemies)
            {
                if (!enemy.IsActive) continue;

                if (enemy.Type == EnemyType.Phaser)
                {
                    // Draw direct danger line for Phasers
                    DrawLine(spriteBatch, pixelTexture, enemy.Position, new Vector2(GridSize * CellSize, enemy.Position.Y), 2, Color.Purple * 0.4f);
                }
                else
                {
                    // Could draw path dots for standard enemies, but let's stick to Phaser danger for now
                }
            }
        }

        private void DrawRange(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float range, Color color, Direction facing)
        {
            float facingAngle = facing switch
            {
                Direction.Up => -MathHelper.PiOver2,
                Direction.Right => 0,
                Direction.Down => MathHelper.PiOver2,
                Direction.Left => MathHelper.Pi,
                _ => 0
            };

            // Highlight cells that are partially or fully within range AND sector
            int minX = (int)Math.Max(0, (center.X - range) / CellSize);
            int maxX = (int)Math.Min(GridSize - 1, (center.X + range) / CellSize);
            int minY = (int)Math.Max(0, (center.Y - range) / CellSize);
            int maxY = (int)Math.Min(GridSize - 1, (center.Y + range) / CellSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2 cellCenter = new Vector2(x * CellSize + CellSize / 2, y * CellSize + CellSize / 2);
                    float dist = Vector2.Distance(center, cellCenter);
                    if (dist <= range)
                    {
                        // Sector Check
                        Vector2 toCell = cellCenter - center;
                        float angle = (float)Math.Atan2(toCell.Y, toCell.X);
                        float diff = MathHelper.WrapAngle(angle - facingAngle);
                        
                        if (Math.Abs(diff) <= MathHelper.PiOver4) // 90 degree arc
                        {
                            spriteBatch.Draw(pixel, new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize), color);
                        }
                    }
                }
            }
        }

        private void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, int thickness, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            spriteBatch.Draw(pixel, 
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
                null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
