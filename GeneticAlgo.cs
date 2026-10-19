using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;


namespace ParallelGenetic
{
    public enum MutationType
    {
        Swap,
        Inversion
    }
    public class GeneticAlgoEventArgs : EventArgs
    {
        public int Generation { get; }
        public int LocalGeneration { get; }
        public double BestDistance { get; }
        public int[] BestRoute { get; }

        public GeneticAlgoEventArgs(int generation, int localGeneration, double bestDistance, int[] bestRoute)
        {
            Generation = generation;
            LocalGeneration = localGeneration;
            BestDistance = bestDistance;
            BestRoute = (int[])bestRoute.Clone();
        }
    }

    public class GeneticAlgo
    {
        private int _N;
        public int N
        {
            set
            { _N = value; this.baseRoute = Enumerable.Range(0, N).ToArray(); updateDistanceMatrix();}

            get => _N;
        }

        public struct Vector2D
        {
            public double X;
            public double Y;

            public Vector2D(double x, double y)
            {
                X = x;
                Y = y;
            }

            public static Vector2D operator +(Vector2D a, Vector2D b) =>
                new(a.X + b.X, a.Y + b.Y);

            public static Vector2D operator *(Vector2D v, double scalar) =>
                new(v.X * scalar, v.Y * scalar);
            public static double Distance(Vector2D a, Vector2D b) =>
                Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        static Vector2D GeneratePoint(double min_value, double max_value) =>
            new(Random.Shared.NextDouble() * (max_value - min_value) + min_value, Random.Shared.NextDouble() * (max_value - min_value) + min_value);
        static Vector2D GenerateNumPoint(int min_value, int max_value) =>
            new(Random.Shared.Next(min_value, max_value+1), Random.Shared.Next(min_value, max_value+1));

        public static Vector2D[] GeneratePoints(int count, int min_value = 0, int max_value = 800)
        {
            Vector2D[] result = new Vector2D[count]; // Initialize the array with a fixed size

            
            //result[0] = new(0, 0);
            //result[1] = new(1, 0);
            //result[2] = new(2, 0);
            //result[3] = new(3, 0);
            //result[4] = new(4, 0);

            //result[5] = new(4, 1);
            //result[6] = new(4, 2);

            //result[7] = new(0, 1);
            //result[8] = new(0, 2);

            //result[9] = new(0, 3);
            //result[10] = new(1, 3);
            //result[11] = new(2, 3);
            //result[12] = new(3, 3);
            //result[13] = new(4, 3);

            for (int i = 0; i < count; i++)
            {
                result[i] = GenerateNumPoint(min_value, max_value);
            }

            return result; // Return the array reference
        }

        public static double[,] PointsToMatrix(Vector2D[] points)
        {
            int n = points.Length;
            double[,] c = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                c[i, i] = 0;
                for (int j = i + 1; j < n; j++)
                {
                    c[i, j] = Vector2D.Distance(points[i], points[j]);
                    c[j, i] = c[i, j];
                }
            }
            return c;
        }

        private Vector2D[] _citiesPoints;

        // Публичное свойство для доступа к точкам городов
        public Vector2D[] CitiesPoints => _citiesPoints;

        void GenerateMatrix()
        {
            _citiesPoints = GeneratePoints(N);
            distanceMatrix = PointsToMatrix(_citiesPoints);
        }
        public void updatePoints(Vector2D[]? citiesPoints = null)
        {
            if(citiesPoints == null)
                _citiesPoints = GeneratePoints(N);
            else
                _citiesPoints = citiesPoints;
        }
        public void updateDistanceMatrix(double[,]? distanceMatrix = null) 
        {
            if (distanceMatrix != null)
            {
                this.distanceMatrix = distanceMatrix;
                _citiesPoints = null;
            }
            else
            {
                if (_citiesPoints == null)
                    updatePoints();
                this.distanceMatrix = PointsToMatrix(_citiesPoints);
            }
        }
        public double[,] DistanceMatrix { get => distanceMatrix; }


        //static void Main(string[] args)
        //{
        //    GeneticAlgo GA = new GeneticAlgo();

        //    //GeneticAlgo GA = new GeneticAlgo();
        //    //double[,] matrix = GenerateMatrix(GA.N);
        //    //Console.WriteLine($"\n");
        //    //for (int i = 0; i < GA.N; i++)
        //    //{
        //    //    for (int j = 0; j < GA.N; j++)
        //    //    {
        //    //        Console.Write($"{matrix[i, j]}  "); // Вывод элемента с отступом
        //    //    }
        //    //    Console.WriteLine(); // Переход на новую строку после каждой строки матрицы
        //    //}
        //    //int[] a = { 1, 2, 3, 4, 5};
        //    //(a[0], a[4]) = (a[4], a[0]);
        //    //Console.Write($"{a[0]}, {a[1]}, {a[2]}, {a[3]}, {a[4]}");
        //}

