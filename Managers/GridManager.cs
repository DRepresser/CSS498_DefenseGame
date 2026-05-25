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
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    _grid[x, y] = new Cell(x, y);
                }
            }
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

        public bool CanPlaceNode(int x, int y, Point spawn, Point core)
        {
            if (IsPathLocked) return false;

            var cell = GetCell(x, y);
            if (cell == null || !cell.IsWalkable || cell.OccupyingNode != null) return false;

            // Temporarily block and check path
            cell.IsWalkable = false;
            var path = FindPath(spawn, core);
            cell.IsWalkable = true;

            return path != null;
        }

        public void PlaceNode(int x, int y, Node node)
        {
            var cell = GetCell(x, y);
            if (cell != null)
            {
                cell.IsWalkable = false;
                cell.OccupyingNode = node;
                UpdateAllSynergies();
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
                UpdateAllSynergies();

                var newPath = FindPath(spawn, core);

                // If path changed, trigger lock
                if (oldPath != null && newPath != null && oldPath.Count != newPath.Count)
                {
                    _pathLockTimer = PathLockDuration;
                }

                NodeRemoved?.Invoke(gridPos);
            }
        }

        public void UpdateAllSynergies()
        {
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
                    Color cellColor = _grid[x, y].IsWalkable ? 
                        ((x + y) % 2 == 0 ? Color.DarkSlateGray : Color.SlateGray) : 
                        Color.DarkRed;
                    
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
                DrawRange(spriteBatch, pixelTexture, focusedNode.Position, effectiveRange, Color.Cyan * 0.2f);
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

        private void DrawRange(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float range, Color color)
        {
            // Highlight cells that are partially or fully within range
            int minX = (int)Math.Max(0, (center.X - range) / CellSize);
            int maxX = (int)Math.Min(GridSize - 1, (center.X + range) / CellSize);
            int minY = (int)Math.Max(0, (center.Y - range) / CellSize);
            int maxY = (int)Math.Min(GridSize - 1, (center.Y + range) / CellSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2 cellCenter = new Vector2(x * CellSize + CellSize / 2, y * CellSize + CellSize / 2);
                    if (Vector2.Distance(center, cellCenter) <= range)
                    {
                        spriteBatch.Draw(pixel, new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize), color);
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
