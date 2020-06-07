namespace Cuku.Geo.Filter
{
	using Sirenix.OdinInspector;
	using Sirenix.OdinInspector.Editor;
	using System;
	using System.Collections.Generic;
	using System.Reflection;

	public class TagFilterDrawer : OdinAttributeProcessor<TagFilter>
	{
		public override void ProcessChildMemberAttributes(
			InspectorProperty parentProperty,
			MemberInfo member,
			List<Attribute> attributes)
		{
			attributes.Add(new InlineEditorAttribute());
			attributes.Add(new HorizontalGroupAttribute("Tags"));
		}
	}
}