using System.Collections.Generic;

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
    }
}
