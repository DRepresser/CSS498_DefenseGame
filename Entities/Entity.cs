using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TacticalDefenseGame.Entities
{
    public abstract class Entity
    {
        public Vector2 Position;
        public Color Color;
        public bool IsActive = true;

        public abstract void Update(GameTime gameTime);
        public abstract void Draw(SpriteBatch spriteBatch, Texture2D pixel);
    }
}
