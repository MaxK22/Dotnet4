using Worker.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Text;
using System.Net.Http.Json;
using Contracts;
using Xunit;

namespace Worker.Tests;

public class WorkerApplicationFactory : WebApplicationFactory<Program>
{
    public TestIslandSolver Solver { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIslandSolver>();

            services.AddSingleton<IIslandSolver>(Solver);
        });
    }
}

/*
- доступность Worker;
- успешную обработку задания;
- передачу задания в IIslandSolver;
- отклонение нулевого и отрицательного количества поколений;
- отклонение некорректного JSON;
- ответ 404 для неизвестного адреса.
*/

public class WorkerApiTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var factory = new WorkerApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Evolve_WithValidTask_ReturnsIslandResult()
    {
        using var factory = new WorkerApplicationFactory();
        using var client = factory.CreateClient();

        var task = CreateValidTask();

        var response = await client.PostAsJsonAsync(
            "/api/islands/evolve",
            task);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IslandResult>();

        Assert.NotNull(result);

        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(task.IslandId, result.IslandId);

        Assert.Equal(100.5, result.BestDistance);
        Assert.Equal(new[] { 0, 1, 2 }, result.BestRoute);

        Assert.Equal(1, factory.Solver.CallCount);
    }

    [Fact]
    public async Task Evolve_WithZeroGenerations_ReturnsBadRequest()
    {
        using var factory = new WorkerApplicationFactory();
        using var client = factory.CreateClient();

        var task = CreateValidTask();
        task.Generations = 0;

        var response = await client.PostAsJsonAsync(
            "/api/islands/evolve", task);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Solver.CallCount);
    }

    [Fact]
    public async Task Evolve_WithNegativeGenerations_ReturnsBadRequest()
    {
        using var factory = new WorkerApplicationFactory();
        using var client = factory.CreateClient();

        var task = CreateValidTask();
        task.Generations = -10;

        var response = await client.PostAsJsonAsync(
            "/api/islands/evolve", task);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Solver.CallCount);
    }

    [Fact]
    public async Task Evolve_WithMalformedJson_ReturnsBadRequest()
    {
        using var factory = new WorkerApplicationFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent(
            "{ invalid json",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(
            "/api/islands/evolve", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Solver.CallCount);
    }

    [Fact]
    public async Task UnknownEndpoint_ReturnsNotFound()
    {
        using var factory = new WorkerApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    private static IslandTask CreateValidTask()
    {
        return new IslandTask
        {
            TaskId = "test-task-001",
            IslandId = 1,
            Generations = 100,

            MutateRate = 0.1,
            CrossoverRate = 0.9,
            EliteRate = 0.01,
            MutationType = "Swap",

            Cities =
            [
                new CityDto { X = 0, Y = 0 },
                new CityDto { X = 10, Y = 0 },
                new CityDto { X = 5, Y = 10 }
            ],

            Population =
            [
                new IndividualDto
                {
                    Route = [0, 1, 2],
                    Distance = 0
                }
            ]
        };
    }
}