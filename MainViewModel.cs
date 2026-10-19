using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParallelGenetic;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static ParallelGenetic.GeneticAlgo;
using static System.Net.Mime.MediaTypeNames;

namespace ParallelGenetic
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly GeneticAlgo _model;
        private CancellationTokenSource? _cts;

        // Токен для отслеживания состояния работы
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isRunning;

        // Связываемые настройки алгоритма
        [ObservableProperty] private int _N = 14;
        [ObservableProperty] private MutationType _currentMutationType = MutationType.Inversion;
        [ObservableProperty] private double _eliteRate = 0.05;
        [ObservableProperty] private double _crossoverRate = 0.85;
        [ObservableProperty] private int _migrationPeriod = 50;
        [ObservableProperty] private double _mutateRate = 0.15;
        [ObservableProperty] private int _numTask = Environment.ProcessorCount;
        [ObservableProperty] private int _populationSize = 300;

        // Финальные результаты
        [ObservableProperty] private string _finalDistance = "—";
        [ObservableProperty] private string _finalRoute = "—";

        // Потокобезопасный список для отображения последних нескольких результатов
        public ObservableCollection<string> RecentMetrics { get; } = new ObservableCollection<string>();

        public ObservableCollection<Point> MapCities { get; } = new ObservableCollection<Point>();

        // Свойство для отрисовки линий пути между городами
        [ObservableProperty] private Geometry _mapPathGeometry = Geometry.Empty;
        public MainViewModel()
        {
            _model = new GeneticAlgo();

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

            _model.ProgressChanged += OnModelProgressChanged;
        }

        // Коллекция точек для графика (X = Поколение/Номер рекорда, Y = Дистанция)
        public ObservableCollection<Point> ChartPoints { get; } = new ObservableCollection<Point>();

        // Дополнительные свойства для динамического масштабирования графика под экран
        [ObservableProperty] private double _chartMaxY = 10000;
        [ObservableProperty] private double _chartMinY = 0;
        [ObservableProperty] private double _chartMaxX = 20;
        //private void OnModelProgressChanged(object sender, GeneticAlgoEventArgs e)
        //{
        //    // Перенаправляем выполнение в UI-поток (Dispatcher)
        //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        string metricText = $"Поколение: {e.Generation} | Дистанция: {e.BestDistance:F2} м.";
        //        RecentMetrics.Add(metricText);

        //        // Ограничиваем список последними 20 результатами
        //        if (RecentMetrics.Count > 20)
        //        {
        //            RecentMetrics.RemoveAt(0);
        //        }
        //    });
        //}
        private void OnModelProgressChanged(object sender, GeneticAlgoEventArgs e)
        {
            bool drawAll = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (drawAll)
                {
                    ChartPoints.Add(new Point(ChartPoints.Count, e.BestDistance));
                }
                else
                {
                    if (ChartPoints.Count >= 20)
                    {
                        ChartPoints.RemoveAt(0);
                    }
                    ChartPoints.Add(new Point(ChartPoints.Count, e.BestDistance));
                    for (int i = 0; i < ChartPoints.Count; i++)
                    {
                        ChartPoints[i] = new Point(i, ChartPoints[i].Y);
                    }
                }

                double min = double.MaxValue;
                double max = double.MinValue;

                for (int i = 0; i < ChartPoints.Count; i++)
                {
                    double y = ChartPoints[i].Y;
                    if (y < min) min = y;
                    if (y > max) max = y;
                }

                // Задаем отступы сверху и снизу (по 5%), чтобы график не лип к краям окна
                double padding = (max - min) * 0.05;
                if (padding == 0) padding = 100; // На случай если все точки одинаковые

                ChartMinY = min - padding;
                ChartMaxY = max + padding;
                ChartMaxX = Math.Max(20, ChartPoints.Count);
            });
        }




        private bool CanStart() => !IsRunning;
        private bool CanStop() => IsRunning;

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

                _cts.Dispose();
                _cts = null;
            }
        }


        private void DrawRouteLines(int[] route)
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

        [RelayCommand]
        private void Clear()
        {
            FinalDistance = "";
            FinalRoute = "";
            ChartPoints.Clear();
            MapPathGeometry = Geometry.Empty;
        }

        partial void OnNChanged(int oldValue, int newValue)
        {
            _model.N = N;
            Generate();
        }

        [RelayCommand]
        private void Generate()
        {
            Console.WriteLine("GENEREATE");
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
        }
    }
}

