namespace Cuku.Geo.Filter
{
	using Sirenix.OdinInspector;
	using Sirenix.OdinInspector.Editor;
	using System;
	using System.Collections.Generic;
	using System.Reflection;

	public class RelationMemberFilterDrawer : OdinAttributeProcessor<RelationMemberFilter>
	{
		public override void ProcessChildMemberAttributes(
			InspectorProperty parentProperty,
			MemberInfo member,
			List<Attribute> attributes)
		{
			if (member.Name != "Tags") return;

			attributes.Add(new InlineEditorAttribute());
		}
	}
}