using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using Player.Equipment;
using Player.Interactor;
using TMPro;

public class ComputerStation : MonoBehaviour, IInteractable
{
    [Header("Computer Settings")]
    public GameObject sdCardPrefab;
    public Transform ejectPoint;

    [Header("UI Settings")]
    public GameObject computerUICanvas;
    public TextMeshProUGUI clipListText;

    private List<string> insertedFiles = new List<string>();
    private int selectedClipIndex = 0;
    private EquipmentInteractor currentInteractor;

    private void Start() { if (computerUICanvas != null) computerUICanvas.SetActive(false); }

    public void OnInteract(GameObject player)
    {
        EquipmentInteractor hotbar = player.GetComponent<EquipmentInteractor>();
        if (hotbar == null) return;
        Equipment heldItem = hotbar.GetHeldItem();

        if (heldItem != null)
        {
            SDCardItem card = heldItem.GetComponent<SDCardItem>();
            // Make sure it only accepts our new .tape files!
            if (card != null && card.isUsedCard && !string.IsNullOrEmpty(card.recordedFileName) && card.recordedFileName.EndsWith(".tape"))
            {
                insertedFiles.Add(card.recordedFileName);
                hotbar.DestroyHeldItem();
                UpdateUI();
            }
        }
    }

    public void OpenComputerUI(EquipmentInteractor interactor)
    {
        currentInteractor = interactor;
        if (computerUICanvas != null) computerUICanvas.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UpdateUI();
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVideoPlayed();
    }

    public void CloseComputerUI()
    {
        TruePixelPlayer player = FindObjectOfType<TruePixelPlayer>();
        if (player != null) player.StopAllCoroutines(); // Stop the video when closing

        if (computerUICanvas != null) computerUICanvas.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (currentInteractor != null) currentInteractor.ClearActiveComputer();
    }

    private void UpdateUI()
    {
        if (clipListText != null)
        {
            clipListText.text = $"Clips Inserted: {insertedFiles.Count}\n\n";
            for (int i = 0; i < insertedFiles.Count; i++)
            {
                if (i == selectedClipIndex) clipListText.text += $"-> [{i + 1}] {insertedFiles[i]} <-\n";
                else clipListText.text += $"   [{i + 1}] {insertedFiles[i]}\n";
            }
        }
    }

    public void SelectNextClip()
    {
        if (insertedFiles.Count == 0) return;
        selectedClipIndex++;
        if (selectedClipIndex >= insertedFiles.Count) selectedClipIndex = 0;
        UpdateUI();
    }

    // THE FIX: Send the .tape file to your new Pixel Player!
    public void PlaySelectedClip()
    {
        if (insertedFiles.Count == 0 || selectedClipIndex >= insertedFiles.Count) return;
        string fileNameToPlay = insertedFiles[selectedClipIndex];

        TruePixelPlayer player = FindObjectOfType<TruePixelPlayer>();
        if (player != null) player.PlayTape(fileNameToPlay);
    }

    // --- UPGRADED BINARY TRIMMING ---
    public void TrimStartOfClip() { TrimClip(true); }
    public void TrimEndOfClip() { TrimClip(false); }

    private void TrimClip(bool trimStart)
    {
        if (insertedFiles.Count == 0 || selectedClipIndex >= insertedFiles.Count) return;
        string fileName = insertedFiles[selectedClipIndex];
        string path = Path.Combine(Application.persistentDataPath, fileName);

        if (File.Exists(path))
        {
            List<byte[]> frames = ReadTapeFile(path);
            int framesToRemove = 8; // At 15fps, 8 frames is about half a second

            if (frames.Count > framesToRemove)
            {
                if (trimStart) frames.RemoveRange(0, framesToRemove);
                else frames.RemoveRange(frames.Count - framesToRemove, framesToRemove);

                WriteTapeFile(path, frames);
                PlaySelectedClip();
            }
        }
    }

    // --- UPGRADED BINARY COMPILING ---
    public void CompileMovie()
    {
        if (insertedFiles.Count == 0) return;

        List<byte[]> finalMovieFrames = new List<byte[]>();

        foreach (string fileName in insertedFiles)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(path))
            {
                finalMovieFrames.AddRange(ReadTapeFile(path));
            }
        }

        string finalFileName = $"CompiledMovie_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.tape";
        WriteTapeFile(Path.Combine(Application.persistentDataPath, finalFileName), finalMovieFrames);

        EjectCard(finalFileName);
        insertedFiles.Clear();
        UpdateUI();
    }

    public void EjectAllCards()
    {
        foreach (string file in insertedFiles) EjectCard(file);
        insertedFiles.Clear();
        selectedClipIndex = 0;
        UpdateUI();
    }

    private void EjectCard(string fileName)
    {
        if (sdCardPrefab == null) return;
        Transform spawnLoc = ejectPoint != null ? ejectPoint : transform;
        GameObject ejectedCard = Instantiate(sdCardPrefab, spawnLoc.position, spawnLoc.rotation);

        SDCardItem cardScript = ejectedCard.GetComponent<SDCardItem>();
        if (cardScript != null) { cardScript.isUsedCard = true; cardScript.recordedFileName = fileName; }

        MeshRenderer renderer = ejectedCard.GetComponentInChildren<MeshRenderer>();
        if (renderer != null) renderer.material.color = Color.red;

        Collider col = ejectedCard.GetComponent<Collider>();
        if (col == null) col = ejectedCard.AddComponent<BoxCollider>();
        Rigidbody rb = ejectedCard.GetComponent<Rigidbody>();
        if (rb == null) rb = ejectedCard.AddComponent<Rigidbody>();

        rb.isKinematic = false; rb.useGravity = true;
        rb.AddForce(transform.up * 2f + transform.forward * 1.5f, ForceMode.Impulse);
    }

    // --- HELPER METHODS FOR BINARY FILES ---
    private List<byte[]> ReadTapeFile(string path)
    {
        List<byte[]> frames = new List<byte[]>();
        using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
        {
            int frameCount = reader.ReadInt32();
            for (int i = 0; i < frameCount; i++) frames.Add(reader.ReadBytes(reader.ReadInt32()));
        }
        return frames;
    }

    private void WriteTapeFile(string path, List<byte[]> frames)
    {
        using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            writer.Write(frames.Count);
            foreach (byte[] frame in frames) { writer.Write(frame.Length); writer.Write(frame); }
        }
    }

    public void OnDrop() { }
}