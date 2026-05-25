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
        public float ScrapValue;
        public float MaxHealth;
        public bool ReachedCore { get; private set; }
        
        // Advanced Behaviors
        public float AbilityCooldown = 2.0f;
        private float _abilityTimer = 0f;
        public float AbilityRange = 100f;
        public float AbilityPower = 10f; // Healing or Damage
        public bool IsAttacking = false;

        private float _speedMultiplier = 1.0f;
        private float _slowTimer = 0f;

        private List<Point> _path;
        private int _currentPathIndex = 0;
        private Vector2 _targetPosition;
        private Vector2 _corePos;
        private Point _corePoint;

        public Enemy()
        {
            IsActive = false;
        }

        public void Initialize(EnemyType type, Vector2 startPosition, List<Point> path, Vector2 corePos, Point corePoint)
        {
            Type = type;
            Position = startPosition;
            _path = path;
            _corePos = corePos;
            _corePoint = corePoint;
            _currentPathIndex = 0; 
            ReachedCore = false;
            IsActive = true;

            switch (type)
            {
                case EnemyType.Standard:
                    Speed = 80f; Health = 100f; Color = Color.Red; ScrapValue = 10f;
                    break;
                case EnemyType.Speedster:
                    Speed = 150f; Health = 40f; Color = Color.Orange; ScrapValue = 5f;
                    break;
                case EnemyType.Tank:
                    Speed = 40f; Health = 300f; Color = Color.DarkRed; ScrapValue = 25f;
                    break;
                case EnemyType.Phaser:
                    Speed = 60f; Health = 80f; Color = Color.Purple; ScrapValue = 15f;
                    _targetPosition = _corePos;
                    break;
                case EnemyType.Support:
                    Speed = 50f; Health = 150f; MaxHealth = 150f; Color = Color.LimeGreen; ScrapValue = 20f;
                    AbilityRange = 120f; AbilityPower = 20f; AbilityCooldown = 3.0f;
                    break;
                case EnemyType.Striker:
                    Speed = 70f; Health = 120f; MaxHealth = 120f; Color = Color.Crimson; ScrapValue = 25f;
                    AbilityRange = 80f; AbilityPower = 15f; AbilityCooldown = 2.0f;
                    break;
                case EnemyType.Harbinger:
                    Speed = 30f; Health = 1500f; MaxHealth = 1500f; Color = Color.DarkSlateBlue; ScrapValue = 200f;
                    AbilityRange = 200f; AbilityPower = 10f; AbilityCooldown = 4.0f; // EMP/Pulse potential
                    break;
            }
            MaxHealth = Health;
            _abilityTimer = 0f;
            IsAttacking = false;
            
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

        public void UpdatePath(List<Point> newPath)
        {
            if (Type == EnemyType.Phaser || newPath == null || newPath.Count == 0) return;

            _path = newPath;
            _currentPathIndex = 0;

            // Fix Rollback/Jitter:
            // If we have at least 2 points, check if we've already passed the first point's center
            // relative to the direction of the second point.
            if (_path.Count > 1)
            {
                Vector2 p0 = new Vector2(_path[0].X * GridManager.CellSize + GridManager.CellSize / 2, _path[0].Y * GridManager.CellSize + GridManager.CellSize / 2);
                Vector2 p1 = new Vector2(_path[1].X * GridManager.CellSize + GridManager.CellSize / 2, _path[1].Y * GridManager.CellSize + GridManager.CellSize / 2);
                
                Vector2 pathDir = p1 - p0;
                Vector2 posDir = Position - p0;

                // If dot product is positive, we are "ahead" of p0 in the direction of p1
                if (Vector2.Dot(pathDir, posDir) > 0)
                {
                    _currentPathIndex = 1;
                }
            }

            SetTargetToNextNode();
        }

        public Point GetCurrentGridPosition()
        {
            return new Point((int)Position.X / GridManager.CellSize, (int)Position.Y / GridManager.CellSize);
        }

        public Point CorePoint => _corePoint;

        public override void Update(GameTime gameTime)
        {
            if (!IsActive || ReachedCore) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update Slow Effect
            if (_slowTimer > 0)
            {
                _slowTimer -= dt;
                if (_slowTimer <= 0) _speedMultiplier = 1.0f;
            }

            // Update Ability Timer
            if (_abilityTimer > 0) _abilityTimer -= dt;
            
            if (Type == EnemyType.Phaser)
            {
                MoveTowards(_corePos, dt);
            }
            else if (_path != null && _currentPathIndex < _path.Count)
            {
                // Strikers stop moving if they are attacking a node, 
                // but move a bit if their ability is on cooldown (to simulate pushing through)
                if (Type == EnemyType.Striker && IsAttacking && _abilityTimer > AbilityCooldown * 0.5f)
                {
                    // Stay put during the actual impact/recoil phase
                }
                else
                {
                    MoveTowards(_targetPosition, dt);
                }
            }
        }

        public bool CanUseAbility()
        {
            if (_abilityTimer <= 0)
            {
                _abilityTimer = AbilityCooldown;
                return true;
            }
            return false;
        }

        private void MoveTowards(Vector2 target, float dt)
        {
            Vector2 direction = target - Position;
            float distanceToTarget = direction.Length();

            if (distanceToTarget > 0.1f)
            {
                direction.Normalize();
                float moveAmount = Speed * _speedMultiplier * dt;

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

        public void ApplySlow(float multiplier, float duration)
        {
            _speedMultiplier = MathHelper.Min(_speedMultiplier, multiplier);
            _slowTimer = MathHelper.Max(_slowTimer, duration);
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
            if (Type == EnemyType.Harbinger) size = 42; // Boss size

            Rectangle rect = new Rectangle((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size);
            spriteBatch.Draw(pixel, rect, Color);

            // Draw Enemy Health Bar
            if (Health < MaxHealth)
            {
                int barWidth = size + 4;
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - barWidth / 2, (int)Position.Y - size / 2 - 8, barWidth, 3), Color.Black * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - barWidth / 2, (int)Position.Y - size / 2 - 8, (int)(barWidth * (Health / MaxHealth)), 3), Color.Red);
            }
        }
    }
}
