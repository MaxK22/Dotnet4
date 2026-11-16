
using Controller.Models;

namespace Controller.Services;

public class WorkerManager : IWorkerManager
{
    private readonly IWorkerClient _workerClient;
    private readonly List<WorkerNode> _workers = [];
    private readonly object _lock = new();

    public WorkerManager(IWorkerClient workerClient)
    {
        _workerClient = workerClient;
    }

    public void AddWorker(WorkerNode worker)
    {
        ArgumentNullException.ThrowIfNull(worker);

        if (worker.Id <= 0)
            throw new ArgumentOutOfRangeException(nameof(worker.Id));

        if (string.IsNullOrWhiteSpace(worker.BaseUrl))
            throw new ArgumentException("Worker URL cannot be empty.");

        lock (_lock)
        {
            if (_workers.Any(w => w.Id == worker.Id))
            {
                throw new InvalidOperationException(
                    $"Worker with ID {worker.Id} already exists.");
            }

            _workers.Add(worker);
        }
    }

    public bool RemoveWorker(int id)
    {
        lock (_lock)
        {
            var worker = _workers.FirstOrDefault(w => w.Id == id);

            if (worker == null)
                return false;

            _workers.Remove(worker);
            return true;
        }
    }


    public IReadOnlyList<WorkerNode> GetAllWorkers()
    {
        lock (_lock)
        {
            return _workers.ToArray();
        }
    }

    public IReadOnlyList<WorkerNode> GetAvailableWorkers()
    {
        lock (_lock)
        {
            return _workers
                .Where(w => w.IsAvailable)
                .ToArray();
        }
    }

    
    public async Task RefreshAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        var workers = GetAllWorkers();

        var checks = workers.Select(async worker =>
        {
            bool isAvailable = await _workerClient.CheckHealthAsync(
                worker,
                cancellationToken);

            lock (_lock)
            {
                 if (_workers.Contains(worker))
                    worker.IsAvailable = isAvailable;
            }
        });

        await Task.WhenAll(checks);
    }

}
