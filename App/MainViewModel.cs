using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParallelGenetic;
// using System;
using System.Collections.ObjectModel;
// using System.ComponentModel;
// using System.Threading;
// using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;
using Contracts;
using Controller;
using Controller.Models;
using Controller.Services;
using ParallelGeneticApp.Models;
using System.Linq;


namespace ParallelGeneticApp
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly GeneticAlgo _model;
        private CancellationTokenSource? _cts;

        // Токен для отслеживания состояния работы
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(OnlineStartCommand))]
        [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isRunning;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
        private bool _isClearable;

        private readonly OptimizationController _optimizationController;
        private readonly IWorkerManager _workerManager;
        public ObservableCollection<WorkerViewModel> Workers { get; } = new();

        // Связываемые настройки алгоритма
        [ObservableProperty] private int _N = 14;
        [ObservableProperty] private MutationType _currentMutationType = MutationType.Inversion;
        [ObservableProperty] private double _eliteRate = 0.05;
        [ObservableProperty] private double _crossoverRate = 0.85;
        [ObservableProperty] private int _migrationPeriod = 50;
        [ObservableProperty] private double _mutateRate = 0.15;
        [ObservableProperty] private int _numTask = 1; //Environment.ProcessorCount;
        [ObservableProperty] private int _populationSize = 300;

        // online
        [ObservableProperty] private int _epochs = 10;
        [ObservableProperty] private int _generationsPerEpoch = 100;
        [ObservableProperty] private int _migrationCount = 5;

        // Финальные результаты
        [ObservableProperty] private string _finalDistance = "—";
        [ObservableProperty] private string _finalRoute = "—";

        // Потокобезопасный список для отображения последних нескольких результатов
        public ObservableCollection<string> RecentMetrics { get; } = new ObservableCollection<string>();

        public ObservableCollection<Point> MapCities { get; } = new ObservableCollection<Point>();

        // Свойство для отрисовки линий пути между городами
        [ObservableProperty] private Geometry _mapPathGeometry = Geometry.Empty;
        public MainViewModel(OptimizationController optimizationController,
                             IWorkerManager workerManager)
        {
            _optimizationController = optimizationController
                ?? throw new ArgumentNullException(nameof(optimizationController));
            _workerManager = workerManager;
            foreach (var node in _workerManager.GetAllWorkers())
            {
                var uri = new Uri(node.BaseUrl);

                Workers.Add(new WorkerViewModel
                {
                    Id = node.Id,
                    IpAddress = uri.Host,
                    Port = uri.Port,
                    IsAvailable = node.IsAvailable
                });
            }

            _nextWorkerId = Workers.Count > 0
                ? Workers.Max(w => w.Id) + 1
                : 1;


            _model = new GeneticAlgo();

            MapCities.Clear();
            MapPathGeometry = Geometry.Empty;

            if (_model.CitiesPoints != null)
            {
                foreach (var p in _model.CitiesPoints)
                {
                    MapCities.Add(new Point(p.X, p.Y));
                }
            }

            _model.ProgressChanged += OnModelProgressChanged;
        }

        // Коллекция точек для графика (X = Поколение/Номер рекорда, Y = Дистанция)
        public ObservableCollection<Point> ChartPoints { get; } = new ObservableCollection<Point>();

        // Дополнительные свойства для динамического масштабирования графика под экран
        [ObservableProperty] private double _chartMaxY = 10000;
        [ObservableProperty] private double _chartMinY = 0;
        [ObservableProperty] private double _chartMaxX = 20;
       
        private int _isUpdatingUi = 0; // Защита от накопления очереди задач в Dispatcher

        private void OnModelProgressChanged(object sender, GeneticAlgoEventArgs e)
        {
            // Если UI ещё не успел отрисовать предыдущий кадр — просто пропускаем этот, чтобы не забивать очередь
            if (System.Threading.Interlocked.CompareExchange(ref _isUpdatingUi, 1, 0) != 0)
                return;

            // Используем InvokeAsync с приоритетом Background, чтобы клики мышью и кнопки имели высший приоритет
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                
                try
                {
                    bool drawAll = false;
                    if (drawAll)
                    {
                        ChartPoints.Add(new Point(ChartPoints.Count, e.BestDistance));
                    }
                    else
                    {
                        // Собираем новые точки в обычный список, чтобы не дёргать UI на каждом шаге цикла
                        var updatedPoints = new List<Point>(20);
                        int startIndex = ChartPoints.Count >= 20 ? 1 : 0;

                        for (int i = startIndex; i < ChartPoints.Count; i++)
                        {
                            updatedPoints.Add(new Point(updatedPoints.Count, ChartPoints[i].Y));
                        }
                        updatedPoints.Add(new Point(updatedPoints.Count, e.BestDistance));

                        // Обновляем ObservableCollection (или замените всю коллекцию, если ваш график это поддерживает)
                        ChartPoints.Clear();
                        foreach (var pt in updatedPoints)
                        {
                            ChartPoints.Add(pt);
                        }
                    }

                    // Считаем min и max
                    double min = double.MaxValue;
                    double max = double.MinValue;
                    for (int i = 0; i < ChartPoints.Count; i++)
                    {
                        double y = ChartPoints[i].Y;
                        if (y < min) min = y;
                        if (y > max) max = y;
                    }

                    double padding = (max - min) * 0.05;
                    if (padding == 0) padding = 100;

                    ChartMinY = min - padding;
                    ChartMaxY = max + padding;
                    ChartMaxX = Math.Max(20, ChartPoints.Count);
                }
                finally
                {
                    // Разрешаем следующему обновлению пройти
                    System.Threading.Volatile.Write(ref _isUpdatingUi, 0);
                    FinalDistance = $"{_model.BestDistance:F2} м.";
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }


        [ObservableProperty]
        private string _newWorkerIp = "127.0.0.1";

        [ObservableProperty]
        private int _newWorkerPort = 5101;

        private int _nextWorkerId = 1;

        [RelayCommand]
        private void AddWorker()
        {
            if (!System.Net.IPAddress.TryParse(
                    NewWorkerIp, out var address))
            {
                MessageBox.Show("Введите корректный IP-адрес.");
                return;
            }

            if (NewWorkerPort < 1 || NewWorkerPort > 65535)
            {
                MessageBox.Show("Порт должен быть в диапазоне 1–65535.");
                return;
            }

            var worker = new WorkerViewModel
            {
                Id = _nextWorkerId++,
                IpAddress = address.ToString(),
                Port = NewWorkerPort
            };

            try
            {
                _workerManager.AddWorker(new WorkerNode
                {
                    Id = worker.Id,
                    BaseUrl = worker.BaseUrl,
                    IsAvailable = false
                });

                Workers.Add(worker);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Add Worker");
            }
        }

        [RelayCommand]
        private void RemoveWorker(WorkerViewModel? worker)
        {
            if (worker == null)
                return;

            _workerManager.RemoveWorker(worker.Id);
            Workers.Remove(worker);
        }

        [RelayCommand]
        private async Task CheckWorkers()
        {
            try
            {
                await _workerManager.RefreshAvailabilityAsync(
                    _cts?.Token ?? CancellationToken.None);

                foreach (var worker in Workers)
                {
                    var node = _workerManager.GetAllWorkers()
                        .FirstOrDefault(w => w.Id == worker.Id);

                    if (node != null)
                        worker.IsAvailable = node.IsAvailable;
                }
            }
            catch (OperationCanceledException)
            {
                // Проверка была отменена.
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Worker check");
            }
        }


        private bool CanStart() => !IsRunning;
        private bool CanGenerate() => !IsRunning;
        private bool CanStop() => IsRunning;
        private bool CanClear() => IsClearable;

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task Start()
        {
            MapPathGeometry = Geometry.Empty;
            ChartPoints.Clear();
            IsRunning = true;
            RecentMetrics.Clear();
            FinalDistance = "Поиск...";
            FinalRoute = "Поиск...";

            ChartMinY = 0;
            ChartMaxY = 10000;
            ChartMaxX = 20;

            // Синхронизируем параметры из UI с моделью перед запуском
            _model.N = N;
            _model.CurrentMutationType = CurrentMutationType;
            _model.EliteRate = EliteRate;
            _model.CrossoverRate = CrossoverRate;
            _model.MigrationPeriod = MigrationPeriod;
            _model.MutateRate = MutateRate;
            _model.NumTask = NumTask;
            _model.PopulationSize = PopulationSize;

            _cts = new CancellationTokenSource();


            Task algoTask = Task.Run(() => _model.Run(_cts.Token), _cts.Token);
            _ = HandleTaskCompletionAsync(algoTask);
        }

        
        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task OnlineStart()
        {
            if (_model.CitiesPoints == null ||
                _model.CitiesPoints.Length < 3)
            {
                MessageBox.Show(
                    "Недостаточно городов для построения маршрута.",
                    "Online Start",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (PopulationSize <= 0)
            {
                MessageBox.Show("PopulationSize должен быть больше нуля.");
                return;
            }

            if (Epochs <= 0 || GenerationsPerEpoch <= 0)
            {
                MessageBox.Show("Количество эпох и поколений должно быть больше нуля.");
                return;
            }

            if (MigrationCount < 0)
            {
                MessageBox.Show("MigrationCount не может быть отрицательным.");
                return;
            }

            MapPathGeometry = Geometry.Empty;
            ChartPoints.Clear();
            RecentMetrics.Clear();

            FinalDistance = "Поиск...";
            FinalRoute = "Поиск...";

            ChartMinY = 0;
            ChartMaxY = 10000;
            ChartMaxX = 20;

            IsRunning = true;
            IsClearable = false;

            var cts = new CancellationTokenSource();
            _cts = cts;

            try
            {
                // 1. Преобразуем координаты городов.
                var cities = _model.CitiesPoints
                    .Select(p => new CityDto
                    {
                        X = p.X,
                        Y = p.Y
                    })
                    .ToArray();

                // 2. Создаём начальную популяцию.
                var population = CreateInitialPopulation(
                    cities.Length,
                    PopulationSize);

                // 3. Формируем настройки распределённого алгоритма.
                var options = new OptimizationOptions
                {
                    Epochs = Epochs,
                    GenerationsPerEpoch = GenerationsPerEpoch,
                    MigrationCount = MigrationCount,

                    MutateRate = MutateRate,
                    CrossoverRate = CrossoverRate,
                    EliteRate = EliteRate,

                    MutationType = CurrentMutationType.ToString()
                };

                // 4. Создаём идентификатор запуска.
                string taskId = Guid.NewGuid().ToString("N");

                // 5. Запускаем распределённую оптимизацию.
                var result = await _optimizationController.RunAsync(
                    population,
                    cities,
                    options,
                    taskId,
                    cts.Token);

                // 6. Отображаем историю лучшего результата.
                for (int i = 0; i < result.BestDistanceHistory.Count; i++)
                {
                    double distance = result.BestDistanceHistory[i];

                    ChartPoints.Add(new Point(i + 1, distance));

                    RecentMetrics.Add(
                        $"Эпоха: {i + 1} | Дистанция: {distance:F2} м.");
                }

                // 7. Обновляем масштаб графика.
                if (ChartPoints.Count > 0)
                {
                    double min = ChartPoints.Min(p => p.Y);
                    double max = ChartPoints.Max(p => p.Y);

                    double padding = (max - min) * 0.05;

                    if (padding == 0)
                        padding = 100;

                    ChartMinY = min - padding;
                    ChartMaxY = max + padding;
                    ChartMaxX = Math.Max(20, ChartPoints.Count);
                }

                // 8. Отображаем лучший маршрут.
                FinalDistance = $"{result.BestDistance:F2} м.";

                FinalRoute = result.BestRoute.Length > 0
                    ? string.Join(" -> ", result.BestRoute)
                        + $" -> {result.BestRoute[0]}"
                    : "Не определен";

                DrawRouteLines(result.BestRoute);

                IsClearable = true;

                MessageBox.Show(
                    $"Распределённая оптимизация завершена.\n" +
                    $"Эпох: {result.CompletedEpochs}\n" +
                    $"Лучшая дистанция: {result.BestDistance:F2} м.",
                    "Online Start",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                FinalDistance = "Остановлено";
                FinalRoute = "—";
                IsClearable = true;
            }
            catch (Exception ex)
            {
                FinalDistance = "Ошибка";
                FinalRoute = "—";
                IsClearable = true;

                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка распределённой оптимизации",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsRunning = false;

                if (ReferenceEquals(_cts, cts))
                    _cts = null;

                cts.Dispose();
            }
        }

        private static List<IndividualDto> CreateInitialPopulation(
            int cityCount,
            int populationSize)
        {
            var population = new List<IndividualDto>(populationSize);

            var random = Random.Shared;

            for (int i = 0; i < populationSize; i++)
            {
                int[] route = Enumerable.Range(0, cityCount).ToArray();

                // Перемешивание маршрута алгоритмом Fisher-Yates.
                for (int j = route.Length - 1; j > 0; j--)
                {
                    int k = random.Next(j + 1);

                    (route[j], route[k]) = (route[k], route[j]);
                }

                population.Add(new IndividualDto
                {
                    Route = route,
                    Distance = 0
                });
            }

            return population;
        }




        private async Task HandleTaskCompletionAsync(Task task)
        {
            try
            {
                await task;
            }
            finally
            {
                IsRunning = false;

                // Выводим финальные результаты
                FinalDistance = $"{_model.BestDistance:F2} м.";
                FinalRoute = _model.BestRoute != null
                    ? string.Join(" -> ", _model.BestRoute) + $" -> {_model.BestRoute[0]}"
                    : "Не определен";

                DrawRouteLines(_model.BestRoute);
                IsClearable = true;

                _cts.Dispose();
                _cts = null;
            }
        }


        private void DrawRouteLines(int[]? route)
        {
            if (route == null || route.Length == 0 || _model.CitiesPoints == null) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                PathFigure figure = new PathFigure();
                bool isFirst = true;

                // Идем по порядку посещения городов из лучшего маршрута
                foreach (int cityIndex in route)
                {
                    var vectorPoint = _model.CitiesPoints[cityIndex];
                    Point wpfPoint = new Point(vectorPoint.X, vectorPoint.Y);

                    if (isFirst)
                    {
                        figure.StartPoint = wpfPoint;
                        isFirst = false;
                    }
                    else
                    {
                        figure.Segments.Add(new LineSegment(wpfPoint, true));
                    }
                }

                // Замыкаем маршрут обратно в первый город
                var firstCityVector = _model.CitiesPoints[route[0]];
                figure.Segments.Add(new LineSegment(new Point(firstCityVector.X, firstCityVector.Y), true));

                PathGeometry geometry = new PathGeometry();
                geometry.Figures.Add(figure);

                // Обновляем свойство MapPathGeometry — WPF мгновенно перерисует линии на экране
                MapPathGeometry = geometry;
            });
        }


        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            _cts?.Cancel();

        }

        [RelayCommand(CanExecute = nameof(CanClear))]
        private void Clear()
        {
            if (_cts != null) _cts?.Cancel();
            FinalDistance = "";
            FinalRoute = "";
            ChartPoints.Clear();
            MapPathGeometry = Geometry.Empty;
            IsClearable = false;
        }

        partial void OnNChanged(int oldValue, int newValue)
        {
            _model.N = N;
            Generate();
        }

        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private void Generate()
        {

            //Console.WriteLine("GENEREATE");
            FinalDistance = "";
            FinalRoute = "";
            ChartPoints.Clear();
            MapPathGeometry = Geometry.Empty;

            _model.updatePoints();
            _model.updateDistanceMatrix();

            MapCities.Clear();
            MapPathGeometry = Geometry.Empty;

            if (_model.CitiesPoints != null)
            {
                foreach (var p in _model.CitiesPoints)
                {
                    // Переводим Vector2D в стандартный System.Windows.Point для WPF
                    MapCities.Add(new Point(p.X, p.Y));
                }
            }
            IsClearable = false;
        }
    }
}

