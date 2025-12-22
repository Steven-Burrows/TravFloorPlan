using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TravFloorPlan
{
    public abstract class ObjectTypeBase
    {
        public string Name { get; }
        public ObjectGroup Group { get; }
        public float DefaultLineWidth { get; }
        protected ObjectTypeBase(string name, ObjectGroup group, float defaultLineWidth = 2f)
        {
            Name = name;
            Group = group;
            DefaultLineWidth = defaultLineWidth;
        }
        public override string ToString() => Name;

        public abstract int GetSnapSize(int gridSize);
        public abstract string GetDefaultBaseName();
        public abstract float ComputeAreaUnits(Rectangle rect, int gridSize);
        public abstract GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored);

        // Allow types to provide custom drawing beyond generic path fill/stroke
        public virtual bool DrawCustom(Graphics g, PlacedObject obj, int gridSize)
        {
            return false; // default: not handled
        }
    }

    // Room types
    public sealed class RoomType : ObjectTypeBase
    {
        public static readonly RoomType Instance = new RoomType();
        private RoomType() : base("Room", ObjectGroup.Rooms) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Room";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize)
        {
            if (gridSize <= 0) gridSize = 1;
            return (rect.Width / (float)gridSize) * (rect.Height / (float)gridSize);
        }
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
        }
    }

    public sealed class CircularRoomType : ObjectTypeBase
    {
        public static readonly CircularRoomType Instance = new CircularRoomType();
        private CircularRoomType() : base("CircularRoom", ObjectGroup.Rooms) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Room";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize)
        {
            if (gridSize <= 0) gridSize = 1;
            float w = rect.Width / (float)gridSize;
            float h = rect.Height / (float)gridSize;
            return (float)(Math.PI * 0.25 * w * h);
        }
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddEllipse(rect);
            return path;
        }
    }

    public sealed class TriangleRightType : ObjectTypeBase
    {
        public static readonly TriangleRightType Instance = new TriangleRightType();
        private TriangleRightType() : base("TriangleRight", ObjectGroup.Rooms) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Room";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize)
        {
            if (gridSize <= 0) gridSize = 1;
            float w = rect.Width / (float)gridSize;
            float h = rect.Height / (float)gridSize;
            return 0.5f * w * h;
        }
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            var pts = ObjectTypes.ObjectTypeHelpers.GetTrianglePoints(rect, mirrored);
            path.AddPolygon(pts);
            return path;
        }
    }

    public sealed class TriangleIsoType : ObjectTypeBase
    {
        public static readonly TriangleIsoType Instance = new TriangleIsoType();
        private TriangleIsoType() : base("TriangleIso", ObjectGroup.Rooms) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Room";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize)
        {
            if (gridSize <= 0) gridSize = 1;
            float w = rect.Width / (float)gridSize;
            float h = rect.Height / (float)gridSize;
            return 0.5f * w * h;
        }
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            var pts = ObjectTypes.ObjectTypeHelpers.GetTriangleIsoPoints(rect, mirrored);
            path.AddPolygon(pts);
            return path;
        }
    }

    // Doorways
    public sealed class DoorType : ObjectTypeBase
    {
        public static readonly DoorType Instance = new DoorType();
        private DoorType() : base("Door", ObjectGroup.Doorways) { }
        public override int GetSnapSize(int gridSize) => Math.Max(1, gridSize / 2);
        public override string GetDefaultBaseName() => "Door";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize) => 0f;
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
        }
        public override bool DrawCustom(Graphics g, PlacedObject obj, int gridSize)
        {
            // Draw door symbol using eraser and black lines
            var rect = obj.Rect;
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(obj.RotationDegrees);
            g.TranslateTransform(-center.X, -center.Y);

            using var erasePen = new Pen(Color.White, 6);
            using var pen = new Pen(Color.Black, 1);

            int y = rect.Top + rect.Height / 2;
            int x1 = rect.Left;
            int x2 = rect.Right;
            int barSize = Math.Min(rect.Height, 10);

            g.DrawLine(erasePen, x1, y - barSize / 2, x1, y + barSize / 2);
            g.DrawLine(erasePen, x2, y - barSize / 2, x2, y + barSize / 2);
            g.DrawLine(erasePen, x1, y, x2, y);

            g.DrawLine(pen, x1, y - barSize / 2, x1, y + barSize / 2);
            g.DrawLine(pen, x2, y - barSize / 2, x2, y + barSize / 2);
            g.DrawLine(pen, x1, y, x2, y);

            g.Restore(state);
            return true;
        }
    }

    public sealed class OpeningType : ObjectTypeBase
    {
        public static readonly OpeningType Instance = new OpeningType();
        private OpeningType() : base("Opening", ObjectGroup.Doorways) { }
        public override int GetSnapSize(int gridSize) => Math.Max(1, gridSize / 2);
        public override string GetDefaultBaseName() => "Opening";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize) => 0f;
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
        }
        public override bool DrawCustom(Graphics g, PlacedObject obj, int gridSize)
        {
            // Erase underlying lines within a small band, then redraw grid inside opening bounds
            var rect = obj.Rect;
            var mid = new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            using var erasePen = new Pen(Color.White, Math.Max(6, obj.LineWidth));
            var state = g.Save();
            var c = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            g.TranslateTransform(c.X, c.Y);
            g.RotateTransform(obj.RotationDegrees);
            g.TranslateTransform(-c.X, -c.Y);
            int y = mid.Y;
            int x1 = rect.Left;
            int x2 = rect.Right;
            int barSize = Math.Min(rect.Height, 10);
            g.DrawLine(erasePen, x1, y - barSize / 2, x1, y + barSize / 2);
            g.DrawLine(erasePen, x2, y - barSize / 2, x2, y + barSize / 2);
            g.DrawLine(erasePen, x1, y, x2, y);
            g.Restore(state);

            // Clip and redraw grid lines within opening rectangle
            var clipState = g.Save();
            g.SetClip(new RectangleF(rect.Left, rect.Top, rect.Width, rect.Height), CombineMode.Replace);
            MainForm.Logic_DrawGrid(g, new RectangleF(rect.Left, rect.Top, rect.Width, rect.Height), gridSize);
            g.Restore(clipState);
            return true;
        }
    }

    // Others
    public sealed class WindowType : ObjectTypeBase
    {
        public static readonly WindowType Instance = new WindowType();
        private WindowType() : base("Window", ObjectGroup.Others, 0f) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Window";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize) => 0f;
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
        }
        public override bool DrawCustom(Graphics g, PlacedObject obj, int gridSize)
        {
            var rect = obj.Rect;
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(obj.RotationDegrees);
            g.TranslateTransform(-center.X, -center.Y);
            // Determine translucent fill color
            Color fill = obj.BackgroundColor.A == 0 ? Color.FromArgb(120, Color.LightSkyBlue) : Color.FromArgb(120, obj.BackgroundColor);
            obj.BackgroundColor = fill;
            using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, rect);
            if (obj.LineWidth > 0)
            {
                using (var pen = new Pen(obj.LineColor, obj.LineWidth)) g.DrawRectangle(pen, rect);
            }
            g.Restore(state);
            return true;
        }
    }

    public sealed class TableType : ObjectTypeBase
    {
        public static readonly TableType Instance = new TableType();
        private TableType() : base("Table", ObjectGroup.Others, 0f) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Table";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize) => 0f;
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
        }
        public override bool DrawCustom(Graphics g, PlacedObject obj, int gridSize)
        {
            var rect = obj.Rect;
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(obj.RotationDegrees);
            g.TranslateTransform(-center.X, -center.Y);
            // Determine translucent fill color
            Color fill = obj.BackgroundColor.A == 0 ? Color.FromArgb(120, Color.Peru) : Color.FromArgb(120, obj.BackgroundColor);
            obj.BackgroundColor = fill;
            using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, rect);
            if (obj.LineWidth > 0)
            {
                using (var pen = new Pen(obj.LineColor, obj.LineWidth)) g.DrawRectangle(pen, rect);
            }
            g.Restore(state);
            return true;
        }
    }

    public sealed class ChairType : ObjectTypeBase
    {
        public static readonly ChairType Instance = new ChairType();
        private ChairType() : base("Chair", ObjectGroup.Others, 0f) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Chair";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize) => 0f;
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
        }
        public override bool DrawCustom(Graphics g, PlacedObject obj, int gridSize)
        {
            var rect = obj.Rect;
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(obj.RotationDegrees);
            g.TranslateTransform(-center.X, -center.Y);
            // Determine translucent fill color
            Color fill = obj.BackgroundColor.A == 0 ? Color.FromArgb(120, Color.DarkOliveGreen) : Color.FromArgb(120, obj.BackgroundColor);
            obj.BackgroundColor = fill;
            using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, rect);
            if (obj.LineWidth > 0)
            {
                using (var pen = new Pen(obj.LineColor, obj.LineWidth)) g.DrawRectangle(pen, rect);
            }
            g.Restore(state);
            return true;
        }
    }

    public static class ObjectTypes
    {
        public static readonly ObjectTypeBase Room = RoomType.Instance;
        public static readonly ObjectTypeBase CircularRoom = CircularRoomType.Instance;
        public static readonly ObjectTypeBase TriangleRight = TriangleRightType.Instance;
        public static readonly ObjectTypeBase TriangleIso = TriangleIsoType.Instance;
        public static readonly ObjectTypeBase Door = DoorType.Instance;
        public static readonly ObjectTypeBase Opening = OpeningType.Instance;
        public static readonly ObjectTypeBase Window = WindowType.Instance;
        public static readonly ObjectTypeBase Table = TableType.Instance;
        public static readonly ObjectTypeBase Chair = ChairType.Instance;

        public static IEnumerable<ObjectTypeBase> AllTypes()
        {
            yield return Room;
            yield return CircularRoom;
            yield return TriangleRight;
            yield return TriangleIso;
            yield return Door;
            yield return Opening;
            yield return Window;
            yield return Table;
            yield return Chair;
        }

        public static ObjectTypeBase? FromName(string name)
        {
            foreach (var t in AllTypes())
            {
                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }

        public static class ObjectTypeHelpers
        {
            public static Point[] GetTrianglePoints(Rectangle rect, bool mirrored)
            {
                if (!mirrored)
                {
                    return new[]
                    {
                        new Point(rect.Left, rect.Bottom),
                        new Point(rect.Left, rect.Top),
                        new Point(rect.Right, rect.Top)
                    };
                }
                else
                {
                    return new[]
                    {
                        new Point(rect.Right, rect.Bottom),
                        new Point(rect.Right, rect.Top),
                        new Point(rect.Left, rect.Top)
                    };
                }
            }
            public static Point[] GetTriangleIsoPoints(Rectangle rect, bool mirrored)
            {
                int midX = rect.Left + rect.Width / 2;
                if (!mirrored)
                {
                    return new[]
                    {
                        new Point(rect.Left, rect.Bottom),
                        new Point(midX, rect.Top),
                        new Point(rect.Right, rect.Bottom)
                    };
                }
                else
                {
                    return new[]
                    {
                        new Point(rect.Left, rect.Top),
                        new Point(midX, rect.Bottom),
                        new Point(rect.Right, rect.Top)
                    };
                }
            }
        }
    }
}
