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

    public static class CreateFeatures
    {
        static float borderHeight = 500.0f;

        [MenuItem("Cuku/Create Features")]
        static void CreateFeaturesGeometry()
        {
            var bytes = File.ReadAllBytes("Assets/StreamingAssets/Data/border.cuk");
            var borderData = MessagePackSerializer.Deserialize<Feature>(bytes);

            // Construct border
            var borderObject = new GameObject("Border").transform;
            var quadPrefab = Resources.Load<GameObject>("Quad");

            var members = borderData.Relations[0].Members;

            for (int m = 0; m < members.Length; m++)
            {
                var line = borderData.Lines.FirstOrDefault(l => l.Id == members[m].Id);
                var points = new Point[line.Points.Length + 1];
                for (int p = 0; p < points.Length - 1; p++)
                {
                    points[p] = line.Points[p];
                }

                var nextLine = borderData.Lines.FirstOrDefault(l => l.Id == members[(m + 1) % members.Length].Id);
                points[points.Length - 1] = nextLine.Points[0];

                points.GetPositions().CreateWall(members[m].Id.ToString(), borderObject);
            }
        }

        static Vector3[] GetPositions(this Point[] points)
        {
            var positions = new Vector3[points.Length];
            for (int p = 0; p < points.Length; p++)
            {
                positions[p] = new Vector3((float)points[p].X, 0, (float)points[p].Y);
            }
            return positions;
        }

        static void CreateWall(this Vector3[] basePoints, string name, Transform parent = null)
        {
            // Create vertices
            var vertices = new List<Vector3>();
            for (int p = 0; p < basePoints.Length - 1; p++)
            {
                var point0 = basePoints[p];
                var point1 = basePoints[p + 1];

                vertices.Add(point0);
                vertices.Add(point1);
                vertices.Add(new Vector3(point0.x, borderHeight, point0.z));
                vertices.Add(new Vector3(point1.x, borderHeight, point1.z));
            }

            // Create faces
            var faces = new List<Face>();
            for (int f = 0; f < vertices.Count - 4; f += 4)
            {
                var faceVertices = new int[] { f, f + 1, f + 2, f + 1, f + 3, f + 2 };
                faces.Add(new Face(faceVertices));
            }

            var wall = ProBuilderMesh.Create(vertices, faces);
            Normals.CalculateNormals(wall);
            Normals.CalculateTangents(wall);
            Smoothing.ApplySmoothingGroups(wall, faces, 30);
            wall.Refresh();
            wall.SetMaterial(faces, Resources.Load<Material>("TwoSideWithFace"));
            wall.gameObject.name = wall.name = name;
            wall.transform.SetParent(parent, true);
        }
    }
}
