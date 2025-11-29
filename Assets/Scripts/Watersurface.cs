using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeightFieldWaterSim
{
    public class WaterSurface
    {

        // Wave physics parameters
        private float waveSpeed;
        private float posDamping;
        private float velDamping;
        private float couplingStrength;

        // Grid dimensions
        private int numColumnsX;
        private int numColumnsZ;
        private int totalWaterColumns;
        private float columnSpacing;

        // Water state (persists across frames)
        private float[] waterColumnHeights;
        private float[] waterColumnVelocities;
        
        // Ball displacement (recalculated each frame)
        private float[] ballSubmergedHeights;
        private float[] prevBallSubmergedHeights;

        public bool enableRadiusClamping = true;
        public float maxInfluenceRadius = 0.6f;
        public bool enableDragForce = true;
        public float dragCoefficient = 2.0f;

        // Visualization
        private Mesh waterMesh;
        private Vector3[] waterMeshVertices;
        private Material waterMaterial;

        public WaterSurface(float sizeX, float sizeZ, float initialDepth, float columnSpacing, Material waterMaterial)
        {
            // Physics parameters
            this.waveSpeed = 2f;
            this.posDamping = 1f;
            this.velDamping = 0.3f;
            this.couplingStrength = 0.5f;

            // Grid setup
            this.numColumnsX = Mathf.FloorToInt(sizeX / columnSpacing) + 1;
            this.numColumnsZ = Mathf.FloorToInt(sizeZ / columnSpacing) + 1;
            this.columnSpacing = columnSpacing;
            this.totalWaterColumns = this.numColumnsX * this.numColumnsZ;

            // Initialize water state
            this.waterColumnHeights = new float[this.totalWaterColumns];
            this.waterColumnVelocities = new float[this.totalWaterColumns];
            this.ballSubmergedHeights = new float[this.totalWaterColumns];
            this.prevBallSubmergedHeights = new float[this.totalWaterColumns];

            System.Array.Fill(this.waterColumnHeights, initialDepth);
            System.Array.Fill(this.waterColumnVelocities, 0f);

            // Create visualization mesh
            InitializeWaterMesh(waterMaterial);
        }


        private void InitializeWaterMesh(Material waterMaterial)
        {
            int centerColumnX = Mathf.FloorToInt(this.numColumnsX / 2f);
            int centerColumnZ = Mathf.FloorToInt(this.numColumnsZ / 2f);

            Vector3[] vertexPositions = new Vector3[this.totalWaterColumns];
            Vector2[] vertexUVs = new Vector2[this.totalWaterColumns];

            for (int xIndex = 0; xIndex < this.numColumnsX; xIndex++)
            {
                for (int zIndex = 0; zIndex < this.numColumnsZ; zIndex++)
                {
                    int columnIndex = xIndex * this.numColumnsZ + zIndex;
                    
                    float worldX = (xIndex - centerColumnX) * columnSpacing;
                    float worldZ = (zIndex - centerColumnZ) * columnSpacing;
                    vertexPositions[columnIndex] = new Vector3(worldX, 0f, worldZ);

                    vertexUVs[columnIndex] = new Vector2(
                        xIndex / (float)this.numColumnsX, 
                        zIndex / (float)this.numColumnsZ
                    );
                }
            }

            int numTriangles = (this.numColumnsX - 1) * (this.numColumnsZ - 1) * 2;
            int[] triangleIndices = new int[numTriangles * 3];
            int indexPosition = 0;

            for (int xIndex = 0; xIndex < this.numColumnsX - 1; xIndex++)
            {
                for (int zIndex = 0; zIndex < this.numColumnsZ - 1; zIndex++)
                {
                    int bottomLeft = xIndex * this.numColumnsZ + zIndex;
                    int bottomRight = xIndex * this.numColumnsZ + zIndex + 1;
                    int topRight = (xIndex + 1) * this.numColumnsZ + zIndex + 1;
                    int topLeft = (xIndex + 1) * this.numColumnsZ + zIndex;

                    triangleIndices[indexPosition++] = bottomLeft;
                    triangleIndices[indexPosition++] = bottomRight;
                    triangleIndices[indexPosition++] = topRight;

                    triangleIndices[indexPosition++] = bottomLeft;
                    triangleIndices[indexPosition++] = topRight;
                    triangleIndices[indexPosition++] = topLeft;
                }
            }

            Mesh newWaterMesh = new()
            {
                vertices = vertexPositions,
                triangles = triangleIndices,
                uv = vertexUVs
            };
            newWaterMesh.MarkDynamic();

            this.waterMesh = newWaterMesh;
            this.waterMeshVertices = vertexPositions;
            this.waterMaterial = waterMaterial;

            UpdateWaterMeshVisualization();
        }


        public void Simulate(float deltaTime)
        {
            CalculateBallSubmergedVolumes();
            ApplyBuoyancyAndWaterDisplacement(deltaTime);
            SimulateWavePropagation(deltaTime);
        }


        private void CalculateBallSubmergedVolumes()
        {
            System.Array.Copy(this.ballSubmergedHeights, this.prevBallSubmergedHeights, this.totalWaterColumns);
            System.Array.Fill(this.ballSubmergedHeights, 0f);

            int centerColumnX = Mathf.FloorToInt(this.numColumnsX / 2f);
            int centerColumnZ = Mathf.FloorToInt(this.numColumnsZ / 2f);
            float inverseSpacing = 1f / this.columnSpacing;

            for (int ballIndex = 0; ballIndex < MyPhysicsScene.objects.Count; ballIndex++)
            {
                DensityAwareBall ball = MyPhysicsScene.objects[ballIndex];
                AccumulateBallSubmergedVolume(ball, centerColumnX, centerColumnZ, inverseSpacing);
            }

            SmoothBallSubmergedHeights();
        }


        private void AccumulateBallSubmergedVolume(DensityAwareBall ball, int centerColumnX, int centerColumnZ, float inverseSpacing)
        {
            Vector3 ballPosition = ball.pos;
            float ballRadius = enableRadiusClamping 
                ? Mathf.Min(ball.radius, maxInfluenceRadius)
                : ball.radius;

            int minColumnX = Mathf.Max(0, centerColumnX + Mathf.FloorToInt((ballPosition.x - ballRadius) * inverseSpacing));
            int maxColumnX = Mathf.Min(this.numColumnsX - 1, centerColumnX + Mathf.FloorToInt((ballPosition.x + ballRadius) * inverseSpacing));
            int minColumnZ = Mathf.Max(0, centerColumnZ + Mathf.FloorToInt((ballPosition.z - ballRadius) * inverseSpacing));
            int maxColumnZ = Mathf.Min(this.numColumnsZ - 1, centerColumnZ + Mathf.FloorToInt((ballPosition.z + ballRadius) * inverseSpacing));

            for (int xIndex = minColumnX; xIndex <= maxColumnX; xIndex++)
            {
                for (int zIndex = minColumnZ; zIndex <= maxColumnZ; zIndex++)
                {
                    float columnWorldX = (xIndex - centerColumnX) * this.columnSpacing;
                    float columnWorldZ = (zIndex - centerColumnZ) * this.columnSpacing;

                    float horizontalDistanceSquared = 
                        (ballPosition.x - columnWorldX) * (ballPosition.x - columnWorldX) + 
                        (ballPosition.z - columnWorldZ) * (ballPosition.z - columnWorldZ);
                    
                    if (horizontalDistanceSquared < ballRadius * ballRadius)
                    {
                        int columnIndex = xIndex * this.numColumnsZ + zIndex;
                        float currentWaterHeight = this.waterColumnHeights[columnIndex];
                        
                        float ballSubmergedHeight = CalculateBallSubmergedHeightAtColumn(
                            ballPosition.y, 
                            ballRadius, 
                            horizontalDistanceSquared, 
                            currentWaterHeight
                        );
                        
                        this.ballSubmergedHeights[columnIndex] += ballSubmergedHeight;
                    }
                }
            }
        }


        private float CalculateBallSubmergedHeightAtColumn(
            float ballCenterY, 
            float ballRadius, 
            float horizontalDistanceSquared, 
            float waterSurfaceHeight)
        {
            float ballVerticalHalfExtent = Mathf.Sqrt(ballRadius * ballRadius - horizontalDistanceSquared);
            float ballBottomY = Mathf.Max(ballCenterY - ballVerticalHalfExtent, 0f);
            float ballTopY = Mathf.Min(ballCenterY + ballVerticalHalfExtent, waterSurfaceHeight);
            return Mathf.Max(ballTopY - ballBottomY, 0f);
        }


        private void SmoothBallSubmergedHeights()
        {
            for (int iter = 0; iter < 2; iter++)
            {
                for (int x = 0; x < this.numColumnsX; x++)
                {
                    for (int z = 0; z < this.numColumnsZ; z++)
                    {
                        int columnIndex = x * this.numColumnsZ + z;
                        int num = x > 0 && x < this.numColumnsX - 1 ? 2 : 1;
                        num += z > 0 && z < this.numColumnsZ - 1 ? 2 : 1;
                        
                        float avg = 0f;
                        if (x > 0) avg += this.ballSubmergedHeights[columnIndex - this.numColumnsZ];
                        if (x < this.numColumnsX - 1) avg += this.ballSubmergedHeights[columnIndex + this.numColumnsZ];
                        if (z > 0) avg += this.ballSubmergedHeights[columnIndex - 1];
                        if (z < this.numColumnsZ - 1) avg += this.ballSubmergedHeights[columnIndex + 1];
                        avg /= num;
                        
                        this.ballSubmergedHeights[columnIndex] = avg;
                    }
                }
            }
        }


        private void ApplyBuoyancyAndWaterDisplacement(float deltaTime)
        {
            int centerColumnX = Mathf.FloorToInt(this.numColumnsX / 2f);
            int centerColumnZ = Mathf.FloorToInt(this.numColumnsZ / 2f);
            float inverseSpacing = 1f / this.columnSpacing;
            float waterColumnArea = this.columnSpacing * this.columnSpacing;

            for (int ballIndex = 0; ballIndex < MyPhysicsScene.objects.Count; ballIndex++)
            {
                DensityAwareBall ball = MyPhysicsScene.objects[ballIndex];
                ApplyBuoyancyForceToBall(ball, centerColumnX, centerColumnZ, inverseSpacing, waterColumnArea, deltaTime);
            }

            for (int columnIndex = 0; columnIndex < this.totalWaterColumns; columnIndex++)
            {
                float ballVolumeChange = this.ballSubmergedHeights[columnIndex] - this.prevBallSubmergedHeights[columnIndex];
                this.waterColumnHeights[columnIndex] += this.couplingStrength * ballVolumeChange;
            }
        }


        private void ApplyBuoyancyForceToBall(
            DensityAwareBall ball, 
            int centerColumnX, 
            int centerColumnZ, 
            float inverseSpacing, 
            float waterColumnArea, 
            float deltaTime)
        {
            Vector3 ballPosition = ball.pos;
            float ballRadius = enableRadiusClamping 
                ? Mathf.Min(ball.radius, maxInfluenceRadius)
                : ball.radius;


            int minColumnX = Mathf.Max(0, centerColumnX + Mathf.FloorToInt((ballPosition.x - ballRadius) * inverseSpacing));
            int maxColumnX = Mathf.Min(this.numColumnsX - 1, centerColumnX + Mathf.FloorToInt((ballPosition.x + ballRadius) * inverseSpacing));
            int minColumnZ = Mathf.Max(0, centerColumnZ + Mathf.FloorToInt((ballPosition.z - ballRadius) * inverseSpacing));
            int maxColumnZ = Mathf.Min(this.numColumnsZ - 1, centerColumnZ + Mathf.FloorToInt((ballPosition.z + ballRadius) * inverseSpacing));

            for (int xIndex = minColumnX; xIndex <= maxColumnX; xIndex++)
            {
                for (int zIndex = minColumnZ; zIndex <= maxColumnZ; zIndex++)
                {
                    float columnWorldX = (xIndex - centerColumnX) * this.columnSpacing;
                    float columnWorldZ = (zIndex - centerColumnZ) * this.columnSpacing;
                    
                    float horizontalDistanceSquared = 
                        (ballPosition.x - columnWorldX) * (ballPosition.x - columnWorldX) + 
                        (ballPosition.z - columnWorldZ) * (ballPosition.z - columnWorldZ);

                    if (horizontalDistanceSquared < ballRadius * ballRadius)
                    {
                        int columnIndex = xIndex * this.numColumnsZ + zIndex;
                        float currentWaterHeight = this.waterColumnHeights[columnIndex];
                        
                        float ballSubmergedHeight = CalculateBallSubmergedHeightAtColumn(
                            ballPosition.y, 
                            ballRadius, 
                            horizontalDistanceSquared, 
                            currentWaterHeight
                        );

                        if (ballSubmergedHeight > 0f)
                        {
                            float displacedWaterVolume = ballSubmergedHeight * waterColumnArea;

                            if (enableRadiusClamping && ball.radius > maxInfluenceRadius)
                            {
                                float areaScale = (ball.radius * ball.radius) / (maxInfluenceRadius * maxInfluenceRadius);
                                displacedWaterVolume *= areaScale;
                            }

                            float buoyancyForce = -displacedWaterVolume * MyPhysicsScene.gravity.y;
                            ball.ApplyForce(buoyancyForce, deltaTime);

                            if (enableDragForce)
                            {
                                Vector3 dragForce = -ball.vel * dragCoefficient * displacedWaterVolume;
                                ball.pos += dragForce * deltaTime * deltaTime * 0.5f;
                            }
                        }
                    }
                }
            }
        }


        private void SimulateWavePropagation(float deltaTime)
        {
            this.waveSpeed = Mathf.Min(this.waveSpeed, 0.5f * this.columnSpacing / deltaTime);

            float waveConstant = (this.waveSpeed * this.waveSpeed) / (this.columnSpacing * this.columnSpacing);
            float positionDamping = Mathf.Min(this.posDamping * deltaTime, 1f);
            float velocityDamping = Mathf.Max(0f, 1f - this.velDamping * deltaTime);

            for (int xIndex = 0; xIndex < this.numColumnsX; xIndex++)
            {
                for (int zIndex = 0; zIndex < this.numColumnsZ; zIndex++)
                {
                    int columnIndex = xIndex * this.numColumnsZ + zIndex;
                    float currentHeight = this.waterColumnHeights[columnIndex];

                    float neighborHeightSum = 0f;
                    neighborHeightSum += (xIndex > 0) ? this.waterColumnHeights[columnIndex - this.numColumnsZ] : currentHeight;
                    neighborHeightSum += (xIndex < this.numColumnsX - 1) ? this.waterColumnHeights[columnIndex + this.numColumnsZ] : currentHeight;
                    neighborHeightSum += (zIndex > 0) ? this.waterColumnHeights[columnIndex - 1] : currentHeight;
                    neighborHeightSum += (zIndex < this.numColumnsZ - 1) ? this.waterColumnHeights[columnIndex + 1] : currentHeight;

                    float acceleration = waveConstant * (neighborHeightSum - 4f * currentHeight);
                    this.waterColumnVelocities[columnIndex] += deltaTime * acceleration;

                    this.waterColumnHeights[columnIndex] += (0.25f * neighborHeightSum - currentHeight) * positionDamping;
                }
            }

            for (int columnIndex = 0; columnIndex < this.totalWaterColumns; columnIndex++)
            {
                this.waterColumnVelocities[columnIndex] *= velocityDamping;
                this.waterColumnHeights[columnIndex] += this.waterColumnVelocities[columnIndex] * deltaTime;
            }
        }

        public void UpdateWaterMeshVisualization()
        {
            for (int columnIndex = 0; columnIndex < this.totalWaterColumns; columnIndex++)
            {
                waterMeshVertices[columnIndex].y = this.waterColumnHeights[columnIndex];
            }

            waterMesh.SetVertices(waterMeshVertices);
            waterMesh.RecalculateNormals();
            waterMesh.RecalculateBounds();

            Graphics.DrawMesh(waterMesh, Vector3.zero, Quaternion.identity, waterMaterial, 0);
        }

        public Vector3 GetVertexPosition(int index) => waterMeshVertices[index];
        public void AddHeightToColumn(int index, float deltaHeight) => waterColumnHeights[index] += deltaHeight;
        public float getNumColumnsX() => numColumnsX;
        public float getNumColumnsZ() => numColumnsZ;
        public float getColumnSpacing() => columnSpacing;

    }
}