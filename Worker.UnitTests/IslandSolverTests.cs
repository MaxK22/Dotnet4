using Contracts;
using Worker.Services;
using NUnit.Framework;

namespace Worker.UnitTests;

[TestFixture]
public class IslandSolverTests
{
    [Test]
    public async Task EvolveAsync_SquareCities_FindsOptimalDistance()
    {
        // Arrange
        var solver = new IslandSolver();

        var task = CreateSquareTask();

        // Act
        var result = await solver.EvolveAsync(
            task,
            CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);

        Assert.That(result.TaskId, Is.EqualTo(task.TaskId));
        Assert.That(result.IslandId, Is.EqualTo(task.IslandId));

        Assert.That(
            result.BestDistance,
            Is.EqualTo(4.0).Within(0.01));

        Assert.That(result.BestRoute, Is.Not.Null);
        Assert.That(result.BestRoute.Length, Is.EqualTo(4));

        Assert.That(
            result.BestRoute.OrderBy(x => x),
            Is.EqualTo(new[] { 0, 1, 2, 3 }));

        Assert.That(result.Population, Is.Not.Empty);
    }

    [Test]
    public void EvolveAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var solver = new IslandSolver();
        var task = CreateSquareTask();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await solver.EvolveAsync(task, cts.Token));
    }

    private static IslandTask CreateSquareTask()
    {
        var cities = new[]
        {
            new CityDto { X = 0, Y = 0 },
            new CityDto { X = 1, Y = 0 },
            new CityDto { X = 1, Y = 1 },
            new CityDto { X = 0, Y = 1 }
        };

        var population = Enumerable.Range(0, 20)
            .Select(_ => new IndividualDto
            {
                Route = [0, 1, 2, 3],
                Distance = 0
            })
            .ToList();

        return new IslandTask
        {
            TaskId = "unit-test-square",
            IslandId = 1,
            Generations = 5,

            Cities = cities,
            Population = population,

            MutateRate = 0,
            CrossoverRate = 0,
            EliteRate = 0.1,
            MutationType = "Swap"
        };
    }
}