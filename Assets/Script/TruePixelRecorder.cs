using UnityEngine;
using System.Collections;
using System.IO;

public class TruePixelRecorder : MonoBehaviour
{
    public Camera filmCamera;

    [Header("Tape Quality Settings")]
    public int captureWidth = 640;  // Lower resolution saves massive disk space
    public int captureHeight = 360;
    public float framesPerSecond = TapeSettings.framesPerSecond;
    [Range(10, 100)] public int jpgQuality = 50;

    private bool isRecording = false;
    private Coroutine recordingCoroutine;
    private RenderTexture captureTexture;
    private Texture2D screenShot;

    private FileStream tapeStream;
    private BinaryWriter tapeWriter;
    private string currentFileName = "";
    private int recordedFrameCount = 0;

    public bool StartRecording()
    {
        if (filmCamera == null) filmCamera = GetComponent<Camera>();
        if (filmCamera == null)
        {
            Debug.LogError("TruePixelRecorder: Film Camera reference is missing!");
            return false;
        }

        if (captureWidth <= 0 || captureHeight <= 0)
        {
            Debug.LogError("TruePixelRecorder: Capture width and height must be greater than zero!");
            return false;
        }

        if (isRecording) CancelRecording();

        currentFileName = "Film_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tape";
        string path = Path.Combine(Application.persistentDataPath, currentFileName);

        try
        {
            tapeStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            tapeWriter = new BinaryWriter(tapeStream);
            tapeWriter.Write(0); // Frame count is filled in when recording stops.

            recordedFrameCount = 0;
            captureTexture = new RenderTexture(captureWidth, captureHeight, 24);
            captureTexture.Create();
            screenShot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);

            isRecording = true;
            recordingCoroutine = StartCoroutine(RecordFramesCoroutine());
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e, this);
            CancelRecording();
            return false;
        }
    }

    public string StopRecording()
    {
        if (!isRecording && tapeWriter == null) return "";

        isRecording = false;

        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }

        string savedFileName = FinalizeTapeFile();
        CleanUpCaptureResources();
        return savedFileName;
    }

    public void CancelRecording()
    {
        isRecording = false;

        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }

        CloseTapeWriter();
        CleanUpCaptureResources();

        if (!string.IsNullOrEmpty(currentFileName))
        {
            string path = Path.Combine(Application.persistentDataPath, currentFileName);
            if (File.Exists(path)) File.Delete(path);
        }

        currentFileName = "";
        recordedFrameCount = 0;
    }

    private IEnumerator RecordFramesCoroutine()
    {
        float frameInterval = 1f / Mathf.Max(1f, framesPerSecond);
        float nextCaptureTime = Time.unscaledTime;

        while (isRecording)
        {
            yield return new WaitForEndOfFrame();
            if (!isRecording) break;
            if (Time.unscaledTime < nextCaptureTime) continue;

            nextCaptureTime += frameInterval;

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = filmCamera.targetTexture;

            try
            {
                filmCamera.targetTexture = captureTexture;
                filmCamera.Render();
                RenderTexture.active = captureTexture;

                screenShot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                screenShot.Apply(false, false);
            }
            finally
            {
                filmCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
            }

            byte[] frameData = screenShot.EncodeToJPG(Mathf.Clamp(jpgQuality, 10, 100));
            if (tapeWriter != null)
            {
                tapeWriter.Write(frameData.Length);
                tapeWriter.Write(frameData);
                recordedFrameCount++;
            }
        }
    }

    private string FinalizeTapeFile()
    {
        if (tapeWriter == null || string.IsNullOrEmpty(currentFileName)) return "";

        string savedFileName = currentFileName;

        if (recordedFrameCount > 0)
        {
            tapeWriter.Flush();
            tapeStream.Position = 0;
            tapeWriter.Write(recordedFrameCount);
            tapeWriter.Flush();
        }

        CloseTapeWriter();

        if (recordedFrameCount == 0)
        {
            string emptyPath = Path.Combine(Application.persistentDataPath, savedFileName);
            if (File.Exists(emptyPath)) File.Delete(emptyPath);
            savedFileName = "";
        }
        else
        {
            Debug.Log("Tape saved! Total Frames: " + recordedFrameCount);
        }

        currentFileName = "";
        recordedFrameCount = 0;
        return savedFileName;
    }

    private void CloseTapeWriter()
    {
        if (tapeWriter != null)
        {
            tapeWriter.Dispose();
            tapeWriter = null;
        }

        if (tapeStream != null)
        {
            tapeStream.Dispose();
            tapeStream = null;
        }
    }

    private void CleanUpCaptureResources()
    {
        if (filmCamera != null && filmCamera.targetTexture == captureTexture)
            filmCamera.targetTexture = null;

        if (RenderTexture.active == captureTexture)
            RenderTexture.active = null;

        if (captureTexture != null)
        {
            captureTexture.Release();
            Destroy(captureTexture);
            captureTexture = null;
        }

        if (screenShot != null)
        {
            Destroy(screenShot);
            screenShot = null;
        }
    }

    private void OnDestroy()
    {
        if (isRecording || tapeWriter != null) CancelRecording();
        else CleanUpCaptureResources();
    }
}
