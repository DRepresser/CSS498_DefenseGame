using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TacticalDefenseGame.Entities;
using TacticalDefenseGame.Models;
using TacticalDefenseGame.Utils;

namespace TacticalDefenseGame.Managers
{
    public class EntityManager
    {
        private readonly List<Entity> _allEntities = new();
        private readonly List<Enemy> _enemies = new();
        private readonly List<Node> _nodes = new();
        private readonly List<Projectile> _projectiles = new();

        private readonly ObjectPool<Enemy> _enemyPool;
        private readonly ObjectPool<Projectile> _projectilePool;
        private readonly ObjectPool<Node> _nodePool;

        private Dictionary<string, Texture2D> _textures;

        public EntityManager()
        {
            _enemyPool = new ObjectPool<Enemy>(() => new Enemy(), 20);
            _projectilePool = new ObjectPool<Projectile>(() => new Projectile(), 50);
            _nodePool = new ObjectPool<Node>(() => new Node(), 20);
        }

        private GridManager _gridManager;
        private ResourceManager _resourceManager;

        public void Initialize(GridManager gridManager, ResourceManager resourceManager, Dictionary<string, Texture2D> textures)
        {
            _gridManager = gridManager;
            _resourceManager = resourceManager;
            _textures = textures;
            _gridManager.NodePlaced += AddNode;
            _gridManager.NodeRemoved += RemoveNodeAt;
            _gridManager.NodePlaced += _ => RecalculateAllPaths();
            _gridManager.NodeRemoved += _ => RecalculateAllPaths();
        }

        public void SpawnEnemy(EnemyType type, Vector2 pos, List<Point> path, Vector2 corePos, Point corePoint, int wave)
        {
            Enemy e = _enemyPool.Get(); e.Initialize(type, pos, path, corePos, corePoint, wave);
            _enemies.Add(e); _allEntities.Add(e);
        }

        private void RecalculateAllPaths()
        {
            foreach (var e in _enemies) if (e.IsActive && !e.ReachedCore) { var p = _gridManager.FindPath(e.GetCurrentGridPosition(), e.CorePoint); if (p != null) e.UpdatePath(p); }
        }

        public void SpawnProjectile(Vector2 pos, Enemy target, NodeSpecialization spec)
        {
            Projectile p = _projectilePool.Get(); p.Initialize(pos, target, spec);
            _projectiles.Add(p); _allEntities.Add(p);
        }

        public Node GetNodeFromPool(Point pos, Direction f) { Node n = _nodePool.Get(); n.Initialize(pos, f); return n; }
        public void AddNode(Node n) { _nodes.Add(n); _allEntities.Add(n); }
        public void RemoveNodeAt(Point p) { for (int i = _nodes.Count - 1; i >= 0; i--) if (_nodes[i].GridPosition == p) { Node n = _nodes[i]; n.IsActive = false; _nodePool.Return(n); _nodes.RemoveAt(i); _allEntities.Remove(n); break; } }
        public bool IsWaveCleared() => _enemies.Count == 0;
        public IReadOnlyList<Enemy> ActiveEnemies => _enemies;

        public void HealAllNodes(float amount) { foreach (var n in _nodes) n.Health = MathHelper.Min(n.MaxHealth, n.Health + amount); }

