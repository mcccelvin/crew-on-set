using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class TruePixelRecorder : MonoBehaviour
{
    public Camera filmCamera;

    [Header("Tape Quality Settings")]
    public int captureWidth = 640;  // Lower resolution saves massive disk space
    public int captureHeight = 360;
    public float framesPerSecond = 15f;
    [Range(10, 100)] public int jpgQuality = 50;

    private bool isRecording = false;
    private List<byte[]> recordedFrames = new List<byte[]>();

    public void StartRecording()
    {
        if (filmCamera == null) filmCamera = GetComponent<Camera>();
        recordedFrames.Clear();
        isRecording = true;
        StartCoroutine(RecordFramesCoroutine());
    }

    public string StopRecording()
    {
        isRecording = false;
        StopAllCoroutines();
        return SaveTapeToDisk();
    }

    private IEnumerator RecordFramesCoroutine()
    {
        // Create a temporary darkroom to develop our photos
        RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24);
        Texture2D screenShot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);

        while (isRecording)
        {
            // Wait for the game to finish drawing the screen
            yield return new WaitForEndOfFrame();

            // Force the camera to draw to our custom texture
            filmCamera.targetTexture = rt;
            filmCamera.Render();
            RenderTexture.active = rt;

            // Read the actual pixels
            screenShot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenShot.Apply();

            // Reset the camera back to normal
            filmCamera.targetTexture = null;
            RenderTexture.active = null;

            // Compress the image and save it to our RAM list
            recordedFrames.Add(screenShot.EncodeToJPG(jpgQuality));

            // Wait until it is time to snap the next frame
            yield return new WaitForSeconds(1f / framesPerSecond);
        }

        Destroy(rt);
        Destroy(screenShot);
    }

    private string SaveTapeToDisk()
    {
        if (recordedFrames.Count == 0) return "";

        string fileName = "Film_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tape";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        // Pack all the compressed JPGs into one single, space-efficient binary file
        using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            writer.Write(recordedFrames.Count);
            foreach (byte[] frame in recordedFrames)
            {
                writer.Write(frame.Length);
                writer.Write(frame);
            }
        }

        Debug.Log("Tape saved! Total Frames: " + recordedFrames.Count);
        return fileName;
    }
}