        private int numTask;

        private int taskPopulationSize;

        private int populationSize;


        public int PopulationSize
        {
            get => populationSize;
            set
            {
                if(value <= 0)
                    throw new ArgumentException("Размер популяции должен быть больше 0");
                populationSize = value;
                taskPopulationSize = (int)(populationSize / numTask);
            }
        }

        //public int TaskPopulationSize
        //{
        //    get => taskPopulationSize;
        //    set
        //    {
        //        if (value <= 0)
        //            throw new ArgumentException("Размер популяции должен быть больше 0");
        //        taskPopulationSize = value;
        //        populationSize = taskPopulationSize * numTask;
        //    }
        //}

        public int NumTask
        {
            get => numTask;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Число задач не может быть меньше 0");
                numTask = value;
                taskPopulationSize = (int)(populationSize / numTask);
            }
        }

        public double MutateRate
        {
            get => mutateRate;
            set => mutateRate = (value >= 0 && value <= 1) ? value : throw new ArgumentOutOfRangeException("Шанс мутации должен быть от 0.0 до 1.0");
        }

        public int MigrationPeriod
        {
            get => migrationPeriod;
            set => migrationPeriod = value > 0 ? value : throw new ArgumentException("Период миграции должен быть больше 0");
        }

        //public double MigrationRate
        //{
        //    get => migrationRate;
        //    set => migrationRate = (value >= 0 && value <= 1) ? value : throw new ArgumentOutOfRangeException("Коэффициент миграции должен быть от 0.0 до 1.0");
        //}

        public double CrossoverRate
        {
            get => crossoverRate;
            set => crossoverRate = (value >= 0 && value <= 1) ? value : throw new ArgumentOutOfRangeException("Шанс скрещивания должен быть от 0.0 до 1.0");
        }

        public double EliteRate
        {
            get => eliteRate;
            set => eliteRate = (value >= 0 && value <= 1) ? value : throw new ArgumentOutOfRangeException("Коэффициент элитизма должен быть от 0.0 до 1.0");
        }

        private int migrationPeriod;

        private double crossoverRate;
        private double mutateRate;
        private double eliteRate;
        //private double migrationRate;
        private delegate Individual MutateDelegate(Individual obj);
        private MutateDelegate? mutate;
        private MutationType _mutationType;
        public MutationType CurrentMutationType
        {
            get => _mutationType;
            set
            {
                _mutationType = value;
                UpdateMutationMethod(value);
            }
        }
        private void UpdateMutationMethod(MutationType type)
        {
            mutate = type switch
            {
                MutationType.Swap => SwapMutate,
                MutationType.Inversion => InversionMutate,
                _ => InversionMutate // Дефолтный вариант
            };
        }

        private int[] baseRoute;

        public struct Individual
        {
            public int[] Route;
            public double Distance;
            //public Individual(int[] route)
            //{
            //    this.Route = route;
            //    this.Distance = 0;
            //}
            public void updateDistance(double[,] distanceMatrix)
            {
                Distance = 0;
                int N = distanceMatrix.GetLength(0);
                for (int j = 0; j < N - 1; j++)
                {
                    Distance += distanceMatrix[Route[j], Route[j + 1]];
                }
                Distance += distanceMatrix[Route[N - 1], Route[0]];
            }
            public void clearDistance() => Distance = 0;
        }

        private double[,] distanceMatrix;

        public GeneticAlgo(
            MutationType type = MutationType.Swap,
            int n = 14,
            //double[,]? distanceMatrix = null,
            int populationMinSize = 1000,
            double mutateRate = 0.1,
            int migrationPeriod = 50,
            double crossoverRate = 0.9,
            double eliteRate = 0.01, 
            int? numTask = null)
        {
            this.N = n;
            if (numTask == null) this.numTask = Environment.ProcessorCount;
            else this.numTask = (int) numTask;
            this.taskPopulationSize = (int)(populationMinSize / this.numTask);
            this.populationSize = this.taskPopulationSize * this.numTask;

            MutateRate = mutateRate;
            MigrationPeriod = migrationPeriod;
            CrossoverRate = crossoverRate;
            EliteRate = eliteRate;

            this.baseRoute = Enumerable.Range(0, N).ToArray();

            CurrentMutationType = type;

            GenerateMatrix();
            //if (distanceMatrix != null) this.distanceMatrix = distanceMatrix;
            //else this.distanceMatrix = GenerateMatrix(n);

        }

        public event EventHandler<GeneticAlgoEventArgs>? ProgressChanged;

