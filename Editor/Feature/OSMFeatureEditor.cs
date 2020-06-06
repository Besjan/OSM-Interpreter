namespace Cuku.Geo
{
    using System.Collections.Generic;
    using UnityEditor;
    using System.IO;
    using System.Linq;
    using OsmSharp.Streams;
    using OsmSharp;
    using ProjNet.CoordinateSystems.Transformations;
    using ProjNet.CoordinateSystems;
    using OsmSharp.Tags;
    using MessagePack;
    using Sirenix.OdinInspector.Editor;
    using Sirenix.Utilities.Editor;
    using Sirenix.OdinInspector;
    using Sirenix.Utilities;
	using Cuku.Utilities;

	public class OSMFeatureEditor : OdinEditorWindow
    {
        #region Editor
        [MenuItem("Cuku/OSM/Feature Editor", priority = 1)]
        private static void OpenWindow()
        {
            var window = GetWindow<OSMFeatureEditor>();
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(700, 700);
        }

        [PropertySpace, InlineEditor, Required]
        public OSMFeatureConfig FeatureConfig;

        static ICoordinateTransformation CoordinateTransformation;

        private bool IsConfigValid()
        {
            return FeatureConfig != null;
        }
		#endregion

		#region Actions
		[ShowIf("IsConfigValid"), PropertySpace(20), Button(ButtonSizes.Large)]
		public void ExtractFeaturesData()
		{
			SetCoordinateTransformation();

			var centerPoint = TransformPoint(FeatureConfig.CenterCoordinates);

			// Filter
			var boundaryFlter = new FeatureFilter
			{
                Name = "Boundary",
				Tags = new TagFilter
				{
					AllOfTags = new Tag[]
					 {
						 new Tag { Key = "type", Value = "boundary" },
						 new Tag { Key = "boundary", Value = "administrative" },
						 new Tag { Key = "admin_level", Value = "4" }
					 },
					NoneOfTags = new Tag[] { }
				},
				RelationMembers = new RelationMemberFilter[]
				{
					new RelationMemberFilter
					{
						Type = Type.Line,
						Role = "outer",
						Tags = new TagFilter
						{
							AllOfTags = new Tag[]
							{
								new Tag { Key = "boundary", Value = "administrative" },
								new Tag { Key = "admin_level", Value = "4" }
							},
							NoneOfTags = new Tag[]
							{
								new Tag { Key = "description", Value = "Berlin Exclave" }
							}
						}
					}
				}
			};

			using (var fileStream = File.OpenRead(FeatureConfig.CityOSMData.GetPathInStreamingAssets()))
			{
				var source = new PBFOsmStreamSource(fileStream);

				var filtered = from osmGeo in source
							   where osmGeo.Type == OsmGeoType.Node ||
							   osmGeo.Type == OsmGeoType.Way ||
							   (osmGeo.Type == OsmGeoType.Relation && osmGeo.Tags != null && Match(osmGeo.Tags, boundaryFlter.Tags))
							   select osmGeo;

				var complete = filtered.ToComplete();

				var relationsCompleted = complete.Where(x => x.Type == OsmGeoType.Relation).ToArray();
				var relations = new Relation[relationsCompleted.Length];
				var lines = new List<Line>();

				UnityEngine.Debug.Log(relationsCompleted.Length);

				for (int r = 0; r < relations.Length; r++)
				{
					// Get RelationMembers
					foreach (OsmGeo osmgeo in filtered)
					{
						var osmRelation = osmgeo as OsmSharp.Relation;
						if (osmRelation == null || osmRelation.Id != relationsCompleted[r].Id) continue;

						var relationMembers = GetMembers(filtered.ToArray(), osmRelation, boundaryFlter.RelationMembers);
						UnityEngine.Debug.Log(relationMembers.Length);

						var relation = new Relation
						{
							Id = osmRelation.Id.Value,
							Type = Type.Relation,
							Tags = boundaryFlter.Tags.AllOfTags,
							Members = relationMembers
						};

						relations[r] = relation;
					}

					var relationMemberIds = relations[r].Members.Select(rm => rm.Id);

					// Get Ids
					Dictionary<long, long[]> memberNodes = new Dictionary<long, long[]>();
					List<long> nodeIds = new List<long>();
					foreach (OsmGeo osmgeo in filtered)
					{
						var way = osmgeo as Way;
						if (way == null || !relationMemberIds.Contains(way.Id.Value)) continue;

						memberNodes.Add(way.Id.Value, way.Nodes);
						nodeIds.AddRange(way.Nodes);
					}

					// Get Nodes
					List<Node> nodes = new List<Node>();
					foreach (OsmGeo osmGeo in filtered)
					{
						var node = osmGeo as Node;
						if (node == null || !nodeIds.Contains(node.Id.Value)) continue;
						nodes.Add(node);
					}

					// Add Relation Member Points
					for (int m = 0; m < relations[r].Members.Length; m++)
					{
						var member = relations[r].Members[m];
						var points = new List<Point>();
						var memberNodeIds = memberNodes.FirstOrDefault(mn => mn.Key == member.Id).Value;
						for (int ni = 0; ni < memberNodeIds.Length; ni++)
						{
							var node = nodes.FirstOrDefault(n => n.Id.Value == memberNodeIds[ni]);
							points.Add(ToPoint(node, centerPoint));
						}

						var line = new Line
						{
							Id = member.Id,
							Points = points.ToArray()
						};

						lines.Add(line);
					}
				}

				var feature = new Feature
				{
					Relations = relations,
					Lines = lines.ToArray()
				};

				var bytes = MessagePackSerializer.Serialize(feature);

				var boundaryDataPath = Path.Combine(FeatureConfig.FeaturesDataPath.GetPathInStreamingAssets(), boundaryFlter.Name + FeatureConfig.GeoFormat);
				File.WriteAllBytes(boundaryDataPath, bytes);
			}
		}
		#endregion

		void SetCoordinateTransformation()
        {
            // Source Coordinate System use by OSM
            var sourceCS = GeographicCoordinateSystem.WGS84;

            // Target Coordinate System used by LiDAR data (Berlin)
            // https://epsg.io/25833
            var ETRS89_EPSG25833 = "PROJCS[\"ETRS89 / UTM zone 33N\", GEOGCS[\"ETRS89\", DATUM[\"European_Terrestrial_Reference_System_1989\", SPHEROID[\"GRS 1980\", 6378137, 298.257222101, AUTHORITY[\"EPSG\", \"7019\"]], TOWGS84[0, 0, 0, 0, 0, 0, 0], AUTHORITY[\"EPSG\", \"6258\"]], PRIMEM[\"Greenwich\", 0, AUTHORITY[\"EPSG\", \"8901\"]], UNIT[\"degree\", 0.0174532925199433, AUTHORITY[\"EPSG\", \"9122\"]], AUTHORITY[\"EPSG\", \"4258\"]], PROJECTION[\"Transverse_Mercator\"], PARAMETER[\"latitude_of_origin\", 0], PARAMETER[\"central_meridian\", 15], PARAMETER[\"scale_factor\", 0.9996], PARAMETER[\"false_easting\", 500000], PARAMETER[\"false_northing\", 0], UNIT[\"metre\", 1, AUTHORITY[\"EPSG\", \"9001\"]], AXIS[\"Easting\", EAST], AXIS[\"Northing\", NORTH], AUTHORITY[\"EPSG\", \"25833\"]]";
            var cf = new CoordinateSystemFactory();
            var targetCS = cf.CreateFromWkt(ETRS89_EPSG25833);

            CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();
            CoordinateTransformation = ctfac.CreateFromCoordinateSystems(sourceCS, targetCS);
        }

        bool Match(TagsCollectionBase osmGeoTags, TagFilter tagFilter)
        {
            var allOfTags = tagFilter.AllOfTags;
            var noneOfTags = tagFilter.NoneOfTags;

            // Doesn't match if it doesn't contain all of tags
            for (int i = 0; i < allOfTags.Length; i++)
            {
                if (!osmGeoTags.Contains(allOfTags[i].Key, allOfTags[i].Value))
                {
                    return false;
                }
            }

            // Doesn't match if it contains at least none of tags
            for (int i = 0; i < noneOfTags.Length; i++)
            {
                if (osmGeoTags.Contains(noneOfTags[i].Key, noneOfTags[i].Value))
                {
                    return false;
                }
            }

            return true;
        }

        RelationMember[] GetMembers(OsmGeo[] osmGeo, OsmSharp.Relation osmRelation, RelationMemberFilter[] memberFilters)
        {
            var members = new List<RelationMember>();

            for (int mf = 0; mf < memberFilters.Length; mf++)
            {
                var filter = memberFilters[mf];

                var osmMembers = osmRelation.Members
                    .Where(m => GetGeoType(m.Type) == filter.Type && m.Role == filter.Role).ToArray();

                for (int m = 0; m < osmMembers.Length; m++)
                {
                    var osmMember = osmMembers[m];

                    var osmMemberTags = osmGeo.FirstOrDefault(geo => geo.Id.Value == osmMember.Id).Tags;
                    var tagsMatch = Match(osmMemberTags, filter.Tags);

                    if (!tagsMatch)
                    {
                        UnityEngine.Debug.Log("Tags don't match: " + osmMember.Id);
                        continue;
                    }

                    var member = new RelationMember
                    {
                        Id = osmMember.Id,
                        Type = GetGeoType(osmMember.Type),
                        Role = osmMember.Role
                    };

                    members.Add(member);
                }
            }

            UnityEngine.Debug.Log(members.Count);

            return members.ToArray();
        }

        Point ToPoint(Node node, double[] center)
        {
            var point = new double[] { node.Longitude.Value, node.Latitude.Value };
            var transformPoint = TransformPoint(point);

            return new Point
            {
                X = transformPoint[0] - center[0],
                Y = transformPoint[1] - center[1]
            };
        }

        double[] TransformPoint(double[] point)
        {
            return CoordinateTransformation.MathTransform.Transform(point);
        }

        Type GetGeoType(OsmGeoType osmGeoType)
        {
            switch (osmGeoType)
            {
                case OsmGeoType.Node:
                    return Type.Point;
                case OsmGeoType.Way:
                    return Type.Line;
                case OsmGeoType.Relation:
                    return Type.Relation;
                default:
                    return Type.Point;
            }
        }
    }
}
