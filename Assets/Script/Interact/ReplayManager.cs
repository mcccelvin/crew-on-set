using UnityEngine;

public class ReplayManager : MonoBehaviour
{
    public Camera mainCamera;
    public Camera replayCamera;

    // CHANGED TO PUBLIC: Now you can see it in the Inspector to make sure it finds your camera!
    public RecordableTransform[] allRecordables;
    private bool isRecording = false;
    private bool isReplaying = false;

    void Start()
    {
        // THE FIX: Adding 'true' inside the brackets forces Unity to find the 
        // RecordableTransform even if the camera starts the game turned OFF!
        allRecordables = FindObjectsOfType<RecordableTransform>(true);

        if (replayCamera != null) replayCamera.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P)) ToggleReplay();
    }

    public string SetRecordingState(bool shouldRecord)
    {
        if (isReplaying) return "";
        isRecording = shouldRecord;

        if (isRecording)
        {
            TriggerStartRecording();
            return "";
        }
        else
        {
            return TriggerStopRecording();
        }
    }

    private void ToggleReplay()
    {
        if (isRecording) SetRecordingState(false);
        isReplaying = !isReplaying;

        if (isReplaying) TriggerStartReplay();
        else TriggerStopReplay();
    }

    public void TriggerStartRecording()
    {
        foreach (var obj in allRecordables)
        {
            if (obj != null) obj.StartRecording();
        }
    }

    public string TriggerStopRecording()
    {
        string finalFileName = "";

        foreach (var obj in allRecordables)
        {
            if (obj != null)
            {
                string recordedName = obj.StopRecording();

                // THE FIX: Only save the name if it actually successfully created a file!
                // This prevents blank files from overwriting your real video file name.
                if (!string.IsNullOrEmpty(recordedName))
                {
                    finalFileName = recordedName;
                }
            }
        }

        // A safety warning just in case it still fails
        if (string.IsNullOrEmpty(finalFileName))
        {
            Debug.LogWarning("ReplayManager: WARNING! No file was created. Is your RecordableTransform missing?");
        }

        return finalFileName;
    }

    public void TriggerStartReplay()
    {
        if (mainCamera != null) mainCamera.gameObject.SetActive(false);
        if (replayCamera != null) replayCamera.gameObject.SetActive(true);
        foreach (var obj in allRecordables) { if (obj != null) obj.StartReplay(); }
    }

    public void TriggerStopReplay()
    {
        if (mainCamera != null) mainCamera.gameObject.SetActive(true);
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);
        foreach (var obj in allRecordables) { if (obj != null) obj.StopReplay(); }
    }

    public void TriggerPreviewReplay()
    {
        if (replayCamera != null) replayCamera.gameObject.SetActive(true);
        foreach (var obj in allRecordables)
        {
            if (obj != null) obj.StartReplay();
        }
    }

    public void TriggerStopPreview()
    {
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);
        foreach (var obj in allRecordables)
        {
            if (obj != null) obj.StopReplay();
        }
    }
}