
using Controller.Models;

namespace Controller.Services;

public interface IWorkerManager
{
    void AddWorker(WorkerNode worker);
    bool RemoveWorker(int id);


    IReadOnlyList<WorkerNode> GetAllWorkers();

    IReadOnlyList<WorkerNode> GetAvailableWorkers();

    Task RefreshAvailabilityAsync(
        CancellationToken cancellationToken);
}
