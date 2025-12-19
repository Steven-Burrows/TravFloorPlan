using System;
using System.ComponentModel;
using System.Drawing;

namespace TravFloorPlan
{
    public class PlacedObject
    {
        [Browsable(false)]
        public ObjectType Type { get; set; }
        public Rectangle Rect { get; set; }
        public float RotationDegrees { get; set; }
        public string? Name { get; set; }

        [Browsable(false)]
        public int GridSizeForArea { get; set; }

        [Browsable(true)]
        [DisplayName("Mirror (triangle)")]
        public bool Mirrored { get; set; }

        [Browsable(true)]
        [DisplayName("Line Width")]
        public float LineWidth { get; set; } = 4f;

        [Browsable(true)]
        [DisplayName("Line Color")]
        public Color LineColor { get; set; } = Color.Black;

        [Browsable(true)]
        [DisplayName("Background Color")]
        public Color BackgroundColor { get; set; } = Color.Transparent;

        [Browsable(true)]
        [ReadOnly(true)]
        [DisplayName("Area (grid)")]
        public float AreaGridUnits
        {
            get
            {
                int g = GridSizeForArea > 0 ? GridSizeForArea : 1;
                float w = Rect.Width / (float)g;
                float h = Rect.Height / (float)g;
                if (Type == ObjectType.Room)
                    return w * h;
                if (Type == ObjectType.CircularRoom)
                    return (float)(System.Math.PI * 0.25 * w * h);
                if (Type == ObjectType.TriangleRight || Type == ObjectType.TriangleIso)
                    return 0.5f * w * h;
                return w * h;
            }
        }
    }
}
