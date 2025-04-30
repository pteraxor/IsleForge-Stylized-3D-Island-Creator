using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IsleForge.Helpers;
using System.Windows.Media.Media3D;

namespace IsleForge.PageStates
{
    public class MeshMakerPageState
    {
        public Dictionary<string, MeshGeometry3D> LabelMeshes { get; set; }
        public Dictionary<string, Point3DCollection> OriginalMeshPositions { get; set; }
        public LabeledValue[,] LabeledHeightMap { get; set; }
        public Point3D CameraPosition { get; set; }
        public Vector3D CameraLookDirection { get; set; }
        public Vector3D CameraUpDirection { get; set; }
        public bool MeshCreated { get; set; }
        public float NoiseStrength { get; set; }
        public float NoiseScale { get; set; }
        public int NoiseOctaves { get; set; }
        public float NoiseLacunarity { get; set; }
    }

}
