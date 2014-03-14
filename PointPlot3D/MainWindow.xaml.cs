using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using WPFChart3D;
using Model3D = WPFChart3D.Model3D;

namespace PointPlot3D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        #region INNER TYPES

        private struct DataPoint
        {
            internal readonly float X;
            internal readonly float Y;
            internal readonly float Z;
            internal readonly float Value;

            internal DataPoint(float x, float y, float z, float value)
            {
                X = x;
                Y = y;
                Z = z;
                Value = value;
            }
        }

        #endregion

        #region FIELDS

        private int _index;
        private TransformMatrix _transformMatrix;
        private List<DataPoint> _dataPoints;

        #endregion

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _index = -1;
            _transformMatrix = new TransformMatrix();
            _dataPoints = null;
        }

        private void OpenFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            Title = "Point Plot 3D";

            var dlg = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "All files (*.*)|*.*",
                Title = "Open file with data points"
            };

            if (!dlg.ShowDialog(this).GetValueOrDefault()) return;

            Title = dlg.FileName;

            using (var stream = dlg.OpenFile())
            {
                _dataPoints = GetDataPoints(stream);
            }

            if (_dataPoints != null)
            {
                MinValue.Text = String.Format("{0}", _dataPoints.Min(p => p.Value));
                MaxValue.Text = String.Format("{0}", _dataPoints.Max(p => p.Value));
            }

            DisplayDataPoints();
        }

        private void UpdateButton_OnClick(object sender, RoutedEventArgs e)
        {
            DisplayDataPoints();
        }

        private static List<DataPoint> GetDataPoints(Stream stream)
        {
            var list = new List<DataPoint>();

            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("!")) continue;

                    var strs = line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (strs.Length < 4) continue;

                    float x, y, z, value;
                    if (Single.TryParse(strs[0], NumberStyles.Any, CultureInfo.InvariantCulture, out x) &&
                        Single.TryParse(strs[1], NumberStyles.Any, CultureInfo.InvariantCulture, out y) &&
                        Single.TryParse(strs[2], NumberStyles.Any, CultureInfo.InvariantCulture, out z) &&
                        Single.TryParse(strs[3], NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                    {
                        list.Add(new DataPoint(x, y, z, value));
                    }
                }
            }

            return list;
        }

        private void DisplayDataPoints()
        {
            if (_dataPoints == null || _dataPoints.Count <= 0) return;

            var useLogX = LogAxisX.IsChecked.GetValueOrDefault();
            var useLogY = LogAxisY.IsChecked.GetValueOrDefault();
            var useLogZ = LogAxisZ.IsChecked.GetValueOrDefault();

            var minX = useLogX ? (float)Math.Log(_dataPoints.Min(p => p.X)) : _dataPoints.Min(p => p.X);
            var maxX = useLogX ? (float)Math.Log(_dataPoints.Max(p => p.X)) : _dataPoints.Max(p => p.X);
            var minY = useLogY ? (float)Math.Log(_dataPoints.Min(p => p.Y)) : _dataPoints.Min(p => p.Y);
            var maxY = useLogY ? (float)Math.Log(_dataPoints.Max(p => p.Y)) : _dataPoints.Max(p => p.Y);
            var minZ = useLogZ ? (float)Math.Log(_dataPoints.Min(p => p.Z)) : _dataPoints.Min(p => p.Z);
            var maxZ = useLogZ ? (float)Math.Log(_dataPoints.Max(p => p.Z)) : _dataPoints.Max(p => p.Z);

            // parse min and max display values
            float rangeMin, rangeMax;
            if (!Single.TryParse(DisplayMin.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out rangeMin) ||
                !Single.TryParse(DisplayMax.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out rangeMax)) return;

            // Get index of minimum value
            var minIdx =
                _dataPoints.Select((p, idx) => Tuple.Create(p.Value, idx)).OrderBy(tuple => tuple.Item1).First().Item2;

            // set value color range function
            var func = new Func<float, int, Color>((value, idx) =>
            {
                var k = (value - rangeMin) / (rangeMax - rangeMin);
                return idx == minIdx
                    ? Colors.Black
                    : k >= 0.0 && k <= 1.0 ? TextureMapping.PseudoColor(Math.Min(1.0, k)) : Colors.Transparent;
            });

            // parse manual point and show in scatter chart if it is within min and max limits
            float x, y = 0.0f, z = 0.0f;
            var plotManual = false;

            if (Single.TryParse(PosX.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out x) &&
                Single.TryParse(PosY.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out y) &&
                Single.TryParse(PosZ.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out z))
            {
                if (useLogX) x = (float)Math.Log(x);
                if (useLogY) y = (float)Math.Log(y);
                if (useLogZ) z = (float)Math.Log(z);
                plotManual = minX <= x && x <= maxX && minY <= y && y <= maxY && minZ <= z && z <= maxZ;
            }

            // Create scatter chart
            var scatterChart = new ScatterChart3D();
            scatterChart.SetDataNo(_dataPoints.Count + (plotManual ? 1 : 0) + 6);

            var i = 0;
            foreach (var dataPoint in _dataPoints)
            {
                var plotItem = new ScatterPlotItem
                {
                    x = useLogX ? (float)Math.Log(dataPoint.X) : dataPoint.X,
                    y = useLogY ? (float)Math.Log(dataPoint.Y) : dataPoint.Y,
                    z = useLogZ ? (float)Math.Log(dataPoint.Z) : dataPoint.Z,
                    w = 0.03f,
                    h = 0.03f,
                    shape = (int)Chart3D.SHAPE.ELLIPSE,
                    color = func(dataPoint.Value, i)
                };
                scatterChart.SetVertex(i++, plotItem);
            }

            // if valid, plot manual position
            if (plotManual)
            {
                scatterChart.SetVertex(i++,
                    new ScatterPlotItem
                    {
                        color = Colors.DarkGray,
                        w = 0.1f,
                        h = 0.1f,
                        x = x,
                        y = y,
                        z = z,
                        shape = (int)Chart3D.SHAPE.PYRAMID
                    });
            }

            // add axes symbols, one bar for X
            const float barW = 0.2f;
            const float barH = 0.01f;

            scatterChart.SetVertex(i++,
                new ScatterPlotItem
                {
                    color = Colors.Black,
                    w = barW,
                    h = barH,
                    x = 1.05f * maxX - 0.05f * minX,
                    y = minY,
                    z = minZ,
                    shape = (int)Chart3D.SHAPE.BAR
                });

            // two bars for Y
            scatterChart.SetVertex(i++,
                new ScatterPlotItem
                {
                    color = Colors.Black,
                    w = barW,
                    h = barH,
                    x = minX,
                    y = 1.05f * maxY - 0.05f * minY,
                    z = 1.01f * minZ - 0.01f * maxZ,
                    shape = (int)Chart3D.SHAPE.BAR
                });

            scatterChart.SetVertex(i++,
                new ScatterPlotItem
                {
                    color = Colors.Black,
                    w = barW,
                    h = barH,
                    x = minX,
                    y = 1.05f * maxY - 0.05f * minY,
                    z = 0.99f * minZ + 0.01f * maxZ,
                    shape = (int)Chart3D.SHAPE.BAR
                });

            // three bars for Z
            scatterChart.SetVertex(i++,
                new ScatterPlotItem
                {
                    color = Colors.Black,
                    w = barW,
                    h = barH,
                    x = minX,
                    y = minY,
                    z = 1.03f * maxZ - 0.07f * minZ,
                    shape = (int)Chart3D.SHAPE.BAR
                });

            scatterChart.SetVertex(i++,
                new ScatterPlotItem
                {
                    color = Colors.Black,
                    w = barW,
                    h = barH,
                    x = minX,
                    y = minY,
                    z = 1.05f * maxZ - 0.05f * minZ,
                    shape = (int)Chart3D.SHAPE.BAR
                });

            scatterChart.SetVertex(i++,
                new ScatterPlotItem
                {
                    color = Colors.Black,
                    w = barW,
                    h = barH,
                    x = minX,
                    y = minY,
                    z = 1.07f * maxZ - 0.03f * minZ,
                    shape = (int)Chart3D.SHAPE.BAR
                });

            // set axes
            scatterChart.GetDataRange();
            scatterChart.SetAxes();

            // get Mesh3D array from the scatter plot
            var meshs = scatterChart.GetMeshes();

            // display scatter plot in Viewport3D
            _index = new Model3D().UpdateModel(meshs, null, _index, MainViewport);

            // set projection matrix
            _transformMatrix.CalculateProjectionMatrix(minX, maxX, minY, maxY, minZ, maxZ, 0.25);
            TransformChart();
        }

        private void OnViewportMouseDown(object sender, MouseButtonEventArgs args)
        {
            var pt = args.GetPosition(MainViewport);
            if (args.ChangedButton == MouseButton.Left) // rotate or drag 3d model
            {
                _transformMatrix.OnLBtnDown(pt);
            }
        }

        private void OnViewportMouseMove(object sender, MouseEventArgs args)
        {
            var pt = args.GetPosition(MainViewport);

            if (args.LeftButton == MouseButtonState.Pressed) // rotate or drag 3d model
            {
                _transformMatrix.OnMouseMove(pt, MainViewport);

                TransformChart();
            }
        }

        private void OnViewportMouseUp(object sender, MouseButtonEventArgs args)
        {
            args.GetPosition(MainViewport);
            switch (args.ChangedButton)
            {
                case MouseButton.Left:
                    _transformMatrix.OnLBtnUp();
                    break;
            }
        }

        // this function is used to rotate, drag and zoom the 3d chart
        private void TransformChart()
        {
            if (_index == -1) return;
            var visual3D = MainViewport.Children[_index] as ModelVisual3D;
            if (visual3D == null || visual3D.Content == null) return;

            var group1 = visual3D.Content.Transform as Transform3DGroup;
            if (group1 == null) return;

            group1.Children.Clear();
            group1.Children.Add(new MatrixTransform3D(_transformMatrix.m_totalMatrix));
        }
    }
}
