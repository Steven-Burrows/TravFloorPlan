namespace TravFloorPlan
{
    public class ObjectGroup
    {
        public string Name { get; }
        private ObjectGroup(string name) { Name = name; }
        public override string ToString() => Name;

        public static readonly ObjectGroup Rooms = new ObjectGroup("Rooms");
        public static readonly ObjectGroup Doorways = new ObjectGroup("Doorways");
        public static readonly ObjectGroup Others = new ObjectGroup("Others");
    }
}
