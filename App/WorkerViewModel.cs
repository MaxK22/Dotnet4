
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParallelGeneticApp.Models;

public partial class WorkerViewModel : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty]
    private string _ipAddress = "";

    [ObservableProperty]
    private int _port = 5101;

    [ObservableProperty]
    private bool _isAvailable;

    public string BaseUrl => $"http://{IpAddress}:{Port}";

    public string Status => IsAvailable
        ? "Available"
        : "Unavailable";

    partial void OnIsAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(Status));
    }
}
