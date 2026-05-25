using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TacticalDefenseGame.Models;
namespace TacticalDefenseGame.Entities
{
    public class Projectile : Entity
    {
        public float Speed = 300f;
        public float Damage = 10f;
        public Enemy Target;
        public NodeSpecialization Specialization = NodeSpecialization.None;

        public Projectile()
        {
            Color = Color.Yellow;
            IsActive = false;
        }

        public void Initialize(Vector2 position, Enemy target, NodeSpecialization spec)
        {
            Position = position;
            Target = target;
            Specialization = spec;
            IsActive = true;

            switch (Specialization)
            {
                case NodeSpecialization.Cryo:
                    Color = Color.LightBlue;
                    Damage = 5f; // Lower damage for CC
                    break;
                case NodeSpecialization.ArmorPiercing:
                    Color = Color.OrangeRed;
                    Damage = 25f; // High damage
                    break;
                default:
                    Color = Color.Yellow;
                    Damage = 10f;
                    break;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (Target == null || !Target.IsActive)
            {
                IsActive = false;
                return;
            }

            Vector2 dir = Target.Position - Position;
            if (dir.Length() < 5)
            {
                ApplyEffect();
                IsActive = false;
                return;
            }

            dir.Normalize();
            Position += dir * Speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        private void ApplyEffect()
        {
            Target.Health -= Damage;
            
            if (Specialization == NodeSpecialization.Cryo)
            {
                Target.ApplySlow(0.5f, 2.0f); // 50% slow for 2 seconds
            }

            if (Target.Health <= 0) Target.IsActive = false;
        }

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 3, (int)Position.Y - 3, 6, 6), Color);
        }
    }
}
