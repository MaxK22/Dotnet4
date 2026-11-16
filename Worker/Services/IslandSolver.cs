using Contracts;
using ParallelGenetic;

namespace Worker.Services;

public class IslandSolver : IIslandSolver
{
    public async Task<IslandResult> EvolveAsync(
        IslandTask task,
        CancellationToken cancellationToken)
    {
        // 1. Проверка входных данных
        ValidateTask(task);

        cancellationToken.ThrowIfCancellationRequested();

        // 2. Преобразование MutationType из строки в enum Core
        if (!Enum.TryParse<MutationType>(
                task.MutationType,
                ignoreCase: true,
                out var mutationType) ||
            !Enum.IsDefined(typeof(MutationType), mutationType))
        {
            throw new ArgumentException(
                $"Unknown mutation type: {task.MutationType}");
        }

        // 3. Создание экземпляра генетического алгоритма.
        // Один Worker выполняет один остров, поэтому numTask = 1.
        var algorithm = new GeneticAlgo(
            type: mutationType,
            n: task.Cities.Length,
            populationMinSize: task.Population.Count,
            mutateRate: task.MutateRate,
            crossoverRate: task.CrossoverRate,
            eliteRate: task.EliteRate,
            numTask: 1,
            toGenerateMatrix: false
        );

        // 4. Передача координат городов из Contracts в Core
        var cities = task.Cities
            .Select(city => new GeneticAlgo.Vector2D(
                city.X,
                city.Y))
            .ToArray();

        algorithm.updatePoints(cities);
        algorithm.updateDistanceMatrix();

        // 5. Преобразование популяции из DTO в Core
        var population = task.Population
            .Select(individual => new GeneticAlgo.Individual
            {
                Route = (int[])individual.Route.Clone(),

                // Пересчитываем расстояние для текущей матрицы.
                Distance = 0
            })
            .ToList();

        // 6. Выполнение заданного количества поколений
        var evolvedPopulation = await Task.Run(
            () => algorithm.RunEvolveTask(
                population,
                task.Generations,
                cancellationToken),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // 7. Преобразование результата Core обратно в DTO
        var resultPopulation = evolvedPopulation
            .Select(individual => new IndividualDto
            {
                Route = (int[])individual.Route.Clone(),
                Distance = individual.Distance
            })
            .ToList();

        // 8. Формирование результата
        return new IslandResult
        {
            TaskId = task.TaskId,
            IslandId = task.IslandId,

            Population = resultPopulation,

            BestDistance = algorithm.BestDistance,

            BestRoute = algorithm.BestRoute is not null
                ? (int[])algorithm.BestRoute.Clone()
                : Array.Empty<int>()
        };
    }

    private static void ValidateTask(IslandTask task)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));

        if (string.IsNullOrWhiteSpace(task.TaskId))
            throw new ArgumentException("TaskId is required.");

        if (task.IslandId < 0)
            throw new ArgumentException("IslandId cannot be negative.");

        if (task.Generations <= 0)
            throw new ArgumentException(
                "Generations must be greater than zero.");

        if (task.Cities is null || task.Cities.Length < 2)
            throw new ArgumentException(
                "At least two cities are required.");

        if (task.Population is null || task.Population.Count == 0)
            throw new ArgumentException(
                "Population cannot be empty.");

        if (!IsValidRate(task.MutateRate) ||
            !IsValidRate(task.CrossoverRate) ||
            !IsValidRate(task.EliteRate))
        {
            throw new ArgumentException(
                "Mutation, crossover and elite rates must be between 0 and 1.");
        }

        int cityCount = task.Cities.Length;

        foreach (var individual in task.Population)
        {
            if (individual.Route is null ||
                individual.Route.Length != cityCount)
            {
                throw new ArgumentException(
                    "Every route must contain exactly one entry per city.");
            }

            if (individual.Route.Any(city =>
                    city < 0 || city >= cityCount))
            {
                throw new ArgumentException(
                    "Route contains an invalid city index.");
            }

            if (individual.Route.Distinct().Count() != cityCount)
            {
                throw new ArgumentException(
                    "Route must visit every city exactly once.");
            }
        }
    }

    private static bool IsValidRate(double value)
    {
        return double.IsFinite(value) &&
               value >= 0.0 &&
               value <= 1.0;
    }
}