namespace Cuku.Geo
{
	using UnityEngine;

	public static class GeoUtilities
    {
        #region Points
        public static Vector3[] ToVector3(this Point[] points)
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
