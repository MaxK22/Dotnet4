
using Controller;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Windows;

namespace ParallelGeneticApp;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Регистрируем распределённый Controller.
                services.AddGeneticController(context.Configuration);

                // Регистрируем ViewModel.
                services.AddSingleton<MainViewModel>();

                // Регистрируем главное окно.
                services.AddSingleton<MainWindow>();
            })
            .Build();

        host.Start();

        var app = new Application();

        var mainWindow = host.Services
            .GetRequiredService<MainWindow>();

        app.Run(mainWindow);

        host.StopAsync().GetAwaiter().GetResult();
    }
}


