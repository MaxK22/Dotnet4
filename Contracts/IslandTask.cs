namespace Contracts;

public class IslandTask
{
    public string TaskId { get; set; } = "";

    public int IslandId { get; set; }

    public int Generations { get; set; }

    public List<IndividualDto> Population { get; set; } = [];

    public CityDto[] Cities { get; set; } = [];

    public double MutateRate { get; set; }

    public double CrossoverRate { get; set; }

    public double EliteRate { get; set; }

    public string MutationType { get; set; } = "Swap";
}