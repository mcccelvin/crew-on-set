using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class TruePixelPlayer : MonoBehaviour
{
    [Tooltip("Drag the RawImage from your Computer UI Canvas here")]
    public RawImage computerScreen;
    public float framesPerSecond = 60f;

    public void PlayTape(string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(path))
        {
            StopAllCoroutines();
            StartCoroutine(PlayVideoCoroutine(path));
        }
        else
        {
            Debug.LogError("Could not find the tape file!");
        }
    }

    private IEnumerator PlayVideoCoroutine(string path)
    {
        List<byte[]> rawFrames = new List<byte[]>();

        // 1. Unpack the binary file (Lightning fast)
        using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
        {
            int frameCount = reader.ReadInt32();
            for (int i = 0; i < frameCount; i++)
            {
                rawFrames.Add(reader.ReadBytes(reader.ReadInt32()));
            }
        }

        Debug.Log("Computer: Buffering video into memory...");
        List<Texture2D> preloadedTextures = new List<Texture2D>();

        // 2. THE FIX: Pre-decode all the JPGs into memory BEFORE playing!
        foreach (byte[] frame in rawFrames)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(frame); // This is the heavy math causing your lag
            tex.Apply();
            preloadedTextures.Add(tex);

            // Yielding here prevents the game from completely freezing while it loads
            yield return null;
        }

        Debug.Log("Computer: Buffering complete. Playing video!");

        // 3. Play the pre-loaded textures (Buttery smooth 60 FPS!)
        foreach (Texture2D frameTex in preloadedTextures)
        {
            if (computerScreen != null) computerScreen.texture = frameTex;
            yield return new WaitForSeconds(1f / framesPerSecond);
        }

        Debug.Log("Computer: Playback finished. Cleaning up memory.");

        // 4. CRUCIAL: Destroy the textures to empty out the RAM!
        foreach (Texture2D tex in preloadedTextures)
        {
            Destroy(tex);
        }
    }
}