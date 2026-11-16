
using Contracts;
using Controller.Services;

namespace Controller.Services;

public class IslandCoordinator : IIslandCoordinator
{
    private readonly IWorkerManager _workerManager;
    private readonly IWorkerClient _workerClient;

    public IslandCoordinator(
        IWorkerManager workerManager,
        IWorkerClient workerClient)
    {
        _workerManager = workerManager;
        _workerClient = workerClient;
    }

    
    public async Task<IReadOnlyList<IslandResult>>
        ExecuteGenerationAsync(
            IReadOnlyList<IslandTask> tasks,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var workers = _workerManager.GetAvailableWorkers();

        if (workers.Count == 0)
        {
            throw new InvalidOperationException(
                "No available workers.");
        }

        var workersById = workers.ToDictionary(w => w.Id);

        var executions = tasks.Select(task =>
        {
            if (!workersById.TryGetValue(task.IslandId, out var worker))
            {
                throw new InvalidOperationException(
                    $"Worker with ID {task.IslandId} is not available.");
            }

            return _workerClient.ExecuteIslandAsync(
                worker,
                task,
                cancellationToken);
        });

        return await Task.WhenAll(executions);
    }

}
