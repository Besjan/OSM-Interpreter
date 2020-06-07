namespace Cuku.Geo.Filter
{
    public class FeatureFilter : Sirenix.OdinInspector.SerializedScriptableObject
	{
        public string Name;
        public TagFilter Tags;
        public RelationMemberFilter[] RelationMembers;
    }
}