        public int Update(GameTime gameTime, Point spawn, Point core, out float totalDmg)
        {
            int reached = 0; totalDmg = 0f; float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float armyMult = 1.0f + (_nodes.Count * 0.02f);
            float globalFireRateMult = (_resourceManager.OverclockTimer > 0) ? 1.5f : 1.0f;

            foreach (var n in _nodes) n.SuppressionMultiplier = 1.0f;
            for (int i = _allEntities.Count - 1; i >= 0; i--) {
                var ent = _allEntities[i]; ent.Update(gameTime);
                if (ent is Enemy e) {
                    var cell = _gridManager.GetCell(e.GetCurrentGridPosition().X, e.GetCurrentGridPosition().Y);
                    if (cell != null && cell.Type == CellType.Corrosive) { e.Health -= 5f * dt; if (e.Health <= 0) e.IsActive = false; }
                    if (e.IsActive && e.Type == EnemyType.Harbinger && e.AuraRange > 0) foreach (var n in _nodes) if (Vector2.Distance(e.Position, n.Position) <= e.AuraRange) { n.SuppressionMultiplier = 1.5f; n.TakeDamage(5f * dt); }
                    if (e.ReachedCore) { reached++; totalDmg += e.CoreDamage; ReturnEnemy(e); } else if (!e.IsActive) ReturnEnemy(e);
                } else if (ent is Projectile p && !p.IsActive) { _projectilePool.Return(p); _projectiles.Remove(p); _allEntities.RemoveAt(i); }
                else if (ent is Node n) {
                    if (!n.IsActive) { _gridManager.RemoveNode(n.GridPosition.X, n.GridPosition.Y, spawn, core); continue; }
                    float hMult = (_gridManager.GetCell(n.GridPosition.X, n.GridPosition.Y)?.Type == CellType.Volcanic) ? 1.5f : 1.0f;
                    var t = n.FindTarget(_enemies); n.AimAt(t, dt);
                    // Apply Overclock multiplier to fire check
                    if (n.CanFire(hMult, armyMult * (1.0f / globalFireRateMult)) && t != null) { SpawnProjectile(n.Position, t, n.Specialization); n.AddExperience(10f); }
                }
            }
            foreach (var e in _enemies) {
                if (!e.IsActive) continue;
                if (e.Type == EnemyType.Support && e.CanUseAbility()) foreach (var o in _enemies) if (o.IsActive && Vector2.Distance(e.Position, o.Position) <= e.AbilityRange) o.Health = MathHelper.Min(o.MaxHealth, o.Health + e.AbilityPower);
                else if (e.Type == EnemyType.Striker) {
                    e.IsAttacking = false; Node targetNode = null; float minDist = e.AbilityRange;
                    foreach (var n in _nodes) { float d = Vector2.Distance(e.Position, n.Position); if (d <= minDist) { minDist = d; targetNode = n; } }
                    if (targetNode != null) { e.IsAttacking = true; e.AttackTargetPosition = targetNode.Position; if (e.CanUseAbility()) targetNode.TakeDamage(e.AbilityPower); }
                }
            }
            return reached;
        }

        private void ReturnEnemy(Enemy e) { if (!e.ReachedCore) _resourceManager.AddScrap(e.ScrapValue); _enemyPool.Return(e); _enemies.Remove(e); _allEntities.Remove(e); }

        public void Draw(SpriteBatch sb, Texture2D px)
        {
            if (_textures == null) return;
            foreach (var ent in _allEntities) {
                if (ent is Node n) {
                    string key = n.Specialization switch { NodeSpecialization.Cryo => "tower_ice", NodeSpecialization.ArmorPiercing => "tower_laser", _ => "tower_base" };
                    if (_textures.ContainsKey(key)) {
                        Texture2D tex = _textures[key];
                        Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                        float rot = n.Facing switch { Direction.Up => 0, Direction.Right => MathHelper.PiOver2, Direction.Down => MathHelper.Pi, Direction.Left => -MathHelper.PiOver2, _ => 0 };
                        // Standardized 1.4x Scale
                        float scale = (GridManager.CellSize * 1.4f) / tex.Width;
                        sb.Draw(tex, n.Position, null, Color.White, rot, origin, scale, SpriteEffects.None, 0);
                    }
                    n.DrawExtras(sb, px);
                } else if (ent is Enemy e) {
                    string key = e.Type switch { EnemyType.Standard => "enemy_standard", EnemyType.Speedster => "enemy_speedster", EnemyType.Tank => "enemy_tank", EnemyType.Phaser => "enemy_phaser", EnemyType.Support => "enemy_support", EnemyType.Striker => "enemy_striker", EnemyType.Harbinger => "enemy_harbinger", _ => null };
                    if (key != null && _textures.ContainsKey(key)) {
                        Texture2D tex = _textures[key];
                        Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                        float rot = e.Rotation; 
                        if (e.AttackTargetPosition.HasValue && e.Type == EnemyType.Striker && e.IsAttacking) {
                            Vector2 dir = e.AttackTargetPosition.Value - e.Position;
                            rot = (float)Math.Atan2(dir.Y, dir.X) + MathHelper.PiOver2; 
                        }
                        // Standardized 1.4x Scale
                        float scale = (GridManager.CellSize * 1.4f) / tex.Width;
                        sb.Draw(tex, e.Position, null, Color.White, rot, origin, scale, SpriteEffects.None, 0);
                    }
                    e.DrawExtras(sb, px);
                } else ent.Draw(sb, px);
            }
        }
    }
}
