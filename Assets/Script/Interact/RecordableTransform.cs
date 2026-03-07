using System.Collections.Generic;
using UnityEngine;

public struct PointInTime
{
    public Vector3 position;
    public Quaternion rotation;

    public PointInTime(Vector3 pos, Quaternion rot)
    {
        //Construct a new snapshot with the specified position and rotatio
        position = pos;
        rotation = rot;
    }
}

public class RecordableTransform : MonoBehaviour
{
    // Attach to any GameObject whose movement you want to record/playbac
    private List<PointInTime> pointsInTime = new List<PointInTime>();
    private bool isRecording = false;
    private bool isReplaying = false;
    private int playbackIndex = 0;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {   // FixedUpdate is used to align recording with the physics timestep.
        if (isRecording)
        {   // Capture current position and rotation as a PointInTime.
            pointsInTime.Add(new PointInTime(transform.position, transform.rotation));
        }
        else if (isReplaying)
        {
            PlayBack();
        }
    }

    public void StartRecording()
    {
        // Clears any previous recordings, sets recording state and ensures replay is off.
        pointsInTime.Clear();
        isRecording = true;
        isReplaying = false;
    }

    public void StopRecording()
    {
        isRecording = false;
    }

    public void StartReplay()
    {
        if (pointsInTime.Count == 0) return;

        isReplaying = true;
        isRecording = false;
        playbackIndex = 0;

        // Disable physics simulation so playback directly controls the transform.
        if (rb != null) rb.isKinematic = true;
    }

    public void StopReplay()
    {
        isReplaying = false;
        if (rb != null) rb.isKinematic = false;
    }

    private void PlayBack()
    {
        if (playbackIndex < pointsInTime.Count)
        {   // Apply the recorded snapshot to the transform.
            transform.position = pointsInTime[playbackIndex].position;
            transform.rotation = pointsInTime[playbackIndex].rotation;
            playbackIndex++;
        }
        else
        {
            StopReplay();
        }
    }
}