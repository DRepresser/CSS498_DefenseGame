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
        public const int GridSize = 20;
        public const int CellSize = 48;
        private Cell[,] _grid;

        // Anti-Juggling
        private float _pathLockTimer = 0f;
        public const float PathLockDuration = 2.5f;
        public bool IsPathLocked => _pathLockTimer > 0;

        // Texture Storage
        private Dictionary<string, Texture2D> _textures;

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

                    // Randomly assign hazards
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

            // Randomly assign exactly 5 Power Plants
            int plantsPlaced = 0;
            while (plantsPlaced < 5)
            {
                int rx = rng.Next(2, GridSize - 2);
                int ry = rng.Next(1, GridSize - 1);
                if (_grid[rx, ry].Type == CellType.Standard)
                {
                    _grid[rx, ry].Type = CellType.PowerPlant;
                    plantsPlaced++;
                }
            }
        }

        public void LoadTextures(Dictionary<string, Texture2D> textures)
        {
            _textures = textures;
        }

        public (Point spawn, Point core) GenerateRandomPoints()
        {
            Random rng = new Random();
            Point spawn, core;
            List<Point> path = null;
            int attempts = 0;
            do
            {
                spawn = new Point(0, rng.Next(0, GridSize));
                core = new Point(GridSize - 1, rng.Next(0, GridSize));
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
            if (_pathLockTimer > 0) _pathLockTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
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
            openSet.Enqueue(new PathNode(start, 0, GetDistance(start, end)));
            while (openSet.Count > 0)
            {
                var curr = openSet.Dequeue();
                if (closedSet.Contains(curr.Position)) continue;
                closedSet.Add(curr.Position);
                if (curr.Position == end) return RetracePath(curr);
                foreach (var neighborPos in GetNeighbors(curr.Position))
                {
                    if (closedSet.Contains(neighborPos)) continue;
                    var cell = GetCell(neighborPos.X, neighborPos.Y);
                    if (cell == null || !cell.IsWalkable) continue;
                    int cost = curr.G + 10;
                    if (cell.Type == CellType.Corrosive) cost += 20;
                    openSet.Enqueue(new PathNode(neighborPos, cost, GetDistance(neighborPos, end), curr));
                }
            }
            return null;
        }

        private List<Point> GetNeighbors(Point p)
        {
            var neighbors = new List<Point>();
            Point[] dirs = { new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0) };
            foreach (var dir in dirs)
            {
                var next = new Point(p.X + dir.X, p.Y + dir.Y);
                if (next.X >= 0 && next.X < GridSize && next.Y >= 0 && next.Y < GridSize) neighbors.Add(next);
            }
            return neighbors;
        }

        private int GetDistance(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private List<Point> RetracePath(PathNode endNode)
        {
            var path = new List<Point>();
            var curr = endNode;
            while (curr != null) { path.Add(curr.Position); curr = curr.Parent; }
            path.Reverse();
            return path;
        }

        private class PathNode : IComparable<PathNode>
        {
            public Point Position;
            public int G, H;
            public int F => G + H;
            public PathNode Parent;
            public PathNode(Point pos, int g, int h, PathNode parent = null) { Position = pos; G = g; H = h; Parent = parent; }
            public int CompareTo(PathNode other) { int res = F.CompareTo(other.F); if (res == 0) res = H.CompareTo(other.H); return res; }
        }

        public bool CanPlaceNode(int x, int y, Point spawn, Point core, IEnumerable<Enemy> activeEnemies)
        {
            if (IsPathLocked) return false;
            if (new Point(x, y) == spawn || new Point(x, y) == core) return false;
            var cell = GetCell(x, y);
            if (cell == null || !cell.IsWalkable || cell.OccupyingNode != null) return false;
            foreach (var enemy in activeEnemies) if (enemy.IsActive && enemy.GetCurrentGridPosition() == new Point(x, y)) return false;
            cell.IsWalkable = false;
            var path = FindPath(spawn, core);
            cell.IsWalkable = true;
            return path != null;
        }

        public void PlaceNode(int x, int y, Node node, Point core)
        {
            var cell = GetCell(x, y);
            if (cell != null) { cell.IsWalkable = false; cell.OccupyingNode = node; UpdateAllSynergies(core); NodePlaced?.Invoke(node); }
        }

        public void RemoveNode(int x, int y, Point spawn, Point core)
        {
            var cell = GetCell(x, y);
            if (cell != null && cell.OccupyingNode != null)
            {
                Point gridPos = cell.OccupyingNode.GridPosition;
                var oldPath = FindPath(spawn, core);
                cell.IsWalkable = true;
                cell.OccupyingNode = null;
                UpdateAllSynergies(core);
                var newPath = FindPath(spawn, core);
                if (oldPath != null && newPath != null && oldPath.Count != newPath.Count) _pathLockTimer = PathLockDuration;
                NodeRemoved?.Invoke(gridPos);
            }
        }

        public Color GetCellColor(int x, int y)
        {
            Cell cell = GetCell(x, y);
            if (cell == null) return Color.Black;
            return cell.Type switch {
                CellType.Obstacle => Color.Black,
                CellType.PowerPlant => Color.Yellow * 0.4f,
                CellType.Volcanic => Color.OrangeRed * 0.4f,
                CellType.Corrosive => Color.DarkGreen * 0.4f,
                _ => (x + y) % 2 == 0 ? Color.DarkSlateGray : Color.SlateGray
            };
        }

        public void UpdateAllSynergies(Point core)
        {
            for (int x = 0; x < GridSize; x++)
                for (int y = 0; y < GridSize; y++)
                    if (_grid[x, y].OccupyingNode != null) _grid[x, y].OccupyingNode.IsPowered = false;

            Queue<Point> queue = new Queue<Point>();
            HashSet<Point> visited = new HashSet<Point>();
            foreach (var neighbor in GetNeighbors(core))
                if (_grid[neighbor.X, neighbor.Y].OccupyingNode != null) { queue.Enqueue(neighbor); visited.Add(neighbor); }

            for (int x = 0; x < GridSize; x++)
                for (int y = 0; y < GridSize; y++)
                    if (_grid[x, y].Type == CellType.PowerPlant)
                        foreach (var neighbor in GetNeighbors(new Point(x, y)))
                            if (_grid[neighbor.X, neighbor.Y].OccupyingNode != null && !visited.Contains(neighbor)) { queue.Enqueue(neighbor); visited.Add(neighbor); }

            while (queue.Count > 0)
            {
                Point curr = queue.Dequeue();
                _grid[curr.X, curr.Y].OccupyingNode.IsPowered = true;
                foreach (var neighbor in GetNeighbors(curr))
                    if (!visited.Contains(neighbor) && _grid[neighbor.X, neighbor.Y].OccupyingNode != null) { visited.Add(neighbor); queue.Enqueue(neighbor); }
            }

            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    Node node = _grid[x, y].OccupyingNode;
                    if (node != null)
                    {
                        int neighborCount = 0; float plantBoost = 0f;
                        foreach (var neighborPos in GetNeighbors(new Point(x, y)))
                        {
                            if (_grid[neighborPos.X, neighborPos.Y].OccupyingNode != null) neighborCount++;
                            if (_grid[neighborPos.X, neighborPos.Y].Type == CellType.PowerPlant) plantBoost += 1.0f;
                            if (neighborPos == core) plantBoost += 1.0f;
                        }
                        node.SynergyBonus = neighborCount >= 1 ? 5.0f : 0f;
                        node.SynergyRangeBonus = neighborCount >= 2 ? 30.0f : 0f;
                        node.SynergyCostMultiplier = neighborCount >= 3 ? 0.75f : 1.0f;
                        node.IsNearPowerPlant = (plantBoost > 0);
                        node.IsExpandedRange = (neighborCount >= 1);
                    }
                }
            }
        }

        public void Draw(SpriteBatch sb, Texture2D px, Point hoveredCell, IReadOnlyList<Enemy> activeEnemies)
        {
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    Rectangle rect = new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize);
                    
                    // Draw cell background
                    sb.Draw(px, rect, GetCellColor(x, y) * 0.3f);
                    DrawBorder(sb, px, rect, 1, Color.Black * 0.2f);
                    
                    // Draw PNG-based Hazards
                    DrawHazardTexture(sb, x, y);
                }
            }
            if (IsPathLocked)
            {
                sb.Draw(px, new Rectangle(0, 0, GridSize * CellSize, GridSize * CellSize), Color.Red * 0.15f);
                DrawBorder(sb, px, new Rectangle(0, 0, GridSize * CellSize, GridSize * CellSize), 10, Color.Red * 0.5f);
            }
            Node focusedNode = GetCell(hoveredCell.X, hoveredCell.Y)?.OccupyingNode;
            if (focusedNode != null)
            {
                var pattern = focusedNode.GetAttackPattern();
                foreach (var p in pattern) if (p.X >= 0 && p.X < GridSize && p.Y >= 0 && p.Y < GridSize) sb.Draw(px, new Rectangle(p.X * CellSize, p.Y * CellSize, CellSize, CellSize), Color.Cyan * 0.2f);
            }
        }

        private void DrawHazardTexture(SpriteBatch sb, int x, int y)
        {
            Cell cell = _grid[x, y];
            if (cell.Type == CellType.Standard || _textures == null) return;

            string key = cell.Type switch {
                CellType.Obstacle => "hazard_obstacle",
                CellType.Volcanic => "hazard_volcanic",
                CellType.Corrosive => "hazard_corrosive",
                _ => null
            };

            Vector2 center = new Vector2(x * CellSize + CellSize / 2, y * CellSize + CellSize / 2);

            if (key != null && _textures.ContainsKey(key))
            {
                Texture2D tex = _textures[key];
                Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                // Standardized 1.4x Scale
                float scale = (CellSize * 1.4f) / tex.Width;
                sb.Draw(tex, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0);
            }
            else if (cell.Type == CellType.PowerPlant)
            {
                 Texture2D tex = _textures["tower_base"];
                 Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                 float scale = (CellSize * 1.4f) / tex.Width;
                 sb.Draw(tex, center, null, Color.Yellow * 0.5f, 0f, origin, scale, SpriteEffects.None, 0);
            }
        }

        private void DrawRange(SpriteBatch sb, Texture2D px, Vector2 center, float range, Color color, Direction facing)
        {
            float facingAngle = facing switch { Direction.Up => -MathHelper.PiOver2, Direction.Right => 0, Direction.Down => MathHelper.PiOver2, Direction.Left => MathHelper.Pi, _ => 0 };
            int minX = (int)Math.Max(0, (center.X - range) / CellSize);
            int maxX = (int)Math.Min(GridSize - 1, (center.X + range) / CellSize);
            int minY = (int)Math.Max(0, (center.Y - range) / CellSize);
            int maxY = (int)Math.Min(GridSize - 1, (center.Y + range) / CellSize);
            for (int x = minX; x <= maxX; x++) {
                for (int y = minY; y <= maxY; y++) {
                    Vector2 cellCenter = new Vector2(x * CellSize + CellSize / 2, y * CellSize + CellSize / 2);
                    if (Vector2.Distance(center, cellCenter) <= range) {
                        Vector2 toCell = cellCenter - center;
                        float diff = MathHelper.WrapAngle((float)Math.Atan2(toCell.Y, toCell.X) - facingAngle);
                        if (Math.Abs(diff) <= MathHelper.PiOver4) sb.Draw(px, new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize), color);
                    }
                }
            }
        }

        private void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, int thick, Color col)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            sb.Draw(px, new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thick), null, col, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }

        private void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle r, int thick, Color col)
        {
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, thick), col);
            sb.Draw(px, new Rectangle(r.X, r.Y + r.Height - thick, r.Width, thick), col);
            sb.Draw(px, new Rectangle(r.X, r.Y, thick, r.Height), col);
            sb.Draw(px, new Rectangle(r.X + r.Width - thick, r.Y, thick, r.Height), col);
        }
    }
}
