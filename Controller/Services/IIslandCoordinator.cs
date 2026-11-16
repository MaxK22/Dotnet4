
using Contracts;

namespace Controller.Services;

public interface IIslandCoordinator
{
    Task<IReadOnlyList<IslandResult>> ExecuteGenerationAsync(
        IReadOnlyList<IslandTask> tasks,
        CancellationToken cancellationToken);
}
