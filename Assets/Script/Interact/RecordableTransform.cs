using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

[System.Serializable]
public struct PointInTime
{
    public Vector3 position;
    public Quaternion rotation;
    public PointInTime(Vector3 pos, Quaternion rot) { position = pos; rotation = rot; }
}

[System.Serializable]
public class RecordingData { public List<PointInTime> points = new List<PointInTime>(); }

public class RecordableTransform : MonoBehaviour
{
    private List<PointInTime> pointsInTime = new List<PointInTime>();
    private bool isRecording = false;
    private bool isReplaying = false;
    private int playbackIndex = 0;
    private Rigidbody rb;

    void Start() { rb = GetComponent<Rigidbody>(); }

    void FixedUpdate()
    {
        if (isRecording) pointsInTime.Add(new PointInTime(transform.position, transform.rotation));
        else if (isReplaying) PlayBack();
    }

    public void StartRecording() { pointsInTime.Clear(); isRecording = true; isReplaying = false; }

    public void StopRecording()
    {
        isRecording = false;
        SaveToJSON();
    }

    private void SaveToJSON()
    {
        if (pointsInTime.Count == 0) return;
        RecordingData data = new RecordingData { points = pointsInTime };
        string json = JsonUtility.ToJson(data);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path = Path.Combine(Application.persistentDataPath, $"{name}_{timestamp}.json");
        File.WriteAllText(path, json);
        Debug.Log($"Auto-saved to: {path}");
    }

    public void LoadFromSpecificFile(string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            RecordingData loadedData = JsonUtility.FromJson<RecordingData>(json);
            pointsInTime = loadedData.points;
        }
    }

    public void StartReplay()
    {
        isReplaying = true;
        isRecording = false;
        playbackIndex = 0;

        // Turn off physics so the replay can move the object
        if (rb != null) rb.isKinematic = true;
    }

    public void StopReplay()
    {
        isReplaying = false;

        // FIX: Only turn physics back on if the item is NOT being held (has no parent)
        if (rb != null)
        {
            if (transform.parent == null)
            {
                rb.isKinematic = false;
            }
            else
            {
                // If it has a parent, it's being held, so keep it kinematic
                rb.isKinematic = true;
            }
        }
    }

    private void PlayBack()
    {
        if (playbackIndex < pointsInTime.Count)
        {
            transform.position = pointsInTime[playbackIndex].position;
            transform.rotation = pointsInTime[playbackIndex].rotation;
            playbackIndex++;
        }
        else StopReplay();
    }
}