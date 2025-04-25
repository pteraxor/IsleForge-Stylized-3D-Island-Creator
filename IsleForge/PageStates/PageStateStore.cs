using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsleForge.PageStates
{
    public static class PageStateStore
    {
        public static BaseMapPageState BaseMapState { get; set; }

        public static EdgeEditingPageState EdgeEditingState { get; set; }

        public static HeightMapPageState HeightMapState { get; set; }

        public static MeshMakerPageState MeshMakerState { get; set; }

    }
}
