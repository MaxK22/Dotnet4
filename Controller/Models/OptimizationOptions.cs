
namespace Controller.Models;

public class OptimizationOptions
{

    public int Epochs { get; set; } = 10;

    public int MigrationCount { get; set; } = 5;

    public int GenerationsPerEpoch { get; set; } = 100;

    public double MutateRate { get; set; } = 0.1;

    public double CrossoverRate { get; set; } = 0.9;

    public double EliteRate { get; set; } = 0.01;

    public string MutationType { get; set; } = "Swap";
}
