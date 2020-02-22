namespace Cuku.Geo
{
    public class GeoFilter
    {
        public TagFilter Tags { get; set; }
        public RelationMemberFilter[] RelationMembers { get; set; }
    }

    public class RelationMemberFilter
    {
        public Type Type { get; set; }
        public string Role { get; set; }
        public TagFilter Tags { get; set; }
    }

    public class TagFilter
    {
        public Tag[] AllOfTags { get; set; }
        public Tag[] NoneOfTags { get; set; }
    }
}
