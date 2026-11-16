using Contracts;
using Worker.Services;

namespace Worker.Tests;

public class TestIslandSolver : IIslandSolver
{
    public int CallCount { get; private set; }

    public Task<IslandResult> EvolveAsync(
        IslandTask task,
        CancellationToken cancellationToken)
    {
        CallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        var result = new IslandResult
        {
            TaskId = task.TaskId,
            IslandId = task.IslandId,
            Population = task.Population,
            BestDistance = 100.5,
            BestRoute = [0, 1, 2]
        };

        return Task.FromResult(result);
    }
}