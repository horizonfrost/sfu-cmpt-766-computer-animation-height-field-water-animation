using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace HeightFieldWaterSim
{
    public class DensityAwareBall : IGrabbable
    {
        // ========== Physical State ==========
        public Vector3 pos;     // Current position in world space
        public Vector3 vel;     // Current velocity vector
        public float radius;    // Sphere radius
        public float mass;      // Calculated from volume and density

        // ========== Material Properties ==========
        public float restitution;  // Coefficient of restitution (bounciness)

        // ========== Interaction State ==========
        public bool isGrabbed;  // Whether user is currently dragging this ball

        // ========== Visualization ==========
        private readonly Transform visMesh;  // Unity transform for rendering



        /// <summary>
        /// Creates a new ball with specified physical and visual properties
        /// </summary>
        public DensityAwareBall(Vector3 initialPosition, float sphereRadius, float materialDensity, 
                      Material visualMaterial, Transform parentTransform)
        {
            // Initialize physics properties
            this.pos = initialPosition;
            this.radius = sphereRadius;
            this.vel = Vector3.zero;
            this.isGrabbed = false;
            this.restitution = 0.1f;

            // Calculate mass from volume: m = ρV = ρ(4πr³/3)
            float volume = (4f / 3f) * Mathf.PI * Mathf.Pow(sphereRadius, 3);
            this.mass = volume * materialDensity;

            // Create visual representation
            this.visMesh = CreateVisualMesh(initialPosition, sphereRadius, visualMaterial, parentTransform);
        }



        /// <summary>
        /// Creates the Unity GameObject that visually represents this ball
        /// </summary>
        private Transform CreateVisualMesh(Vector3 position, float sphereRadius, 
                                          Material material, Transform parent)
        {
            GameObject sphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereObject.transform.parent = parent;
            sphereObject.transform.position = position;
            
            float diameter = 2f * sphereRadius;
            sphereObject.transform.localScale = new Vector3(diameter, diameter, diameter);
            
            sphereObject.GetComponent<MeshRenderer>().material = material;

            return sphereObject.transform;
        }



        /// <summary>
        /// Handles elastic collision between two spherical balls using impulse-based method
        /// </summary>
        public void HandleCollision(DensityAwareBall otherBall)
        {
            // Calculate separation vector and distance
            Vector3 separationVector = otherBall.pos - this.pos;
            float currentDistance = separationVector.magnitude;
            float requiredMinDistance = this.radius + otherBall.radius;

            // Early exit if balls aren't overlapping
            if (currentDistance >= requiredMinDistance)
            {
                return;
            }

            // Normalize separation direction
            Vector3 collisionNormal = separationVector / currentDistance;

            // Position correction: push balls apart equally
            float overlap = requiredMinDistance - currentDistance;
            float correctionPerBall = overlap * 0.5f;
            
            this.pos -= collisionNormal * correctionPerBall;
            otherBall.pos += collisionNormal * correctionPerBall;

            // Velocity resolution using conservation of momentum and restitution
            float thisVelocityAlongNormal = Vector3.Dot(this.vel, collisionNormal);
            float otherVelocityAlongNormal = Vector3.Dot(otherBall.vel, collisionNormal);

            float m1 = this.mass;
            float m2 = otherBall.mass;
            float e = this.restitution;  // Coefficient of restitution

            // Calculate new velocities using impulse formula
            float newV1 = (m1 * thisVelocityAlongNormal + m2 * otherVelocityAlongNormal - 
                          m2 * (thisVelocityAlongNormal - otherVelocityAlongNormal) * e) / (m1 + m2);
            
            float newV2 = (m1 * thisVelocityAlongNormal + m2 * otherVelocityAlongNormal - 
                          m1 * (otherVelocityAlongNormal - thisVelocityAlongNormal) * e) / (m1 + m2);

            // Apply velocity changes only along collision normal
            this.vel += collisionNormal * (newV1 - thisVelocityAlongNormal);
            otherBall.vel += collisionNormal * (newV2 - otherVelocityAlongNormal);
        }



        /// <summary>
        /// Updates ball physics for one timestep (gravity, motion, boundary collisions)
        /// </summary>
        public void Simulate(float deltaTime)
        {
            if (this.isGrabbed)
            {
                return;
            }

            // Apply gravity acceleration
            this.vel += MyPhysicsScene.gravity * deltaTime;

            // Integrate velocity to get new position
            this.pos += this.vel * deltaTime;

            // Handle collisions with tank boundaries
            HandleTankBoundaryCollisions();
        }



        /// <summary>
        /// Checks and resolves collisions with the water tank walls and floor
        /// </summary>
        private void HandleTankBoundaryCollisions()
        {
            float maxX = 0.5f * MyPhysicsScene.tankSize.x - this.radius - 0.5f * MyPhysicsScene.tankBorder;
            float maxZ = 0.5f * MyPhysicsScene.tankSize.z - this.radius - 0.5f * MyPhysicsScene.tankBorder;
            float minY = this.radius;  // Floor at y=0

            if (this.pos.x < -maxX)
            {
                this.pos.x = -maxX;
                this.vel.x = -this.restitution * this.vel.x;
            }
            else if (this.pos.x > maxX)
            {
                this.pos.x = maxX;
                this.vel.x = -this.restitution * this.vel.x;
            }

            if (this.pos.z < -maxZ)
            {
                this.pos.z = -maxZ;
                this.vel.z = -this.restitution * this.vel.z;
            }
            else if (this.pos.z > maxZ)
            {
                this.pos.z = maxZ;
                this.vel.z = -this.restitution * this.vel.z;
            }

            if (this.pos.y < minY)
            {
                this.pos.y = minY;
                this.vel.y = -this.restitution * this.vel.y;
            }
        }



        /// <summary>
        /// Synchronizes the visual mesh position with the physics position
        /// Called every frame to update rendering
        /// </summary>
        public void MyUpdate()
        {
            this.visMesh.position = this.pos;
        }



        /// <summary>
        /// Applies an external force to the ball (e.g., buoyancy from water)
        /// </summary>
        public void ApplyForce(float forceY, float deltaTime)
        {
            // F = ma → a = F/m
            float accelerationY = forceY / this.mass;
            this.vel.y += accelerationY * deltaTime;

            // Apply small damping to improve stability
            this.vel *= 0.999f;
        }



        // ========================================================================
        // IGrabbable Interface Implementation - User Interaction Methods
        // ========================================================================

        /// <summary>
        /// Called when user starts dragging this ball
        /// </summary>
        public void StartGrab(Vector3 grabPosition)
        {
            this.isGrabbed = true;
            this.pos = grabPosition;
        }


        /// <summary>
        /// Called each frame while user is dragging
        /// </summary>
        public void MoveGrabbed(Vector3 newPosition)
        {
            this.pos = newPosition;
        }


        /// <summary>
        /// Called when user releases the ball
        /// </summary>
        public void EndGrab(Vector3 releasePosition, Vector3 releaseVelocity)
        {
            this.isGrabbed = false;
            this.pos = releasePosition;
            this.vel = releaseVelocity;
        }

        public void IsRayHittingBody(Ray ray, out CustomHit hit)
        {
            hit = null;

            if (RayUtil.IsRayHittingSphere(ray, this.pos, this.radius, out float hitDistance))
            {
                hit = new CustomHit(hitDistance, Vector3.zero, Vector3.zero);
            }
        }


        /// <summary>
        /// Returns current position (used for user interaction feedback)
        /// </summary>
        public Vector3 GetGrabbedPos()
        {
            return this.pos;
        }
    }
}