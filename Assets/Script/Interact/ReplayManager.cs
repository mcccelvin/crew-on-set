using UnityEngine;

public class ReplayManager : MonoBehaviour
{
    public Camera mainCamera;
    public Camera replayCamera;

    private RecordableTransform[] allRecordables;

    // Booleans to track the current states
    private bool isRecording = false;
    private bool isReplaying = false;

    void Start()
    {
        allRecordables = FindObjectsOfType<RecordableTransform>();

        if (replayCamera != null)
        {
            replayCamera.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Press R to toggle Recording
        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleRecording();
        }

        // Press P to toggle Replay
        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleReplay();
        }
    }

    private void ToggleRecording()
    {
        // Safety check: don't allow recording while a replay is happening
        if (isReplaying)
        {
            Debug.LogWarning("Cannot record while a replay is playing!");
            return;
        }

        isRecording = !isRecording;

        if (isRecording)
        {
            TriggerStartRecording();
            Debug.Log("Recording Started");
        }
        else
        {
            TriggerStopRecording();
            Debug.Log("Recording Stopped");
        }
    }

    private void ToggleReplay()
    {
        // Safety check: if we are still recording, stop recording before replaying
        if (isRecording)
        {
            Debug.Log("Auto-stopping recording to start replay.");
            ToggleRecording();
        }

        isReplaying = !isReplaying;

        if (isReplaying)
        {
            TriggerStartReplay();
            Debug.Log("Replay Started");
        }
        else
        {
            TriggerStopReplay();
            Debug.Log("Replay Stopped");
        }
    }

    public void TriggerStartRecording()
    {
        foreach (var obj in allRecordables)
        {
            obj.StartRecording();
        }
    }

    public void TriggerStopRecording()
    {
        foreach (var obj in allRecordables)
        {
            obj.StopRecording();
        }
    }

    public void TriggerStartReplay()
    {
        if (mainCamera != null) mainCamera.gameObject.SetActive(false);
        if (replayCamera != null) replayCamera.gameObject.SetActive(true);

        foreach (var obj in allRecordables)
        {
            obj.StartReplay();
        }
    }

    public void TriggerStopReplay()
    {
        if (mainCamera != null) mainCamera.gameObject.SetActive(true);
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);

        foreach (var obj in allRecordables)
        {
            obj.StopReplay();
        }
    }
}