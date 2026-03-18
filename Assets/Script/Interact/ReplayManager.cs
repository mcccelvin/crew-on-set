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
    private int playbackIndex = 0;
    private MasterTape loadedTape;

    // --- NEW: Memory for your quick-test hotkey! ---
    private string lastRecordedFileName = "";

    void Start()
    {
        allRecordables = FindObjectsOfType<RecordableTransform>(true);
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);
    }

    void Update()
    {
        // --- QUICK TEST HOTKEY ('P') ---
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isReplayingPreview)
            {
                // Stop the video if it is currently playing
                TriggerStopPreview();
                Debug.Log("ReplayManager: Playback stopped.");
            }
            else if (!string.IsNullOrEmpty(lastRecordedFileName))
            {
                // Instantly play the last thing you recorded!
                Debug.Log("ReplayManager: Instantly testing the last recording!");
                PlayMovieOnScreen(lastRecordedFileName);
            }
            else
            {
                Debug.LogWarning("ReplayManager: You haven't recorded anything yet to test!");
            }
        }
    }

    void FixedUpdate()
    {
        if (isReplayingPreview && loadedTape != null)
        {
            bool stillPlaying = false;

            foreach (var track in loadedTape.tracks)
            {
                if (playbackIndex < track.points.Count)
                {
                    stillPlaying = true;

                    if (track.id == "LowCam")
                    {
                        if (replayCamera != null)
                        {
                            Transform ghostRig = replayCamera.transform.parent != null ? replayCamera.transform.parent : replayCamera.transform;
                            ghostRig.position = track.points[playbackIndex].position;
                            ghostRig.rotation = track.points[playbackIndex].rotation;
                        }
                    }
                    else
                    {
                        foreach (var rec in allRecordables)
                        {
                            if (rec.uniqueObjectID == track.id)
                            {
                                rec.transform.position = track.points[playbackIndex].position;
                                rec.transform.rotation = track.points[playbackIndex].rotation;

                                Rigidbody rb = rec.GetComponent<Rigidbody>();
                                if (rb != null) rb.isKinematic = true;
                            }
                        }
                    }
                }
            }

            if (stillPlaying) playbackIndex++;
            else TriggerStopPreview();
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

            string json = JsonUtility.ToJson(newTape);
            string fileName = $"Take_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.json";
            File.WriteAllText(Path.Combine(Application.persistentDataPath, fileName), json);

            // --- NEW: Remember this specific file so we can quick-test it later! ---
            lastRecordedFileName = fileName;

            return fileName;
        }
    }

    public void PlayMovieOnScreen(string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            loadedTape = JsonUtility.FromJson<MasterTape>(json);
            isReplayingPreview = true;
            playbackIndex = 0;

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