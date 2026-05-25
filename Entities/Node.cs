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
        public float BaseCoolingRate = 15f; // R_base

        // Health & Durability
        public float MaxHealth = 100f;
        public float Health;

        // Synergy Specializations
        public float SynergyBonus = 0f; // beta_synergy
        public float SynergyRangeBonus = 0f; // Range boost
        public float SynergyCostMultiplier = 1.0f; // Cost efficiency

        public float ActionCost = 10f; // C_action
        
        // Non-Linear Heat Scaling
        public float HeatModifier = 1.0f; // alpha
        public float HeatIncrement = 0.08f; // Base increment per shot
        public float HeatDecay = 0.6f; // alpha decay per second
        public float HeatExponent = 1.3f; // Exponential growth factor
        
        // Heat Soak: Regeneration is penalized at high heat
        public float HeatSoakThreshold = 3.0f; // Heat level where soak begins
        public float HeatSoakPenalty = 0.5f; // Max regeneration multiplier at high heat
        
        public bool IsOverheated { get; private set; }
        public float OverheatCooldown = 3.0f; // T_cooldown
        private float _overheatTimer = 0f;

        // Upgrade & Specialization
        public int Rank = 1;
        public float Experience = 0f;
        public float ExperienceToNextRank = 100f;
        public NodeSpecialization Specialization = NodeSpecialization.None;
        public bool IsPowered = true;
        public float SuppressionMultiplier = 1.0f; // 1.0 = normal, >1.0 = debuffed
        public bool IsNearPowerPlant = false; // Negates Logistics Strain if true

        // State Effects
        private List<SteamParticle> _particles = new();
        private float _particleTimer = 0f;
        private Random _rng = new Random();

        public Direction Facing = Direction.Right;
        public float TurretAngle;
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
            TurretAngle = GetFacingAngle();
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
            IsNearPowerPlant = false;
            Rank = 1;
            Experience = 0;
            Specialization = NodeSpecialization.None;
            Health = MaxHealth;
            IsActive = true;
        }

        public void AimAt(Enemy target, float dt)
        {
            float targetAngle = GetFacingAngle();
            if (target != null)
            {
                Vector2 dir = target.Position - Position;
                targetAngle = (float)Math.Atan2(dir.Y, dir.X);
            }

            // Smoothly rotate towards target (10 rad/s)
            float diff = MathHelper.WrapAngle(targetAngle - TurretAngle);
            float step = 10f * dt;
            
            if (Math.Abs(diff) < step) TurretAngle = targetAngle;
            else TurretAngle += Math.Sign(diff) * step;
        }

        public void TakeDamage(float amount)
        {
            Health -= amount;
            if (Health <= 0)
            {
                Health = 0;
                IsActive = false;
                // Note: The GridManager should handle removing this node if it dies
            }
        }

        public void AddExperience(float amount)
        {
            Experience += amount;
            if (Rank < 2 && Experience >= ExperienceToNextRank)
            {
                Rank = 2;
                // At rank 2, node can be specialized
            }
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
                    HeatModifier = 1.0f;
                }
            }

            // Always Regenerate Stamina and Decay Heat
            float soakMultiplier = 1.0f;
            if (HeatModifier > HeatSoakThreshold)
            {
                float t = MathHelper.Clamp((HeatModifier - HeatSoakThreshold) / 3.0f, 0, 1);
                soakMultiplier = MathHelper.Lerp(1.0f, HeatSoakPenalty, t);
            }

            float powerMult = IsPowered ? 1.0f : 0.2f;
            CurrentStamina += (BaseCoolingRate + SynergyBonus) * soakMultiplier * powerMult * dt;
            if (CurrentStamina > MaxStamina) CurrentStamina = MaxStamina;

            HeatModifier -= HeatDecay * dt;
            if (HeatModifier < 1.0f) HeatModifier = 1.0f;

            if (!IsOverheated)
            {
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

        public bool CanFire(float heatMult = 1.0f, float armyMult = 1.0f)
        {
            if (IsOverheated) return false;

            // Apply Suppression Multiplier and Army Scaling
            float effectiveArmyMult = IsNearPowerPlant ? 1.0f : armyMult;
            float suppressedFireRate = FireRate / SuppressionMultiplier;
            float suppressedActionCost = ActionCost * SuppressionMultiplier * effectiveArmyMult;

            if (_fireTimer >= 1.0f / suppressedFireRate)
            {
                float effectiveAlpha = (float)Math.Pow(HeatModifier, HeatExponent);
                float cost = suppressedActionCost * effectiveAlpha * SynergyCostMultiplier;
                
                if (CurrentStamina >= cost)
                {
                    CurrentStamina -= cost;
                    HeatModifier += HeatIncrement * heatMult;
                    _fireTimer = 0;
                    return true;
                }
                else
                {
                    IsOverheated = true;
                    _overheatTimer = OverheatCooldown;
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
            Vector2 toTarget = targetPos - Position;
            float angle = (float)Math.Atan2(toTarget.Y, toTarget.X);
            float facingAngle = GetFacingAngle();

            float diff = MathHelper.WrapAngle(angle - facingAngle);
            return Math.Abs(diff) <= MathHelper.PiOver4;
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
            float angle = TurretAngle;
            Vector2 directionVec = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            
            // 1. Draw "Wall" (Base Rect - Fixed dark color)
            Color wallColor = new Color(42, 42, 42); // Standard Dark Gray
            if (Specialization == NodeSpecialization.Cryo) wallColor = new Color(9, 24, 42);
            else if (Specialization == NodeSpecialization.ArmorPiercing) wallColor = new Color(30, 8, 8);
            
            Color strokeColor = Specialization switch
            {
                NodeSpecialization.Cryo => new Color(77, 217, 255),
                NodeSpecialization.ArmorPiercing => new Color(255, 68, 68),
                _ => new Color(85, 85, 85)
            };

            Rectangle baseRect = new Rectangle((int)Position.X - 18, (int)Position.Y - 18, 36, 36);
            spriteBatch.Draw(pixel, baseRect, wallColor);
            DrawBorder(spriteBatch, pixel, baseRect, 1, strokeColor);

            // 2. Corner turrets
            int detSize = 8;
            spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 18, (int)Position.Y - 18, detSize, detSize), strokeColor * 0.5f);
            spriteBatch.Draw(pixel, new Rectangle((int)Position.X + 18 - detSize, (int)Position.Y - 18, detSize, detSize), strokeColor * 0.5f);
            spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 18, (int)Position.Y + 18 - detSize, detSize, detSize), strokeColor * 0.5f);
            spriteBatch.Draw(pixel, new Rectangle((int)Position.X + 18 - detSize, (int)Position.Y + 18 - detSize, detSize, detSize), strokeColor * 0.5f);

            // 3. Platform
            DrawRadialBar(spriteBatch, pixel, Position, 12f, 1.0f, wallColor * 1.5f);
            DrawBorder(spriteBatch, pixel, new Rectangle((int)Position.X - 10, (int)Position.Y - 10, 20, 20), 1, strokeColor * 0.3f);

            // 4. Barrel
            Vector2 barrelEnd = Position + directionVec * 20;
            Color barrelColor = Specialization switch
            {
                NodeSpecialization.Cryo => new Color(12, 30, 52),
                NodeSpecialization.ArmorPiercing => new Color(32, 8, 8),
                _ => new Color(48, 48, 48)
            };
            DrawLine(spriteBatch, pixel, Position, barrelEnd, 6, barrelColor);
            DrawLine(spriteBatch, pixel, Position, barrelEnd, 2, strokeColor * 0.5f);

            // 5. Specialized Details
            if (Specialization == NodeSpecialization.Cryo)
                DrawRadialBar(spriteBatch, pixel, barrelEnd, 4f, 1.0f, new Color(128, 238, 255));
            else if (Specialization == NodeSpecialization.ArmorPiercing)
            {
                Vector2 perp = new Vector2(-directionVec.Y, directionVec.X);
                DrawLine(spriteBatch, pixel, Position + directionVec * 10 + perp * 6, Position + directionVec * 10 - perp * 6, 2, strokeColor);
                DrawRadialBar(spriteBatch, pixel, barrelEnd, 3f, 1.0f, Color.Red);
            }

            // 6. Particles
            foreach (var p in _particles)
            {
                float alpha = 1.0f - (p.Life / p.MaxLife);
                spriteBatch.Draw(pixel, new Rectangle((int)(Position.X + p.Offset.X), (int)(Position.Y + p.Offset.Y), 4, 4), Color.White * 0.6f * alpha);
            }

            // 7. Stamina Ring (Minimal: Only show when not full)
            float staminaPercent = CurrentStamina / MaxStamina;
            if (staminaPercent < 0.99f || IsOverheated)
            {
                Color barColor = IsOverheated ? Color.Orange : Color.Cyan;
                if (HeatModifier > HeatSoakThreshold) barColor = Color.Lerp(barColor, Color.Yellow, (HeatModifier - HeatSoakThreshold) / 2f);
                DrawRadialBar(spriteBatch, pixel, Position, 19f, staminaPercent, barColor * 0.6f);
            }

            // 8. Indicators
            if (Specialization == NodeSpecialization.None && Rank < 2)
            {
                float xpPercent = Experience / ExperienceToNextRank;
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 15, (int)Position.Y + 22, 30, 4), Color.Black * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 15, (int)Position.Y + 22, (int)(30 * xpPercent), 4), Color.White * 0.8f);
            }
            if (Rank >= 2) spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 4, (int)Position.Y - 26, 8, 8), Color.Gold);
            if (IsOverheated)
            {
                int secondsLeft = (int)Math.Ceiling(_overheatTimer);
                DrawDigit(spriteBatch, pixel, secondsLeft, Position - new Vector2(4, 34), 3, Color.Red);
            }

            if (SynergyBonus > 0) spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 12, (int)Position.Y - 12, 4, 4), Color.Lime);
            if (SynergyRangeBonus > 0) spriteBatch.Draw(pixel, new Rectangle((int)Position.X + 8, (int)Position.Y - 12, 4, 4), Color.Cyan);
            if (SynergyCostMultiplier < 1.0f) spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 12, (int)Position.Y + 8, 4, 4), Color.Gold);
            if (!IsPowered) spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 2, (int)Position.Y - 2, 4, 4), Color.Red);
            if (SuppressionMultiplier > 1.0f) spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 6, (int)Position.Y + 8, 4, 4), Color.Purple);
            if (IsNearPowerPlant) spriteBatch.Draw(pixel, new Rectangle((int)Position.X + 2, (int)Position.Y + 2, 4, 4), Color.Yellow);

            // 9. Health Bar
            if (Health < MaxHealth)
            {
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 15, (int)Position.Y - 20, 30, 4), Color.Black * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 15, (int)Position.Y - 20, (int)(30 * (Health / MaxHealth)), 4), Color.LimeGreen);
            }
        }

        private void DrawRadialBar(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, float percent, Color color)
        {
            const int segments = 60; // Increased for smoothness
            const float step = MathHelper.TwoPi / segments;
            for (int i = 0; i < segments; i++)
            {
                float progress = (float)i / segments;
                if (progress <= percent)
                {
                    float angle = i * step - MathHelper.PiOver2;
                    Vector2 pos = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                    // Draw 1x1 point for minimal look
                    spriteBatch.Draw(pixel, new Rectangle((int)pos.X, (int)pos.Y, 1, 1), color);
                }
            }
        }

        private void DrawDigit(SpriteBatch spriteBatch, Texture2D pixel, int digit, Vector2 pos, int size, Color color)
        {
            bool[,] segments = digit switch
            {
                1 => new[,] { { false, false, true, false, false }, { false, true, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, false, true, false, false }, { false, true, true, true, false } },
                2 => new[,] { { false, true, true, true, false }, { true, false, false, false, true }, { false, false, false, false, true }, { false, false, true, true, false }, { false, true, false, false, false }, { true, false, false, false, false }, { true, true, true, true, true } },
                3 => new[,] { { true, true, true, true, true }, { false, false, false, true, false }, { false, false, true, false, false }, { false, false, false, true, false }, { false, false, false, false, true }, { true, false, false, false, true }, { false, true, true, true, false } },
                _ => new[,] { { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false }, { false, false, false, false, false } }
            };

            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 5; x++)
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

        private void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
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
