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

    private void Start()
    {
        if (computerUICanvas != null) computerUICanvas.SetActive(false);
    }

    public void OnInteract(GameObject player)
    {
        EquipmentInteractor hotbar = player.GetComponent<EquipmentInteractor>();
        if (hotbar == null) return;

        Equipment heldItem = hotbar.GetHeldItem();

        if (heldItem != null)
        {
            SDCardItem card = heldItem.GetComponent<SDCardItem>();
            if (card != null && card.isUsedCard && !string.IsNullOrEmpty(card.recordedFileName))
            {
                insertedFiles.Add(card.recordedFileName);
                hotbar.DestroyHeldItem();
                UpdateUI();
            }
        }
        else if (insertedFiles.Count > 0)
        {
            CompileAndEject();
        }
    }

    public void OpenComputerUI(EquipmentInteractor interactor)
    {
        currentInteractor = interactor;
        if (computerUICanvas != null) computerUICanvas.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UpdateUI();
    }

    public void CloseComputerUI()
    {
        // SAFETY: Stop the video if we walk away while it's playing!
        ReplayManager replayManager = FindObjectOfType<ReplayManager>();
        if (replayManager != null) replayManager.TriggerStopPreview();

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

    // CHANGED: Now previews the video INSIDE the UI instead of closing the screen!
    public void PlaySelectedClip()
    {
        if (insertedFiles.Count == 0 || selectedClipIndex >= insertedFiles.Count) return;

        string fileNameToPlay = insertedFiles[selectedClipIndex];

        ReplayManager replayManager = FindObjectOfType<ReplayManager>();
        if (replayManager != null)
        {
            replayManager.TriggerStopPreview(); // Stop any currently playing video first

            RecordableTransform[] recordables = FindObjectsOfType<RecordableTransform>();
            foreach (var rec in recordables) rec.LoadFromSpecificFile(fileNameToPlay);

            replayManager.TriggerPreviewReplay(); // Start the preview!
        }
    }

    // --- NEW: TRIMMING LOGIC ---

    public void TrimStartOfClip()
    {
        TrimClip(true);
    }

    public void TrimEndOfClip()
    {
        TrimClip(false);
    }

    private void TrimClip(bool trimStart)
    {
        if (insertedFiles.Count == 0 || selectedClipIndex >= insertedFiles.Count) return;

        string fileName = insertedFiles[selectedClipIndex];
        string path = Path.Combine(Application.persistentDataPath, fileName);

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            RecordingData clip = JsonUtility.FromJson<RecordingData>(json);

            int framesToRemove = 25; // 25 frames is about half a second of video

            if (clip.points.Count > framesToRemove)
            {
                if (trimStart)
                    clip.points.RemoveRange(0, framesToRemove); // Chop off the beginning
                else
                    clip.points.RemoveRange(clip.points.Count - framesToRemove, framesToRemove); // Chop off the end

                // Save the trimmed file back to the hard drive!
                File.WriteAllText(path, JsonUtility.ToJson(clip));
                Debug.Log($"Computer: Trimmed 0.5 seconds from the {(trimStart ? "START" : "END")} of {fileName}");

                // Automatically replay the clip so you can see your trim!
                PlaySelectedClip();
            }
            else
            {
                Debug.LogWarning("Computer: Clip is too short to trim anymore!");
            }
        }
    }

    // ---------------------------

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

    private void CompileAndEject()
    {
        RecordingData finalMovie = new RecordingData();
        foreach (string fileName in insertedFiles)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                RecordingData clip = JsonUtility.FromJson<RecordingData>(json);
                finalMovie.points.AddRange(clip.points);
            }
        }

        string finalFileName = $"CompiledMovie_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.json";
        File.WriteAllText(Path.Combine(Application.persistentDataPath, finalFileName), JsonUtility.ToJson(finalMovie));

        EjectCard(finalFileName); // Spit out the gold card
        insertedFiles.Clear();
        UpdateUI();
    }

    public void OnDrop() { }
}