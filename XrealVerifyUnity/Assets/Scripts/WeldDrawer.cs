using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WeldDrawer : MonoBehaviour
{
    public Camera arCamera; // assign Main Camera (XR camera) in inspector
    public GameObject weldPrefab; // optional: small sphere to mark points
    public float drawDistance = 2.0f; // distance in front of camera to project points
    public float minDistanceBetweenPoints = 0.01f; // prevent dense point spam

    private LineRenderer lineRenderer;
    private List<Vector3> points = new List<Vector3>();
    private bool touching = false;
    private bool sessionActive = false;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        // basic LineRenderer setup
        lineRenderer.positionCount = 0;
        lineRenderer.loop = false;
        lineRenderer.widthMultiplier = 0.01f; // thin weld bead
        lineRenderer.numCapVertices = 6;
        lineRenderer.numCornerVertices = 6;
    }

    void Update()
    {
        if (!sessionActive) return;

        if (touching)
        {
            Vector3 point = arCamera.transform.position + arCamera.transform.forward * drawDistance;
            // optional offset: small random jitter to mimic welding bead
            point += Random.insideUnitSphere * 0.0025f;

            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], point) > minDistanceBetweenPoints)
            {
                AddPoint(point);
            }
        }
    }

    public void BeginSession()
    {
        Debug.Log("Weld session started");
        sessionActive = true;
        touching = false;
        Clear();
    }

    public void EndSession()
    {
        Debug.Log("Weld session ended");
        sessionActive = false;
        touching = false;
        // Optionally flush / finalize the weld look here (apply smoothing, particle effects etc.)
        ApplyFinalSmoothing();
    }

    public void ToggleContact()
    {
        touching = !touching;
        Debug.Log("Touching: " + touching);

        if (touching && sessionActive)
        {
            // start a new segment
            // leave existing points and continue
        }
    }

    private void AddPoint(Vector3 p)
    {
        points.Add(p);
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPosition(points.Count - 1, p);

        if (weldPrefab != null)
        {
            Instantiate(weldPrefab, p, Quaternion.identity);
        }
    }

    public void Clear()
    {
        points.Clear();
        lineRenderer.positionCount = 0;
    }

    private void ApplyFinalSmoothing()
    {
        // Simple smoothing example: not heavy math to keep it lightweight for device
        if (points.Count < 3) return;

        List<Vector3> smooth = new List<Vector3>();
        smooth.Add(points[0]);
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 a = points[i - 1];
            Vector3 b = points[i];
            Vector3 c = points[i + 1];
            Vector3 m = (a + b + c) / 3f;
            smooth.Add(Vector3.Lerp(b, m, 0.5f));
        }
        smooth.Add(points[points.Count - 1]);

        points = smooth;
        lineRenderer.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            lineRenderer.SetPosition(i, points[i]);
    }
}
