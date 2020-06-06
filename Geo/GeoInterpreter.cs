namespace Cuku.Geo
{
    using System.Collections.Generic;
    using UnityEngine;
    using System.IO;
    using System;
    using System.Linq;
    using MessagePack;
    using Utilities;

    public static class GeoInterpreter
    {
        #region Points
        public static Vector3[] GetBoundaryPoints(this string boundaryDataPath)
        {
            var bytes = File.ReadAllBytes(boundaryDataPath);
            var boundaryData = MessagePackSerializer.Deserialize<Feature>(bytes);

            var members = boundaryData.Relations[0].Members;

            var boundaryPoints = new List<Vector3>();
            for (int m = 0; m < members.Length; m++)
            {
                var line = boundaryData.Lines.FirstOrDefault(l => l.Id == members[m].Id);
                var points = line.Points.ToVector3().ProjectToTerrain();

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
        #endregion
    }
}
