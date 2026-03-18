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
            if (card != null && card.isUsedCard && !string.IsNullOrEmpty(card.recordedFileName))
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
    }

    public void CloseComputerUI()
    {
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

    public void PlaySelectedClip()
    {
        if (insertedFiles.Count == 0 || selectedClipIndex >= insertedFiles.Count) return;
        string fileNameToPlay = insertedFiles[selectedClipIndex];
        ReplayManager replayManager = FindObjectOfType<ReplayManager>();
        if (replayManager != null)
        {
            replayManager.TriggerStopPreview();
            replayManager.PlayMovieOnScreen(fileNameToPlay);
        }
    }

    public void TrimStartOfClip() { TrimClip(true); }
    public void TrimEndOfClip() { TrimClip(false); }

    private void TrimClip(bool trimStart)
    {
        if (insertedFiles.Count == 0 || selectedClipIndex >= insertedFiles.Count) return;
        string fileName = insertedFiles[selectedClipIndex];
        string path = Path.Combine(Application.persistentDataPath, fileName);

        if (File.Exists(path))
        {
            MasterTape tape = JsonUtility.FromJson<MasterTape>(File.ReadAllText(path));
            int framesToRemove = 25;
            bool hasEnoughFrames = false;

            foreach (var track in tape.tracks)
            {
                if (track.points.Count > framesToRemove)
                {
                    hasEnoughFrames = true;
                    if (trimStart) track.points.RemoveRange(0, framesToRemove);
                    else track.points.RemoveRange(track.points.Count - framesToRemove, framesToRemove);
                }
            }

            if (hasEnoughFrames)
            {
                File.WriteAllText(path, JsonUtility.ToJson(tape));
                PlaySelectedClip();
            }
        }
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

    public void CompileMovie()
    {
        MasterTape finalMovie = new MasterTape();
        foreach (string fileName in insertedFiles)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(path))
            {
                MasterTape clip = JsonUtility.FromJson<MasterTape>(File.ReadAllText(path));
                foreach (var clipTrack in clip.tracks)
                {
                    ObjectTrack existingTrack = finalMovie.tracks.Find(t => t.id == clipTrack.id);
                    if (existingTrack != null) existingTrack.points.AddRange(clipTrack.points);
                    else
                    {
                        ObjectTrack newTrack = new ObjectTrack();
                        newTrack.id = clipTrack.id;
                        newTrack.points = new List<PointInTime>(clipTrack.points);
                        finalMovie.tracks.Add(newTrack);
                    }
                }
            }
        }

        string finalFileName = $"CompiledMovie_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.json";
        File.WriteAllText(Path.Combine(Application.persistentDataPath, finalFileName), JsonUtility.ToJson(finalMovie));
        EjectCard(finalFileName);
        insertedFiles.Clear();
        UpdateUI();
    }

    public void OnDrop() { }
}