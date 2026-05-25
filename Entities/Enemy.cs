using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TacticalDefenseGame.Models;
using TacticalDefenseGame.Managers;
namespace TacticalDefenseGame.Entities
{
    public class Enemy : Entity
    {
        public EnemyType Type;
        public float Speed;
        public float Health;
        public bool ReachedCore { get; private set; }
        
        private List<Point> _path;
        private int _currentPathIndex = 0;
        private Vector2 _targetPosition;
        private Vector2 _corePos;

        public Enemy()
        {
            IsActive = false;
        }

        public void Initialize(EnemyType type, Vector2 startPosition, List<Point> path, Vector2 corePos)
        {
            Type = type;
            Position = startPosition;
            _path = path;
            _corePos = corePos;
            _currentPathIndex = 0; 
            ReachedCore = false;
            IsActive = true;

            switch (type)
            {
                case EnemyType.Standard:
                    Speed = 80f; Health = 100f; Color = Color.Red;
                    break;
                case EnemyType.Speedster:
                    Speed = 150f; Health = 40f; Color = Color.Orange;
                    break;
                case EnemyType.Tank:
                    Speed = 40f; Health = 300f; Color = Color.DarkRed;
                    break;
                case EnemyType.Phaser:
                    Speed = 60f; Health = 80f; Color = Color.Purple;
                    _targetPosition = _corePos;
                    break;
            }
            
            if (Type != EnemyType.Phaser && _path != null && _path.Count > 0)
            {
                SetTargetToNextNode();
                Vector2 targetPos = new Vector2(
                    _path[0].X * GridManager.CellSize + GridManager.CellSize / 2,
                    _path[0].Y * GridManager.CellSize + GridManager.CellSize / 2
                );
                if (Vector2.Distance(Position, targetPos) < 1f && _path.Count > 1)
                {
                    _currentPathIndex = 1;
                    SetTargetToNextNode();
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive || ReachedCore) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            if (Type == EnemyType.Phaser)
            {
                MoveTowards(_corePos, dt);
            }
            else if (_path != null && _currentPathIndex < _path.Count)
            {
                MoveTowards(_targetPosition, dt);
            }
        }

        private void MoveTowards(Vector2 target, float dt)
        {
            Vector2 direction = target - Position;
            float distanceToTarget = direction.Length();

            if (distanceToTarget > 0.1f)
            {
                direction.Normalize();
                float moveAmount = Speed * dt;

                if (moveAmount >= distanceToTarget)
                {
                    Position = target;
                    AdvancePath();
                }
                else
                {
                    Position += direction * moveAmount;
                }
            }
            else
            {
                AdvancePath();
            }
        }

        private void AdvancePath()
        {
            if (Type == EnemyType.Phaser || _path == null || _currentPathIndex >= _path.Count - 1)
            {
                ReachedCore = true;
                IsActive = false;
            }
            else
            {
                _currentPathIndex++;
                SetTargetToNextNode();
            }
        }

        private void SetTargetToNextNode()
        {
            if (_path != null && _currentPathIndex < _path.Count)
            {
                _targetPosition = new Vector2(
                    _path[_currentPathIndex].X * GridManager.CellSize + GridManager.CellSize / 2,
                    _path[_currentPathIndex].Y * GridManager.CellSize + GridManager.CellSize / 2
                );
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            if (!IsActive) return;

            int size = (Type == EnemyType.Tank) ? 26 : 20;
            if (Type == EnemyType.Speedster) size = 14;

            Rectangle rect = new Rectangle((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size);
            spriteBatch.Draw(pixel, rect, Color);
        }
    }
}
