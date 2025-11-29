using HeightFieldWaterSim;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq.Expressions;


public class WaterAndBallAnimationController : MonoBehaviour
{
    [Header("Materials")]
    public Material waterMaterial;
    public Material ballMaterial;
    public Material tankMaterial;

    [Header("Scene Organization")]
    public Transform environmentParent;
    public Transform ballsParent;

    [Header("Cursor")]
    public Texture2D cursorTexture;

    private Grabber grabber;


    private void Start()
    {
        SetupSimulation();
        MyPhysicsScene.isPaused = false;

        ConfigureCursor();
        grabber = new Grabber(Camera.main);
    }


    private void Update()
    {
        if (MyPhysicsScene.isPaused) return;

        MyPhysicsScene.waterSurface.UpdateWaterMeshVisualization();

        foreach (DensityAwareBall ball in MyPhysicsScene.objects)
        {
            ball.MyUpdate();
        }

        grabber.MoveGrab();
    }


    private void LateUpdate()
    {
        HandleMouseInput();
    }


    private void FixedUpdate()
    {
        RunPhysicsStep();
    }


    private void ConfigureCursor()
    {
        Cursor.visible = true;
        Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.ForceSoftware);
    }


    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            List<IGrabbable> grabbableObjects = new();

            foreach (DensityAwareBall ball in MyPhysicsScene.objects)
            {
                grabbableObjects.Add(ball);
            }

            grabber.StartGrab(grabbableObjects);
        }

        if (Input.GetMouseButtonUp(0))
        {
            grabber.EndGrab();
        }
    }


    private void SetupSimulation()
    {
        InitializeWaterSurface();
        ConstructTankWalls();
        SpawnBalls();
        CreateGroundPlane();
    }


    private void InitializeWaterSurface()
    {
        float tankWidth = MyPhysicsScene.tankSize.x;
        float tankDepth = MyPhysicsScene.tankSize.z;
        
        waterMaterial.SetColor("_BaseColor", new Color(0.0f, 0.45f, 0.65f, 0.55f));  

        WaterSurface surface = new(
            tankWidth,
            tankDepth,
            MyPhysicsScene.waterHeight,
            MyPhysicsScene.waterSpacing,
            waterMaterial
        );

        MyPhysicsScene.waterSurface = surface;
    }


    private void ConstructTankWalls()
    {
        GameObject wallPrototype = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallPrototype.GetComponent<MeshRenderer>().material = tankMaterial;

        GameObject leftWall = wallPrototype;
        GameObject rightWall = Instantiate(wallPrototype);
        GameObject frontWall = Instantiate(wallPrototype);
        GameObject backWall = Instantiate(wallPrototype);

        leftWall.transform.parent = environmentParent;
        rightWall.transform.parent = environmentParent;
        frontWall.transform.parent = environmentParent;
        backWall.transform.parent = environmentParent;

        float borderThickness = MyPhysicsScene.tankBorder;
        float tankWidth = MyPhysicsScene.tankSize.x;
        float tankHeight = MyPhysicsScene.tankSize.y;
        float tankDepth = MyPhysicsScene.tankSize.z;

        Vector3 sideWallScale = new(borderThickness, tankHeight, tankDepth);
        Vector3 endWallScale = new(tankWidth, tankHeight, borderThickness);

        leftWall.transform.localScale = sideWallScale;
        rightWall.transform.localScale = sideWallScale;
        frontWall.transform.localScale = endWallScale;
        backWall.transform.localScale = endWallScale;

        leftWall.transform.position = new(-0.5f * tankWidth, 0.5f * tankHeight, 0.0f);
        rightWall.transform.position = new(0.5f * tankWidth, 0.5f * tankHeight, 0.0f);
        frontWall.transform.position = new(0.0f, 0.5f * tankHeight, -0.5f * tankDepth);
        backWall.transform.position = new(0.0f, 0.5f * tankHeight, 0.5f * tankDepth);
    }


    private void SpawnBalls()
    {
        Vector3 position1 = new(-0.5f, 1.0f, -0.5f);
        Vector3 position2 = new(0.5f, 1.0f, -0.5f);
        Vector3 position3 = new(0.5f, 1.0f, 0.5f);

        Material material1 = new(ballMaterial) { color = Color.gray };
        Material material2 = new(ballMaterial) { color = Color.white };
        Material material3 = new(ballMaterial) { color = Color.green };

        DensityAwareBall ball1 = new(position1, 0.20f, 2.0f, material1, ballsParent);
        DensityAwareBall ball2 = new(position2, 0.30f, 0.7f, material2, ballsParent);
        DensityAwareBall ball3 = new(position3, 0.25f, 0.2f, material3, ballsParent);

        MyPhysicsScene.objects.Add(ball1);
        MyPhysicsScene.objects.Add(ball2);
        MyPhysicsScene.objects.Add(ball3);
    }


    private void CreateGroundPlane()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = Vector3.one * 100f;
    }


    private void RunPhysicsStep()
    {
        if (MyPhysicsScene.isPaused) return;

        float timeStep = Time.fixedDeltaTime;

        MyPhysicsScene.waterSurface.Simulate(timeStep);

        int objectCount = MyPhysicsScene.objects.Count;
        for (int i = 0; i < objectCount; i++)
        {
            DensityAwareBall currentBall = MyPhysicsScene.objects[i];
            currentBall.Simulate(timeStep);
            
            for (int j = 0; j < i; j++)
            {
                currentBall.HandleCollision(MyPhysicsScene.objects[j]);
            }
        }
    }

}