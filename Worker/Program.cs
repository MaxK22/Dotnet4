using Contracts;
using Worker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<Worker.Services.IIslandSolver, Worker.Services.IslandSolver>();

var app = builder.Build();

// Проверка доступности Worker
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy"
    });
});

// Получение задания и запуск вычисления
app.MapPost(
    "/api/islands/evolve",
    async (
        IslandTask task,
        IIslandSolver solver,
        CancellationToken cancellationToken) =>
    {
        if (task.Generations <= 0)
            return Results.BadRequest("Generations must be positive.");

        var result = await solver.EvolveAsync(
            task,
            cancellationToken);

        return Results.Ok(result);
    });

app.Run();

// Необходимо для WebApplicationFactory в проекте тестов
public partial class Program { }