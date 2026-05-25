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
        
        // Specialized Buckets for performance-critical lookups
        private readonly List<Enemy> _enemies = new();
        private readonly List<Node> _nodes = new();
        private readonly List<Projectile> _projectiles = new();

        // Object Pools
        private readonly ObjectPool<Enemy> _enemyPool;
        private readonly ObjectPool<Projectile> _projectilePool;
        private readonly ObjectPool<Node> _nodePool;

        public EntityManager()
        {
            _enemyPool = new ObjectPool<Enemy>(() => new Enemy(), initialCapacity: 20);
            _projectilePool = new ObjectPool<Projectile>(() => new Projectile(), initialCapacity: 50);
            _nodePool = new ObjectPool<Node>(() => new Node(), initialCapacity: 20);
        }

        private GridManager _gridManager;
        private ResourceManager _resourceManager;

        public void Initialize(GridManager gridManager, ResourceManager resourceManager)
        {
            _gridManager = gridManager;
            _resourceManager = resourceManager;
            _gridManager.NodePlaced += AddNode;
            _gridManager.NodeRemoved += RemoveNodeAt;
            _gridManager.NodePlaced += _ => RecalculateAllPaths();
            _gridManager.NodeRemoved += _ => RecalculateAllPaths();
        }

        public void SpawnEnemy(EnemyType type, Vector2 startPosition, List<Point> path, Vector2 corePos, Point corePoint)
        {
            Enemy enemy = _enemyPool.Get();
            enemy.Initialize(type, startPosition, path, corePos, corePoint);
            _enemies.Add(enemy);
            _allEntities.Add(enemy);
        }

        private void RecalculateAllPaths()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsActive && !enemy.ReachedCore)
                {
                    var newPath = _gridManager.FindPath(enemy.GetCurrentGridPosition(), enemy.CorePoint);
                    if (newPath != null)
                    {
                        enemy.UpdatePath(newPath);
                    }
                }
            }
        }

        public void SpawnProjectile(Vector2 position, Enemy target, NodeSpecialization spec)
        {
            Projectile projectile = _projectilePool.Get();
            projectile.Initialize(position, target, spec);
            _projectiles.Add(projectile);
            _allEntities.Add(projectile);
        }

        public Node GetNodeFromPool(Point gridPos, Direction facing)
        {
            Node node = _nodePool.Get();
            node.Initialize(gridPos, facing);
            return node;
        }

        public void AddNode(Node node)
        {
            _nodes.Add(node);
            _allEntities.Add(node);
        }

        public void RemoveNodeAt(Point gridPos)
        {
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i].GridPosition == gridPos)
                {
                    Node node = _nodes[i];
                    node.IsActive = false;
                    _nodePool.Return(node);
                    _nodes.RemoveAt(i);
                    _allEntities.Remove(node);
                    break;
                }
            }
        }

        public bool IsWaveCleared() => _enemies.Count == 0;
        public IReadOnlyList<Enemy> ActiveEnemies => _enemies;
        public IReadOnlyList<Node> ActiveNodes => _nodes;

        public int Update(GameTime gameTime, Point spawn, Point core)
        {
            int reachedCoreCount = 0;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // 1. Reset Node Debuffs
            foreach (var node in _nodes)
            {
                node.SuppressionMultiplier = 1.0f;
            }

            // 2. Unified Update Loop
            for (int i = _allEntities.Count - 1; i >= 0; i--)
            {
                var entity = _allEntities[i];
                entity.Update(gameTime);

                if (entity is Enemy enemy)
                {
                    // Apply Corrosive Damage
                    var cell = _gridManager.GetCell(enemy.GetCurrentGridPosition().X, enemy.GetCurrentGridPosition().Y);
                    if (cell != null && cell.Type == CellType.Corrosive)
                    {
                        enemy.Health -= 5f * dt; // 5 damage per second
                        if (enemy.Health <= 0) enemy.IsActive = false;
                    }

                    // Apply Harbinger Suppression Aura
                    if (enemy.IsActive && enemy.Type == EnemyType.Harbinger && enemy.AuraRange > 0)
                    {
                        foreach (var node in _nodes)
                        {
                            if (Vector2.Distance(enemy.Position, node.Position) <= enemy.AuraRange)
                            {
                                node.SuppressionMultiplier = 1.5f; // 50% penalty
                                node.TakeDamage(2f * dt); // 2 damage per second
                            }
                        }
                    }

                    if (enemy.ReachedCore)
                    {
                        reachedCoreCount++;
                        ReturnEnemy(enemy);
                    }
                    else if (!enemy.IsActive)
                    {
                        ReturnEnemy(enemy);
                    }
                }
                else if (entity is Projectile projectile && !projectile.IsActive)
                {
                    _projectilePool.Return(projectile);
                    _projectiles.Remove(projectile);
                    _allEntities.RemoveAt(i);
                }
                else if (entity is Node node)
                {
                    // Handle Node Death
                    if (!node.IsActive)
                    {
                        _gridManager.RemoveNode(node.GridPosition.X, node.GridPosition.Y, spawn, core);
                        continue;
                    }

                    // Check for Volcanic heat penalty
                    float heatMult = 1.0f;
                    var cell = _gridManager.GetCell(node.GridPosition.X, node.GridPosition.Y);
                    if (cell != null && cell.Type == CellType.Volcanic) heatMult = 1.5f;

                    // Continuous Aiming
                    var target = node.FindTarget(_enemies);
                    node.AimAt(target, dt);

                    if (node.CanFire(heatMult))
                    {
                        if (target != null)
                        {
                            SpawnProjectile(node.Position, target, node.Specialization);
                            node.AddExperience(10f); // Award XP
                        }
                    }
                }
            }

            // 3. Execute Advanced Enemy Abilities
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsActive) continue;

                if (enemy.Type == EnemyType.Support)
                {
                    if (enemy.CanUseAbility())
                    {
                        foreach (var other in _enemies)
                        {
                            if (other.IsActive && Vector2.Distance(enemy.Position, other.Position) <= enemy.AbilityRange)
                            {
                                other.Health = MathHelper.Min(other.MaxHealth, other.Health + enemy.AbilityPower);
                            }
                        }
                    }
                }
                else if (enemy.Type == EnemyType.Striker)
                {
                    enemy.IsAttacking = false;
                    Node targetNode = null;
                    float minDist = enemy.AbilityRange;

                    foreach (var node in _nodes)
                    {
                        float dist = Vector2.Distance(enemy.Position, node.Position);
                        if (dist <= minDist)
                        {
                            minDist = dist;
                            targetNode = node;
                        }
                    }

                    if (targetNode != null)
                    {
                        enemy.IsAttacking = true;
                        if (enemy.CanUseAbility())
                        {
                            targetNode.TakeDamage(enemy.AbilityPower);
                        }
                    }
                }
            }

            return reachedCoreCount;
        }

        private void ReturnEnemy(Enemy enemy)
        {
            if (!enemy.ReachedCore)
            {
                _resourceManager.AddScrap(enemy.ScrapValue);
            }
            _enemyPool.Return(enemy);
            _enemies.Remove(enemy);
            _allEntities.Remove(enemy);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // Unified Draw Loop
            foreach (var entity in _allEntities)
            {
                entity.Draw(spriteBatch, pixel);
            }
        }
    }
}
