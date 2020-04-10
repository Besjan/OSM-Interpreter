namespace Cuku.Geo
{
    using System.Collections.Generic;
    using UnityEngine;
    using System.IO;
    using System;
    using System.Linq;
    using MessagePack;
    using Utilities;

    public static class Features
    {
        const string boundaryDataPath = "Assets/StreamingAssets/Data/boundary.cuk";


        #region Points
        public static Vector3[] GetBoundaryPoints()
        {
            var bytes = File.ReadAllBytes(boundaryDataPath);
            var boundaryData = MessagePackSerializer.Deserialize<Feature>(bytes);

            var members = boundaryData.Relations[0].Members;

            var boundaryPoints = new List<Vector3>();
            for (int m = 0; m < members.Length; m++)
            {
                var line = boundaryData.Lines.FirstOrDefault(l => l.Id == members[m].Id);
                var points = line.Points.ToVector3();
                Utilities.ProjectToTerrain(ref points);

                // Reverse line points to match previous line's direction
                if (boundaryPoints.Count != 0 && boundaryPoints.Last() != points[0])
                {
                    points = points.Reverse().ToArray();
                }

                boundaryPoints.AddRange(points);
            }

            boundaryPoints.Reverse(); // Normals face outside

            return boundaryPoints.ToArray();
        }

        static Vector3[] ToVector3(this Point[] points)
        {
            var points3D = new Vector3[points.Length];
            for (int p = 0; p < points.Length; p++)
            {
                points3D[p] = new Vector3((float)points[p].X, 0, (float)points[p].Y);
            }
            return points3D;
        }
        #endregion
    }
}
