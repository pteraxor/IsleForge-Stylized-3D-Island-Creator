using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Prototyping.Helpers
{
    public static class MeshDataStore
    {
        public static Dictionary<string, MeshGeometry3D> Meshes { get; set; }
        public static Dictionary<string, Point3DCollection> OriginalMeshPositions { get; set; }
    }
}
