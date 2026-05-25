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
        public float Cost = 25f, Range = 120f, FireRate = 1.0f;
        private float _fireTimer = 0f;
        public float MaxStamina = 100f, CurrentStamina, BaseCoolingRate = 15f;
        public float MaxHealth = 100f, Health;
        public float SynergyBonus = 0f, SynergyRangeBonus = 0f, SynergyCostMultiplier = 1.0f;
        public float ActionCost = 10f, HeatModifier = 1.0f, HeatIncrement = 0.08f, HeatDecay = 0.6f, HeatExponent = 1.3f, HeatSoakThreshold = 3.0f, HeatSoakPenalty = 0.5f;
        public bool IsOverheated { get; private set; }
        public float OverheatCooldown = 3.0f;
        private float _overheatTimer = 0f;
        public int Rank = 1;
        public float Experience = 0f, ExperienceToNextRank = 100f;
        public NodeSpecialization Specialization = NodeSpecialization.None;
        public bool IsPowered = true, IsNearPowerPlant = false;
        public float SuppressionMultiplier = 1.0f;
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
            Position = new Vector2(gridPos.X * GridManager.CellSize + GridManager.CellSize / 2, gridPos.Y * GridManager.CellSize + GridManager.CellSize / 2);
            Facing = facing; TurretAngle = GetFacingAngle(); CurrentStamina = MaxStamina; HeatModifier = 1.0f;
            IsOverheated = false; _overheatTimer = 0f; _fireTimer = 0f; _particles.Clear();
            SynergyBonus = 0; SynergyRangeBonus = 0; SynergyCostMultiplier = 1.0f; IsNearPowerPlant = false;
            Rank = 1; Experience = 0; Specialization = NodeSpecialization.None; Health = MaxHealth; IsActive = true;
        }

        public void AimAt(Enemy target, float dt) {
            float targetAngle = GetFacingAngle();
            if (target != null) { Vector2 dir = target.Position - Position; targetAngle = (float)Math.Atan2(dir.Y, dir.X); }
            float diff = MathHelper.WrapAngle(targetAngle - TurretAngle);
            float step = 10f * dt;
            if (Math.Abs(diff) < step) TurretAngle = targetAngle;
            else TurretAngle += Math.Sign(diff) * step;
        }

        public void TakeDamage(float amount) { Health -= amount; if (Health <= 0) { Health = 0; IsActive = false; } }
        public void AddExperience(float amount) { Experience += amount; if (Rank < 2 && Experience >= ExperienceToNextRank) Rank = 2; }

        public override void Update(GameTime gameTime) {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (IsOverheated) {
                _overheatTimer -= dt; _particleTimer += dt;
                if (_particleTimer >= 0.15f) {
                    _particleTimer = 0; _particles.Add(new SteamParticle { Offset = new Vector2(_rng.Next(-10, 11), _rng.Next(-10, 11)), Life = 0, MaxLife = 0.8f + (float)_rng.NextDouble() * 0.4f, Speed = 20f + (float)_rng.NextDouble() * 20f });
                }
                if (_overheatTimer <= 0) { IsOverheated = false; HeatModifier = 1.0f; }
            }
            float soak = 1.0f; if (HeatModifier > HeatSoakThreshold) { float t = MathHelper.Clamp((HeatModifier - HeatSoakThreshold) / 3.0f, 0, 1); soak = MathHelper.Lerp(1.0f, HeatSoakPenalty, t); }
            float powerMult = IsPowered ? 1.0f : 0.2f;
            CurrentStamina += (BaseCoolingRate + SynergyBonus) * soak * powerMult * dt;
            if (CurrentStamina > MaxStamina) CurrentStamina = MaxStamina;
            HeatModifier -= HeatDecay * dt; if (HeatModifier < 1.0f) HeatModifier = 1.0f;
            if (!IsOverheated) _fireTimer += dt;
            for (int i = _particles.Count - 1; i >= 0; i--) { var p = _particles[i]; p.Life += dt; p.Offset.Y -= p.Speed * dt; if (p.Life >= p.MaxLife) _particles.RemoveAt(i); else _particles[i] = p; }
        }

        public bool CanFire(float hMult = 1.0f, float aMult = 1.0f) {
            if (IsOverheated) return false;
            float eArmyMult = IsNearPowerPlant ? 1.0f : aMult;
            float sFireRate = FireRate / SuppressionMultiplier;
            float sCost = ActionCost * SuppressionMultiplier * eArmyMult;
            if (_fireTimer >= 1.0f / sFireRate) {
                float cost = sCost * (float)Math.Pow(HeatModifier, HeatExponent) * SynergyCostMultiplier;
                if (CurrentStamina >= cost) { CurrentStamina -= cost; HeatModifier += HeatIncrement * hMult; _fireTimer = 0; return true; }
                else { IsOverheated = true; _overheatTimer = OverheatCooldown; return false; }
            }
            return false;
        }

        public bool IsExpandedRange = false;

        public Enemy FindTarget(List<Enemy> enemies) {
            var pattern = GetAttackPattern();
            Enemy closest = null; float minD = float.MaxValue;
            foreach (var e in enemies) {
                if (!e.IsActive) continue;
                Point gp = e.GetCurrentGridPosition();
                bool inPattern = false;
                foreach (var p in pattern) if (p == gp) { inPattern = true; break; }
                if (inPattern) {
                    float d = Vector2.Distance(Position, e.Position);
                    if (d < minD) { minD = d; closest = e; }
                }
            }
            return closest;
        }

        public List<Point> GetAttackPattern() {
            List<Point> pts = new();
            Point fv = Facing switch { Direction.Up => new Point(0, -1), Direction.Down => new Point(0, 1), Direction.Left => new Point(-1, 0), Direction.Right => new Point(1, 0), _ => new Point(0, 0) };
            Point sv = (Facing == Direction.Up || Facing == Direction.Down) ? new Point(1, 0) : new Point(0, 1);
            
            // Rows 0, 1 (Total 2 rows including unit's row)
            for (int i = 0; i <= 1; i++) {
                Point basePt = new Point(GridPosition.X + fv.X * i, GridPosition.Y + fv.Y * i);
                pts.Add(basePt);
                pts.Add(new Point(basePt.X + sv.X, basePt.Y + sv.Y));
                pts.Add(new Point(basePt.X - sv.X, basePt.Y - sv.Y));
            }
            
            // Row 2 (Extension)
            Point extPt = new Point(GridPosition.X + fv.X * 2, GridPosition.Y + fv.Y * 2);
            if (IsExpandedRange) {
                pts.Add(extPt);
                pts.Add(new Point(extPt.X + sv.X, extPt.Y + sv.Y));
                pts.Add(new Point(extPt.X - sv.X, extPt.Y - sv.Y));
            } else {
                pts.Add(extPt); // 1-wide ahead extension
            }
            return pts;
        }

        private float GetFacingAngle() => Facing switch { Direction.Up => 0, Direction.Right => MathHelper.PiOver2, Direction.Down => MathHelper.Pi, Direction.Left => -MathHelper.PiOver2, _ => 0 };

        public void DrawExtras(SpriteBatch sb, Texture2D px) {
            foreach (var p in _particles) sb.Draw(px, new Rectangle((int)(Position.X + p.Offset.X), (int)(Position.Y + p.Offset.Y), 4, 4), Color.White * 0.6f * (1.0f - (p.Life / p.MaxLife)));
            float sPct = CurrentStamina / MaxStamina;
            if (sPct < 0.99f || IsOverheated) {
                Color col = IsOverheated ? Color.Orange : Color.Cyan; if (HeatModifier > HeatSoakThreshold) col = Color.Lerp(col, Color.Yellow, (HeatModifier - HeatSoakThreshold) / 2f);
                DrawRadial(sb, px, Position, 22f, sPct, col * 0.6f);
            }
            if (Rank >= 2) sb.Draw(px, new Rectangle((int)Position.X - 4, (int)Position.Y - 26, 8, 8), Color.Gold);
            if (IsOverheated) DrawDigit(sb, px, (int)Math.Ceiling(_overheatTimer), Position - new Vector2(4, 34), 3, Color.Red);
            if (SynergyBonus > 0) sb.Draw(px, new Rectangle((int)Position.X - 12, (int)Position.Y - 12, 4, 4), Color.Lime);
            if (!IsPowered) sb.Draw(px, new Rectangle((int)Position.X - 2, (int)Position.Y - 2, 4, 4), Color.Red);
            if (IsNearPowerPlant) sb.Draw(px, new Rectangle((int)Position.X + 8, (int)Position.Y + 8, 4, 4), Color.Yellow);
            if (SuppressionMultiplier > 1.0f) sb.Draw(px, new Rectangle((int)Position.X - 6, (int)Position.Y + 8, 4, 4), Color.Purple);
            if (Health < MaxHealth) { sb.Draw(px, new Rectangle((int)Position.X - 15, (int)Position.Y - 20, 30, 4), Color.Black * 0.5f); sb.Draw(px, new Rectangle((int)Position.X - 15, (int)Position.Y - 20, (int)(30 * (Health / MaxHealth)), 4), Color.LimeGreen); }
            if (Specialization == NodeSpecialization.None && Rank < 2) { float xp = Experience / ExperienceToNextRank; sb.Draw(px, new Rectangle((int)Position.X - 15, (int)Position.Y + 22, 30, 4), Color.Black * 0.5f); sb.Draw(px, new Rectangle((int)Position.X - 15, (int)Position.Y + 22, (int)(30 * xp), 4), Color.White * 0.8f); }
        }

        public override void Draw(SpriteBatch sb, Texture2D px) { }

        private void DrawRadial(SpriteBatch sb, Texture2D px, Vector2 center, float radius, float percent, Color col) {
            const int segs = 72; const float step = MathHelper.TwoPi / segs;
            for (int i = 0; i < segs; i++) if ((float)i / segs <= percent) { float a = i * step - MathHelper.PiOver2; Vector2 p = center + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * radius; sb.Draw(px, new Rectangle((int)p.X, (int)p.Y, 2, 2), col); }
        }

        private void DrawDigit(SpriteBatch sb, Texture2D px, int d, Vector2 pos, int sz, Color col) {
            bool[,] segs = d switch { 1 => new[,] { { false, true, false }, { false, true, false }, { false, true, false }, { false, true, false }, { false, true, false } }, 2 => new[,] { { true, true, true }, { false, false, true }, { true, true, true }, { true, false, false }, { true, true, true } }, 3 => new[,] { { true, true, true }, { false, false, true }, { true, true, true }, { false, false, true }, { true, true, true } }, _ => new[,] { { true, true, true }, { true, false, true }, { true, false, true }, { true, false, true }, { true, true, true } } };
            for (int y = 0; y < 5; y++) for (int x = 0; x < 3; x++) if (segs[y, x]) sb.Draw(px, new Rectangle((int)pos.X + x * sz, (int)pos.Y + y * sz, sz, sz), col);
        }
    }
    public struct SteamParticle { public Vector2 Offset; public float Life, MaxLife, Speed; }
}
