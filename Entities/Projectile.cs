using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace TacticalDefenseGame.Entities
{
    public class Projectile : Entity
    {
        public float Speed = 300f;
        public float Damage = 10f;
        public Enemy Target;

        public Projectile()
        {
            Color = Color.Yellow;
            IsActive = false;
        }

        public void Initialize(Vector2 position, Enemy target)
        {
            Position = position;
            Target = target;
            IsActive = true;
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
                Target.Health -= Damage;
                if (Target.Health <= 0) Target.IsActive = false;
                IsActive = false;
                return;
            }

            dir.Normalize();
            Position += dir * Speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            spriteBatch.Draw(pixel, new Rectangle((int)Position.X - 3, (int)Position.Y - 3, 6, 6), Color);
        }
    }
}
