using MessagePack;

namespace Cuku.Osm
{
    [MessagePackObject]
    public class Element
    {
        [Key(0)]
        public string Role { get; set; }
        [Key(1)]
        public Type Type { get; set; }
        [Key(2)]
        public Tag[] Tags { get; set; }
        [Key(3)]
        public Element[] Elements { get; set; }
        [Key(4)]
        public Point[] Points { get; set; }
    }

    public enum Type
    {
        Point = 0,
        Line = 1,
        Feature = 2
    }

    public class Tag
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class Point
    {
        public long X { get; set; }
        public long Y { get; set; }
    }
}
