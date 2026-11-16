namespace Contracts;

public class IslandResult
{
    public string TaskId { get; set; } = "";

    public int IslandId { get; set; }

    public List<IndividualDto> Population { get; set; } = [];

    public double BestDistance { get; set; }

    public int[] BestRoute { get; set; } = [];
}