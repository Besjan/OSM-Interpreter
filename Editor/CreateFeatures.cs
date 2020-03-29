namespace Cuku.Geo
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using System.IO;
    using System;
    using System.Linq;
    using MessagePack;
    using UnityEngine.ProBuilder;
    using UnityEngine.ProBuilder.MeshOperations;
    using UnityEditor.ProBuilder;
        
    public static class CreateFeatures
    {
        static float boundaryHeight = 100.0f;

        [MenuItem("Cuku/Create Boundary Geometry")]
        static void CreateBoundaryGeometry()
        {
            var bytes = File.ReadAllBytes("Assets/StreamingAssets/Data/border.cuk");
            var boundaryData = MessagePackSerializer.Deserialize<Feature>(bytes);

            var members = boundaryData.Relations[0].Members;

            var boundaryPoints = new List<Vector3>();
            for (int m = 0; m < members.Length; m++)
            {
                var line = boundaryData.Lines.FirstOrDefault(l => l.Id == members[m].Id);
                var points = line.Points.GetPointsWorldPositions();

                // Reverse line points to match previous line's direction
                if (boundaryPoints.Count != 0 && boundaryPoints.Last() != points[0])
                {
                    points = points.Reverse().ToArray();
                }

                boundaryPoints.AddRange(points);
            }

            // Close boundary
            var firstPoint = new Point[] { boundaryData.Lines.FirstOrDefault(l => l.Id == members[0].Id).Points[0] }.GetPointsWorldPositions()[0];
            boundaryPoints.Add(firstPoint);
            boundaryPoints.Reverse(); // Normals face outside
            boundaryPoints = boundaryPoints.AddTileIntersectionPoints();

            // boundaryPoints.ToArray().CreateWalls("Boundary");
            boundaryPoints.ToArray().CreateWall("Boundary");
        }

        static void CreateWalls(this Vector3[] basePoints, string name, Transform parent = null)
        {
            var tilePoints = basePoints.GroupBy(bp => bp.GetTerrainName());

            foreach(var group in tilePoints) {
                group.ToArray().CreateWall(group.Key);
            }
        }

        static void CreateWall(this Vector3[] basePoints, string name, Transform parent = null)
        {
            // Create vertices
            var wallVertices = new List<Vector3>();

            for (int p = 0; p < basePoints.Length - 1; p++)
            {
                var point0 = basePoints[p];
                var point1 = basePoints[p + 1];

                wallVertices.Add(point0);
                wallVertices.Add(point1);
                wallVertices.Add(new Vector3(point0.x, point0.y + boundaryHeight, point0.z));
                wallVertices.Add(new Vector3(point1.x, point1.y + boundaryHeight, point1.z));
            }

            var sharedVertices = new List<SharedVertex>();

            // Create faces
            var faces = new List<Face>();
            for (int f = 0; f < wallVertices.Count - 3; f += 4)
            {
                var faceVertices = new int[] { f, f + 1, f + 2, f + 1, f + 3, f + 2 };
                faces.Add(new Face(faceVertices));
            }

            var wall = ProBuilderMesh.Create(wallVertices, faces);

            Normals.CalculateNormals(wall);
            Normals.CalculateTangents(wall);
            Smoothing.ApplySmoothingGroups(wall, faces, 30);
            wall.ToMesh();
            wall.Refresh();
            EditorMeshUtility.Optimize(wall);

            wall.SetMaterial(faces, Resources.Load<Material>("2Sided"));

            wall.gameObject.name = wall.name = name;
            wall.transform.SetParent(parent, true);
        }

        static Vector3[] GetPointsWorldPositions(this Point[] points)
        {
            var positions = new Vector3[points.Length];
            for (int p = 0; p < points.Length; p++)
            {
                positions[p] = new Vector3((float)points[p].X, 0, (float)points[p].Y);
                positions[p].y = positions[p].GetTerrainHeight();
            }
            return positions;
        }

        static List<Vector3> AddTileIntersectionPoints(this List<Vector3> points)
        {
            var allPoints = new List<Vector3>();
            allPoints.Add(points[0]);

            for (int i = 1; i < points.Count; i++)
            {
                var point1 = points[i - 1];
                var point2 = points[i];

                if (point1.GetTerrainName() != point2.GetTerrainName())
                {
                    allPoints.Add(GetTileIntersectionPoint(point1, point2));
                }

                allPoints.Add(point2);
            }

            return allPoints;
        }

        static Vector3 GetTileIntersectionPoint(Vector3 point1, Vector3 point2)
        {
            var terrain = point1.GetTerrainHit().transform.GetComponent<Terrain>();
            var terrainAnglePoints = terrain.GetTerrainAnglePoints();

            var A1 = new Vector2(point1.x, point1.z);
            var A2 = new Vector2(point2.x, point2.z);

            bool found;
            Vector2[] intersections = new Vector2[4];
            intersections[0] = GetLinesIntersectionPoint(A1, A2, terrainAnglePoints[0], terrainAnglePoints[1], out found);
            intersections[1] = GetLinesIntersectionPoint(A1, A2, terrainAnglePoints[0], terrainAnglePoints[2], out found);
            intersections[2] = GetLinesIntersectionPoint(A1, A2, terrainAnglePoints[1], terrainAnglePoints[3], out found);
            intersections[3] = GetLinesIntersectionPoint(A1, A2, terrainAnglePoints[2], terrainAnglePoints[3], out found);

            var closest = 0;
            for (int i = 1; i < intersections.Length; i++)
            {
                if (Vector3.Distance(A1, intersections[i]) < Vector3.Distance(A1, intersections[closest]))
                {
                    closest = i;
                }
            }

            return intersections[closest].GetTerrainHitPosition();
        }

        /// <summary>
        /// Gets the coordinates of the intersection point of two lines.
        /// </summary>
        /// <param name="A1">A point on the first line.</param>
        /// <param name="A2">Another point on the first line.</param>
        /// <param name="B1">A point on the second line.</param>
        /// <param name="B2">Another point on the second line.</param>
        /// <param name="found">Is set to false of there are no solution. true otherwise.</param>
        /// <returns>The intersection point coordinates. Returns Vector2.zero if there is no solution.</returns>
        static Vector2 GetLinesIntersectionPoint(Vector2 A1, Vector2 A2, Vector2 B1, Vector2 B2, out bool found)
        {
            float tmp = (B2.x - B1.x) * (A2.y - A1.y) - (B2.y - B1.y) * (A2.x - A1.x);

            if (tmp == 0)
            {
                // No solution!
                found = false;
                return Vector2.zero;
            }

            float mu = ((A1.x - B1.x) * (A2.y - A1.y) - (A1.y - B1.y) * (A2.x - A1.x)) / tmp;

            found = true;

            var intersectionPoint = new Vector2(
                B1.x + (B2.x - B1.x) * mu,
                B1.y + (B2.y - B1.y) * mu
            );

            return intersectionPoint;
        }

        static Vector2[] GetTerrainAnglePoints(this Terrain terrain)
        {
            var tileResolution = terrain.terrainData.heightmapResolution;

            var tp1 = new Vector2(terrain.GetPosition().x, terrain.GetPosition().z);
            var tp2 = new Vector2(tp1.x + tileResolution, tp1.y);
            var tp3 = new Vector2(tp1.x, tp1.y + tileResolution);
            var tp4 = new Vector2(tp1.x + tileResolution, tp1.y + tileResolution);

            return new Vector2[] { tp1, tp2, tp3, tp4 };
        }

        static string GetTerrainName(this Vector3 position)
        {
            return position.GetTerrainHit().transform.name;
        }

        static Vector3 GetTerrainHitPosition(this Vector2 position)
        {
            Vector3 hitPosition = new Vector3(position.x, 0, position.y);
            hitPosition.y = hitPosition.GetTerrainHit().point.y;

            //var c = GameObject.CreatePrimitive(PrimitiveType.Cube);            
            //c.transform.position = hitPosition;

            return hitPosition;
        }

        static float GetTerrainHeight(this Vector3 position)
        {
            return position.GetTerrainHit().point.y;
        }

        static RaycastHit GetTerrainHit(this Vector3 origin)
        {
            origin.y = 10000;

            RaycastHit hit;
            Ray ray = new Ray(origin, Vector3.down);
            if (Physics.Raycast(ray, out hit, 100000))
            {
                if (hit.transform.GetComponent<Terrain>())
                {
                    return hit;
                }
            }

            Debug.LogError("No Terrain was Hit!" + origin);

            return hit;
        }
    }
}
