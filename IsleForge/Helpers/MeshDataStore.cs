using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace IsleForge.Helpers
{
    public static class MeshDataStore
    {
        public static Dictionary<string, MeshGeometry3D> Meshes { get; set; }
        public static Dictionary<string, Point3DCollection> OriginalMeshPositions { get; set; }

        public static Point3D CameraPosition { get; set; }
        public static Vector3D CameraLookDirection { get; set; }
        public static Vector3D CameraUpDirection { get; set; }
    }
}
