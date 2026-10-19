using NUnit.Framework;
using ParallelGenetic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ParallelGenetic.GeneticAlgo;

namespace AlgoTests
{
    [TestFixture]
    public class GeneticAlgoTests
    {
        [Test]
        public void GeneticAlgo_DistanceShouldDecreaseOverTime()
        {
            // Arrange
            var algo = new GeneticAlgo(n: 10, populationMinSize: 500, numTask: 2);
            double initialBest = double.MaxValue;
            bool hasImproved = false;
            int eventCount = 0;

            algo.ProgressChanged += (sender, e) =>
            {
                eventCount++;
                if (eventCount == 1)
                {
                    initialBest = e.BestDistance;
                }
                else if (e.BestDistance <= initialBest)
                {
                    hasImproved = true;
                }
            };

            // Запускаем алгоритм на 3 sec
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            algo.Run(cts.Token);

            Assert.That(eventCount, Is.GreaterThan(0), "Алгоритм должен генерировать события прогресса.");
            Assert.That(algo.BestDistance, Is.LessThanOrEqualTo(initialBest), "Финальная дистанция не должна быть хуже начальной.");
        }


        [Test]
        public void GeneticAlgo_CircularMatrix_FindsOptimalDistance()
        {
            int n = 4;
            // Матрица расстояний для 4 городов в форме квадрата:
            // Соседние города = 1.0, диагональные = sqrt(2) ≈ 1.4142
            double[,] circularMatrix = {
        { 0.0,    1.0,    1.4142, 1.0 },
        { 1.0,    0.0,    1.0,    1.4142 },
        { 1.4142, 1.0,    0.0,    1.0 },
        { 1.0,    1.4142, 1.0,    0.0 }
    };

            var algo = new GeneticAlgo(n: n, populationMinSize: 200, numTask: 2);
            algo.updateDistanceMatrix(circularMatrix);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
            algo.Run(cts.Token);

            Assert.That(algo.BestDistance, Is.EqualTo(4.0).Within(0.01), "Алгоритм должен найти идеальный круговой маршрут.");
            Assert.That(algo.BestRoute, Is.Not.Null);
            Assert.That(algo.BestRoute.Length, Is.EqualTo(n), "Длина маршрута должна соответствовать количеству городов.");
        }



        [Test]
        public void GeneticAlgo_BigCircularMatrix_FindsOptimalDistance()
        {
            int n = 14;
            // Прямоуглоьник 4 на 3
            

            var algo = new GeneticAlgo(n: n, populationMinSize: 200, numTask: 2);

            Vector2D[] result = new Vector2D[14];
            result[0] = new(0, 0);
            result[1] = new(1, 0);
            result[2] = new(2, 0);
            result[3] = new(3, 0);
            result[4] = new(4, 0);

            result[5] = new(4, 1);
            result[6] = new(4, 2);

            result[7] = new(0, 1);
            result[8] = new(0, 2);

            result[9] = new(0, 3);
            result[10] = new(1, 3);
            result[11] = new(2, 3);
            result[12] = new(3, 3);
            result[13] = new(4, 3);


            double[,] circularMatrix = GeneticAlgo.PointsToMatrix(result);


            algo.updateDistanceMatrix(circularMatrix);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
            algo.Run(cts.Token);

            Assert.That(algo.BestDistance, Is.EqualTo(14.0).Within(0.01), "Алгоритм должен найти идеальный круговой маршрут.");
            Assert.That(algo.BestRoute, Is.Not.Null);
            Assert.That(algo.BestRoute.Length, Is.EqualTo(n), "Длина маршрута должна соответствовать количеству городов.");
        }



        [Test]
        public void GeneticAlgo_MultiThreading_PerformsEfficiently()
        {
            int n = 100;
            int timeLimitMs = 100;

            var singleThreadAlgo = new GeneticAlgo(n: n, populationMinSize: 300, numTask: 1);
            using (var cts1 = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeLimitMs)))
            {
                singleThreadAlgo.Run(cts1.Token);
            }
            double singleBest = singleThreadAlgo.BestDistance;

            var multiThreadAlgo = new GeneticAlgo(n: n, populationMinSize: 300, numTask: 4, migrationPeriod: 20);
            using (var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeLimitMs)))
            {
                multiThreadAlgo.Run(cts2.Token);
            }
            double multiBest = multiThreadAlgo.BestDistance;


            Assert.That(multiBest, Is.LessThanOrEqualTo(singleBest * 1.05),
                "Многопоточный алгоритм с миграцией должен превосходить или не уступать однопоточному за то же время.");
            Assert.That(multiThreadAlgo.BestRoute, Is.Not.Null);
        }


    }
}