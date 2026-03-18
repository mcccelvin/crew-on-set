using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PointInTime
{
    public Vector3 position;
    public Quaternion rotation;
    public PointInTime(Vector3 pos, Quaternion rot) { position = pos; rotation = rot; }
}

public class RecordableTransform : MonoBehaviour
{
    [Tooltip("MUST BE UNIQUE! e.g., 'LowCam', 'Chair1', 'Table'")]
    public string uniqueObjectID = "LowCam";

    public List<PointInTime> pointsInTime = new List<PointInTime>();
    private bool isRecording = false;

    void FixedUpdate()
    {
        if (isRecording) pointsInTime.Add(new PointInTime(transform.position, transform.rotation));
    }

    public void StartRecording() { pointsInTime.Clear(); isRecording = true; }
    public void StopRecording() { isRecording = false; }
}