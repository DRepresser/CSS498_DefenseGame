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

        public void Initialize(GridManager gridManager)
        {
            gridManager.NodePlaced += AddNode;
            gridManager.NodeRemoved += RemoveNodeAt;
        }

        public void SpawnEnemy(EnemyType type, Vector2 startPosition, List<Point> path, Vector2 corePos)
        {
            Enemy enemy = _enemyPool.Get();
            enemy.Initialize(type, startPosition, path, corePos);
            _enemies.Add(enemy);
            _allEntities.Add(enemy);
        }

        public void SpawnProjectile(Vector2 position, Enemy target)
        {
            Projectile projectile = _projectilePool.Get();
            projectile.Initialize(position, target);
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

        public int Update(GameTime gameTime)
        {
            int reachedCoreCount = 0;

            // 1. Unified Update Loop
            for (int i = _allEntities.Count - 1; i >= 0; i--)
            {
                var entity = _allEntities[i];
                entity.Update(gameTime);

                if (entity is Enemy enemy)
                {
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
                else if (entity is Node node && node.CanFire())
                {
                    var target = node.FindTarget(_enemies);
                    if (target != null) SpawnProjectile(node.Position, target);
                }
            }

            return reachedCoreCount;
        }

        private void ReturnEnemy(Enemy enemy)
        {
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
