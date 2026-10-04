using System.Collections.Generic;
using UnityEngine;

// Gameplay preferences belong to the selected career. Account identity and options remain global.
// With no selected career, direct Editor scene testing retains the original PlayerPrefs behavior.
public static class GameSavePrefs
{
    public static List<GameSaveValue> Values { get; private set; }
    private static List<GameSaveValue> roomValues;
    public static bool IsRoomSession => roomValues != null;
    private static List<GameSaveValue> Current => roomValues ?? Values;
    public static void BeginRoomSession() { roomValues = new List<GameSaveValue>(); }
    public static void EndRoomSession() { roomValues = null; }
    public static bool IsGlobal(string key) => key == "PlayerName" || key == "PlayFabId" || key == "LastEmail" || key.StartsWith("Options.") || key.StartsWith("SaveSystem.");
    private static bool Local(string key) => Current != null && !IsGlobal(key);
    public static void Activate(GameSaveSlot slot) { Values = slot == null ? null : GameSaveRepository.Clone(slot.values); }
    public static bool HasKey(string key) => Local(key) ? Current.Exists(v => v.key == key) : PlayerPrefs.HasKey(key);
    public static int GetInt(string key, int fallback = 0) => Local(key) ? Current.Find(v => v.key == key && v.kind == 0)?.integer ?? fallback : PlayerPrefs.GetInt(key, fallback);
    public static float GetFloat(string key, float fallback = 0f) => Local(key) ? Current.Find(v => v.key == key && v.kind == 1)?.number ?? fallback : PlayerPrefs.GetFloat(key, fallback);
    public static string GetString(string key, string fallback = "") => Local(key) ? Current.Find(v => v.key == key && v.kind == 2)?.text ?? fallback : PlayerPrefs.GetString(key, fallback);
    private static void Set(GameSaveValue value) { Current.RemoveAll(v => v.key == value.key); Current.Add(value); }
    public static void SetInt(string key, int value) { if (Local(key)) Set(new GameSaveValue { key = key, integer = value }); else PlayerPrefs.SetInt(key, value); }
    public static void SetFloat(string key, float value) { if (Local(key)) Set(new GameSaveValue { key = key, kind = 1, number = value }); else PlayerPrefs.SetFloat(key, value); }
    public static void SetString(string key, string value) { if (Local(key)) Set(new GameSaveValue { key = key, kind = 2, text = value }); else PlayerPrefs.SetString(key, value); }
    public static void DeleteKey(string key) { if (Local(key)) Current.RemoveAll(v => v.key == key); else PlayerPrefs.DeleteKey(key); }
    public static void Save() { PlayerPrefs.Save(); }
    public static void DeleteAll()
    {
        if (roomValues != null) { roomValues.Clear(); return; }
        if (Values != null) { Values.Clear(); GameSaveManager.Instance?.SaveCheckpoint(); }
        else { foreach (var entry in LegacyGameSave.Read(false)) PlayerPrefs.DeleteKey(entry.key); PlayerPrefs.Save(); }
    }
}
