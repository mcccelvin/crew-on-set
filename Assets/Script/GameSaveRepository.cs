using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class GameSaveValue
{
    public string key;
    public int kind; // 0 = integer, 1 = float, 2 = string
    public int integer;
    public float number;
    public string text;
}

[Serializable]
public sealed class GameSaveSlot
{
    public int version = 1;
    public string id;
    public string owner;
    public string name;
    public string revision;
    public string cloudRevision;
    public string updatedUtc;
    public List<GameSaveValue> values = new List<GameSaveValue>();
    public int Level => Mathf.Clamp(Int("CurrentLevel", 1), 1, 5);
    public int Money => Int("PlayerMoney", 0);
    public int Int(string key, int fallback) => values.Find(v => v.key == key && v.kind == 0)?.integer ?? fallback;
}

// File names are generated IDs, never player-entered save names. Each account has its own directory.
public sealed class GameSaveRepository
{
    public readonly string Owner;
    public readonly string Folder;
    public readonly List<GameSaveSlot> Slots = new List<GameSaveSlot>();
    public string Warning { get; private set; }

    public GameSaveRepository(string root, string owner)
    {
        Owner = owner;
        using (var hash = SHA256.Create())
            Folder = Path.Combine(root, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(owner))).Replace("-", ""));
        Directory.CreateDirectory(Folder);
        foreach (string path in Directory.GetFiles(Folder, "*.json"))
        {
            try { Slots.Add(Read(path)); }
            catch (Exception)
            {
                try { Slots.Add(Read(path + ".bak")); Warning = "A save was recovered from its backup."; }
                catch (Exception) { Warning = "A damaged save could not be loaded. Its files have been kept."; }
            }
        }
    }

    public GameSaveSlot Create(string name, List<GameSaveValue> values = null)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Game " + (Slots.Count + 1) : name.Trim();
        var slot = new GameSaveSlot { id = Guid.NewGuid().ToString("N"), owner = Owner, name = name.Substring(0, Math.Min(name.Length, 40)), values = Clone(values ?? new List<GameSaveValue>()) };
        Commit(slot);
        Slots.Add(slot);
        return slot;
    }

    public void Commit(GameSaveSlot slot)
    {
        slot.revision = Guid.NewGuid().ToString("N");
        slot.updatedUtc = DateTime.UtcNow.ToString("o");
        Write(slot);
    }

    public void Write(GameSaveSlot slot)
    {
        Validate(slot, Owner);
        string path = Path.Combine(Folder, slot.id + ".json");
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonUtility.ToJson(slot), Encoding.UTF8);
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }

    private GameSaveSlot Read(string path)
    {
        var slot = JsonUtility.FromJson<GameSaveSlot>(File.ReadAllText(path));
        Validate(slot, Owner);
        string expectedId = Path.GetFileName(path).Split('.')[0];
        if (slot.id != expectedId) throw new InvalidDataException("Save ID mismatch.");
        return slot;
    }

    public static void Validate(GameSaveSlot slot, string owner)
    {
        if (slot == null || slot.version != 1 || slot.owner != owner || !Guid.TryParseExact(slot.id, "N", out _) ||
            !Guid.TryParseExact(slot.revision, "N", out _) || slot.values == null || slot.values.Count > 2000 ||
            string.IsNullOrWhiteSpace(slot.name) || slot.name.Length > 60)
            throw new InvalidDataException("Invalid or unsupported game save.");
        var keys = new HashSet<string>();
        foreach (var value in slot.values)
            if (value == null || string.IsNullOrEmpty(value.key) || value.key.Length > 200 || value.kind < 0 || value.kind > 2 ||
                GameSavePrefs.IsGlobal(value.key) || !keys.Add(value.key) || (value.text != null && value.text.Length > 10000))
                throw new InvalidDataException("Invalid save value.");
    }

    // Keep both versions if two devices changed the same checkpoint while offline.
    // Never replace an active game with a late cloud response.
    public void MergeCloud(GameSaveSlot remote, string activeId)
    {
        Validate(remote, Owner);
        var local = Slots.Find(s => s.id == remote.id);
        if (local != null && local.revision == remote.revision)
        {
            local.cloudRevision = remote.revision;
            Write(local);
            return;
        }
        if (local != null && local.cloudRevision == remote.revision) return;
        if (local != null && (local.revision != local.cloudRevision || local.id == activeId))
        {
            // Preserve the local file and fork the incoming branch under a new ID.
            Create(remote.name + " (cloud copy)", remote.values);
            Warning = "Different local and cloud progress was found. Both copies were kept.";
            // Record the observed remote version so another sync does not duplicate it.
            local.cloudRevision = remote.revision;
            Write(local);
            return;
        }
        remote.cloudRevision = remote.revision;
        Write(remote);
        if (local != null) Slots.Remove(local);
        Slots.Add(remote);
    }

    public static List<GameSaveValue> Clone(List<GameSaveValue> source) => source.Select(v => new GameSaveValue { key = v.key, kind = v.kind, integer = v.integer, number = v.number, text = v.text }).ToList();
}
