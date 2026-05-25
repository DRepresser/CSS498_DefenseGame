using Microsoft.Xna.Framework;

namespace TacticalDefenseGame.Managers
{
    public class ResourceManager
    {
        public float Energy { get; private set; }
        public float Scrap { get; private set; } = 0f;
        public float CoreHealth { get; private set; } = 100f;
        public float EnergyRegenRate { get; set; } = 5f;

        // Ability State
        public float OverclockTimer = 0f;
        public float ShieldTimer = 0f;
        public float[] AbilityCooldowns = new float[3]; // [0] Overclock, [1] RepairGrid, [2] Shield

        public ResourceManager(float startingEnergy)
        {
            Energy = startingEnergy;
        }

        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Energy += EnergyRegenRate * dt;
            if (OverclockTimer > 0) OverclockTimer -= dt;
            if (ShieldTimer > 0) ShieldTimer -= dt;
            for (int i = 0; i < AbilityCooldowns.Length; i++) if (AbilityCooldowns[i] > 0) AbilityCooldowns[i] -= dt;
        }

        public bool TrySpendEnergy(float amount) { if (Energy >= amount) { Energy -= amount; return true; } return false; }
        public void AddScrap(float amount) { Scrap += amount; }
        public bool TrySpendScrap(float amount) { if (Scrap >= amount) { Scrap -= amount; return true; } return false; }

        public bool UseAbility(int index) {
            if (AbilityCooldowns[index] > 0) return false;
            switch(index) {
                case 0: // Overclock
                    if (TrySpendEnergy(50f)) { OverclockTimer = 8f; AbilityCooldowns[0] = 30f; return true; } break;
                case 1: // Repair Grid
                    if (TrySpendScrap(30f)) { AbilityCooldowns[1] = 45f; return true; } break;
                case 2: // Shield
                    if (TrySpendEnergy(60f)) { ShieldTimer = 5f; AbilityCooldowns[2] = 60f; return true; } break;
            }
            return false;
        }

        public void TakeDamage(float amount)
        {
            if (ShieldTimer > 0) return;
            CoreHealth -= amount;
            if (CoreHealth < 0) CoreHealth = 0;
        }
    }
}
