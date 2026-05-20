namespace Attrition.API.Models;

public class Level
{
    public int LevelNumber { get; set; }
    public int ExpRequired { get; set; }
    public int HpGrowth { get; set; } = 0;
    public int AdGrowth { get; set; } = 0;
    public int ApGrowth { get; set; } = 0;
    public int DefGrowth { get; set; } = 0;
    public int ResGrowth { get; set; } = 0;
    public float SpeedGrowth { get; set; } = 0;
}