
using System.Net.Http.Json;
using Contracts;
using Controller.Models;

namespace Controller.Services;

public class WorkerClient : IWorkerClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public WorkerClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> CheckHealthAsync(
        WorkerNode worker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);

        try
        {
            var client = _httpClientFactory.CreateClient("Workers");

            string url = $"{worker.BaseUrl.TrimEnd('/')}/health";

            using var response = await client.GetAsync(
                url, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<IslandResult> ExecuteIslandAsync(
        WorkerNode worker,
        IslandTask task,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(task);

        var client = _httpClientFactory.CreateClient("Workers");

        string url =
            $"{worker.BaseUrl.TrimEnd('/')}/api/islands/evolve";

        using var response = await client.PostAsJsonAsync(
            url, task, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IslandResult>(
            cancellationToken: cancellationToken);

        return result ?? throw new InvalidOperationException(
            $"Worker {worker.Id} returned an empty response.");
    }
}

