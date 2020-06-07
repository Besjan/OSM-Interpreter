namespace Cuku.Geo.Filter
{
	using Sirenix.OdinInspector;
	using Sirenix.OdinInspector.Editor;
	using System;
	using System.Collections.Generic;
	using System.Reflection;

	public class FeatureFilterDrawer : OdinAttributeProcessor<FeatureFilter>
	{
		public override void ProcessChildMemberAttributes(
			InspectorProperty parentProperty,
			MemberInfo member,
			List<Attribute> attributes)
		{
			if (member.Name == "Name") return;

			attributes.Add(new InlineEditorAttribute());
		}
	}
}