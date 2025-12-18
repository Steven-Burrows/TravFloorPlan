using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace TravFloorPlan
{
    public partial class MainForm
    {
        private enum ObjectType { Room, Door, Window, Table, Chair }

        private class PlacedObject
        {
            [Browsable(false)]
            public ObjectType Type { get; set; }
            public Rectangle Rect { get; set; }
            public float RotationDegrees { get; set; }
            public string? Name { get; set; }
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

        // Drag/resize state
        private enum InteractionMode { None, Move, Resize }
        private InteractionMode _interaction = InteractionMode.None;
        private Point _dragStart;
        private Rectangle _originalRect;
        private ResizeHandle _activeHandle = ResizeHandle.None;
        private const int HandleSize = 8;

        private enum ResizeHandle
        {
            None,
            N, S, E, W,
            NE, NW, SE, SW
        }

        private ContextMenuStrip _canvasMenu;

        private class PaletteEntry
        {
            public bool IsHeader { get; }
            public ObjectType HeaderType { get; }
            public PlacedObject? ObjectRef { get; }
            private readonly string _text;
            public PaletteEntry(ObjectType type)
            {
                IsHeader = true;
                HeaderType = type;
                _text = type.ToString();
            }
            public PaletteEntry(PlacedObject obj)
            {
                IsHeader = false;
                ObjectRef = obj;
                HeaderType = obj.Type;
                var name = string.IsNullOrWhiteSpace(obj.Name) ? "<unnamed>" : obj.Name;
                _text = $"   - {name}";
            }
            public override string ToString() => _text;
        }

        private void InitializeFloorPlanUi()
        {
            // remove static type list, we dynamically build it with objects
            paletteListBox.Items.Clear();
            paletteListBox.SelectedIndexChanged += (_, __) =>
            {
                if (paletteListBox.SelectedItem is PaletteEntry entry)
                {
                    if (entry.IsHeader)
                    {
                        _selectedType = entry.HeaderType;
                    }
                    else if (entry.ObjectRef != null)
                    {
                        _selectedObject = entry.ObjectRef;
                        propertyGrid.SelectedObject = _selectedObject;
                        canvasPanel.Invalidate();
                    }
                }
                else if (paletteListBox.SelectedItem is ObjectType type)
                {
                    _selectedType = type;
                }
            };
            canvasPanel.MouseDown += CanvasPanel_MouseDown;
            canvasPanel.MouseMove += CanvasPanel_MouseMove;
            canvasPanel.MouseUp += CanvasPanel_MouseUp;
            canvasPanel.MouseClick += CanvasPanel_MouseClick;
            canvasPanel.Paint += CanvasPanel_Paint;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            _canvasMenu = new ContextMenuStrip();
            var rotateLeftItem = new ToolStripMenuItem("Rotate Left 90", null, (_, __) => RotateSelected(-90f));
            var rotateRightItem = new ToolStripMenuItem("Rotate Right 90", null, (_, __) => RotateSelected(90f));
            var deleteItem = new ToolStripMenuItem("Delete", null, (_, __) => DeleteSelected());
            _canvasMenu.Items.AddRange(new ToolStripItem[] { rotateLeftItem, rotateRightItem, new ToolStripSeparator(), deleteItem });
            canvasPanel.ContextMenuStrip = _canvasMenu;
            _canvasMenu.Opening += (_, e) =>
            {
                bool hasSelection = _selectedObject != null;
                rotateLeftItem.Enabled = hasSelection;
                rotateRightItem.Enabled = hasSelection;
                deleteItem.Enabled = hasSelection;
            };

            RefreshPaletteList();
            propertyGrid.PropertyValueChanged += (_, __) => RefreshPaletteList();
        }

        private void RefreshPaletteList()
        {
            var prevObj = _selectedObject;
            var prevType = _selectedType;
            paletteListBox.BeginUpdate();
            paletteListBox.Items.Clear();
            foreach (ObjectType type in Enum.GetValues(typeof(ObjectType)))
            {
                var header = new PaletteEntry(type);
                paletteListBox.Items.Add(header);
                foreach (var obj in _objects)
                {
                    if (obj.Type == type)
                    {
                        paletteListBox.Items.Add(new PaletteEntry(obj));
                    }
                }
            }
            paletteListBox.EndUpdate();
            if (prevObj != null)
            {
                foreach (var item in paletteListBox.Items)
                {
                    if (item is PaletteEntry pe && pe.ObjectRef == prevObj)
                    {
                        paletteListBox.SelectedItem = item;
                        return;
                    }
                }
            }
            if (prevType.HasValue)
            {
                foreach (var item in paletteListBox.Items)
                {
                    if (item is PaletteEntry pe && pe.IsHeader && pe.HeaderType == prevType.Value)
                    {
                        paletteListBox.SelectedItem = item;
                        return;
                    }
                }
            }
        }

        private void RotateSelected(float deltaDegrees)
        {
            if (_selectedObject == null) return;
            _selectedObject.RotationDegrees = NormalizeAngle(_selectedObject.RotationDegrees + deltaDegrees);
            propertyGrid.Refresh();
            canvasPanel.Invalidate();
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
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
            }
        }

        private void DeleteSelected()
        {
            if (_selectedObject != null)
            {
                _objects.Remove(_selectedObject);
                _selectedObject = null;
                propertyGrid.SelectedObject = null;
                canvasPanel.Invalidate();
                RefreshPaletteList();
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

            // draw non-door objects first
            foreach (var obj in _objects)
            {
                if (obj.Type != ObjectType.Door)
                {
                    DrawObject(g, obj);
                }
            }
            // draw doors last
            foreach (var obj in _objects)
            {
                if (obj.Type == ObjectType.Door)
                {
                    DrawDoorSymbol(g, obj.Rect, obj.RotationDegrees);
                }
            }

            if (_selectedObject != null)
            {
                using var selPen = new Pen(Color.Red, 2) { DashStyle = DashStyle.Dash };
                DrawRotatedRectangle(g, selPen, _selectedObject.Rect, _selectedObject.RotationDegrees);
                if (Math.Abs(_selectedObject.RotationDegrees % 90f) < 0.001f)
                {
                    DrawResizeHandles(g, _selectedObject.Rect);
                }
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

        private void DrawResizeHandles(Graphics g, Rectangle rect)
        {
            using var brush = new SolidBrush(Color.White);
            using var pen = new Pen(Color.Red);
            foreach (var handleRect in GetHandleRects(rect))
            {
                g.FillRectangle(brush, handleRect);
                g.DrawRectangle(pen, handleRect);
            }
        }

        private IEnumerable<Rectangle> GetHandleRects(Rectangle r)
        {
            int hs = HandleSize;
            yield return new Rectangle(r.Left - hs / 2, r.Top - hs / 2, hs, hs);
            yield return new Rectangle(r.Right - hs / 2, r.Top - hs / 2, hs, hs);
            yield return new Rectangle(r.Left - hs / 2, r.Bottom - hs / 2, hs, hs);
            yield return new Rectangle(r.Right - hs / 2, r.Bottom - hs / 2, hs, hs);
            yield return new Rectangle(r.Left - hs / 2, r.Top + r.Height / 2 - hs / 2, hs, hs);
            yield return new Rectangle(r.Right - hs / 2, r.Top + r.Height / 2 - hs / 2, hs, hs);
            yield return new Rectangle(r.Left + r.Width / 2 - hs / 2, r.Top - hs / 2, hs, hs);
            yield return new Rectangle(r.Left + r.Width / 2 - hs / 2, r.Bottom - hs / 2, hs, hs);
        }

        private ResizeHandle HitTestHandle(Point p, Rectangle r)
        {
            int hs = HandleSize;
            var map = new Dictionary<ResizeHandle, Rectangle>
            {
                { ResizeHandle.NW, new Rectangle(r.Left - hs / 2, r.Top - hs / 2, hs, hs) },
                { ResizeHandle.NE, new Rectangle(r.Right - hs / 2, r.Top - hs / 2, hs, hs) },
                { ResizeHandle.SW, new Rectangle(r.Left - hs / 2, r.Bottom - hs / 2, hs, hs) },
                { ResizeHandle.SE, new Rectangle(r.Right - hs / 2, r.Bottom - hs / 2, hs, hs) },
                { ResizeHandle.W, new Rectangle(r.Left - hs / 2, r.Top + r.Height / 2 - hs / 2, hs, hs) },
                { ResizeHandle.E, new Rectangle(r.Right - hs / 2, r.Top + r.Height / 2 - hs / 2, hs, hs) },
                { ResizeHandle.N, new Rectangle(r.Left + r.Width / 2 - hs / 2, r.Top - hs / 2, hs, hs) },
                { ResizeHandle.S, new Rectangle(r.Left + r.Width / 2 - hs / 2, r.Bottom - hs / 2, hs, hs) },
            };
            foreach (var kv in map)
            {
                if (kv.Value.Contains(p)) return kv.Key;
            }
            return ResizeHandle.None;
        }

        private void CanvasPanel_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _interaction == InteractionMode.None)
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
            for (int i = _objects.Count - 1; i >= 0; i--)
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
            using var path = new GraphicsPath();
            path.AddRectangle(rect);
            var m = new Matrix();
            m.RotateAt(degrees, center);
            path.Transform(m);
            return path.IsVisible(p);
        }

        private void CanvasPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_selectedObject != null && Math.Abs(_selectedObject.RotationDegrees % 90f) < 0.001f)
                {
                    var handle = HitTestHandle(e.Location, _selectedObject.Rect);
                    if (handle != ResizeHandle.None)
                    {
                        _interaction = InteractionMode.Resize;
                        _activeHandle = handle;
                        var snap = _snapEnabled ? GetSnapSizeFor(_selectedObject.Type) : 0;
                        _dragStart = _snapEnabled ? SnapPoint(e.Location, snap) : e.Location;
                        _originalRect = _selectedObject.Rect;
                        return;
                    }
                    else if (PointInRotatedRect(e.Location, _selectedObject.Rect, _selectedObject.RotationDegrees))
                    {
                        _interaction = InteractionMode.Move;
                        var snap = _snapEnabled ? GetSnapSizeFor(_selectedObject.Type) : 0;
                        _dragStart = _snapEnabled ? SnapPoint(e.Location, snap) : e.Location;
                        _originalRect = _selectedObject.Rect;
                        return;
                    }
                }

                if (_selectedType.HasValue)
                {
                    _isPlacing = true;
                    var snap = _snapEnabled ? GetSnapSizeFor(_selectedType.Value) : 0;
                    _placeStart = _snapEnabled ? SnapPoint(e.Location, snap) : e.Location;
                    _lastMouse = _placeStart;
                }
            }
        }

        private int GetSnapSizeFor(ObjectType type) => type == ObjectType.Door ? Math.Max(1, _gridSize / 2) : _gridSize;

        private void CanvasPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            int snap = _snapEnabled
                ? (_interaction != InteractionMode.None && _selectedObject != null
                    ? GetSnapSizeFor(_selectedObject.Type)
                    : (_isPlacing && _selectedType.HasValue ? GetSnapSizeFor(_selectedType.Value) : _gridSize))
                : 0;
            var loc = _snapEnabled ? SnapPoint(e.Location, snap) : e.Location;
            _lastMouse = loc;

            if (_interaction == InteractionMode.Move && _selectedObject != null)
            {
                int dx = loc.X - _dragStart.X;
                int dy = loc.Y - _dragStart.Y;
                var newRect = new Rectangle(_originalRect.X + dx, _originalRect.Y + dy, _originalRect.Width, _originalRect.Height);
                _selectedObject.Rect = newRect;
                propertyGrid.Refresh();
                canvasPanel.Invalidate();
                return;
            }
            else if (_interaction == InteractionMode.Resize && _selectedObject != null)
            {
                var r = _originalRect;
                int dx = loc.X - _dragStart.X;
                int dy = loc.Y - _dragStart.Y;
                Rectangle newRect = r;
                switch (_activeHandle)
                {
                    case ResizeHandle.N:
                        newRect = new Rectangle(r.X, r.Y + dy, r.Width, r.Height - dy);
                        break;
                    case ResizeHandle.S:
                        newRect = new Rectangle(r.X, r.Y, r.Width, r.Height + dy);
                        break;
                    case ResizeHandle.W:
                        newRect = new Rectangle(r.X + dx, r.Y, r.Width - dx, r.Height);
                        break;
                    case ResizeHandle.E:
                        newRect = new Rectangle(r.X, r.Y, r.Width + dx, r.Height);
                        break;
                    case ResizeHandle.NW:
                        newRect = new Rectangle(r.X + dx, r.Y + dy, r.Width - dx, r.Height - dy);
                        break;
                    case ResizeHandle.NE:
                        newRect = new Rectangle(r.X, r.Y + dy, r.Width + dx, r.Height - dy);
                        break;
                    case ResizeHandle.SW:
                        newRect = new Rectangle(r.X + dx, r.Y, r.Width - dx, r.Height + dy);
                        break;
                    case ResizeHandle.SE:
                        newRect = new Rectangle(r.X, r.Y, r.Width + dx, r.Height + dy);
                        break;
                }
                if (newRect.Width < 1) newRect.Width = 1;
                if (newRect.Height < 1) newRect.Height = 1;
                _selectedObject.Rect = newRect;
                propertyGrid.Refresh();
                canvasPanel.Invalidate();
                return;
            }

            if (_isPlacing)
                canvasPanel.Invalidate();
        }

        private void CanvasPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_interaction != InteractionMode.None)
            {
                _interaction = InteractionMode.None;
                _activeHandle = ResizeHandle.None;
                return;
            }

            if (_isPlacing && e.Button == MouseButtons.Left && _selectedType.HasValue)
            {
                var end = _snapEnabled ? SnapPoint(e.Location, GetSnapSizeFor(_selectedType.Value)) : e.Location;
                var rect = GetCurrentRect(_placeStart, end);
                if (_snapEnabled)
                {
                    rect = SnapRect(rect, GetSnapSizeFor(_selectedType.Value));
                }
                if (rect.Width > 4 && rect.Height > 4)
                {
                    var obj = new PlacedObject { Type = _selectedType.Value, Rect = rect, RotationDegrees = _currentRotation };


                    if (string.IsNullOrWhiteSpace(obj.Name))
                    {
                        obj.Name = GenerateDefaultName(obj.Type);
                    }
                    _objects.Add(obj);
                    _selectedObject = obj;
                    propertyGrid.SelectedObject = _selectedObject;
                    canvasPanel.Invalidate();
                    RefreshPaletteList();
                }
            }
            _isPlacing = false;
        }

        private string GenerateDefaultName(ObjectType type)
        {
            string baseName = type switch
            {
                ObjectType.Room => "Room",
                ObjectType.Door => "Door",
                ObjectType.Window => "Window",
                ObjectType.Table => "Table",
                ObjectType.Chair => "Chair",
                _ => "Object"
            };
            int idx = 1;
            while (_objects.Exists(o => o.Type == type && string.Equals(o.Name, $"{baseName} {idx}", StringComparison.OrdinalIgnoreCase)))
            {
                idx++;
            }
            return $"{baseName} {idx}";
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
                case ObjectType.Room:
                    using (var pen = new Pen(Color.Black, 4)) DrawRotatedRectangle(g, pen, rect, obj.RotationDegrees);
                    if (!string.IsNullOrWhiteSpace(obj.Name))
                    {
                        using var f = new Font("Segoe UI", 10f, FontStyle.Bold);
                        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(obj.Name, f, Brushes.Black, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), format);
                    }
                    break;
                case ObjectType.Door:
                    // handled in a separate pass to ensure doors are drawn last
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

        private static void DrawDoorSymbol(Graphics g, Rectangle rect, float degrees)
        {
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(degrees);
            g.TranslateTransform(-center.X, -center.Y);

            using var erasePen = new Pen(Color.White, 6);
            using var pen = new Pen(Color.Black, 1);

            // Draw a horizontal bar symbol centered, rotation is handled by transform
            int y = rect.Top + rect.Height / 2;
            int x1 = rect.Left;
            int x2 = rect.Right;
            int barSize = Math.Min(rect.Height, 10);

            // erase underlying room edges along the symbol path
            g.DrawLine(erasePen, x1, y - barSize / 2, x1, y + barSize / 2); // left bar
            g.DrawLine(erasePen, x2, y - barSize / 2, x2, y + barSize / 2); // right bar
            g.DrawLine(erasePen, x1, y, x2, y); // center line

            // draw symbol
            g.DrawLine(pen, x1, y - barSize / 2, x1, y + barSize / 2);
            g.DrawLine(pen, x2, y - barSize / 2, x2, y + barSize / 2);
            g.DrawLine(pen, x1, y, x2, y);

            g.Restore(state);
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
