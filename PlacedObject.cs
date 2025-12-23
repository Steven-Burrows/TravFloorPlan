using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace TravFloorPlan
{
    public class PlacedObject
    {
        private static readonly string[] RoomSidePropertyNames = new[]
        {
            nameof(HideNorthSide),
            nameof(HideEastSide),
            nameof(HideSouthSide),
            nameof(HideWestSide)
        };

        static PlacedObject()
        {
            var parentProvider = TypeDescriptor.GetProvider(typeof(PlacedObject));
            TypeDescriptor.AddProvider(new PlacedObjectTypeDescriptionProvider(parentProvider), typeof(PlacedObject));
        }

        [Browsable(false)]
        public ObjectTypeBase Type
        {
            get => _type;
            set
            {
                _type = value;
                if (!SupportsRoomSides)
                {
                    _hideNorthSide = _hideEastSide = _hideSouthSide = _hideWestSide = false;
                }
            }
        }
        private ObjectTypeBase _type = ObjectTypes.Room;
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
        public float LineWidth { get; set; }

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
                if (Type == ObjectTypes.Room)
                    return w * h;
                if (Type == ObjectTypes.CircularRoom)
                    return (float)(System.Math.PI * 0.25 * w * h);
                if (Type == ObjectTypes.TriangleRight || Type == ObjectTypes.TriangleIso)
                    return 0.5f * w * h;
                return w * h;
            }
        }

        [Browsable(true)]
        [Category("Room Sides")]
        [DisplayName("Hide North Side")]
        public bool HideNorthSide
        {
            get => SupportsRoomSides && _hideNorthSide;
            set
            {
                if (!SupportsRoomSides)
                {
                    _hideNorthSide = false;
                    return;
                }
                _hideNorthSide = value;
            }
        }

        [Browsable(true)]
        [Category("Room Sides")]
        [DisplayName("Hide East Side")]
        public bool HideEastSide
        {
            get => SupportsRoomSides && _hideEastSide;
            set
            {
                if (!SupportsRoomSides)
                {
                    _hideEastSide = false;
                    return;
                }
                _hideEastSide = value;
            }
        }

        [Browsable(true)]
        [Category("Room Sides")]
        [DisplayName("Hide South Side")]
        public bool HideSouthSide
        {
            get => SupportsRoomSides && _hideSouthSide;
            set
            {
                if (!SupportsRoomSides)
                {
                    _hideSouthSide = false;
                    return;
                }
                _hideSouthSide = value;
            }
        }

        [Browsable(true)]
        [Category("Room Sides")]
        [DisplayName("Hide West Side")]
        public bool HideWestSide
        {
            get => SupportsRoomSides && _hideWestSide;
            set
            {
                if (!SupportsRoomSides)
                {
                    _hideWestSide = false;
                    return;
                }
                _hideWestSide = value;
            }
        }

        internal bool HasHiddenRoomSide => SupportsRoomSides && (_hideNorthSide || _hideEastSide || _hideSouthSide || _hideWestSide);

        private bool SupportsRoomSides =>
            Type.Group == ObjectGroup.Rooms && Type != ObjectTypes.CircularRoom;
        private bool _hideNorthSide;
        private bool _hideEastSide;
        private bool _hideSouthSide;
        private bool _hideWestSide;

        private sealed class PlacedObjectTypeDescriptionProvider : TypeDescriptionProvider
        {
            public PlacedObjectTypeDescriptionProvider(TypeDescriptionProvider parent)
                : base(parent)
            {
            }

            public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance)
            {
                var baseDescriptor = base.GetTypeDescriptor(objectType, instance);
                if (instance is PlacedObject placed)
                {
                    return new PlacedObjectCustomTypeDescriptor(baseDescriptor, placed);
                }
                return baseDescriptor;
            }
        }

        private sealed class PlacedObjectCustomTypeDescriptor : CustomTypeDescriptor
        {
            private readonly PlacedObject _instance;

            public PlacedObjectCustomTypeDescriptor(ICustomTypeDescriptor parent, PlacedObject instance)
                : base(parent)
            {
                _instance = instance;
            }

            public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
            {
                var props = base.GetProperties(attributes);
                if (_instance.SupportsRoomSides)
                {
                    return props;
                }

                var filtered = props.Cast<PropertyDescriptor>()
                    .Where(p => !RoomSidePropertyNames.Contains(p.Name, StringComparer.Ordinal))
                    .ToArray();
                return new PropertyDescriptorCollection(filtered, true);
            }

            public override PropertyDescriptorCollection GetProperties() => GetProperties(null);
        }
    }
}
