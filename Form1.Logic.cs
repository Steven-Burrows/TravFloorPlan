using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Linq;

namespace TravFloorPlan
{
    public partial class MainForm
    {
        private readonly Plan _plan = new Plan();
        private List<PlacedObject> _objects => _plan.Objects;

        private static ObjectGroup GetGroupForType(ObjectType type) => type.Group;
        private static ObjectType GetDefaultTypeForGroup(ObjectGroup group)
        {
            if (group == ObjectGroup.Rooms) return ObjectType.Room;
            if (group == ObjectGroup.Doorways) return ObjectType.Door;
            return ObjectType.Window;
        }

        private class PaletteEntry
        {
            public bool IsHeader { get; }
            public ObjectGroup HeaderGroup { get; }
            public PlacedObject? ObjectRef { get; }
            private readonly string _text;
            public PaletteEntry(ObjectGroup group)
            {
                IsHeader = true;
                HeaderGroup = group;
                _text = group.ToString();
            }
            public PaletteEntry(PlacedObject obj)
            {
                IsHeader = false;
                ObjectRef = obj;
                HeaderGroup = obj.Type.Group;
                var name = string.IsNullOrWhiteSpace(obj.Name) ? "<unnamed>" : obj.Name;
                _text = $"      - {name}";
            }
            public override string ToString() => _text;
        }

        private class TypeEntry
        {
            public ObjectType Type { get; }
            private readonly string _text;
            public TypeEntry(ObjectType type)
            {
                Type = type;
                _text = $"   • {type.Name}";
            }
            public override string ToString() => _text;
        }

        private PlacedObject? _selectedObject = null;
        private ObjectType? _selectedType = null;
        private bool _isPlacing = false;
        private Point _placeStart;
        private Point _lastMouse;
        private float _currentRotation = 0f;
        private int _gridSize = 20;
        private bool _snapEnabled = true;
        private enum InteractionMode { None, Move, Resize }
        private InteractionMode _interaction = InteractionMode.None;
        private Point _dragStart;
        private Rectangle _originalRect;
        private ResizeHandle _activeHandle = ResizeHandle.None;
        private const int HandleSize = 8;
        private enum ResizeHandle { None, N, S, E, W, NE, NW, SE, SW }
        private ContextMenuStrip _canvasMenu;
        private Panel _summaryPanel;
        private Label _summaryLabel;
        private PlacedObject? _clipboardObject;
        private float _zoom = 1f;
        private PointF _pan = new PointF(0, 0);
        private bool _isPanning = false;
        private Point _panStartScreen;
        private PointF _panStartOffset;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeFloorPlanUi()
        {
            paletteListBox.Items.Clear();
            paletteListBox.SelectedIndexChanged += (_, __) =>
            {
                if (paletteListBox.SelectedItem is PaletteEntry entry)
                {
                    if (entry.IsHeader)
                    {
                        _selectedType = GetDefaultTypeForGroup(entry.HeaderGroup);
                    }
                    else if (entry.ObjectRef != null)
                    {
                        _selectedObject = entry.ObjectRef;
                        propertyGrid.SelectedObject = _selectedObject;
                        canvasPanel.Invalidate();
                    }
                }
                else if (paletteListBox.SelectedItem is TypeEntry te)
                {
                    _selectedType = te.Type;
                }
            };
            canvasPanel.MouseDown += CanvasPanel_MouseDown;
            canvasPanel.MouseMove += CanvasPanel_MouseMove;
            canvasPanel.MouseUp += CanvasPanel_MouseUp;
            canvasPanel.MouseClick += CanvasPanel_MouseClick;
            canvasPanel.Paint += CanvasPanel_Paint;
            canvasPanel.MouseWheel += CanvasPanel_MouseWheel;
            canvasPanel.MouseEnter += (_, __) => canvasPanel.Focus();
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            _canvasMenu = new ContextMenuStrip();
            var copyItem = new ToolStripMenuItem("Copy", null, (_, __) => CopySelected());
            var pasteItem = new ToolStripMenuItem("Paste", null, (_, __) => PasteClipboard());
            var rotateLeftItem = new ToolStripMenuItem("Rotate Left 90", null, (_, __) => RotateSelected(-90f));
            var rotateRightItem = new ToolStripMenuItem("Rotate Right 90", null, (_, __) => RotateSelected(90f));
            var mirrorItem = new ToolStripMenuItem("Mirror (Triangle)", null, (_, __) => ToggleMirrorSelected());
            var deleteItem = new ToolStripMenuItem("Delete", null, (_, __) => DeleteSelected());
            var panItem = new ToolStripMenuItem("Pan", null, (_, __) => { paletteListBox.ClearSelected(); _selectedType = null; });
            _canvasMenu.Items.AddRange(new ToolStripItem[] { panItem, new ToolStripSeparator(), copyItem, pasteItem, new ToolStripSeparator(), rotateLeftItem, rotateRightItem, mirrorItem, new ToolStripSeparator(), deleteItem });
            canvasPanel.ContextMenuStrip = _canvasMenu;
            _canvasMenu.Opening += (_, e) =>
            {
                bool hasSelection = _selectedObject != null;
                copyItem.Enabled = hasSelection;
                pasteItem.Enabled = _clipboardObject != null;
                rotateLeftItem.Enabled = hasSelection;
                rotateRightItem.Enabled = hasSelection;
                mirrorItem.Enabled = hasSelection && _selectedObject!.Type == ObjectType.TriangularRoom;
                deleteItem.Enabled = hasSelection;
            };

            _summaryPanel = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(6) };
            _summaryLabel = new Label { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.TopLeft };
            _summaryPanel.Controls.Add(_summaryLabel);
            if (paletteListBox.Parent != null)
            {
                paletteListBox.Parent.Controls.Add(_summaryPanel);
                _summaryPanel.BringToFront();
            }

