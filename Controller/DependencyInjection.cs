
using Controller.Services;
using Controller.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Controller;

public static class DependencyInjection
{
    public static IServiceCollection AddGeneticController(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WorkerSettings>(
            configuration.GetSection("Workers"));

        services.AddHttpClient("Workers");

        services.AddSingleton<IWorkerClient, WorkerClient>();

        services.AddSingleton<IWorkerManager>(serviceProvider =>
        {
            var workerClient =
                serviceProvider.GetRequiredService<IWorkerClient>();

            var manager = new WorkerManager(workerClient);

            var settings = configuration
                .GetSection("Workers")
                .Get<WorkerSettings>() ?? new WorkerSettings();

            var ids = new HashSet<int>();

            foreach (var endpoint in settings.Nodes)
            {
                if (endpoint.Id <= 0)
                    throw new InvalidOperationException(
                        "Worker ID must be positive.");

                if (!ids.Add(endpoint.Id))
                    throw new InvalidOperationException(
                        $"Duplicate Worker ID: {endpoint.Id}");

                if (string.IsNullOrWhiteSpace(endpoint.BaseUrl))
                    throw new InvalidOperationException(
                        $"Worker {endpoint.Id} has an empty BaseUrl.");

                manager.AddWorker(new WorkerNode
                {
                    Id = endpoint.Id,
                    BaseUrl = endpoint.BaseUrl.TrimEnd('/'),
                    IsAvailable = false
                });
            }

            return manager;
        });

        services.AddSingleton<IIslandTaskDistributor, IslandTaskDistributor>();
        services.AddSingleton<IIslandCoordinator, IslandCoordinator>();
        services.AddSingleton<IslandMigrator>();

        services.AddTransient<OptimizationController>();

        return services;
    }
}