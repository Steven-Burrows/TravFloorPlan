using System;
using System.Collections.Generic;

namespace TravFloorPlan
{
    // Public DTOs for robust JSON serialization
    public class PlanDto
    {
        public List<PlacedObjectDto> Objects { get; set; } = new();
    }

    public class PlacedObjectDto
    {
        public string? Name { get; set; }
        public string Type { get; set; } = "Room";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float RotationDegrees { get; set; }
        public bool Mirrored { get; set; }
        public float LineWidth { get; set; }
        public int LineColorArgb { get; set; }
        public int BackgroundColorArgb { get; set; }
    }
}
