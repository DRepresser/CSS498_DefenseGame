namespace TacticalDefenseGame.Models
{
    public enum GameState { Gameplay, GameOver, Victory }
    public enum Direction { Up, Right, Down, Left }
    public enum EnemyType { Standard, Speedster, Tank, Phaser, Support, Striker }
    public enum NodeSpecialization { None, Cryo, ArmorPiercing }
    public enum CellType { Standard, Volcanic, Corrosive, Obstacle }
}
