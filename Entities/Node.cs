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
        public float SynergyBonus = 0f; // beta_synergy
        public float ActionCost = 15f; // C_action
        public float HeatModifier = 1.0f; // alpha
        public float HeatIncrement = 0.2f; // Increases alpha per shot
        public float HeatDecay = 0.5f; // Decays alpha over time
        
        public bool IsOverheated { get; private set; }
        public float OverheatCooldown = 3.0f; // T_cooldown
        private float _overheatTimer = 0f;

        public Direction Facing = Direction.Right;
        public Point GridPosition;

        public Node(Point gridPos)
        {
            GridPosition = gridPos;
            Position = new Vector2(
                gridPos.X * GridManager.CellSize + GridManager.CellSize / 2,
                gridPos.Y * GridManager.CellSize + GridManager.CellSize / 2
            );
            Color = Color.White;
            CurrentStamina = MaxStamina;
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (IsOverheated)
            {
                _overheatTimer -= dt;
                if (_overheatTimer <= 0)
                {
                    IsOverheated = false;
                    CurrentStamina = MaxStamina * 0.2f; // Return with some stamina
                }
            }
            else
            {
                // Regenerate Stamina: (R_base + beta_synergy) * t
                CurrentStamina += (BaseCoolingRate + SynergyBonus) * dt;
                if (CurrentStamina > MaxStamina) CurrentStamina = MaxStamina;

                // Decay Heat Modifier
                HeatModifier -= HeatDecay * dt;
                if (HeatModifier < 1.0f) HeatModifier = 1.0f;

                _fireTimer += dt;
            }
        }

        public bool CanFire()
        {
            if (IsOverheated) return false;

            if (_fireTimer >= 1.0f / FireRate)
            {
                // Calculate shot cost: C_action * alpha
                float cost = ActionCost * HeatModifier;
                
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
                    HeatModifier = 1.0f;
                    return false;
                }
            }
            return false;
        }

        public Enemy FindTarget(List<Enemy> enemies)
        {
            Enemy closest = null;
            float minDistance = Range;

            foreach (var enemy in enemies)
            {
                if (!enemy.IsActive) continue;

                float dist = Vector2.Distance(Position, enemy.Position);
                if (dist <= Range && IsInSector(enemy.Position))
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
            // Draw Node base
            Rectangle rect = new Rectangle((int)Position.X - 15, (int)Position.Y - 15, 30, 30);
            Color nodeColor = IsOverheated ? Color.Red : Color.DarkGray;
            spriteBatch.Draw(pixel, rect, nodeColor);

            // Draw "Turret" indicator for facing
            Vector2 directionVec = new Vector2((float)Math.Cos(GetFacingAngle()), (float)Math.Sin(GetFacingAngle()));
            Vector2 turretEnd = Position + directionVec * 15;
            
            DrawLine(spriteBatch, pixel, Position, turretEnd, 3, IsOverheated ? Color.Gray : Color.Yellow);

            // Draw Stamina Bar
            int barWidth = 30;
            int barHeight = 4;
            Rectangle barBg = new Rectangle((int)Position.X - barWidth / 2, (int)Position.Y + 18, barWidth, barHeight);
            spriteBatch.Draw(pixel, barBg, Color.Black * 0.5f);

            float staminaPercent = CurrentStamina / MaxStamina;
            Rectangle barFill = new Rectangle(barBg.X, barBg.Y, (int)(barWidth * staminaPercent), barHeight);
            spriteBatch.Draw(pixel, barFill, IsOverheated ? Color.Orange : Color.Cyan);
            
            // Draw Synergy Indicator (small dot if bonus > 0)
            if (SynergyBonus > 0)
            {
                spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 2, (int)Position.Y - 2, 4, 4), Color.Lime);
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
}
