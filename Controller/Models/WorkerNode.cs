
namespace Controller.Models;

public class WorkerNode
{
    public int Id { get; set; }

    public string BaseUrl { get; set; } = "";

    public bool IsAvailable { get; set; }
}


public class WorkerSettings
{
    public List<WorkerEndpoint> Nodes { get; set; } = [];
}

public class WorkerEndpoint
{
    public int Id { get; set; }

    public string BaseUrl { get; set; } = "";
}