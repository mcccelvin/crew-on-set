using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class TruePixelPlayer : MonoBehaviour
{
    public RawImage computerScreen;
    public float framesPerSecond = 24f;

    [Header("UI Buttons")]
    public GameObject playButtonUI;
    public GameObject pauseButtonUI;

    private List<Texture2D> preloadedTextures = new List<Texture2D>();
    private bool isPaused = false;
    private bool isFinished = false; // Tracks if the tape reached the end
    private string currentTapePath;  // Remembers which video we are watching

    public void PlayTape(string path)
    {
        if (File.Exists(path))
        {
            currentTapePath = path;
            StopTape();

            isPaused = false;
            isFinished = false;
            UpdatePlayPauseUI(); // Instantly show the Pause button

            StartCoroutine(PlayVideoCoroutine(path));
        }
    }

    public void StopTape()
    {
        StopAllCoroutines();
        isPaused = true;
        isFinished = true;
        if (computerScreen != null) computerScreen.texture = null;

        foreach (Texture2D tex in preloadedTextures)
        {
            if (tex != null) Destroy(tex);
        }
        preloadedTextures.Clear();
    }

    // Link BOTH your Play and Pause buttons to this exact method!
    public void TogglePlayPause()
    {
        // If the video ended, pressing Play restarts it from the beginning!
        if (isFinished && !string.IsNullOrEmpty(currentTapePath))
        {
            PlayTape(currentTapePath);
            return;
        }

        isPaused = !isPaused; // Flips between true and false
        UpdatePlayPauseUI();
    }

    private void UpdatePlayPauseUI()
    {
        // If paused (or finished), show Play. If playing, show Pause.
        if (playButtonUI != null) playButtonUI.SetActive(isPaused);
        if (pauseButtonUI != null) pauseButtonUI.SetActive(!isPaused);
    }

    private IEnumerator PlayVideoCoroutine(string path)
    {
        List<byte[]> rawFrames = new List<byte[]>();

        using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
        {
            int frameCount = reader.ReadInt32();
            for (int i = 0; i < frameCount; i++)
            {
                rawFrames.Add(reader.ReadBytes(reader.ReadInt32()));
            }
        }

        if (rawFrames.Count == 0) yield break;

        Texture2D thumbnail = new Texture2D(2, 2);
        thumbnail.LoadImage(rawFrames[0]);
        thumbnail.Apply();
        preloadedTextures.Add(thumbnail);
        if (computerScreen != null) computerScreen.texture = thumbnail;

        for (int i = 1; i < rawFrames.Count; i++)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(rawFrames[i]);
            tex.Apply();
            preloadedTextures.Add(tex);
            if (i % 5 == 0) yield return null;
        }

        for (int i = 0; i < preloadedTextures.Count; i++)
        {
            // Wait right here if paused!
            while (isPaused)
            {
                yield return null;
            }

            if (computerScreen != null) computerScreen.texture = preloadedTextures[i];
            yield return new WaitForSeconds(1f / framesPerSecond);
        }

        // END OF TAPE
        if (computerScreen != null) computerScreen.texture = thumbnail;

        isFinished = true;
        isPaused = true;
        UpdatePlayPauseUI(); // Switch back to the Play button!

        for (int i = 1; i < preloadedTextures.Count; i++)
        {
            if (preloadedTextures[i] != null) Destroy(preloadedTextures[i]);
        }
        preloadedTextures.Clear();
        preloadedTextures.Add(thumbnail);
    }
}