using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ParallelGenetic
{
    // 1. Конвертер горизонтальной оси X
    public class XCoordinateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 || values[0] == null || values[1] == null || values[2] == null)
                return 0.0;

            try
            {
                double x = System.Convert.ToDouble(values[0]);
                double width = System.Convert.ToDouble(values[1]);
                double maxX = System.Convert.ToDouble(values[2]);

                if (maxX <= 0 || width <= 0) return 0.0;
                return (x / maxX) * width;
            }
            catch { return 0.0; }
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 2. Конвертер вертикальной оси Y
    public class YCoordinateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4 || values[0] == null || values[1] == null || values[2] == null || values[3] == null)
                return 0.0;

            try
            {
                double y = System.Convert.ToDouble(values[0]);
                double height = System.Convert.ToDouble(values[1]);
                double minY = System.Convert.ToDouble(values[2]);
                double maxY = System.Convert.ToDouble(values[3]);

                double range = maxY - minY;
                if (range <= 0 || height <= 0) return 0.0;

                double pct = (y - minY) / range;
                return height - (pct * height); // Переворот для Canvas (0 вверху)
            }
            catch { return 0.0; }
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 3. Конвертер соединительной линии (Path Geometry)
    public class PointsCollectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 6 || values[0] == null || values[1] == null || values[2] == null || values[3] == null || values[4] == null || values[5] == null)
                return Geometry.Empty;

            if (values[0] is ObservableCollection<Point> points)
            {
                if (points.Count == 0) return Geometry.Empty;

                try
                {
                    double width = System.Convert.ToDouble(values[1]);
                    double height = System.Convert.ToDouble(values[2]);
                    double minY = System.Convert.ToDouble(values[3]);
                    double maxY = System.Convert.ToDouble(values[4]);
                    double maxX = System.Convert.ToDouble(values[5]);

                    if (width <= 0 || height <= 0) return Geometry.Empty;

                    double range = maxY - minY;
                    if (maxX <= 0) maxX = 20;

                    PathFigure figure = new PathFigure();
                    bool isFirst = true;

                    foreach (var pt in points)
                    {
                        double px = (pt.X / maxX) * width;
                        double py = range <= 0 ? height : height - (((pt.Y - minY) / range) * height);

                        Point pixelPoint = new Point(px, py);

                        if (isFirst)
                        {
                            figure.StartPoint = pixelPoint;
                            isFirst = false;
                        }
                        else
                        {
                            figure.Segments.Add(new LineSegment(pixelPoint, true));
                        }
                    }

                    PathGeometry geometry = new PathGeometry();
                    geometry.Figures.Add(figure);
                    return geometry;
                }
                catch { return Geometry.Empty; }
            }

            return Geometry.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
