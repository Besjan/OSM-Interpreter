using MessagePack;

namespace Cuku.Geo
{
    [MessagePackObject]
    public class Feature
    {
        [Key(0)]
        public Relation[] Relations { get; set; }
        [Key(1)]
        public Line[] Lines { get; set; }
    }

    [Union(0, typeof(Point))]
    [Union(1, typeof(Line))]
    [Union(3, typeof(Relation))]
    [MessagePackObject]
    public abstract class Geo
    {
        [Key(0)]
        public long Id { get; set; }
        [Key(1)]
        public Type Type { get; set; }
        [Key(2)]
        public Tag[] Tags { get; set; }
    }

    [MessagePackObject]
    public class Point : Geo
    {
        [Key(3)]
        public double X { get; set; }
        [Key(4)]
        public double Y { get; set; }
    }

    [MessagePackObject]
    public class Line : Geo
    {
        [Key(3)]
        public Point[] Points { get; set; }
    }

    [MessagePackObject]
    public class Relation : Geo
    {
        [Key(3)]
        public RelationMember[] Members { get; set; }
    }

    [MessagePackObject]
    public class RelationMember
    {
        [Key(0)]
        public long Id { get; set; }
        [Key(1)]
        public Type Type { get; set; }
        [Key(2)]
        public string Role { get; set; }
    }

    [MessagePackObject]
    public class Tag
    {
        [Key(0)]
        public string Key { get; set; }
        [Key(1)]
        public string Value { get; set; }
    }

    public enum Type
    {
        Point = 0,
        Line = 1,
        Relation = 2
    }
}
