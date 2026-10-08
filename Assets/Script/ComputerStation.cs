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
    private readonly List<SDCardItem> insertedCards = new List<SDCardItem>();

    private int selectedClipIndex = 0;
    private EquipmentInteractor currentInteractor;
    private Player.PlayerController.PlayerController playerController;
    private bool playerCouldMove = true;
    private bool playerCouldLook = true;
    private bool hasPlayerStateSnapshot = false;
    private CursorLockMode previousCursorLockState = CursorLockMode.Locked;
    private bool previousCursorVisible = false;

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
            if (card != null)
            {
                bool hasRecordings = card.GetRecordings().Count > 0;
                card = hotbar.TakeSDCard(true);
                if (card == null) return;
                currentInteractor = hotbar;
                card.transform.SetParent(transform, true);
                insertedCards.Add(card);
                RebuildInsertedFiles();
                UpdateUI();
                if (TutorialManager.Instance != null && hasRecordings) TutorialManager.Instance.OnCardInsertedToComputer();

                Debug.Log($"Inserted SD card: {card.GetRecordings().Count} clips, {card.UsedSeconds:0.#}/60s used.");
            }
            else Debug.LogWarning("Hold an SD card to insert it.");
        }
        else OpenComputerUI(hotbar);
    }

    public void OpenComputerUI(EquipmentInteractor interactor)
    {
        currentInteractor = interactor;

        if (!hasPlayerStateSnapshot)
        {
            playerController = interactor != null ? interactor.GetComponent<Player.PlayerController.PlayerController>() : null;
            if (playerController != null)
            {
                playerCouldMove = playerController.canMove;
                playerCouldLook = playerController.canLook;
                playerController.canMove = false;
                playerController.canLook = false;
            }

            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            hasPlayerStateSnapshot = true;
        }

        UITransition.ShowImmediately(computerUICanvas);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UpdateUI();

    }

    public void CloseFromShortcut()
    {
        var ui = computerUICanvas != null ? computerUICanvas.GetComponentInChildren<ComputerUIManager>(true) : null;
        if (ui != null && ui.BlocksCloseShortcut) return;
        CloseComputerUI();
    }

    public void CloseComputerUI()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanCloseUI("ComputerStation")) return;
        TruePixelPlayer player = FindObjectOfType<TruePixelPlayer>();
        if (player != null) player.StopTape();

        if (computerUICanvas != null) computerUICanvas.SetActive(false);

        if (hasPlayerStateSnapshot)
        {
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;

            if (playerController != null)
            {
                playerController.canMove = playerCouldMove;
                playerController.canLook = playerCouldLook;
            }

            playerController = null;
            hasPlayerStateSnapshot = false;
        }

        if (currentInteractor != null) currentInteractor.ClearActiveComputer();
        currentInteractor = null;
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
                insertedFiles[selectedClipIndex].duration = frames.Count / TapeSettings.framesPerSecond;
                SDCardStorage.SaveMetadata(insertedFiles[selectedClipIndex]);
                foreach (var card in insertedCards) if (card != null) card.RefreshLatestRecording();
                PlaySelectedClip();
            }
        }
    }

    public void EjectAllCards()
    {
        foreach (SDCardItem card in insertedCards) EjectCard(card);
        insertedCards.Clear();
        insertedFiles.Clear();
        selectedClipIndex = 0;
        UpdateUI();
    }

    public bool TryEjectCard(SDCardItem card)
    {
        if (card == null || !insertedCards.Contains(card)) return false;
        insertedCards.Remove(card);
        EjectCard(card);
        RebuildInsertedFiles();
        UpdateUI();
        return true;
    }

    public List<SDCardItem> GetInsertedCards()
    {
        var cards = new List<SDCardItem>();
        foreach (var card in insertedCards)
            if (card != null) { CardDisplayNumber(card); cards.Add(card); }
        cards.Sort((a, b) => a.CardNumber.CompareTo(b.CardNumber));
        return cards;
    }

    private void EjectCard(SDCardItem card)
    {
        if (card == null) return;
        card.RefreshLatestRecording();
        if (currentInteractor != null && currentInteractor.StoreEjectedCard(card)) return;
        Transform spawnLoc = ejectPoint != null ? ejectPoint : transform;
        card.OnDropped(null);
        card.gameObject.SetActive(true);
        card.transform.SetPositionAndRotation(spawnLoc.position, spawnLoc.rotation);
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

    private void RebuildInsertedFiles()
    {
        insertedFiles.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in insertedCards)
            if (card != null)
            {
                CardDisplayNumber(card);
                foreach (var clip in card.GetRecordings())
                    if (clip != null && !string.IsNullOrEmpty(clip.fileName) && seen.Add(clip.fileName)) insertedFiles.Add(clip);
            }
        selectedClipIndex = Mathf.Clamp(selectedClipIndex, 0, Mathf.Max(0, insertedFiles.Count - 1));
    }

    public List<FootageData> GetInsertedFiles() { return insertedFiles; }
    public bool HasInsertedCards => insertedCards.Exists(x => x != null);

    private int CardDisplayNumber(SDCardItem card)
    {
        return card.CardNumber;
    }

    public int GetSourceCardNumber(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return 0;
        string name = Path.GetFileName(fileName);
        foreach (var card in insertedCards)
        {
            if (card == null) continue;
            int number = CardDisplayNumber(card);
            foreach (var clip in card.GetRecordings())
                if (clip != null && string.Equals(clip.fileName, name, StringComparison.OrdinalIgnoreCase)) return number;
        }
        return 0; // Never invent a source for an unregistered/ejected recording.
    }

    public string StorageSummary()
    {
        float used = 0; int count = 0;
        foreach (var card in insertedCards) if (card != null) { used += card.UsedSeconds; count++; }
        return count == 0 ? "Insert an SD card to view its recordings" :
            $"{insertedFiles.Count} clips · {used:0.#} / {count * 60} seconds used · {count} SD card{(count == 1 ? "" : "s")}";
    }

    public bool TryRenameClip(string name, string newName, out string error)
    {
        var data = insertedFiles.Find(x => string.Equals(x.fileName, name, StringComparison.OrdinalIgnoreCase));
        if (data == null) { error = "Reinsert the SD card for this recording."; return false; }
        string previous = data.fileName;
        if (!SDCardStorage.TryRename(data, newName, out error)) return false;
        foreach (var card in insertedCards)
            if (card != null)
            {
                foreach (var clip in card.GetRecordings()) if (string.Equals(clip.fileName, previous, StringComparison.OrdinalIgnoreCase)) clip.fileName = data.fileName;
                card.RefreshLatestRecording();
            }
        if (ProjectDataManager.Instance != null && ProjectDataManager.Instance.compiledFootage != null)
            foreach (var clip in ProjectDataManager.Instance.compiledFootage)
                if (clip != null && string.Equals(clip.fileName, previous, StringComparison.OrdinalIgnoreCase)) clip.fileName = data.fileName;
        RebuildInsertedFiles(); UpdateUI();
        return true;
    }

    public bool TryDeleteClip(string name, out string error)
    {
        if (!insertedFiles.Exists(x => string.Equals(x.fileName, name, StringComparison.OrdinalIgnoreCase)))
        { error = "Reinsert the SD card for this recording."; return false; }
        if (!SDCardStorage.TryDelete(name, out error)) return false;
        RemoveDeletedFile(name);
        if (ProjectDataManager.Instance != null && ProjectDataManager.Instance.compiledFootage != null)
            ProjectDataManager.Instance.compiledFootage.RemoveAll(x => x != null && string.Equals(x.fileName, name, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    public void RemoveDeletedFile(string fileName)
    {
        foreach (var card in insertedCards) if (card != null) card.RemoveRecording(fileName);
        RebuildInsertedFiles(); UpdateUI();
    }
}
