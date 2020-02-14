namespace Cuku.Osm
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

        [MenuItem("Cuku/OSM/Extract City Border")]
        static void ExtractCityBorder()
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
        }

        static Vector3 GetPosition(this Node node)
        {
            var point = (new double[] { node.Longitude.Value, node.Latitude.Value }).TransformPoint();

            var x = point[0] - centerPoint[0];
            var z = point[1] - centerPoint[1];

            return new Vector3((float)x, 0, (float)z);
        }

        static double[] TransformPoint(this double[] point)
        {
            return trans.MathTransform.Transform(point);
        }
    }
}
