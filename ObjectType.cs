using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TravFloorPlan
{
    public class ObjectType
    {
        public string Name { get; }
        public ObjectGroup Group { get; }
        private ObjectType(string name, ObjectGroup group) { Name = name; Group = group; }
        public override string ToString() => Name;

        public static readonly ObjectType Room = new ObjectType("Room", ObjectGroup.Rooms);
        public static readonly ObjectType CircularRoom = new ObjectType("CircularRoom", ObjectGroup.Rooms);
        public static readonly ObjectType TriangularRoom = new ObjectType("TriangularRoom", ObjectGroup.Rooms);
        public static readonly ObjectType Door = new ObjectType("Door", ObjectGroup.Doorways);
        public static readonly ObjectType Window = new ObjectType("Window", ObjectGroup.Others);
        public static readonly ObjectType Table = new ObjectType("Table", ObjectGroup.Others);
        public static readonly ObjectType Chair = new ObjectType("Chair", ObjectGroup.Others);

        public static IEnumerable<ObjectType> AllTypes()
        {
            yield return Room; yield return CircularRoom; yield return TriangularRoom;
            yield return Door; yield return Window; yield return Table; yield return Chair;
        }

        public int GetSnapSize(int gridSize)
        {
            return this == Door ? Math.Max(1, gridSize / 2) : gridSize;
        }

        public string GetDefaultBaseName()
        {
            if (this == Room || this == CircularRoom || this == TriangularRoom) return "Room";
            if (this == Door) return "Door";
            if (this == Window) return "Window";
            if (this == Table) return "Table";
            if (this == Chair) return "Chair";
            return "Object";
        }

        public float ComputeAreaUnits(Rectangle rect, int gridSize)
        {
            if (gridSize <= 0) gridSize = 1;
            float w = rect.Width / (float)gridSize;
            float h = rect.Height / (float)gridSize;
            if (this == Room) return w * h;
            if (this == CircularRoom) return (float)(Math.PI * 0.25 * w * h);
            if (this == TriangularRoom) return 0.5f * w * h;
            return 0f;
        }

        public GraphicsPath CreateUnrotatedPath(Rectangle rect, bool mirrored)
        {
            var path = new GraphicsPath();
            if (this == Room)
            {
                path.AddRectangle(rect);
            }
            else if (this == CircularRoom)
            {
                path.AddEllipse(rect);
            }
            else if (this == TriangularRoom)
            {
                var pts = GetTrianglePoints(rect, mirrored);
                path.AddPolygon(pts);
            }
            else
            {
                path.AddRectangle(rect);
            }
            return path;
        }

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
    }
}
