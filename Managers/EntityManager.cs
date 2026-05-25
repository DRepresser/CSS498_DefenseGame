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
        private readonly List<Enemy> _enemies = new();
        private readonly List<Node> _nodes = new();
        private readonly List<Projectile> _projectiles = new();

        // Object Pools
        private readonly ObjectPool<Enemy> _enemyPool;
        private readonly ObjectPool<Projectile> _projectilePool;

        public EntityManager()
        {
            _enemyPool = new ObjectPool<Enemy>(() => new Enemy(), initialCapacity: 20);
            _projectilePool = new ObjectPool<Projectile>(() => new Projectile(), initialCapacity: 50);
        }

        public void SpawnEnemy(EnemyType type, Vector2 startPosition, List<Point> path, Vector2 corePos)
        {
            Enemy enemy = _enemyPool.Get();
            enemy.Initialize(type, startPosition, path, corePos);
            _enemies.Add(enemy);
        }

        public void SpawnProjectile(Vector2 position, Enemy target)
        {
            Projectile projectile = _projectilePool.Get();
            projectile.Initialize(position, target);
            _projectiles.Add(projectile);
        }

        public void AddNode(Node node) => _nodes.Add(node);

        public void RemoveNodeAt(Point gridPos)
        {
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i].GridPosition == gridPos)
                {
                    _nodes.RemoveAt(i);
                    break;
                }
            }
        }

        public bool IsWaveCleared() => _enemies.Count == 0;

        public int Update(GameTime gameTime)
        {
            int reachedCoreCount = 0;

            // 1. Process Enemies
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                enemy.Update(gameTime);
                
                if (enemy.ReachedCore)
                {
                    reachedCoreCount++;
                    ReturnEnemy(i);
                }
                else if (!enemy.IsActive)
                {
                    ReturnEnemy(i);
                }
            }

            // 2. Process Nodes & Combat Logic
            foreach (var node in _nodes)
            {
                node.Update(gameTime);
                if (node.CanFire())
                {
                    var target = node.FindTarget(_enemies);
                    if (target != null) SpawnProjectile(node.Position, target);
                }
            }

            // 3. Process Projectiles
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                projectile.Update(gameTime);
                if (!projectile.IsActive)
                {
                    _projectilePool.Return(projectile);
                    _projectiles.RemoveAt(i);
                }
            }

            return reachedCoreCount;
        }

        private void ReturnEnemy(int index)
        {
            _enemyPool.Return(_enemies[index]);
            _enemies.RemoveAt(index);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            foreach (var node in _nodes) node.Draw(spriteBatch, pixel);
            foreach (var enemy in _enemies) enemy.Draw(spriteBatch, pixel);
            foreach (var projectile in _projectiles) projectile.Draw(spriteBatch, pixel);
        }
    }
}
