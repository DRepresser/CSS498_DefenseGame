using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TacticalDefenseGame.Models;
using TacticalDefenseGame.Managers;
namespace TacticalDefenseGame.Entities
{
    public class Node : Entity
    {
        public float Cost = 25f;
        public float Range = 120f;
        public float FireRate = 1.0f; // Shots per second
        private float _fireTimer = 0f;
        
        // Stamina & Thermal Management
        public float MaxStamina = 100f;
        public float CurrentStamina;
        public float BaseCoolingRate = 10f; // R_base
        
        // Synergy Specializations
        public float SynergyBonus = 0f; // beta_synergy (Stamina Regen)
        public float SynergyRangeBonus = 0f; // Range boost
        public float SynergyCostMultiplier = 1.0f; // Cost efficiency

        public float ActionCost = 15f; // C_action
        
        // Non-Linear Heat Scaling
        public float HeatModifier = 1.0f; // alpha
        public float HeatIncrement = 0.15f; // Base increment per shot
        public float HeatDecay = 0.4f; // alpha decay per second
        public float HeatExponent = 1.5f; // Exponential growth factor
        
        // Heat Soak: Regeneration is penalized at high heat
        public float HeatSoakThreshold = 2.0f; // Heat level where soak begins
        public float HeatSoakPenalty = 0.5f; // Max regeneration multiplier at high heat
        
        public bool IsOverheated { get; private set; }
        public float OverheatCooldown = 3.0f; // T_cooldown
        private float _overheatTimer = 0f;

        // State Effects
        private List<SteamParticle> _particles = new();
        private float _particleTimer = 0f;
        private Random _rng = new Random();

        public Direction Facing = Direction.Right;
        public Point GridPosition;

        public Node() { IsActive = false; }

        public void Initialize(Point gridPos, Direction facing)
        {
            GridPosition = gridPos;
            Position = new Vector2(
                gridPos.X * GridManager.CellSize + GridManager.CellSize / 2,
                gridPos.Y * GridManager.CellSize + GridManager.CellSize / 2
            );
            Facing = facing;
            Color = Color.White;
            CurrentStamina = MaxStamina;
            HeatModifier = 1.0f;
            IsOverheated = false;
            _overheatTimer = 0f;
            _fireTimer = 0f;
            _particles.Clear();
            SynergyBonus = 0;
            SynergyRangeBonus = 0;
            SynergyCostMultiplier = 1.0f;
            IsActive = true;
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (IsOverheated)
            {
                _overheatTimer -= dt;
                
                // Spawn Steam Particles
                _particleTimer += dt;
                if (_particleTimer >= 0.15f)
                {
                    _particleTimer = 0;
                    _particles.Add(new SteamParticle
                    {
                        Offset = new Vector2(_rng.Next(-10, 11), _rng.Next(-10, 11)),
                        Life = 0,
                        MaxLife = 0.8f + (float)_rng.NextDouble() * 0.4f,
                        Speed = 20f + (float)_rng.NextDouble() * 20f
                    });
                }

                if (_overheatTimer <= 0)
                {
                    IsOverheated = false;
                    CurrentStamina = MaxStamina * 0.2f; // Return with some stamina
                    HeatModifier = 1.0f;
                }
            }
            else
            {
                // Calculate Heat Soak Multiplier: scales from 1.0 to HeatSoakPenalty
                float soakMultiplier = 1.0f;
                if (HeatModifier > HeatSoakThreshold)
                {
                    // Linear falloff between threshold and a theoretical "max" heat (e.g. 5.0)
                    float t = MathHelper.Clamp((HeatModifier - HeatSoakThreshold) / 3.0f, 0, 1);
                    soakMultiplier = MathHelper.Lerp(1.0f, HeatSoakPenalty, t);
                }

                // Regenerate Stamina: (R_base + beta_synergy) * soak * t
                CurrentStamina += (BaseCoolingRate + SynergyBonus) * soakMultiplier * dt;
                if (CurrentStamina > MaxStamina) CurrentStamina = MaxStamina;

                // Decay Heat Modifier
                HeatModifier -= HeatDecay * dt;
                if (HeatModifier < 1.0f) HeatModifier = 1.0f;

                _fireTimer += dt;
            }

            // Update Steam Particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Life += dt;
                p.Offset.Y -= p.Speed * dt;
                if (p.Life >= p.MaxLife) _particles.RemoveAt(i);
                else _particles[i] = p;
            }
        }

        public bool CanFire()
        {
            if (IsOverheated) return false;

            if (_fireTimer >= 1.0f / FireRate)
            {
                // Calculate non-linear shot cost with Synergy Cost Multiplier
                float effectiveAlpha = (float)Math.Pow(HeatModifier, HeatExponent);
                float cost = ActionCost * effectiveAlpha * SynergyCostMultiplier;
                
                if (CurrentStamina >= cost)
                {
                    CurrentStamina -= cost;
                    HeatModifier += HeatIncrement;
                    _fireTimer = 0;
                    return true;
                }
                else
                {
                    // Trigger Overheat
                    IsOverheated = true;
                    _overheatTimer = OverheatCooldown;
                    // Note: Modifier is NOT reset here to simulate "heat soak" during cooldown
                    return false;
                }
            }
            return false;
        }