            RefreshPaletteList();
            UpdateSummaryPanel();
            propertyGrid.PropertyValueChanged += (_, __) => { RefreshPaletteList(); UpdateSummaryPanel(); };
        }

        private static IEnumerable<ObjectType> GetTypesForGroup(ObjectGroup group)
        {
            return ObjectType.AllTypes().Where(t => t.Group == group);
        }

        private void RefreshPaletteList()
        {
            var prevObj = _selectedObject;
            var prevType = _selectedType;
            paletteListBox.BeginUpdate();
            paletteListBox.Items.Clear();

            var groups = new[] { ObjectGroup.Rooms, ObjectGroup.Doorways, ObjectGroup.Others };
            foreach (var group in groups)
            {
                var header = new PaletteEntry(group);
                paletteListBox.Items.Add(header);

                foreach (var type in GetTypesForGroup(group))
                {
                    paletteListBox.Items.Add(new TypeEntry(type));

                    foreach (var obj in _objects)
                    {
                        if (obj.Type == type)
                        {
                            paletteListBox.Items.Add(new PaletteEntry(obj));
                        }
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
                        UpdateSummaryPanel();
                        return;
                    }
                }
            }
            if (prevType != null)
            {
                var prevGroup = prevType.Group;
                foreach (var item in paletteListBox.Items)
                {
                    if (item is PaletteEntry pe && pe.IsHeader && pe.HeaderGroup == prevGroup)
                    {
                        paletteListBox.SelectedItem = item;
                        UpdateSummaryPanel();
                        return;
                    }
                }
            }
            UpdateSummaryPanel();
        }

        private static float NormalizeAngle(float deg)
        {
            deg %= 360f;
            if (deg < 0) deg += 360f;
            return deg;
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
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelected();
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.V)
            {
                PasteClipboard();
                e.Handled = true;
                return;
            }
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

        private void CopySelected()
        {
            if (_selectedObject == null) return;
            _clipboardObject = CloneObject(_selectedObject);
        }

        private void PasteClipboard()
        {
            if (_clipboardObject == null) return;
            var obj = CloneObject(_clipboardObject);
            var r = obj.Rect;
            r.Offset(_gridSize, _gridSize);
            obj.Rect = r;
            obj.GridSizeForArea = _gridSize;
            if (string.IsNullOrWhiteSpace(obj.Name)) obj.Name = GenerateDefaultName(obj.Type);

            _objects.Add(obj);
            _selectedObject = obj;
            propertyGrid.SelectedObject = _selectedObject;
            canvasPanel.Invalidate();
            RefreshPaletteList();
            UpdateSummaryPanel();
        }