        private readonly object _globalBestLock = new object();
        private double _globalBestDistance = double.MaxValue;
        private int[]? _globalBestRoute;
        private int _globalGeneration = 0;

        // Свойства для получения финального результата
        public double BestDistance => _globalBestDistance;
        public int[]? BestRoute => _globalBestRoute;


        public void Run(CancellationToken token)
        {
            _globalBestDistance = double.MaxValue;
            _globalBestRoute = null;
            _globalGeneration = 0;
            ClearMigrationBuffer();

            Task[] tasks = new Task[numTask];

            for (int i = 0; i < numTask; i++)
            {
                int taskId = i;
                tasks[i] = Task.Run(() => EvolveTask(taskId, token), token);
            }

            try
            {
                Task.WaitAll(tasks, token);
            }
            catch (OperationCanceledException)
            {
                // Корректный выход при отмене пользователем
            }
        }


        private void EvolveTask(int taskId, CancellationToken token)
        {
            List<Individual> subPopulation = InitializePopulation(taskPopulationSize);

            for (int generation = 0; !token.IsCancellationRequested; generation++)
            {
                EvaluateFitness(subPopulation);
                subPopulation.Sort((x, y) => x.Distance.CompareTo(y.Distance));
                subPopulation = SelectionAndCrossover(subPopulation, taskPopulationSize);
                Mutate(subPopulation);

                UpdateBest(subPopulation[0], generation);

                // Периодическая миграция
                if ((generation % migrationPeriod == 0) || token.IsCancellationRequested)
                {
                    MigrateBestToSharedBuffer(taskId, subPopulation);
                    if (token.IsCancellationRequested)
                        break;
                    AcceptMigrantsFromSharedBuffer(taskId, subPopulation);
                }
            }
        }

        private void UpdateBest(Individual localBest, int localGeneration)
        {
            bool isNewGlobalBest = false;
            double currentBestToReport = 0;
            int[] routeToReport = null;
            lock (_globalBestLock)
            {
                if (localBest.Distance < _globalBestDistance)
                {
                    _globalBestDistance = localBest.Distance;
                    _globalBestRoute = (int[])localBest.Route.Clone();

                    isNewGlobalBest = true;
                    currentBestToReport = _globalBestDistance;
                    routeToReport = _globalBestRoute;
                    _globalGeneration++;
                }
            }
            if (isNewGlobalBest)
            {
                ProgressChanged?.Invoke(this, new GeneticAlgoEventArgs(_globalGeneration, localGeneration, currentBestToReport, routeToReport));
            }
        }

        private List<Individual> InitializePopulation(int n)
        {
            List<Individual> pop = new List<Individual>(n);

            for (int i = 0; i < n; i++)
            {
                int[] route = (int[])baseRoute.Clone();

                // Тасование Фишера — Йетса с использованием Random.Shared
                for (int j = N - 1; j > 0; j--)
                {
                    int k = Random.Shared.Next(j + 1);
                    (route[j], route[k]) = (route[k], route[j]);
                }

                pop.Add(new Individual { Route = route, Distance = 0 });
            }

            return pop;
        }

        private void EvaluateFitness(List<Individual> subPopulation)
        {
            for (int i = 0; i < subPopulation.Count; i++)
                if (subPopulation[i].Distance == 0)
                {
                    Individual mutatedInd = subPopulation[i];
                    mutatedInd.updateDistance(distanceMatrix); // метод внутри структуры меняет поле
                    subPopulation[i] = mutatedInd; // сохраняем обратно
                }
        }

        private List<Individual> SelectionAndCrossover(List<Individual> subPopulation, int nextGenerationSize)
        {
            List<Individual> nextGeneration = new List<Individual>(nextGenerationSize);

            int elitismCount = Math.Max(1, (int)(subPopulation.Count * eliteRate));
            for (int i = 0; i < elitismCount; i++)
            {
                nextGeneration.Add(new Individual
                {
                    Route = (int[])subPopulation[i].Route.Clone(),
                    Distance = subPopulation[i].Distance
                });
            }

            bool[] visited = new bool[N];
            while (nextGeneration.Count < nextGenerationSize)
            {
                // Выбираем двух родителей турнирным отбором
                Individual parent1 = TournamentSelection(subPopulation);
                Individual parent2 = TournamentSelection(subPopulation);

                int[] childRoute;

                if (Random.Shared.NextDouble() < crossoverRate)
                {
                    childRoute = OrderedCrossover(parent1.Route, parent2.Route, visited);
                }
                else
                {
                    // Если скрещивание не произошло, копируем первого родителя
                    childRoute = (int[])parent1.Route.Clone();
                }
                nextGeneration.Add(new Individual { Route = childRoute, Distance = 0 });
            }

            return nextGeneration;
        }

