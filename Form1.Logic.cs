using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace TravFloorPlan
{
    public partial class Form1
    {
        private enum ObjectType { Wall, Door, Window, Table, Chair }

        private class PlacedObject
        {
            [Browsable(false)]
            public ObjectType Type { get; set; }
            public Rectangle Rect { get; set; }
            public float RotationDegrees { get; set; }
        }

        private readonly List<PlacedObject> _objects = new();
        private PlacedObject? _selectedObject = null;
        private ObjectType? _selectedType = null;
        private bool _isPlacing = false;
        private Point _placeStart;
        private Point _lastMouse;
        private float _currentRotation = 0f;

        private int _gridSize = 20;
        private bool _snapEnabled = true;

        private void InitializeFloorPlanUi()
        {
            paletteListBox.Items.AddRange(new object[] { ObjectType.Wall, ObjectType.Door, ObjectType.Window, ObjectType.Table, ObjectType.Chair });
            paletteListBox.SelectedIndexChanged += (_, __) =>
            {
                if (paletteListBox.SelectedItem is ObjectType type)
                    _selectedType = type;
            };
            canvasPanel.MouseDown += CanvasPanel_MouseDown;
            canvasPanel.MouseMove += CanvasPanel_MouseMove;
            canvasPanel.MouseUp += CanvasPanel_MouseUp;
            canvasPanel.MouseClick += CanvasPanel_MouseClick;
            canvasPanel.Paint += CanvasPanel_Paint;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            InitializeFloorPlanUi();
        }

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.R)
            {
                _currentRotation = NormalizeAngle(_currentRotation + 90f);
                canvasPanel.Invalidate();
            }
            else if (e.KeyCode == Keys.E)
            {
                _currentRotation = NormalizeAngle(_currentRotation - 90f);
                canvasPanel.Invalidate();
            }
            else if (e.KeyCode == Keys.G)
            {
                _snapEnabled = !_snapEnabled;
                canvasPanel.Invalidate();
            }
        }

        private static float NormalizeAngle(float deg)
        {
            deg %= 360f;
            if (deg < 0) deg += 360f;
            return deg;
        }

        private void CanvasPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            DrawGrid(g, canvasPanel.ClientSize, _gridSize);

            foreach (var obj in _objects)
            {
                DrawObject(g, obj);
            }

            if (_selectedObject != null)
            {
                using var selPen = new Pen(Color.Red, 2) { DashStyle = DashStyle.Dash };
                DrawRotatedRectangle(g, selPen, _selectedObject.Rect, _selectedObject.RotationDegrees);
            }

            if (_isPlacing && _selectedType.HasValue)
            {
                var rect = GetCurrentRect(_placeStart, _lastMouse);
                if (_snapEnabled)
                {
                    rect = SnapRect(rect, _gridSize);
                }
                using var dashed = new Pen(Color.Gray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                DrawRotatedRectangle(g, dashed, rect, _currentRotation);
            }
        }

        private void CanvasPanel_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _selectedObject = HitTest(e.Location);
                propertyGrid.SelectedObject = _selectedObject;
                canvasPanel.Invalidate();
            }
            else if (e.Button == MouseButtons.Right)
            {
                _selectedObject = null;
                propertyGrid.SelectedObject = null;
                canvasPanel.Invalidate();
            }
        }

        private PlacedObject? HitTest(Point location)
        {
            for (int i = _objects.Count - 1; i >= 0; i--) // topmost first
            {
                var obj = _objects[i];
                if (PointInRotatedRect(location, obj.Rect, obj.RotationDegrees))
                    return obj;
            }
            return null;
        }

        private static bool PointInRotatedRect(Point p, Rectangle rect, float degrees)
        {
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            // inverse rotate point around center
            using var path = new GraphicsPath();
            path.AddRectangle(rect);
            var m = new Matrix();
            m.RotateAt(degrees, center);
            path.Transform(m);
            return path.IsVisible(p);
        }

        private void CanvasPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _selectedType.HasValue)
            {
                _isPlacing = true;
                _placeStart = _snapEnabled ? SnapPoint(e.Location, _gridSize) : e.Location;
                _lastMouse = _placeStart;
            }
        }

        private void CanvasPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            _lastMouse = _snapEnabled ? SnapPoint(e.Location, _gridSize) : e.Location;
            if (_isPlacing)
                canvasPanel.Invalidate();
        }

        private void CanvasPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_isPlacing && e.Button == MouseButtons.Left && _selectedType.HasValue)
            {
                var end = _snapEnabled ? SnapPoint(e.Location, _gridSize) : e.Location;
                var rect = GetCurrentRect(_placeStart, end);
                if (_snapEnabled)
                {
                    rect = SnapRect(rect, _gridSize);
                }
                if (rect.Width > 4 && rect.Height > 4)
                {
                    var obj = new PlacedObject { Type = _selectedType.Value, Rect = rect, RotationDegrees = _currentRotation };
                    _objects.Add(obj);
                    _selectedObject = obj;
                    propertyGrid.SelectedObject = _selectedObject;
                    canvasPanel.Invalidate();
                }
            }
            _isPlacing = false;
        }

        private static Rectangle GetCurrentRect(Point start, Point end)
        {
            int x = Math.Min(start.X, end.X);
            int y = Math.Min(start.Y, end.Y);
            int w = Math.Abs(end.X - start.X);
            int h = Math.Abs(end.Y - start.Y);
            return new Rectangle(x, y, w, h);
        }

        private static Point SnapPoint(Point p, int grid)
        {
            int x = (int)Math.Round(p.X / (double)grid) * grid;
            int y = (int)Math.Round(p.Y / (double)grid) * grid;
            return new Point(x, y);
        }

        private static Rectangle SnapRect(Rectangle r, int grid)
        {
            var p1 = SnapPoint(new Point(r.Left, r.Top), grid);
            var p2 = SnapPoint(new Point(r.Right, r.Bottom), grid);
            return GetCurrentRect(p1, p2);
        }

        private static void DrawGrid(Graphics g, Size size, int grid)
        {
            using var pen = new Pen(Color.Gainsboro);
            for (int x = 0; x < size.Width; x += grid)
                g.DrawLine(pen, x, 0, x, size.Height);
            for (int y = 0; y < size.Height; y += grid)
                g.DrawLine(pen, 0, y, size.Width, y);
        }

        private static void DrawObject(Graphics g, PlacedObject obj)
        {
            var rect = obj.Rect;
            switch (obj.Type)
            {
                case ObjectType.Wall:
                    using (var pen = new Pen(Color.Black, 4)) DrawRotatedRectangle(g, pen, rect, obj.RotationDegrees);
                    break;
                case ObjectType.Door:
                    using (var brush = new SolidBrush(Color.SaddleBrown)) FillRotatedRectangle(g, brush, rect, obj.RotationDegrees);
                    using (var pen = new Pen(Color.SaddleBrown)) DrawRotatedRectangle(g, pen, rect, obj.RotationDegrees);
                    break;
                case ObjectType.Window:
                    using (var brush = new SolidBrush(Color.LightSkyBlue)) FillRotatedRectangle(g, brush, rect, obj.RotationDegrees);
                    using (var pen = new Pen(Color.DeepSkyBlue)) DrawRotatedRectangle(g, pen, rect, obj.RotationDegrees);
                    break;
                case ObjectType.Table:
                    using (var brush = new SolidBrush(Color.Peru)) FillRotatedRectangle(g, brush, rect, obj.RotationDegrees);
                    using (var pen = new Pen(Color.SaddleBrown)) DrawRotatedRectangle(g, pen, rect, obj.RotationDegrees);
                    break;
                case ObjectType.Chair:
                    using (var brush = new SolidBrush(Color.DarkOliveGreen)) FillRotatedRectangle(g, brush, rect, obj.RotationDegrees);
                    using (var pen = new Pen(Color.Olive)) DrawRotatedRectangle(g, pen, rect, obj.RotationDegrees);
                    break;
            }
        }

        private static void DrawRotatedRectangle(Graphics g, Pen pen, Rectangle rect, float degrees)
        {
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(degrees);
            g.TranslateTransform(-center.X, -center.Y);
            g.DrawRectangle(pen, rect);
            g.Restore(state);
        }

        private static void FillRotatedRectangle(Graphics g, Brush brush, Rectangle rect, float degrees)
        {
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(degrees);
            g.TranslateTransform(-center.X, -center.Y);
            g.FillRectangle(brush, rect);
            g.Restore(state);
        }
    }
}
