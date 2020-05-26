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

    public static class ExtractFeatures
    {
        const string cityDataPath = "Assets/StreamingAssets/Data/berlin.pbf";
        const string boundaryDataPath = "Assets/StreamingAssets/Data/boundary.cuk";

        /* https://franzpc.com/apps/coordinate-converter-utm-to-geographic-latitude-longitude.html
        Berlin center point in decimal degrees
        Map Datum: WGS 84
        Zone: 33
        Hemisphere: N
        Easting (UTMX): 392000
        Northing (UTMY): 5820000
        Click: Convert Standard UTM 
        Use only 6 decimal points */

        static double[] centerPointGeo = new double[] { 13.408275, 52.519395 };
        static double[] centerPoint;

        static ICoordinateTransformation trans;

        static void Initialize()
        {
            // Source Coordinate System use by OSM
            var sourceCS = GeographicCoordinateSystem.WGS84;

            // Target Coordinate System used by LiDAR data (Berlin)
            // https://epsg.io/25833
            var ETRS89_EPSG25833 = "PROJCS[\"ETRS89 / UTM zone 33N\", GEOGCS[\"ETRS89\", DATUM[\"European_Terrestrial_Reference_System_1989\", SPHEROID[\"GRS 1980\", 6378137, 298.257222101, AUTHORITY[\"EPSG\", \"7019\"]], TOWGS84[0, 0, 0, 0, 0, 0, 0], AUTHORITY[\"EPSG\", \"6258\"]], PRIMEM[\"Greenwich\", 0, AUTHORITY[\"EPSG\", \"8901\"]], UNIT[\"degree\", 0.0174532925199433, AUTHORITY[\"EPSG\", \"9122\"]], AUTHORITY[\"EPSG\", \"4258\"]], PROJECTION[\"Transverse_Mercator\"], PARAMETER[\"latitude_of_origin\", 0], PARAMETER[\"central_meridian\", 15], PARAMETER[\"scale_factor\", 0.9996], PARAMETER[\"false_easting\", 500000], PARAMETER[\"false_northing\", 0], UNIT[\"metre\", 1, AUTHORITY[\"EPSG\", \"9001\"]], AXIS[\"Easting\", EAST], AXIS[\"Northing\", NORTH], AUTHORITY[\"EPSG\", \"25833\"]]";
            var cf = new CoordinateSystemFactory();
            var targetCS = cf.CreateFromWkt(ETRS89_EPSG25833);

            CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();
            trans = ctfac.CreateFromCoordinateSystems(sourceCS, targetCS);

            centerPoint = centerPointGeo.TransformPoint();
        }

        [MenuItem("Cuku/OSM/Extract Features")]
        static void ExtractFeaturesData()
        {
            Initialize();

            // Filter
            var boundaryFlter = new GeoFilter
            {
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

            using (var fileStream = File.OpenRead(cityDataPath))
            {
                var source = new PBFOsmStreamSource(fileStream);

                var filtered = from osmGeo in source
                               where osmGeo.Type == OsmGeoType.Node ||
                               osmGeo.Type == OsmGeoType.Way ||
                               (osmGeo.Type == OsmGeoType.Relation && osmGeo.Tags != null && osmGeo.Tags.Match(boundaryFlter.Tags))
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

                        var relationMembers = filtered.ToArray().GetMembers(osmRelation, boundaryFlter.RelationMembers);
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
                            var point = nodes.FirstOrDefault(n => n.Id.Value == memberNodeIds[ni]).ToPoint(centerPoint);
                            points.Add(point);
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
                File.WriteAllBytes(boundaryDataPath, bytes);
            }
        }

        static bool Match(this TagsCollectionBase osmGeoTags, TagFilter tagFilter)
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

        static RelationMember[] GetMembers(this OsmGeo[] osmGeo, OsmSharp.Relation osmRelation, RelationMemberFilter[] memberFilters)
        {
            var members = new List<RelationMember>();

            for (int mf = 0; mf < memberFilters.Length; mf++)
            {
                var filter = memberFilters[mf];

                var osmMembers = osmRelation.Members
                    .Where(m => m.Type.GetGeoType() == filter.Type && m.Role == filter.Role).ToArray();

                for (int m = 0; m < osmMembers.Length; m++)
                {
                    var osmMember = osmMembers[m];

                    var osmMemberTags = osmGeo.FirstOrDefault(geo => geo.Id.Value == osmMember.Id).Tags;
                    var tagsMatch = osmMemberTags.Match(filter.Tags);

                    if (!tagsMatch)
                    {
                        UnityEngine.Debug.Log("Tags don't match: " + osmMember.Id);
                        continue;
                    }

                    var member = new RelationMember
                    {
                        Id = osmMember.Id,
                        Type = osmMember.Type.GetGeoType(),
                        Role = osmMember.Role
                    };

                    members.Add(member);
                }
            }

            UnityEngine.Debug.Log(members.Count);

            return members.ToArray();
        }

        static Point ToPoint(this Node node, double[] center)
        {
            var transformPoint = (new double[] { node.Longitude.Value, node.Latitude.Value }).TransformPoint();

            return new Point
            {
                X = transformPoint[0] - center[0],
                Y = transformPoint[1] - center[1]
            };
        }

        static double[] TransformPoint(this double[] point)
        {
            return trans.MathTransform.Transform(point);
        }

        static Type GetGeoType(this OsmGeoType osmGeoType)
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
