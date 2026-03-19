using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

[System.Serializable]
public class ObjectTrack
{
    public string id;
    public List<PointInTime> points;
}

[System.Serializable]
public class MasterTape
{
    public List<ObjectTrack> tracks = new List<ObjectTrack>();
}

public class ReplayManager : MonoBehaviour
{
    public Camera replayCamera;
    public RecordableTransform[] allRecordables;

    private bool isRecording = false;
    private bool isReplayingPreview = false;

    private MasterTape loadedTape;
    private string lastRecordedFileName = "";

    private float currentReplayTime = 0f; // NEW: The Manager's built-in stopwatch!

    void Start()
    {
        allRecordables = FindObjectsOfType<RecordableTransform>(true);
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isReplayingPreview) TriggerStopPreview();
            else if (!string.IsNullOrEmpty(lastRecordedFileName)) PlayMovieOnScreen(lastRecordedFileName);
        }

        // --- THE MAGIC: Ultra-Smooth Playback Blending ---
        if (isReplayingPreview && loadedTape != null)
        {
            currentReplayTime += Time.deltaTime; // Move the stopwatch forward smoothly
            bool stillPlaying = false;

            foreach (var track in loadedTape.tracks)
            {
                if (track.points.Count < 2) continue; // Need at least 2 frames to blend!

                int indexA = 0;
                int indexB = 0;

                // Find the two exact frames we are sitting between
                for (int i = 0; i < track.points.Count - 1; i++)
                {
                    if (track.points[i].time <= currentReplayTime && track.points[i + 1].time > currentReplayTime)
                    {
                        indexA = i;
                        indexB = i + 1;
                        break;
                    }
                }

                if (currentReplayTime <= track.points[track.points.Count - 1].time)
                {
                    stillPlaying = true;

                    // Math to calculate the buttery smooth glide between the two frames
                    float timeA = track.points[indexA].time;
                    float timeB = track.points[indexB].time;
                    float lerpPercentage = (currentReplayTime - timeA) / (timeB - timeA);

                    Vector3 smoothPos = Vector3.Lerp(track.points[indexA].position, track.points[indexB].position, lerpPercentage);
                    Quaternion smoothRot = Quaternion.Slerp(track.points[indexA].rotation, track.points[indexB].rotation, lerpPercentage);

                    // Apply the smooth glide to the Camera Rig
                    if (track.id == "LowCam" && replayCamera != null)
                    {
                        Transform ghostRig = replayCamera.transform.parent != null ? replayCamera.transform.parent : replayCamera.transform;
                        ghostRig.position = smoothPos;
                        ghostRig.rotation = smoothRot;
                    }
                    // Apply the smooth glide to the Props
                    else
                    {
                        foreach (var rec in allRecordables)
                        {
                            if (rec.uniqueObjectID == track.id)
                            {
                                rec.transform.position = smoothPos;
                                rec.transform.rotation = smoothRot;
                                Rigidbody rb = rec.GetComponent<Rigidbody>();
                                if (rb != null) rb.isKinematic = true;
                            }
                        }
                    }
                }
            }

            if (!stillPlaying) TriggerStopPreview();
        }
    }

    public string SetRecordingState(bool shouldRecord)
    {
        isRecording = shouldRecord;
        if (isRecording)
        {
            foreach (var obj in allRecordables) if (obj != null) obj.StartRecording();
            return "";
        }
        else
        {
            MasterTape newTape = new MasterTape();
            foreach (var obj in allRecordables)
            {
                if (obj != null && obj.pointsInTime.Count > 0)
                {
                    obj.StopRecording();
                    ObjectTrack track = new ObjectTrack();
                    track.id = obj.uniqueObjectID;
                    track.points = new List<PointInTime>(obj.pointsInTime);
                    newTape.tracks.Add(track);
                }
            }

            if (newTape.tracks.Count == 0) return "";

            string fileName = $"Take_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.json";
            File.WriteAllText(Path.Combine(Application.persistentDataPath, fileName), JsonUtility.ToJson(newTape));
            lastRecordedFileName = fileName;
            return fileName;
        }
    }

    public void PlayMovieOnScreen(string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(path))
        {
            loadedTape = JsonUtility.FromJson<MasterTape>(File.ReadAllText(path));
            isReplayingPreview = true;

            // Set the stopwatch to the exact start time of the tape
            currentReplayTime = 0f;
            if (loadedTape.tracks.Count > 0 && loadedTape.tracks[0].points.Count > 0)
            {
                currentReplayTime = loadedTape.tracks[0].points[0].time;
            }

            if (replayCamera != null)
            {
                Transform ghostRig = replayCamera.transform.parent != null ? replayCamera.transform.parent : replayCamera.transform;
                ghostRig.SetParent(null);
                replayCamera.gameObject.SetActive(true);
            }

            HideDuringReplay[] objectsToHide = FindObjectsOfType<HideDuringReplay>(true);
            foreach (var obj in objectsToHide) obj.SetVisible(false);
        }
    }

    public void TriggerStopPreview()
    {
        isReplayingPreview = false;
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);

        foreach (var rec in allRecordables)
        {
            if (rec.uniqueObjectID == "LowCam") continue;
            Rigidbody rb = rec.GetComponent<Rigidbody>();
            if (rb != null && rec.transform.parent == null) rb.isKinematic = false;
        }

        HideDuringReplay[] objectsToHide = FindObjectsOfType<HideDuringReplay>(true);
        foreach (var obj in objectsToHide) obj.SetVisible(true);
    }
}