        private PlacedObject CloneObject(PlacedObject s)
        {
            return new PlacedObject
            {
                Type = s.Type,
                Rect = s.Rect,
                RotationDegrees = s.RotationDegrees,
                Name = s.Name,
                GridSizeForArea = s.GridSizeForArea,
                Mirrored = s.Mirrored,
                LineWidth = s.LineWidth,
                LineColor = s.LineColor,
                BackgroundColor = s.BackgroundColor
            };
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
                UpdateSummaryPanel();
            }
        }

        private void CanvasPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            // apply pan and zoom
            g.TranslateTransform(_pan.X, _pan.Y);
            g.ScaleTransform(_zoom, _zoom);

            // draw grid in world coordinates within visible bounds
            var worldLeft = (-_pan.X) / _zoom;
            var worldTop = (-_pan.Y) / _zoom;
            var worldWidth = canvasPanel.ClientSize.Width / _zoom;
            var worldHeight = canvasPanel.ClientSize.Height / _zoom;
            var worldBounds = new RectangleF(worldLeft, worldTop, worldWidth, worldHeight);
            DrawGridWorld(g, worldBounds, _gridSize);

            // keep area grid size in sync
            foreach (var o in _objects)
            {
                o.GridSizeForArea = _gridSize;
            }

            // draw non-door objects first
            foreach (var obj in _objects)
            {
                if (obj.Type != ObjectType.Door)
                {
                    DrawObject(g, obj, _gridSize);
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

            if (_isPlacing && _selectedType != null)
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
            var worldPoint = ScreenToWorld(e.Location);
            if (e.Button == MouseButtons.Left && _interaction == InteractionMode.None)
            {
                _selectedObject = HitTest(worldPoint);
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
                if (PointInObject(location, obj))
                    return obj;
            }
            return null;
        }

        private static bool PointInObject(Point p, PlacedObject obj)
        {
            using var path = CreateObjectPath(obj);
            return path.IsVisible(p);
        }

        private static GraphicsPath CreateObjectPath(PlacedObject obj)
        {
            var rect = obj.Rect;
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            using var basePath = obj.Type.CreateUnrotatedPath(rect, obj.Mirrored);
            var path = (GraphicsPath)basePath.Clone();
            using var m = new Matrix();
            m.RotateAt(obj.RotationDegrees, center);
            path.Transform(m);
            return path;
        }

        private static Point[] GetTrianglePoints(Rectangle rect, bool mirrored)
        {
            return ObjectType.GetTrianglePoints(rect, mirrored);
        }

        private void ToggleMirrorSelected()
        {
            if (_selectedObject == null) return;
            if (_selectedObject.Type != ObjectType.TriangularRoom) return;
            _selectedObject.Mirrored = !_selectedObject.Mirrored;
            propertyGrid.Refresh();
            canvasPanel.Invalidate();
        }

        private void CanvasPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                _isPanning = true;
                _panStartScreen = e.Location;
                _panStartOffset = _pan;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // If no placement type selected, allow left-drag panning when clicking empty space
                if (_selectedType == null)
                {
                    var wp = ScreenToWorld(e.Location);
                    var hit = HitTest(wp);
                    if (hit == null)
                    {
                        // clear selection and start pan
                        _selectedObject = null;
                        propertyGrid.SelectedObject = null;
                        _isPanning = true;
                        _panStartScreen = e.Location;
                        _panStartOffset = _pan;
                        return;
                    }
                }

                var worldPoint = _snapEnabled ? SnapPoint(ScreenToWorld(e.Location), GetSnapSizeFor(_selectedObject?.Type ?? _selectedType ?? ObjectType.Room)) : ScreenToWorld(e.Location);

                if (_selectedObject != null && Math.Abs(_selectedObject.RotationDegrees % 90f) < 0.001f)
                {
                    var handle = HitTestHandle(worldPoint, _selectedObject.Rect);
                    if (handle != ResizeHandle.None)
                    {
                        _interaction = InteractionMode.Resize;
                        _activeHandle = handle;
                        var snap = _snapEnabled ? GetSnapSizeFor(_selectedObject.Type) : 0;
                        _dragStart = _snapEnabled ? SnapPoint(worldPoint, snap) : worldPoint;
                        _originalRect = _selectedObject.Rect;
                        return;
                    }
                    else if (PointInObject(worldPoint, _selectedObject))
                    {
                        _interaction = InteractionMode.Move;
                        var snap = _snapEnabled ? GetSnapSizeFor(_selectedObject.Type) : 0;
                        _dragStart = _snapEnabled ? SnapPoint(worldPoint, snap) : worldPoint;
                        _originalRect = _selectedObject.Rect;
                        return;
                    }
                }

                if (_selectedType != null)
                {
                    _isPlacing = true;
                    var snap = _snapEnabled ? GetSnapSizeFor(_selectedType) : 0;
                    _placeStart = _snapEnabled ? SnapPoint(worldPoint, snap) : worldPoint;
                    _lastMouse = _placeStart;
                }
            }
        }

        private PointF ScreenToWorldF(Point p)
        {
            return new PointF((p.X - _pan.X) / _zoom, (p.Y - _pan.Y) / _zoom);
        }
        private Point ScreenToWorld(Point p)
        {
            var f = ScreenToWorldF(p);
            return new Point((int)Math.Round(f.X), (int)Math.Round(f.Y));
        }

        private void CanvasPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _pan = new PointF(_panStartOffset.X + (e.Location.X - _panStartScreen.X), _panStartOffset.Y + (e.Location.Y - _panStartScreen.Y));
                canvasPanel.Invalidate();
                return;
            }

            int snap = _snapEnabled
                ? (_interaction != InteractionMode.None && _selectedObject != null
                    ? GetSnapSizeFor(_selectedObject.Type)
                    : (_isPlacing && _selectedType != null ? GetSnapSizeFor(_selectedType) : _gridSize))
                : 0;
            var worldLoc = ScreenToWorld(e.Location);
            var loc = _snapEnabled ? SnapPoint(worldLoc, snap) : worldLoc;
            _lastMouse = loc;

            if (_interaction == InteractionMode.Move && _selectedObject != null)
            {
                int dx = loc.X - _dragStart.X;
                int dy = loc.Y - _dragStart.Y;
                var newRect = new Rectangle(_originalRect.X + dx, _originalRect.Y + dy, _originalRect.Width, _originalRect.Height);
                _selectedObject.Rect = newRect;
                propertyGrid.Refresh();
                canvasPanel.Invalidate();
                UpdateSummaryPanel();
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
                UpdateSummaryPanel();
                return;
            }

            if (_isPlacing)
                canvasPanel.Invalidate();
        }

        private void CanvasPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_isPanning && (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left))
            {
                _isPanning = false;
                return;
            }

            if (_interaction != InteractionMode.None)
            {
                _interaction = InteractionMode.None;
                _activeHandle = ResizeHandle.None;
                return;
            }

            if (_isPlacing && e.Button == MouseButtons.Left && _selectedType != null)
            {
                var endWorld = ScreenToWorld(e.Location);
                var end = _snapEnabled ? SnapPoint(endWorld, GetSnapSizeFor(_selectedType)) : endWorld;
                var rect = GetCurrentRect(_placeStart, end);
                if (_snapEnabled)
                {
                    rect = SnapRect(rect, GetSnapSizeFor(_selectedType));
                }
                if (rect.Width > 4 && rect.Height > 4)
                {
                    var obj = new PlacedObject { Type = _selectedType, Rect = rect, RotationDegrees = _currentRotation, GridSizeForArea = _gridSize };

                    if (string.IsNullOrWhiteSpace(obj.Name))
                    {
                        obj.Name = GenerateDefaultName(obj.Type);
                    }
                    _objects.Add(obj);
                    _selectedObject = obj;
                    propertyGrid.SelectedObject = _selectedObject;
                    canvasPanel.Invalidate();
                    RefreshPaletteList();
                    UpdateSummaryPanel();
                }
            }
            _isPlacing = false;
        }

        private void CanvasPanel_MouseWheel(object? sender, MouseEventArgs e)
        {
            float oldZoom = _zoom;
            float delta = e.Delta > 0 ? 1.1f : 0.9f;
            float newZoom = Math.Clamp(oldZoom * delta, 0.2f, 10f);
            if (Math.Abs(newZoom - oldZoom) < 0.0001f) return;

            var worldBefore = ScreenToWorldF(e.Location);
            _zoom = newZoom;
            _pan = new PointF(
                e.Location.X - _zoom * worldBefore.X,
                e.Location.Y - _zoom * worldBefore.Y
            );
            canvasPanel.Invalidate();
        }

        private int GetSnapSizeFor(ObjectType type) => type.GetSnapSize(_gridSize);

        private string GenerateDefaultName(ObjectType type)
        {
            string baseName = type.GetDefaultBaseName();
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
            if (grid <= 0) return p;
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

        private static void DrawObject(Graphics g, PlacedObject obj, int gridSize)
        {
            var rect = obj.Rect;
            Color semi = Color.FromArgb(120, obj.BackgroundColor);
            if (obj.Type == ObjectType.Room)
            {
                if (obj.BackgroundColor.A > 0)
                    FillRotatedRectangle(g, new SolidBrush(semi), rect, obj.RotationDegrees);
                using (var pen = new Pen(obj.LineColor, Math.Max(1f, obj.LineWidth)))
                    DrawRotatedRectangle(g, pen, rect, obj.RotationDegrees);
                DrawRoomText(g, obj, rect, gridSize);
            }
            else if (obj.Type == ObjectType.CircularRoom)
            {
                if (obj.BackgroundColor.A > 0)
                    FillRotatedEllipse(g, new SolidBrush(semi), rect, obj.RotationDegrees);
                using (var penC = new Pen(obj.LineColor, Math.Max(1f, obj.LineWidth)))
                    DrawRotatedEllipse(g, penC, rect, obj.RotationDegrees);
                DrawRoomText(g, obj, rect, gridSize);
            }
            else if (obj.Type == ObjectType.TriangularRoom)
            {
                if (obj.BackgroundColor.A > 0)
                    FillRotatedTriangle(g, new SolidBrush(semi), rect, obj.RotationDegrees, obj.Mirrored);
                using (var penT = new Pen(obj.LineColor, Math.Max(1f, obj.LineWidth)))
                    DrawRotatedTriangle(g, penT, rect, obj.RotationDegrees, obj.Mirrored);
                DrawRoomText(g, obj, rect, gridSize);
            }
            else if (obj.Type == ObjectType.Door)
            {
                // handled later
            }
            else if (obj.Type == ObjectType.Window)
            {
                using (var brush = new SolidBrush(Color.FromArgb(120, Color.LightSkyBlue))) FillRotatedRectangle(g, brush, rect, obj.RotationDegrees);
                using (var pen2 = new Pen(Color.DeepSkyBlue)) DrawRotatedRectangle(g, pen2, rect, obj.RotationDegrees);
            }
            else if (obj.Type == ObjectType.Table)
            {
                using (var brush2 = new SolidBrush(Color.FromArgb(120, Color.Peru))) FillRotatedRectangle(g, brush2, rect, obj.RotationDegrees);
                using (var pen3 = new Pen(Color.SaddleBrown)) DrawRotatedRectangle(g, pen3, rect, obj.RotationDegrees);
            }
            else if (obj.Type == ObjectType.Chair)
            {
                using (var brush3 = new SolidBrush(Color.FromArgb(120, Color.DarkOliveGreen))) FillRotatedRectangle(g, brush3, rect, obj.RotationDegrees);
                using (var pen4 = new Pen(Color.Olive)) DrawRotatedRectangle(g, pen4, rect, obj.RotationDegrees);
            }
        }

        private static void DrawRoomText(Graphics g, PlacedObject obj, Rectangle rect, int gridSize)
        {
            if (string.IsNullOrWhiteSpace(obj.Name)) return;
            using var nameFont = new Font("Segoe UI", 10f, FontStyle.Bold);
            using var areaFont = new Font("Segoe UI", 8f, FontStyle.Regular);
            float areaUnits = obj.Type.ComputeAreaUnits(rect, gridSize);
            string areaText = $"{areaUnits:0.#}";
            var nameSize = g.MeasureString(obj.Name, nameFont);
            var areaSize = g.MeasureString(areaText, areaFont);
            float totalH = nameSize.Height + areaSize.Height;
            float startY = rect.Top + rect.Height / 2f - totalH / 2f;
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var nameRect = new RectangleF(rect.Left, startY, rect.Width, nameSize.Height);
            var areaRect = new RectangleF(rect.Left, startY + nameSize.Height, rect.Width, areaSize.Height);
            g.DrawString(obj.Name, nameFont, Brushes.Black, nameRect, format);
            g.DrawString(areaText, areaFont, Brushes.Black, areaRect, format);
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

            int y = rect.Top + rect.Height / 2;
            int x1 = rect.Left;
            int x2 = rect.Right;
            int barSize = Math.Min(rect.Height, 10);

            // erase underlying room edges along the symbol path
            g.DrawLine(erasePen, x1, y - barSize / 2, x1, y + barSize / 2);
            g.DrawLine(erasePen, x2, y - barSize / 2, x2, y + barSize / 2);
            g.DrawLine(erasePen, x1, y, x2, y);

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

        private static void DrawRotatedEllipse(Graphics g, Pen pen, Rectangle rect, float degrees)
        {
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(degrees);
            g.TranslateTransform(-center.X, -center.Y);
            g.DrawEllipse(pen, rect);
            g.Restore(state);
        }

        private static void FillRotatedEllipse(Graphics g, Brush brush, Rectangle rect, float degrees)
        {
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(degrees);
            g.TranslateTransform(-center.X, -center.Y);
            g.FillEllipse(brush, rect);
            g.Restore(state);
        }

        private static void DrawRotatedTriangle(Graphics g, Pen pen, Rectangle rect, float degrees, bool mirrored)
        {
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(degrees);
            g.TranslateTransform(-center.X, -center.Y);
            var pts = GetTrianglePoints(rect, mirrored);
            g.DrawPolygon(pen, pts);
            g.Restore(state);
        }

        private static void FillRotatedTriangle(Graphics g, Brush brush, Rectangle rect, float degrees, bool mirrored)
        {
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(degrees);
            g.TranslateTransform(-center.X, -center.Y);
            var pts = GetTrianglePoints(rect, mirrored);
            g.FillPolygon(brush, pts);
            g.Restore(state);
        }

        private static void DrawGridWorld(Graphics g, RectangleF worldBounds, int grid)
        {
            using var pen = new Pen(Color.Gainsboro);
            float left = (float)Math.Floor(worldBounds.Left / grid) * grid;
            float right = (float)Math.Ceiling(worldBounds.Right / grid) * grid;
            float top = (float)Math.Floor(worldBounds.Top / grid) * grid;
            float bottom = (float)Math.Ceiling(worldBounds.Bottom / grid) * grid;
            for (float x = left; x <= right; x += grid)
                g.DrawLine(pen, x, top, x, bottom);
            for (float y = top; y <= bottom; y += grid)
                g.DrawLine(pen, left, y, right, y);
        }

        // High-performance drawing surface to reduce flicker
        private class DoubleBufferedPanel : Panel
        {
            public DoubleBufferedPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                this.UpdateStyles();
            }
            protected override void OnPaintBackground(PaintEventArgs e)
            {
                // Avoid background erase to reduce flicker; we clear in our paint pipeline
            }
        }

        private void UpdateSummaryPanel()
        {
            if (_summaryLabel == null) return;
            int roomsCount = 0, doorwaysCount = 0, othersCount = 0;
            double totalRoomArea = 0;
            foreach (var o in _objects)
            {
                if (o.Type.Group == ObjectGroup.Rooms)
                {
                    roomsCount++;
                    totalRoomArea += o.Type.ComputeAreaUnits(o.Rect, _gridSize);
                }
                else if (o.Type.Group == ObjectGroup.Doorways)
                {
                    doorwaysCount++;
                }
                else
                {
                    othersCount++;
                }
            }
            _summaryLabel.Text =
                "Summary\r\n" +
                $"Rooms: {roomsCount}    Doorways: {doorwaysCount}    Others: {othersCount}\r\n" +
                $"Total room area (grid): {totalRoomArea:0.#}";
        }
    }
}
