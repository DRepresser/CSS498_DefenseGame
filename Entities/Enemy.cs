using System;
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
        public float Speed, Health, ScrapValue, MaxHealth;
        public bool ReachedCore { get; private set; }
        
        public float AbilityCooldown = 2.0f;
        private float _abilityTimer = 0f;
        public float AbilityRange = 100f, AbilityPower = 10f, AuraRange = 0f, CoreDamage = 10f;
        public bool IsAttacking = false;
        public float Rotation = 0f;
        public Vector2? AttackTargetPosition = null;

        private float _speedMultiplier = 1.0f, _slowTimer = 0f;
        private List<Point> _path;
        private int _currentPathIndex = 0;
        private Vector2 _targetPosition, _corePos;
        private Point _corePoint;

        public Enemy() { IsActive = false; }

        public void Initialize(EnemyType type, Vector2 pos, List<Point> path, Vector2 corePos, Point corePoint, int wave)
        {
            Type = type; Position = pos; _path = path; _corePos = corePos; _corePoint = corePoint;
            _currentPathIndex = 0; ReachedCore = false; IsActive = true; Rotation = 0f; AttackTargetPosition = null;
            switch (type) {
                case EnemyType.Standard: Speed = 80f; Health = 100f; ScrapValue = 10f; break;
                case EnemyType.Speedster: Speed = 150f; Health = 40f; ScrapValue = 5f; break;
                case EnemyType.Tank: Speed = 40f; Health = 300f; ScrapValue = 25f; break;
                case EnemyType.Phaser: Speed = 60f; Health = 80f; ScrapValue = 15f; _targetPosition = _corePos; break;
                case EnemyType.Support: Speed = 50f; Health = 150f; ScrapValue = 20f; AbilityRange = 120f; AbilityPower = 20f; AbilityCooldown = 3.0f; break;
                case EnemyType.Striker: Speed = 70f; Health = 120f; ScrapValue = 25f; AbilityRange = 80f; AbilityPower = 25f; AbilityCooldown = 2.0f; break;
                case EnemyType.Harbinger: Speed = 30f; Health = 1500f; ScrapValue = 200f; AbilityRange = 200f; AbilityPower = 30f; AbilityCooldown = 4.0f; AuraRange = 100f; CoreDamage = 50f; break;
            }
            float mult = (float)Math.Pow(1.15, wave - 1);
            Health *= mult; AbilityPower *= mult; MaxHealth = Health; _abilityTimer = 0f; IsAttacking = false;
            if (Type != EnemyType.Phaser && _path != null && _path.Count > 0) {
                SetTargetToNextNode();
                if (Vector2.Distance(Position, _targetPosition) < 1f && _path.Count > 1) { _currentPathIndex = 1; SetTargetToNextNode(); }
            }
        }

        public void UpdatePath(List<Point> newPath) {
            if (Type == EnemyType.Phaser || newPath == null || newPath.Count == 0) return;
            _path = newPath; _currentPathIndex = 0;
            if (_path.Count > 1) {
                Vector2 p0 = new Vector2(_path[0].X * GridManager.CellSize + GridManager.CellSize / 2, _path[0].Y * GridManager.CellSize + GridManager.CellSize / 2);
                Vector2 p1 = new Vector2(_path[1].X * GridManager.CellSize + GridManager.CellSize / 2, _path[1].Y * GridManager.CellSize + GridManager.CellSize / 2);
                if (Vector2.Dot(p1 - p0, Position - p0) > 0) _currentPathIndex = 1;
            }
            SetTargetToNextNode();
        }

        public Point GetCurrentGridPosition() => new Point((int)Position.X / GridManager.CellSize, (int)Position.Y / GridManager.CellSize);

        public override void Update(GameTime gameTime) {
            if (!IsActive || ReachedCore) return;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_slowTimer > 0) { _slowTimer -= dt; if (_slowTimer <= 0) _speedMultiplier = 1.0f; }
            if (_abilityTimer > 0) _abilityTimer -= dt;

            // Rotation Logic
            Vector2 lookDir = _targetPosition - Position;
            if (Type == EnemyType.Phaser) lookDir = _corePos - Position;
            if (Type == EnemyType.Striker && IsAttacking && AttackTargetPosition.HasValue) 
                lookDir = AttackTargetPosition.Value - Position;

            if (lookDir.Length() > 0.1f) {
                // North-facing asset: 0 is North, Atan2 is East. Add Pi/2 to rotate East(Pi/2) to South(Pi) etc.
                float targetAngle = (float)Math.Atan2(lookDir.Y, lookDir.X) + MathHelper.PiOver2;
                float diff = MathHelper.WrapAngle(targetAngle - Rotation);
                Rotation += Math.Sign(diff) * Math.Min(Math.Abs(diff), 8f * dt);
            }

            if (Type == EnemyType.Phaser) MoveTowards(_corePos, dt);
            else if (_path != null && _currentPathIndex < _path.Count) {
                if (Type == EnemyType.Striker && IsAttacking && _abilityTimer > AbilityCooldown * 0.5f) { }
                else MoveTowards(_targetPosition, dt);
            }
        }

        public bool CanUseAbility() { if (_abilityTimer <= 0) { _abilityTimer = AbilityCooldown; return true; } return false; }

        private void MoveTowards(Vector2 target, float dt) {
            Vector2 dir = target - Position; float dist = dir.Length();
            if (dist > 0.1f) { dir.Normalize(); float move = Speed * _speedMultiplier * dt; if (move >= dist) { Position = target; AdvancePath(); } else Position += dir * move; }
            else AdvancePath();
        }

        public void ApplySlow(float multiplier, float duration) { _speedMultiplier = MathHelper.Min(_speedMultiplier, multiplier); _slowTimer = MathHelper.Max(_slowTimer, duration); }

        private void AdvancePath() { if (Type == EnemyType.Phaser || _path == null || _currentPathIndex >= _path.Count - 1) { ReachedCore = true; IsActive = false; } else { _currentPathIndex++; SetTargetToNextNode(); } }

        private void SetTargetToNextNode() { if (_path != null && _currentPathIndex < _path.Count) _targetPosition = new Vector2(_path[_currentPathIndex].X * GridManager.CellSize + GridManager.CellSize / 2, _path[_currentPathIndex].Y * GridManager.CellSize + GridManager.CellSize / 2); }

        public void DrawExtras(SpriteBatch sb, Texture2D px) {
            if (Health < MaxHealth) {
                int w = 40; sb.Draw(px, new Rectangle((int)Position.X - w/2, (int)Position.Y - 30, w, 6), Color.Black * 0.5f);
                sb.Draw(px, new Rectangle((int)Position.X - w/2, (int)Position.Y - 30, (int)(w * (Health/MaxHealth)), 6), Color.Red);
            }
        }
        public override void Draw(SpriteBatch sb, Texture2D px) { }
        public Point CorePoint => _corePoint;
    }
}
