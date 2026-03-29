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

    private List<string> insertedFiles = new List<string>();

    private int selectedClipIndex = 0;
    private EquipmentInteractor currentInteractor;

    private void Start() { if (computerUICanvas != null) computerUICanvas.SetActive(false); }

    // --- UPDATED: E Key now only handles opening the UI ---
    public void OnInteract(GameObject player)
    {
        EquipmentInteractor hotbar = player.GetComponent<EquipmentInteractor>();
        if (hotbar == null) return;

        OpenComputerUI(hotbar);
    }

    // --- NEW: This is called by EquipmentInteractor when you press F (Equip) ---
    public void TryInsertCard(EquipmentInteractor hotbar)
    {
        Equipment heldItem = hotbar.GetHeldItem();

        if (heldItem != null)
        {
            SDCardItem card = heldItem.GetComponent<SDCardItem>();
            // Check if it's a used .tape file
            if (card != null && card.isUsedCard && !string.IsNullOrEmpty(card.recordedFileName))
            {
                insertedFiles.Add(card.recordedFileName);
                hotbar.DestroyHeldItem();
                UpdateUI();

                Debug.Log($"Inserted {card.recordedFileName} into computer via F key.");
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnCardInsertedToComputer();
            }
        }
        else
        {
            // If hand is empty and we press F, just open the UI as a shortcut
            OpenComputerUI(hotbar);
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
        TruePixelPlayer player = FindObjectOfType<TruePixelPlayer>();
        if (player != null) player.StopAllCoroutines();

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

        TruePixelPlayer player = FindObjectOfType<TruePixelPlayer>();
        if (player != null) player.PlayTape(fileNameToPlay);
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

    public void SendToEditorScene()
    {
        if (insertedFiles.Count == 0) return;

        if (ProjectDataManager.Instance != null)
        {
            ProjectDataManager.Instance.ClearProject();

            foreach (string file in insertedFiles)
            {
                FootageData data = new FootageData();
                data.fileName = file;

                data.camScore = 70f;
                data.lightScore = 30f;

                ProjectDataManager.Instance.compiledFootage.Add(data);
            }
        }

        CloseComputerUI();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Editor");
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
        if (cardScript != null)
        {
            cardScript.isUsedCard = true;
            cardScript.recordedFileName = fileName;
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

    public List<string> GetInsertedFiles() { return insertedFiles; }

    public void RemoveDeletedFile(string fileName)
    {
        if (insertedFiles.Contains(fileName)) insertedFiles.Remove(fileName);
    }
}