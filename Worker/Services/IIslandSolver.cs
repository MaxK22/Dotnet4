using Contracts;

namespace Worker.Services;

public interface IIslandSolver
{
    Task<IslandResult> EvolveAsync(
        IslandTask task,
        CancellationToken cancellationToken);
}