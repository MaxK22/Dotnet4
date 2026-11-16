
using Controller;
using Controller.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


using Controller.Models;
using Contracts;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddGeneticController(builder.Configuration);

using var host = builder.Build();

// Получаем Controller из DI.
var controller =
    host.Services.GetRequiredService<OptimizationController>();

// Создаём четыре города в форме квадрата.
CityDto[] cities =
[
    new CityDto { X = 0, Y = 0 },
    new CityDto { X = 1, Y = 0 },
    new CityDto { X = 1, Y = 1 },
    new CityDto { X = 0, Y = 1 }
];

// Создаём начальную популяцию из 12 особей.
var population = new List<IndividualDto>();

for (int i = 0; i < 12; i++)
{
    population.Add(new IndividualDto
    {
        Route = i % 2 == 0
            ? [0, 1, 2, 3]
            : [0, 3, 2, 1],

        // Worker должен вычислить расстояние заново.
        Distance = 0
    });
}

// Настройки одного поколения распределённого вычисления.
var options = new OptimizationOptions
{
    GenerationsPerEpoch = 50,
    MutateRate = 0.1,
    CrossoverRate = 0.9,
    EliteRate = 0.1,
    MutationType = "Swap"
};

string taskId = Guid.NewGuid().ToString("N");

Console.WriteLine("Starting distributed GA...");
Console.WriteLine($"Task ID: {taskId}");
Console.WriteLine($"Population: {population.Count}");
Console.WriteLine($"Cities: {cities.Length}");
Console.WriteLine();

try
{
    var results = await controller.RunEpochAsync(
        population,
        cities,
        options,
        taskId,
        CancellationToken.None);

    Console.WriteLine("Results:");

    foreach (var result in results.OrderBy(x => x.IslandId))
    {
        Console.WriteLine(
            $"Island {result.IslandId}: " +
            $"Best distance = {result.BestDistance:F4}, " +
            $"Population = {result.Population.Count}");

        Console.WriteLine(
            $"Route: {string.Join(" -> ", result.BestRoute)}");
    }

    var best = results.MinBy(x => x.BestDistance)!;

    Console.WriteLine();
    Console.WriteLine("Global best:");
    Console.WriteLine($"Distance: {best.BestDistance:F4}");
    Console.WriteLine($"Route: {string.Join(" -> ", best.BestRoute)}");

    Console.WriteLine(
        $"Expected optimal distance: 4.0000");
}
catch (Exception ex)
{
    Console.WriteLine("Distributed GA failed:");
    Console.WriteLine(ex);
}
