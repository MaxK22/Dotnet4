using Contracts;
using Controller.Models;
using Controller.Services;

namespace Controller;

public class OptimizationController
{
    private readonly IWorkerManager _workerManager;
    private readonly IIslandTaskDistributor _taskDistributor;
    private readonly IIslandCoordinator _islandCoordinator;
    private readonly IslandMigrator _islandMigrator;

    public OptimizationController(
        IWorkerManager workerManager,
        IIslandTaskDistributor taskDistributor,
        IIslandCoordinator islandCoordinator,
        IslandMigrator islandMigrator)
    {
        _workerManager = workerManager;
        _taskDistributor = taskDistributor;
        _islandCoordinator = islandCoordinator;
        _islandMigrator = islandMigrator;
    }


    public IReadOnlyList<IslandResult> Migrate(
        IReadOnlyList<IslandResult> islands,
        int migrationCount)
    {
        return _islandMigrator.Migrate(
            islands,
            migrationCount);
    }



    public async Task<IReadOnlyList<IslandResult>> RunEpochAsync(
        IReadOnlyList<IndividualDto> population,
        CityDto[] cities,
        OptimizationOptions options,
        string taskId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        await _workerManager.RefreshAvailabilityAsync(
            cancellationToken);

        var workers = _workerManager.GetAvailableWorkers();

        if (workers.Count == 0)
        {
            throw new InvalidOperationException(
                "No available workers.");
        }

        var tasks = _taskDistributor.CreateTasks(
            population,
            cities,
            workers,
            options.GenerationsPerEpoch,
            options.MutateRate,
            options.CrossoverRate,
            options.EliteRate,
            options.MutationType,
            taskId);

        return await _islandCoordinator.ExecuteGenerationAsync(
            tasks,
            cancellationToken);
    }




    public async Task<OptimizationResult> RunAsync(
        IReadOnlyList<IndividualDto> initialPopulation,
        CityDto[] cities,
        OptimizationOptions options,
        string taskId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(initialPopulation);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(options);

        if (initialPopulation.Count == 0)
            throw new ArgumentException("Population cannot be empty.");

        if (options.Epochs <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.Epochs));

        if (options.GenerationsPerEpoch <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(options.GenerationsPerEpoch));

        if (options.MigrationCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(options.MigrationCount));

        cancellationToken.ThrowIfCancellationRequested();

        // Проверяем доступность Worker.
        await _workerManager.RefreshAvailabilityAsync(
            cancellationToken);

        var workers = _workerManager.GetAvailableWorkers()
            .OrderBy(w => w.Id)
            .ToList();

        if (workers.Count == 0)
            throw new InvalidOperationException("No available workers.");

        // Распределяем начальную популяцию.
        var initialTasks = _taskDistributor.CreateTasks(
            initialPopulation,
            cities,
            workers,
            options.GenerationsPerEpoch,
            options.MutateRate,
            options.CrossoverRate,
            options.EliteRate,
            options.MutationType,
            taskId);

        // Сохраняем популяции отдельно для каждого острова.
        var islands = initialTasks.ToList();

        double bestDistance = double.PositiveInfinity;
        int[] bestRoute = [];

        var history = new List<double>();

        for (int epoch = 0; epoch < options.Epochs; epoch++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Выполняем вычисления на всех Worker.
            var epochTasks = islands;

            var results = await _islandCoordinator.ExecuteGenerationAsync(
                epochTasks,
                cancellationToken);

            // Сохраняем результаты каждого острова.
            islands = results.Select(result => new IslandTask
            {
                TaskId = result.TaskId,
                IslandId = result.IslandId,
                Generations = options.GenerationsPerEpoch,
                Population = result.Population,
                Cities = cities.ToArray(),
                MutateRate = options.MutateRate,
                CrossoverRate = options.CrossoverRate,
                EliteRate = options.EliteRate,
                MutationType = options.MutationType
            }).ToList();

            // Находим лучшее решение текущей эпохи.
            var epochBest = results
                .OrderBy(result => result.BestDistance)
                .First();

            if (epochBest.BestDistance < bestDistance)
            {
                bestDistance = epochBest.BestDistance;
                bestRoute = (int[])epochBest.BestRoute.Clone();
            }

            history.Add(bestDistance);

            // Миграция между островами, кроме последней эпохи.
            if (epoch < options.Epochs - 1)
            {
                var migrated = _islandMigrator.Migrate(
                    results,
                    options.MigrationCount);

                islands = migrated.Select(result => new IslandTask
                {
                    TaskId = result.TaskId,
                    IslandId = result.IslandId,
                    Generations = options.GenerationsPerEpoch,
                    Population = result.Population,
                    Cities = cities.ToArray(),
                    MutateRate = options.MutateRate,
                    CrossoverRate = options.CrossoverRate,
                    EliteRate = options.EliteRate,
                    MutationType = options.MutationType
                }).ToList();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var finalPopulation = islands
            .SelectMany(island => island.Population)
            .ToList();

        return new OptimizationResult
        {
            TaskId = taskId,
            BestDistance = bestDistance,
            BestRoute = bestRoute,
            Population = finalPopulation,
            BestDistanceHistory = history,
            CompletedEpochs = history.Count
        };
    }


}
