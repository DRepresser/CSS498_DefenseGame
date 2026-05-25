using Microsoft.Xna.Framework;

namespace TacticalDefenseGame.Managers
{
    public class ResourceManager
    {
        public float Energy { get; private set; }
        public float CoreHealth { get; private set; } = 100f;
        public float EnergyRegenRate { get; set; } = 5f;

        public ResourceManager(float startingEnergy)
        {
            Energy = startingEnergy;
        }

        public void Update(GameTime gameTime)
        {
            Energy += EnergyRegenRate * (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public bool TrySpendEnergy(float amount)
        {
            if (Energy >= amount)
            {
                Energy -= amount;
                return true;
            }
            return false;
        }

        public void TakeDamage(float amount)
        {
            CoreHealth -= amount;
            if (CoreHealth < 0) CoreHealth = 0;
        }
    }
}
