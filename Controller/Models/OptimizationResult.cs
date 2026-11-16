
using Contracts;

namespace Controller.Models;

public class OptimizationResult
{
    public string TaskId { get; set; } = "";

    public double BestDistance { get; set; }

    public int[] BestRoute { get; set; } = [];

    public List<IndividualDto> Population { get; set; } = [];

    public List<double> BestDistanceHistory { get; set; } = [];

    public int CompletedEpochs { get; set; }
}
