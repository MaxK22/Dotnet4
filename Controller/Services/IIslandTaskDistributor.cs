
using Contracts;
using Controller.Models;

namespace Controller.Services;

public interface IIslandTaskDistributor
{
    IReadOnlyList<IslandTask> CreateTasks(
        IReadOnlyList<IndividualDto> population,
        CityDto[] cities,
        IReadOnlyList<WorkerNode> workers,
        int generations,
        double mutateRate,
        double crossoverRate,
        double eliteRate,
        string mutationType,
        string taskId);
}
