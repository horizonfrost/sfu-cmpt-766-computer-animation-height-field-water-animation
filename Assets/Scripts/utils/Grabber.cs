using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grabber
{
    private readonly Camera mainCamera;
    private IGrabbable grabbedBody = null;
    private float distanceToGrabPos;
    private Vector3 lastGrabPos;

    public Grabber(Camera mainCamera)
    {
        this.mainCamera = mainCamera;
    }

    public void StartGrab(List<IGrabbable> bodies)
    {
        if (grabbedBody != null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        float maxDist = float.MaxValue;

        IGrabbable closestBody = null;

        CustomHit closestHit = default;

        foreach (IGrabbable body in bodies)
        {
            body.IsRayHittingBody(ray, out CustomHit hit);

            if (hit != null)
            {
                if (hit.distance < maxDist)
                {
                    closestBody = body;

                    maxDist = hit.distance;

                    closestHit = hit;
                }
            }
            else
            {
                //Debug.Log("Ray missed");
            }
        }

        if (closestBody != null)
        {
            grabbedBody = closestBody;
        
            closestBody.StartGrab(closestHit.location);

            lastGrabPos = closestHit.location;

            distanceToGrabPos = closestHit.distance;
        }
    }




    public void MoveGrab()
    {
        if (grabbedBody == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        Vector3 vertexPos = ray.origin + ray.direction * distanceToGrabPos;

        lastGrabPos = grabbedBody.GetGrabbedPos();

        grabbedBody.MoveGrabbed(vertexPos);
    }



    public void EndGrab()
    {
        if (grabbedBody == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        Vector3 grabPos = ray.origin + ray.direction * distanceToGrabPos;

        float vel = (grabPos - lastGrabPos).magnitude / Time.deltaTime;
        
        Vector3 dir = (grabPos - lastGrabPos).normalized;

        grabbedBody.EndGrab(grabPos, dir * vel);

        grabbedBody = null;
    }
}
