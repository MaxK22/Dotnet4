
using Contracts;

namespace Controller.Services;

public class IslandMigrator
{
    public IReadOnlyList<IslandResult> Migrate(
        IReadOnlyList<IslandResult> islands,
        int migrationCount)
    {
        ArgumentNullException.ThrowIfNull(islands);

        if (migrationCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(migrationCount));
        }

        if (islands.Count <= 1 || migrationCount == 0)
        {
            return islands;
        }

        if (islands.Any(x => migrationCount >= x.Population.Count))
        {
            throw new ArgumentException(
                "MigrationCount must be smaller than every island population.");
        }

        var orderedIslands = islands
            .OrderBy(x => x.IslandId)
            .ToList();

        if (orderedIslands.Any(x =>
            x.Population.Count < migrationCount))
        {
            throw new ArgumentException(
                "Each island must contain at least migrationCount individuals.");
        }

        // Сохраняем лучшие особи каждого острова
        // до начала миграции.
        var migrants = orderedIslands
            .Select(island => island.Population
                .OrderBy(x => x.Distance)
                .Take(migrationCount)
                .Select(CloneIndividual)
                .ToList())
            .ToList();

        var migratedIslands = new List<IslandResult>();

        for (int i = 0; i < orderedIslands.Count; i++)
        {
            // Получаем мигрантов от предыдущего острова.
            int sourceIndex =
                (i - 1 + orderedIslands.Count)
                % orderedIslands.Count;

            var island = orderedIslands[i];

            var newPopulation = island.Population
                .Select(CloneIndividual)
                .OrderBy(x => x.Distance)
                .ToList();

            // Удаляем худших особей.
            newPopulation.RemoveRange(
                newPopulation.Count - migrationCount,
                migrationCount);

            // Добавляем мигрантов.
            newPopulation.AddRange(migrants[sourceIndex]
                .Select(CloneIndividual));

            var best = newPopulation
                .MinBy(x => x.Distance)!;

            migratedIslands.Add(new IslandResult
            {
                TaskId = island.TaskId,
                IslandId = island.IslandId,
                Population = newPopulation,
                BestDistance = best.Distance,
                BestRoute = (int[])best.Route.Clone()
            });
        }

        return migratedIslands;
    }

    private static IndividualDto CloneIndividual(
        IndividualDto individual)
    {
        return new IndividualDto
        {
            Route = (int[])individual.Route.Clone(),
            Distance = individual.Distance
        };
    }
}
