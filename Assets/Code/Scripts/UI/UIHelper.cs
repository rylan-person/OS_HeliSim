using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIHelper{
    public class UIHelper
    {
        public static void PointToUISpace(Canvas canvas, RectTransform uiOjbect, Vector3 target_world_point, Transform cameraTransform)
        {
            // Get the plane of the canvas
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Plane canvasPlane = new Plane(
                canvasRect.forward,
                canvasRect.position
            );

            // Ray from camera to target
            Vector3 direction = (target_world_point - cameraTransform.position).normalized;
            Ray ray = new Ray(cameraTransform.position, direction);

            // Find the intersection point
            if (canvasPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);

                // Convert world position to local position on the canvas
                Vector3 localPoint = canvasRect.InverseTransformPoint(hitPoint);

                uiOjbect.anchoredPosition = new Vector2(localPoint.x, localPoint.y);
            }
        }
    }
}
