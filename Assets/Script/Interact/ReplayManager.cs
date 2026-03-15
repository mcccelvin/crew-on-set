using UnityEngine;

public class ReplayManager : MonoBehaviour
{
    public Camera mainCamera;
    public Camera replayCamera;

    private RecordableTransform[] allRecordables;
    private bool isRecording = false;
    private bool isReplaying = false;

    void Start()
    {
        allRecordables = FindObjectsOfType<RecordableTransform>();
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P)) ToggleReplay();
    }

    public void SetRecordingState(bool shouldRecord)
    {
        if (isReplaying) return;
        isRecording = shouldRecord;

        if (isRecording) TriggerStartRecording();
        else TriggerStopRecording();
    }

    private void ToggleReplay()
    {
        if (isRecording) SetRecordingState(false);
        isReplaying = !isReplaying;

        if (isReplaying) TriggerStartReplay();
        else TriggerStopReplay();
    }

    public void TriggerStartRecording() { foreach (var obj in allRecordables) obj.StartRecording(); }
    public void TriggerStopRecording() { foreach (var obj in allRecordables) obj.StopRecording(); }

    public void TriggerStartReplay()
    {
        if (mainCamera != null) mainCamera.gameObject.SetActive(false);
        if (replayCamera != null) replayCamera.gameObject.SetActive(true);
        foreach (var obj in allRecordables) obj.StartReplay();
    }

    public void TriggerStopReplay()
    {
        if (mainCamera != null) mainCamera.gameObject.SetActive(true);
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);
        foreach (var obj in allRecordables) obj.StopReplay();
    }
}