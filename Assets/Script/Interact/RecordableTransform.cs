using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PointInTime
{
    public float time; // THE UPGRADE: A precise timestamp!
    public Vector3 position;
    public Quaternion rotation;

    public PointInTime(float t, Vector3 pos, Quaternion rot)
    {
        time = t;
        position = pos;
        rotation = rot;
    }
}

public class RecordableTransform : MonoBehaviour
{
    public string uniqueObjectID = "LowCam";
    public List<PointInTime> pointsInTime = new List<PointInTime>();

    private bool isRecording = false;
    private float recordingStartTime;

    // We moved this to Update() for ultra-smooth capture matching your monitor refresh rate
    void Update()
    {
        if (isRecording)
        {
            float currentTime = Time.time - recordingStartTime;
            pointsInTime.Add(new PointInTime(currentTime, transform.position, transform.rotation));
        }
    }

    public void StartRecording()
    {
        pointsInTime.Clear();
        recordingStartTime = Time.time;
        isRecording = true;
    }

    public void StopRecording() { isRecording = false; }
}