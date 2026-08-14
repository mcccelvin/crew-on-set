using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using Player.Equipment;
using Player.Interactor;
using TMPro;
using UnityEngine.SceneManagement;

public class ComputerStation : MonoBehaviour, IInteractable
{
    [Header("Computer Settings")]
    public GameObject sdCardPrefab;
    public Transform ejectPoint;

    [Header("UI Settings")]
    public GameObject computerUICanvas;
    public TextMeshProUGUI clipListText;

    // FIX 1: We now store the full FootageData (which includes scores) instead of just the name!
    private List<FootageData> insertedFiles = new List<FootageData>();

    private int selectedClipIndex = 0;
    private EquipmentInteractor currentInteractor;

    private void Start() { if (computerUICanvas != null) computerUICanvas.SetActive(false); }

    public void OnInteract(GameObject player)
    {
        EquipmentInteractor hotbar = player.GetComponent<EquipmentInteractor>();
        if (hotbar == null) return;
        OpenComputerUI(hotbar);
    }

    public void TryInsertCard(EquipmentInteractor hotbar)
    {
        Equipment heldItem = hotbar.GetHeldItem();

        if (heldItem != null)
        {
            SDCardItem card = heldItem.GetComponent<SDCardItem>();
            if (card != null && card.isUsedCard && !string.IsNullOrEmpty(card.recordedFileName) && card.recordedFileName.EndsWith(".tape"))
            {
                // FIX 1: Capture the REAL scores from the physical SD Card!
                FootageData newData = new FootageData();
                newData.fileName = card.recordedFileName;
                newData.camScore = card.cameraScore;
                newData.lightScore = card.lightScore;
                newData.campaignLevel = card.campaignLevel;
                newData.shotType = card.shotType;
                newData.screenDirection = card.screenDirection;
                newData.actorPose = card.actorPose;
                newData.requiredSubjectsVisible = card.requiredSubjectsVisible;
                newData.usedSoftLight = card.usedSoftLight;
                newData.hasThreePointRoles = card.hasThreePointRoles;
                insertedFiles.Add(newData);

                hotbar.DestroyHeldItem();
                UpdateUI();
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnCardInsertedToComputer();

                Debug.Log($"Inserted {card.recordedFileName}. Real Scores - Cam: {card.cameraScore:F1}, Light: {card.lightScore:F1}");
            }
            else Debug.LogWarning("This SD card is either empty or not a valid tape!");
        }
        else OpenComputerUI(hotbar);
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
        TruePixelPlayer player = FindObjectOfType<TruePixelPlayer>();
        if (player != null) player.StopTape();

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
                if (i == selectedClipIndex) clipListText.text += $"-> [{i + 1}] {insertedFiles[i].fileName} <-\n";
                else clipListText.text += $"   [{i + 1}] {insertedFiles[i].fileName}\n";
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
        string fileNameToPlay = insertedFiles[selectedClipIndex].fileName;
        string filePathToPlay = Path.Combine(Application.persistentDataPath, fileNameToPlay);

        TruePixelPlayer player = FindObjectOfType<TruePixelPlayer>();
        if (player != null) player.PlayTape(filePathToPlay);
    }

    public void TrimStartOfClip() { TrimClip(true); }
    public void TrimEndOfClip() { TrimClip(false); }

    private void TrimClip(bool trimStart)
    {
        if (insertedFiles.Count == 0 || selectedClipIndex >= insertedFiles.Count) return;
        string fileName = insertedFiles[selectedClipIndex].fileName;
        string path = Path.Combine(Application.persistentDataPath, fileName);

        if (File.Exists(path))
        {
            List<byte[]> frames = ReadTapeFile(path);
            int framesToRemove = 8;

            if (frames.Count > framesToRemove)
            {
                if (trimStart) frames.RemoveRange(0, framesToRemove);
                else frames.RemoveRange(frames.Count - framesToRemove, framesToRemove);

                WriteTapeFile(path, frames);
                PlaySelectedClip();
            }
        }
    }

    public void EjectAllCards()
    {
        foreach (FootageData data in insertedFiles) EjectCard(data);
        insertedFiles.Clear();
        selectedClipIndex = 0;
        UpdateUI();
    }

    private void EjectCard(FootageData data)
    {
        if (sdCardPrefab == null) return;
        Transform spawnLoc = ejectPoint != null ? ejectPoint : transform;
        GameObject ejectedCard = Instantiate(sdCardPrefab, spawnLoc.position, spawnLoc.rotation);

        SDCardItem cardScript = ejectedCard.GetComponent<SDCardItem>();
        if (cardScript != null)
        {
            cardScript.isUsedCard = true;
            cardScript.recordedFileName = data.fileName;
            cardScript.cameraScore = data.camScore;
            cardScript.lightScore = data.lightScore;
            cardScript.campaignLevel = data.campaignLevel;
            cardScript.shotType = data.shotType;
            cardScript.screenDirection = data.screenDirection;
            cardScript.actorPose = data.actorPose;
            cardScript.requiredSubjectsVisible = data.requiredSubjectsVisible;
            cardScript.usedSoftLight = data.usedSoftLight;
            cardScript.hasThreePointRoles = data.hasThreePointRoles;
        }

        MeshRenderer renderer = ejectedCard.GetComponentInChildren<MeshRenderer>();
        if (renderer != null) renderer.material.color = Color.red;

        Collider col = ejectedCard.GetComponent<Collider>();
        if (col == null) col = ejectedCard.AddComponent<BoxCollider>();
        Rigidbody rb = ejectedCard.GetComponent<Rigidbody>();
        if (rb == null) rb = ejectedCard.AddComponent<Rigidbody>();

        rb.isKinematic = false; rb.useGravity = true;
        rb.AddForce(transform.up * 2f + transform.forward * 1.5f, ForceMode.Impulse);
    }

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

    public List<FootageData> GetInsertedFiles() { return insertedFiles; }

    public void RemoveDeletedFile(string fileName)
    {
        insertedFiles.RemoveAll(x => x.fileName == fileName);
    }
}
