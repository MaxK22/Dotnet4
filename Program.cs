using System;
using System.Windows;

namespace ParallelGenetic
{
    public class Program
    {
        [STAThread] // Этот атрибут обязателен для запуска окон
        public static void Main(string[] args)
        {
            Application app = new Application();
            app.Run(new MainWindow());
        }
    }
}

