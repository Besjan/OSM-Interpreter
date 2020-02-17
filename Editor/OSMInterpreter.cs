namespace Cuku.Geo
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using System.IO;
    using System.Linq;
    using OsmSharp.Streams;
    using OsmSharp;
    using ProjNet.CoordinateSystems.Transformations;
    using ProjNet.CoordinateSystems;
    using OsmSharp.Tags;
    using MessagePack;

    public static class OSMInterpreter
    {
        static string SourceDataPath = "Assets/StreamingAssets/Data/berlin.pbf";

        static double[] centerPointGeo = new double[] { 13.408297, 52.519461 };
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

        static bool ContainsAllTags(this TagsCollectionBase osmGeoTags, Tag[] tags)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                if (!osmGeoTags.Contains(tags[i].Key, tags[i].Value))
                {
                    return false;
                }
            }

            return true;
        }

        static RelationMember[] GetMembers(this OsmSharp.Relation osmRelation, string[] roles)
        {
            var members = new List<RelationMember>();

            for (int r = 0; r < roles.Length; r++)
            {
                var osmMember = osmRelation.Members.Where(m => m.Role == roles[r]).ToArray();
                for (int m = 0; m < osmMember.Length; m++)
                {
                    var member = new RelationMember
                    {
                        Id = osmMember[m].Id,
                        Type = osmMember[m].Type.GetGeoType(),
                        Role = osmMember[m].Role
                    };

                    members.Add(member);
                }
            }

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

        [MenuItem("Cuku/OSM/Extract City Border")]
        static void ExtractCityBorder()
        {
            Initialize();

            var filterRelation = new Relation
            {
                Tags = new Tag[]
                 {
                     new Tag { Key = "type", Value = "boundary" },
                     new Tag { Key = "admin_level", Value = "4" }
                 },

                Members = new RelationMember[]
                {
                    new RelationMember
                    {
                        Type = Type.Line,
                        Role = "outer" 
                    }
                }
            };

            using (var fileStream = File.OpenRead(SourceDataPath))
            {
                var source = new PBFOsmStreamSource(fileStream);

                var filtered = from osmGeo in source
                    where osmGeo.Type == OsmGeoType.Node ||
                    osmGeo.Type == OsmGeoType.Way ||
                    (osmGeo.Type == OsmGeoType.Relation && osmGeo.Tags != null && osmGeo.Tags.ContainsAllTags(filterRelation.Tags))
                    select osmGeo;

                var complete = filtered.ToComplete();

                var relationsCompleted = complete.Where(x => x.Type == OsmGeoType.Relation).ToArray();
                var relations = new Relation[relationsCompleted.Length];
                var lines = new List<Line>();

                for (int r = 0; r < relations.Length; r++)
                {
                    // Get RelationMembers
                    foreach (OsmGeo osmgeo in filtered)
                    {
                        var osmRelation = osmgeo as OsmSharp.Relation;
                        if (osmRelation == null || osmRelation.Id != relationsCompleted[r].Id) continue;

                        var roles = filterRelation.Members.Select(fr => fr.Role).ToArray();

                        var relation = new Relation
                        {
                            Id = osmRelation.Id.Value,
                            Type = Type.Relation,
                            Tags = filterRelation.Tags,
                            Members = osmRelation.GetMembers(roles)
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

                    //// Construct border
                    //var borderObject = new GameObject("Border").transform;
                    //var quadPrefab = Resources.Load<GameObject>("Quad");

                    //for (int i = 0; i < memberNodes.Count; i++)
                    //{
                    //    var pair = memberNodes.ElementAt(i);
                    //    var wayObject = new GameObject(pair.Key.ToString()).transform;
                    //    wayObject.SetParent(borderObject);

                    //    for (int n = 0; n < pair.Value.Length; n++)
                    //    {
                    //        var nodeId = pair.Value[n];
                    //        var node = nodes.FirstOrDefault(bn => bn.Id.Value == nodeId);
                    //        var nodePosition = node.GetPosition();

                    //        var quad = GameObject.Instantiate(quadPrefab, wayObject).transform;
                    //        quad.name = nodeId.ToString();
                    //        quad.position = nodePosition;
                    //    }
                    //}
                }

                var feature = new Feature
                {
                    Relations = relations,
                    Lines = lines.ToArray()
                };

                var bin = MessagePackSerializer.Serialize(feature);
                File.WriteAllBytes("Assets/StreamingAssets/Data/border.cuk", bin);
            }
        }

        //[MenuItem("Cuku/OSM/Extract City Border")]
        /*static void ExtractCityBorderOld()
        {
            Initialize();

            using (var fileStream = File.OpenRead(SourceDataPath))
            {
                var source = new PBFOsmStreamSource(fileStream);

                var filtered = from osmGeo in source
                               where osmGeo.Type == OsmGeoType.Node ||
                               osmGeo.Type == OsmGeoType.Way ||
                               (osmGeo.Type == OsmGeoType.Relation &&
                               osmGeo.Tags != null && osmGeo.Tags.Contains("admin_level", "4") && osmGeo.Tags.Contains("type", "boundary"))
                               select osmGeo;

                var complete = filtered.ToComplete();

                var boundary = complete.Where(x => x.Type == OsmGeoType.Relation).First();
                var ways = complete.Where(x => x.Type == OsmGeoType.Way).ToArray();
                var nodes = complete.Where(x => x.Type == OsmGeoType.Node).ToArray();

                // Get outer ways
                List<long> outerWays = new List<long>();

                foreach (OsmGeo osmgeo in filtered)
                {
                    var relation = osmgeo as Relation;
                    if (relation == null || relation.Id != boundary.Id) continue;

                    outerWays = relation.Members.Where(m => m.Type == OsmGeoType.Way && m.Role == "outer")
                        .Select(m => m.Id).ToList();
                }

                // Get way's nodes
                Dictionary<long, long[]> wayNodes = new Dictionary<long, long[]>();
                List<long> nodeIds = new List<long>();

                foreach (OsmGeo osmgeo in filtered)
                {
                    var way = osmgeo as Way;
                    if (way == null || !outerWays.Contains(way.Id.Value)) continue;

                    wayNodes.Add(way.Id.Value, way.Nodes);
                    nodeIds.AddRange(way.Nodes);
                }

                // Get boundary nodes
                List<Node> boundaryNodes = new List<Node>();

                foreach (OsmGeo osmGeo in filtered)
                {
                    var node = osmGeo as Node;
                    if (node == null || !nodeIds.Contains(node.Id.Value)) continue;

                    boundaryNodes.Add(node);
                }

                // Construct border
                var borderObject = new GameObject("Border").transform;
                var quadPrefab = Resources.Load<GameObject>("Quad");

                for (int i = 0; i < wayNodes.Count; i++)
                {
                    var pair = wayNodes.ElementAt(i);
                    var wayObject = new GameObject(pair.Key.ToString()).transform;
                    wayObject.SetParent(borderObject);

                    for (int n = 0; n < pair.Value.Length; n++)
                    {
                        var nodeId = pair.Value[n];
                        var node = boundaryNodes.FirstOrDefault(bn => bn.Id.Value == nodeId);
                        var nodePosition = node.GetPosition();

                        var quad = GameObject.Instantiate(quadPrefab, wayObject).transform;
                        quad.name = nodeId.ToString();
                        quad.position = nodePosition;
                    }
                }
            }
        }*/

        static Vector3 GetPosition(this Node node)
        {
            var point = (new double[] { node.Longitude.Value, node.Latitude.Value }).TransformPoint();

            var x = point[0] - centerPoint[0];
            var z = point[1] - centerPoint[1];

            return new Vector3((float)x, 0, (float)z);
        }
    }
}
