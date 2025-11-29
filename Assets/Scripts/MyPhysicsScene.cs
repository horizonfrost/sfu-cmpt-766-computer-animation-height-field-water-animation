using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeightFieldWaterSim
{
    public static class MyPhysicsScene
    {
        // Physical constants
        public static Vector3 gravity = new(0f, -10f, 0f);

        // Simulation container configuration
        public static Vector3 tankSize = new(6f, 1.25f, 8.0f);
        public static float tankBorder = 0.01f;

        // Height field water parameters
        public static float waterHeight = 0.8f;
        public static float waterSpacing = 0.03f;

        // Simulation control
        public static bool isPaused = true;

        // Active simulation components
        public static WaterSurface waterSurface = null;
        public static List<DensityAwareBall> objects = new();
    }
}