        private int[] OrderedCrossover(int[] p1, int[] p2, bool[] visited)
        {
            int[] child = new int[N];

            // Выбираем две случайные точки разреза
            int start = Random.Shared.Next(N);
            int end = Random.Shared.Next(N);
            if (start > end) (start, end) = (end, start);

            // Сбрасываем массив посещенных городов
            Array.Clear(visited, 0, visited.Length);

            // 1. Копируем генетический блок из первого родителя
            for (int i = start; i <= end; i++)
            {
                child[i] = p1[i];
                visited[p1[i]] = true;
            }

            // 2. Заполняем оставшиеся позиции элементами второго родителя,
            // соблюдая их циклический порядок, начиная с позиции (end + 1)
            int currentChildIdx = (end + 1) % N;
            int p2Idx = (end + 1) % N;

            for (int i = 0; i < N; i++)
            {
                int city = p2[p2Idx];

                if (!visited[city])
                {
                    child[currentChildIdx] = city;
                    currentChildIdx = (currentChildIdx + 1) % N;
                }

                p2Idx = (p2Idx + 1) % N;
            }

            return child;
        }

        private Individual TournamentSelection(List<Individual> pop)
        {
            const int tournamentSize = 3;
            Individual best = pop[Random.Shared.Next(pop.Count)];

            for (int i = 1; i < tournamentSize; i++)
            {
                Individual competitor = pop[Random.Shared.Next(pop.Count)];
                if (competitor.Distance < best.Distance)
                {
                    best = competitor;
                }
            }
            return best;
        }

        private void Mutate(List<Individual> subPopulation)
        {
            int elitismCount = Math.Max(1, (int)(subPopulation.Count * eliteRate));
            for (int i = elitismCount; i < subPopulation.Count; i++)
            {
                // Проверяем, срабатывает ли шанс мутации для текущей особи
                if (Random.Shared.NextDouble() < mutateRate)
                {
                    subPopulation[i] = mutate(subPopulation[i]);
                }
            }
        }

        Individual InversionMutate(Individual obj)
        {
            // Выбираем две случайные точки для разворота участка пути
            int idx1 = Random.Shared.Next(N);
            int idx2 = Random.Shared.Next(N);

            if (idx1 == idx2) return obj;

            // Убеждаемся, что idx1 — левая граница, а idx2 — правая
            if (idx1 > idx2) (idx1, idx2) = (idx2, idx1);

            // Инвертируем (разворачиваем) подмассив между idx1 и idx2
            while (idx1 < idx2)
            {
                (obj.Route[idx1], obj.Route[idx2]) = (obj.Route[idx2], obj.Route[idx1]);
                idx1++;
                idx2--;
            }
            obj.clearDistance();
            return obj;
        }
        Individual SwapMutate(Individual obj)
        {
            // Выбираем две случайные точки для разворота участка пути
            int idx1 = Random.Shared.Next(N);
            int idx2 = Random.Shared.Next(N);

            if (idx1 == idx2) return obj;

            // Убеждаемся, что idx1 — левая граница, а idx2 — правая
            if (idx1 > idx2) (idx1, idx2) = (idx2, idx1);

            obj.clearDistance();
            return obj;
        }



        // Объявление буфера на уровне класса (ключ - ID острова, значение - лучшая особь)
        private readonly ConcurrentDictionary<int, Individual> _sharedMigrationBuffer = new ();

        public void ClearMigrationBuffer()
        {
            _sharedMigrationBuffer.Clear();
        }


        private void MigrateBestToSharedBuffer(int taskId, List<Individual> subPopulation)
        {
            if (subPopulation == null || subPopulation.Count == 0) return;

            Individual migrant = new Individual
            {
                Route = (int[])subPopulation[0].Route.Clone(),
                Distance = subPopulation[0].Distance
            };

            _sharedMigrationBuffer[taskId] = migrant;
        }


        private void AcceptMigrantsFromSharedBuffer(int taskId, List<Individual> subPopulation)
        {
            var otherIslands = _sharedMigrationBuffer.Keys.Where(id => id != taskId).ToList();
            if (otherIslands.Count == 0) return;

            int randomIslandId = otherIslands[Random.Shared.Next(otherIslands.Count)];

            if (_sharedMigrationBuffer.TryGetValue(randomIslandId, out Individual migrant))
            {
                Individual migrantCopy = new Individual
                {
                    Route = (int[])migrant.Route.Clone(),
                    Distance = migrant.Distance
                };
                int worstIndex = subPopulation.Count - 1;
                subPopulation[worstIndex] = migrantCopy;
            }
        }






    }
}