        public Enemy FindTarget(List<Enemy> enemies)
        {
            Enemy closest = null;
            float effectiveRange = Range + SynergyRangeBonus;
            float minDistance = effectiveRange;

            foreach (var enemy in enemies)
            {
                if (!enemy.IsActive) continue;

                float dist = Vector2.Distance(Position, enemy.Position);
                if (dist <= effectiveRange && IsInSector(enemy.Position))
                {
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = enemy;
                    }
                }
            }
            return closest;
        }

        private bool IsInSector(Vector2 targetPos)
        {
            // Simple 90-degree sector check based on facing
            Vector2 toTarget = targetPos - Position;
            float angle = (float)Math.Atan2(toTarget.Y, toTarget.X);
            float facingAngle = GetFacingAngle();

            float diff = MathHelper.WrapAngle(angle - facingAngle);
            return Math.Abs(diff) <= MathHelper.PiOver4; // 90 degrees total arc
        }

        private float GetFacingAngle()
        {
            return Facing switch
            {
                Direction.Up => -MathHelper.PiOver2,
                Direction.Right => 0,
                Direction.Down => MathHelper.PiOver2,
                Direction.Left => MathHelper.Pi,
                _ => 0
            };
        }

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // Draw Steam Particles
            foreach (var p in _particles)
            {
                float alpha = 1.0f - (p.Life / p.MaxLife);
                spriteBatch.Draw(pixel, new Rectangle((int)(Position.X + p.Offset.X), (int)(Position.Y + p.Offset.Y), 4, 4), Color.White * 0.6f * alpha);
            }

            // Draw Radial Stamina Ring (Background)
            DrawRadialBar(spriteBatch, pixel, Position, 18f, 1.0f, Color.Black * 0.3f);
            
            // Draw Radial Stamina Ring (Fill)
            float staminaPercent = CurrentStamina / MaxStamina;
            Color barColor = IsOverheated ? Color.Orange : Color.Cyan;
            if (HeatModifier > HeatSoakThreshold) barColor = Color.Lerp(barColor, Color.Yellow, (HeatModifier - HeatSoakThreshold) / 2f);
            DrawRadialBar(spriteBatch, pixel, Position, 18f, staminaPercent, barColor);

            // Draw Node base
            Rectangle rect = new Rectangle((int)Position.X - 15, (int)Position.Y - 15, 30, 30);
            Color nodeColor = IsOverheated ? Color.Red : Color.DarkGray;
            spriteBatch.Draw(pixel, rect, nodeColor);

            // Draw "Turret" indicator for facing
            Vector2 directionVec = new Vector2((float)Math.Cos(GetFacingAngle()), (float)Math.Sin(GetFacingAngle()));
            Vector2 turretEnd = Position + directionVec * 15;
            
            DrawLine(spriteBatch, pixel, Position, turretEnd, 3, IsOverheated ? Color.Gray : Color.Yellow);
            
            // Draw Overheat Countdown
            if (IsOverheated)
            {
                int secondsLeft = (int)Math.Ceiling(_overheatTimer);
                DrawDigit(spriteBatch, pixel, secondsLeft, Position - new Vector2(4, 30), 3, Color.Red);
            }

            // Draw Synergy Indicators (small dots based on levels)
            if (SynergyBonus > 0) // Level 1: Regen
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 6, (int)Position.Y - 6, 4, 4), Color.Lime);
            if (SynergyRangeBonus > 0) // Level 2: Range
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X + 2, (int)Position.Y - 6, 4, 4), Color.Cyan);
            if (SynergyCostMultiplier < 1.0f) // Level 3: Efficiency
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 2, (int)Position.Y + 2, 4, 4), Color.Gold);
        }

        private void DrawRadialBar(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, float percent, Color color)
        {
            const int segments = 20;
            const float step = MathHelper.TwoPi / segments;
            for (int i = 0; i < segments; i++)
            {
                float progress = (float)i / segments;
                if (progress <= percent)
                {
                    float angle = i * step - MathHelper.PiOver2;
                    Vector2 pos = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                    spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 2, (int)pos.Y - 2, 4, 4), color);
                }
            }
        }

        private void DrawDigit(SpriteBatch spriteBatch, Texture2D pixel, int digit, Vector2 pos, int size, Color color)
        {
            bool[,] segments = digit switch
            {
                1 => new[,] { { false, true, false }, { false, true, false }, { false, true, false }, { false, true, false }, { false, true, false } },
                2 => new[,] { { true, true, true }, { false, false, true }, { true, true, true }, { true, false, false }, { true, true, true } },
                3 => new[,] { { true, true, true }, { false, false, true }, { true, true, true }, { false, false, true }, { true, true, true } },
                _ => new[,] { { false, false, false }, { false, false, false }, { false, false, false }, { false, false, false }, { false, false, false } }
            };

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    if (segments[y, x])
                    {
                        spriteBatch.Draw(pixel, new Rectangle((int)pos.X + x * size, (int)pos.Y + y * size, size, size), color);
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
                null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }
    }

    public struct SteamParticle
    {
        public Vector2 Offset;
        public float Life;
        public float MaxLife;
        public float Speed;
    }
}
