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
        protected ObjectTypeBase(string name, ObjectGroup group)
        {
            Name = name;
            Group = group;
        }
        public override string ToString() => Name;

        public abstract int GetSnapSize(int gridSize);
        public abstract string GetDefaultBaseName();
        public abstract float ComputeAreaUnits(Rectangle rect, int gridSize);
        public abstract GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored);
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
    }

    // Others
    public sealed class WindowType : ObjectTypeBase
    {
        public static readonly WindowType Instance = new WindowType();
        private WindowType() : base("Window", ObjectGroup.Others) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Window";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize) => 0f;
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
        }
    }

    public sealed class TableType : ObjectTypeBase
    {
        public static readonly TableType Instance = new TableType();
        private TableType() : base("Table", ObjectGroup.Others) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Table";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize) => 0f;
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
        }
    }

    public sealed class ChairType : ObjectTypeBase
    {
        public static readonly ChairType Instance = new ChairType();
        private ChairType() : base("Chair", ObjectGroup.Others) { }
        public override int GetSnapSize(int gridSize) => gridSize;
        public override string GetDefaultBaseName() => "Chair";
        public override float ComputeAreaUnits(Rectangle rect, int gridSize) => 0f;
        public override GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            path.AddRectangle(rect);
            return path;
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
