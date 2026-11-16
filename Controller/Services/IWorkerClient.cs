
using Contracts;
using Controller.Models;

namespace Controller.Services;

public interface IWorkerClient
{
    Task<bool> CheckHealthAsync(
        WorkerNode worker,
        CancellationToken cancellationToken);

    Task<IslandResult> ExecuteIslandAsync(
        WorkerNode worker,
        IslandTask task,
        CancellationToken cancellationToken);
}
