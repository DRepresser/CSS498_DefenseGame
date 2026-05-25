using TacticalDefenseGame.Entities;

namespace TacticalDefenseGame.Models
{
    public class Cell
    {
        public int X { get; }
        public int Y { get; }
        public bool IsWalkable { get; set; } = true;
        public Node OccupyingNode { get; set; } = null;
        public CellType Type { get; set; } = CellType.Standard;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
