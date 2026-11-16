
using Contracts;
using Controller.Models;

namespace Controller.Services;

public class IslandTaskDistributor : IIslandTaskDistributor
{
    public IReadOnlyList<IslandTask> CreateTasks(
        IReadOnlyList<IndividualDto> population,
        CityDto[] cities,
        IReadOnlyList<WorkerNode> workers,
        int generations,
        double mutateRate,
        double crossoverRate,
        double eliteRate,
        string mutationType,
        string taskId)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(workers);

        if (workers.Count == 0)
        {
            throw new ArgumentException(
                "At least one worker is required.",
                nameof(workers));
        }

        if (population.Count < workers.Count)
        {
            throw new ArgumentException(
                "Population must contain at least one individual per worker.",
                nameof(population));
        }

        if (generations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generations));
        }

        var tasks = new List<IslandTask>();

        int baseSize = population.Count / workers.Count;
        int remainder = population.Count % workers.Count;

        int offset = 0;

        for (int i = 0; i < workers.Count; i++)
        {
            int currentSize = baseSize + (i < remainder ? 1 : 0);

            var islandPopulation = population
                .Skip(offset)
                .Take(currentSize)
                .ToList();

            offset += currentSize;

            var task = new IslandTask
            {
                TaskId = taskId,
                IslandId = workers[i].Id,
                Generations = generations,

                Population = islandPopulation,

                Cities = cities.ToArray(),

                MutateRate = mutateRate,
                CrossoverRate = crossoverRate,
                EliteRate = eliteRate,
                MutationType = mutationType
            };

            tasks.Add(task);
        }

        return tasks;
    }
}
