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
        private readonly Stack<List<PlacedObject>> _undoStack = new Stack<List<PlacedObject>>();
        private List<PlacedObject>? _pendingUndoSnapshot;

        private void PushUndo(List<PlacedObject>? snapshot = null)
        {
            // Snapshot clone of current objects unless a pre-captured snapshot is provided
            _undoStack.Push(snapshot ?? CloneObjectsSnapshot());
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            var snapshot = _undoStack.Pop();
            _objects.Clear();
            _objects.AddRange(snapshot);
            _selectedObject = null;
            propertyGrid.SelectedObject = null;
            canvasPanel.Invalidate();
            RefreshPaletteList();
            UpdateSummaryPanel();
        }


        private PlacedObject? _selectedObject = null;
        private ObjectTypeBase? _selectedType = null;
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
        private TreeView _paletteTree;
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
            // Initialize TreeView-based palette
            _paletteTree ??= new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false
            };
            if (_paletteTree.Parent == null && paletteListBox.Parent != null)
            {
                paletteListBox.Parent.Controls.Add(_paletteTree);
                _paletteTree.BringToFront();
                paletteListBox.Visible = false;
            }
            _paletteTree.Nodes.Clear();
            _paletteTree.AfterSelect -= PaletteTree_AfterSelect;
            _paletteTree.AfterSelect += PaletteTree_AfterSelect;

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
            var undoItem = new ToolStripMenuItem("Undo", null, (_, __) => Undo());
            var copyItem = new ToolStripMenuItem("Copy", null, (_, __) => CopySelected());
            var pasteItem = new ToolStripMenuItem("Paste", null, (_, __) => PasteClipboard());
            var rotateLeftItem = new ToolStripMenuItem("Rotate Left 90", null, (_, __) => RotateSelected(-90f));
            var rotateRightItem = new ToolStripMenuItem("Rotate Right 90", null, (_, __) => RotateSelected(90f));
            var mirrorItem = new ToolStripMenuItem("Mirror (Triangle)", null, (_, __) => ToggleMirrorSelected());
            var deleteItem = new ToolStripMenuItem("Delete", null, (_, __) => DeleteSelected());
            var panItem = new ToolStripMenuItem("Pan", null, (_, __) => { if (_paletteTree != null) _paletteTree.SelectedNode = null; _selectedType = null; });
            _canvasMenu.Items.AddRange(new ToolStripItem[] { panItem, new ToolStripSeparator(), undoItem, new ToolStripSeparator(), copyItem, pasteItem, new ToolStripSeparator(), rotateLeftItem, rotateRightItem, mirrorItem, new ToolStripSeparator(), deleteItem });
            canvasPanel.ContextMenuStrip = _canvasMenu;
            _canvasMenu.Opening += (_, e) =>
            {
                bool hasSelection = _selectedObject != null;
                undoItem.Enabled = _undoStack.Count > 0;
                copyItem.Enabled = hasSelection;
                pasteItem.Enabled = _clipboardObject != null;
                rotateLeftItem.Enabled = hasSelection;
                rotateRightItem.Enabled = hasSelection;
                mirrorItem.Enabled = hasSelection && (_selectedObject!.Type == ObjectTypes.TriangleRight || _selectedObject!.Type == ObjectTypes.TriangleIso);
                deleteItem.Enabled = hasSelection;
            };

            // Add Undo to main menu under an Edit menu if available
            if (this.MainMenuStrip != null)
            {
                // Find existing Edit menu or create one
                ToolStripMenuItem? editMenu = null;
                foreach (ToolStripItem item in this.MainMenuStrip.Items)
                {
                    if (item is ToolStripMenuItem mi && string.Equals(mi.Text, "Edit", StringComparison.OrdinalIgnoreCase))
                    {
                        editMenu = mi;
                        break;
                    }
                }
                if (editMenu == null)
                {
                    editMenu = new ToolStripMenuItem("Edit");
                    this.MainMenuStrip.Items.Add(editMenu);
                }
                var undoMainItem = new ToolStripMenuItem("Undo", null, (_, __) => Undo());
                editMenu.DropDownOpening += (_, __) => { undoMainItem.Enabled = _undoStack.Count > 0; };
                editMenu.DropDownItems.Add(undoMainItem);
            }

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
            // Capture snapshot at the start of a manual property edit
            propertyGrid.SelectedGridItemChanged += (_, __) =>
            {
                _pendingUndoSnapshot = CloneObjectsSnapshot();
            };
            // Push undo when a property value is changed manually
            propertyGrid.PropertyValueChanged += (_, __) =>
            {
                if (_pendingUndoSnapshot != null)
                {
                    PushUndo(_pendingUndoSnapshot);
                    _pendingUndoSnapshot = null;
                }
                RefreshPaletteList();
                UpdateSummaryPanel();
            };
        }

        private static IEnumerable<ObjectTypeBase> GetTypesForGroup(ObjectGroup group)
        {
            return ObjectTypes.AllTypes().Where(t => t.Group == group);
        }

        private void RefreshPaletteList()
        {
            var prevObj = _selectedObject;
            var prevType = _selectedType;

            if (_paletteTree == null) return;

            _paletteTree.BeginUpdate();
            _paletteTree.Nodes.Clear();

            var groups = new[] { ObjectGroup.Rooms, ObjectGroup.Doorways, ObjectGroup.Others };
            foreach (var group in groups)
            {
                var groupNode = new TreeNode(group.ToString()) { Tag = group };
                foreach (var type in GetTypesForGroup(group))
                {
                    var typeNode = new TreeNode(type.Name) { Tag = type };
                    foreach (var obj in _objects.Where(o => o.Type == type))
                    {
                        var name = string.IsNullOrWhiteSpace(obj.Name) ? "<unnamed>" : obj.Name;
                        var objNode = new TreeNode(name) { Tag = obj };
                        typeNode.Nodes.Add(objNode);
                    }
                    groupNode.Nodes.Add(typeNode);
                }
                _paletteTree.Nodes.Add(groupNode);
            }
            _paletteTree.ExpandAll();
            _paletteTree.EndUpdate();

            // Restore selection if possible
            if (prevObj != null)
            {
                var node = FindNodeByTag(_paletteTree.Nodes, prevObj);
                if (node != null) _paletteTree.SelectedNode = node;
            }
            else if (prevType != null)
            {
                var node = FindNodeByTag(_paletteTree.Nodes, prevType);
                if (node != null) _paletteTree.SelectedNode = node;
            }
            UpdateSummaryPanel();
        }

        private void PaletteTree_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            switch (e.Node?.Tag)
            {
                case ObjectTypeBase type:
                    _selectedType = type;
                    _selectedObject = null;
                    propertyGrid.SelectedObject = null;
                    break;
                case PlacedObject obj:
                    _selectedObject = obj;
                    _selectedType = null;
                    propertyGrid.SelectedObject = obj;
                    break;
                default:
                    _selectedType = null;
                    _selectedObject = null;
                    propertyGrid.SelectedObject = null;
                    break;
            }
            canvasPanel.Invalidate();
            UpdateSummaryPanel();
        }

        private static TreeNode? FindNodeByTag(TreeNodeCollection nodes, object tag)
        {
            foreach (TreeNode n in nodes)
            {
                if (Equals(n.Tag, tag)) return n;
                var child = FindNodeByTag(n.Nodes, tag);
                if (child != null) return child;
            }
            return null;
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
            if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
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
            PushUndo();
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
                BackgroundColor = s.BackgroundColor,
                HideNorthSide = s.HideNorthSide,
                HideEastSide = s.HideEastSide,
                HideSouthSide = s.HideSouthSide,
                HideWestSide = s.HideWestSide
            };
        }

        private void DeleteSelected()
        {
            if (_selectedObject != null)
            {
                PushUndo();
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
                if (obj.Type != ObjectTypes.Door)
                {
                    DrawObject(g, obj, _gridSize);
                }
            }
            // draw doors last
            foreach (var obj in _objects)
            {
                if (obj.Type == ObjectTypes.Door)
                {
                    DrawObject(g, obj, _gridSize);
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

        private void ToggleMirrorSelected()
        {
            if (_selectedObject == null) return;
            if (_selectedObject.Type != ObjectTypes.TriangleRight && _selectedObject.Type != ObjectTypes.TriangleIso) return;
            PushUndo();
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

                var worldPoint = _snapEnabled ? SnapPoint(ScreenToWorld(e.Location), GetSnapSizeFor(_selectedObject?.Type ?? _selectedType ?? ObjectTypes.Room)) : ScreenToWorld(e.Location);

                if (_selectedObject != null)
                {
                    bool axisAligned = Math.Abs(_selectedObject.RotationDegrees % 90f) < 0.001f;
                    if (axisAligned)
                    {
                        var handle = HitTestHandle(worldPoint, _selectedObject.Rect);
                        if (handle != ResizeHandle.None)
                        {
                            PushUndo();
                            _interaction = InteractionMode.Resize;
                            _activeHandle = handle;
                            var snap = _snapEnabled ? GetSnapSizeFor(_selectedObject.Type) : 0;
                            _dragStart = _snapEnabled ? SnapPoint(worldPoint, snap) : worldPoint;
                            _originalRect = _selectedObject.Rect;
                            return;
                        }
                    }

                    if (PointInObject(worldPoint, _selectedObject))
                    {
                        PushUndo();
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
                PushUndo();
                _interaction = InteractionMode.None;
                _activeHandle = ResizeHandle.None;
                return;
            }

            if (_isPlacing && e.Button == MouseButtons.Left && _selectedType != null)
            {
                PushUndo();
                var endWorld = ScreenToWorld(e.Location);
                var end = _snapEnabled ? SnapPoint(endWorld, GetSnapSizeFor(_selectedType)) : endWorld;
                var rect = GetCurrentRect(_placeStart, end);
                if (_snapEnabled)
                {
                    rect = SnapRect(rect, GetSnapSizeFor(_selectedType));
                }
                // Force door/opening size to 1x1 grid units
                if (_selectedType == ObjectTypes.Door || _selectedType == ObjectTypes.Opening)
                {
                    rect = new Rectangle(end.X, end.Y, _gridSize, _gridSize);
                }
                if (rect.Width > 4 && rect.Height > 4)
                {
                    var obj = new PlacedObject { Type = _selectedType, Rect = rect, RotationDegrees = _currentRotation, GridSizeForArea = _gridSize };
                    if (obj.LineWidth <= 0)
                    {
                        obj.LineWidth = obj.Type.DefaultLineWidth;
                    }

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

        private int GetSnapSizeFor(ObjectTypeBase type) => type.GetSnapSize(_gridSize);

        private string GenerateDefaultName(ObjectTypeBase type)
        {
            if (type.Group == ObjectGroup.Rooms)
            {
                int roomsCount = _objects.Count(o => o.Type.Group == ObjectGroup.Rooms);
                return $"{roomsCount + 1}";
            }

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
            if (obj.Type == ObjectTypes.Room)
            {
                DrawRoomObject(g, obj, gridSize);
                return;
            }
            if (obj.Type == ObjectTypes.TriangleRight || obj.Type == ObjectTypes.TriangleIso)
            {
                DrawTriangleRoomObject(g, obj, gridSize);
                return;
            }

            var rect = obj.Rect;
            Color semi = Color.FromArgb(120, obj.BackgroundColor);

            // Allow the type to handle drawing fully
            if (obj.Type.DrawCustom(g, obj, gridSize))
            {
                return;
            }

            // Generic draw using type path
            using var basePath = obj.Type.CreateUnrotatedPath(rect, obj.Mirrored);
            using var path = (GraphicsPath)basePath.Clone();
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            using var m = new Matrix();
            m.RotateAt(obj.RotationDegrees, center);
            path.Transform(m);

            if (obj.BackgroundColor.A > 0)
            {
                using var brush = new SolidBrush(semi);
                g.FillPath(brush, path);
            }
            float strokeWidth = Math.Max(1f, obj.LineWidth);
            using (var pen = new Pen(obj.LineColor, strokeWidth))
             {
                 g.DrawPath(pen, path);
             }

            if (obj.Type.Group == ObjectGroup.Rooms)
            {
                DrawRoomText(g, obj, rect, gridSize);
            }
        }

        private static void DrawRoomObject(Graphics g, PlacedObject obj, int gridSize)
        {
            var rect = obj.Rect;
            Color semi = Color.FromArgb(120, obj.BackgroundColor);

            // Allow Room type to provide a custom draw (currently it does not)
            if (obj.Type.DrawCustom(g, obj, gridSize))
            {
                DrawRoomText(g, obj, rect, gridSize);
                return;
            }

            using var basePath = obj.Type.CreateUnrotatedPath(rect, obj.Mirrored);
            using var path = (GraphicsPath)basePath.Clone();
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            using var m = new Matrix();
            m.RotateAt(obj.RotationDegrees, center);
            path.Transform(m);

            if (obj.BackgroundColor.A > 0)
            {
                using var brush = new SolidBrush(semi);
                g.FillPath(brush, path);
            }

            float strokeWidth = Math.Max(1f, obj.LineWidth);
            using var pen = new Pen(obj.LineColor, strokeWidth);

            if (!obj.HasHiddenRoomSide)
            {
                g.DrawPath(pen, path);
            }
            else
            {
                var corners = new PointF[]
                {
                    new PointF(rect.Left, rect.Top), // North-West
                    new PointF(rect.Right, rect.Top), // North-East
                    new PointF(rect.Right, rect.Bottom), // South-East
                    new PointF(rect.Left, rect.Bottom) // South-West
                };
                m.TransformPoints(corners);
                if (!obj.HideNorthSide) g.DrawLine(pen, corners[0], corners[1]);
                if (!obj.HideEastSide) g.DrawLine(pen, corners[1], corners[2]);
                if (!obj.HideSouthSide) g.DrawLine(pen, corners[2], corners[3]);
                if (!obj.HideWestSide) g.DrawLine(pen, corners[3], corners[0]);
            }

            DrawRoomText(g, obj, rect, gridSize);
        }

        private static void DrawTriangleRoomObject(Graphics g, PlacedObject obj, int gridSize)
        {
            var rect = obj.Rect;
            Color semi = Color.FromArgb(120, obj.BackgroundColor);

            Point[] basePoints = obj.Type == ObjectTypes.TriangleRight
                ? ObjectTypes.ObjectTypeHelpers.GetTrianglePoints(rect, obj.Mirrored)
                : ObjectTypes.ObjectTypeHelpers.GetTriangleIsoPoints(rect, obj.Mirrored);

            using var path = new GraphicsPath();
            path.AddPolygon(basePoints);
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            using var m = new Matrix();
            m.RotateAt(obj.RotationDegrees, center);
            path.Transform(m);

            if (obj.BackgroundColor.A > 0)
            {
                using var brush = new SolidBrush(semi);
                g.FillPath(brush, path);
            }

            var drawPoints = basePoints.Select(p => new PointF(p.X, p.Y)).ToArray();
            m.TransformPoints(drawPoints);

            float strokeWidth = Math.Max(1f, obj.LineWidth);
            using var pen = new Pen(obj.LineColor, strokeWidth);
            var hideEdges = GetTriangleEdgeHideFlags(obj);
            for (int i = 0; i < 3; i++)
            {
                if (hideEdges[i]) continue;
                int next = (i + 1) % 3;
                g.DrawLine(pen, drawPoints[i], drawPoints[next]);
            }

            DrawRoomText(g, obj, rect, gridSize);
        }

        private static bool[] GetTriangleEdgeHideFlags(PlacedObject obj)
        {
            var hide = new bool[3];
            if (obj.Type == ObjectTypes.TriangleRight)
            {
                if (!obj.Mirrored)
                {
                    hide[0] = obj.HideWestSide;   // vertical edge on the left
                    hide[1] = obj.HideNorthSide;  // horizontal top edge
                    hide[2] = obj.HideSouthSide;  // diagonal edge
                }
                else
                {
                    hide[0] = obj.HideEastSide;   // vertical edge on the right
                    hide[1] = obj.HideNorthSide;  // horizontal top edge
                    hide[2] = obj.HideSouthSide;  // diagonal edge
                }
            }
            else // TriangleIso
            {
                if (!obj.Mirrored)
                {
                    hide[0] = obj.HideWestSide;   // left diagonal
                    hide[1] = obj.HideEastSide;   // right diagonal
                    hide[2] = obj.HideSouthSide;  // bottom edge
                }
                else
                {
                    hide[0] = obj.HideWestSide;   // left diagonal (flipped)
                    hide[1] = obj.HideEastSide;   // right diagonal (flipped)
                    hide[2] = obj.HideNorthSide;  // top edge
                }
            }
            return hide;
        }

        // Helper so type implementations can reuse grid drawing
        public static void Logic_DrawGrid(Graphics g, RectangleF worldBounds, int grid)
        {
            DrawGridWorld(g, worldBounds, grid);
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
                $"Rooms: {roomsCount}" + Environment.NewLine +
                $"Total room area (grid): {totalRoomArea:0.#}";
        }

        private List<PlacedObject> CloneObjectsSnapshot()
        {
            var snapshot = new List<PlacedObject>(_objects.Count);
            foreach (var o in _objects)
            {
                snapshot.Add(CloneObject(o));
            }
            return snapshot;
        }
    }
}
