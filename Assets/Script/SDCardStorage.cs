using System;
using System.IO;
using UnityEngine;

// Raw tape names stay compatible with playback/editor imports. Metadata lives
// beside the tape, so a renamed/reinserted legacy card retains its take evidence.
public static class SDCardStorage
{
    public const float CapacitySeconds = 60f;

    public static bool TryPath(string name, out string path, string root = null)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) ||
            name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.EndsWith(".tape", StringComparison.OrdinalIgnoreCase)) return false;
        string directory = Path.GetFullPath(root ?? Application.persistentDataPath);
        path = Path.GetFullPath(Path.Combine(directory, name));
        return string.Equals(Path.GetDirectoryName(path), directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    public static float ReadDuration(string name, string root = null)
    {
        if (!TryPath(name, out string path, root) || !File.Exists(path)) return 0;
        try
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
                return Mathf.Max(0, reader.ReadInt32()) / TapeSettings.framesPerSecond;
        }
        catch (IOException) { return 0; }
    }

    public static FootageData ReadMetadata(string name)
    {
        if (!TryPath(name, out string path)) return null;
        try { return File.Exists(path + ".json") ? JsonUtility.FromJson<FootageData>(File.ReadAllText(path + ".json")) : null; }
        catch (Exception) { return null; }
    }

    public static void SaveMetadata(FootageData data, string root = null)
    {
        if (data == null || !TryPath(data.fileName, out string path, root)) return;
        File.WriteAllText(path + ".json", JsonUtility.ToJson(data));
    }

    public static bool TryRename(FootageData data, string newName, out string error, string root = null)
    {
        error = "";
        string stem = (newName ?? "").Trim();
        if (stem.EndsWith(".tape", StringComparison.OrdinalIgnoreCase)) stem = stem.Substring(0, stem.Length - 5);
        if (stem.Length == 0 || stem.Length > 48 || stem.EndsWith(".") || stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            stem.IndexOf('/') >= 0 || stem.IndexOf('\\') >= 0 || stem == "." || stem == "..")
        { error = "Use a name of 1–48 characters without file-path symbols."; return false; }
        string reserved = stem.Split('.')[0].ToUpperInvariant();
        if (reserved == "CON" || reserved == "PRN" || reserved == "AUX" || reserved == "NUL" ||
            System.Text.RegularExpressions.Regex.IsMatch(reserved, "^(COM|LPT)[0-9]$"))
        { error = "That name is reserved. Choose a different name."; return false; }
        string name = stem + ".tape";
        if (data == null || !TryPath(data.fileName, out string oldPath, root) || !TryPath(name, out string path, root) || !File.Exists(oldPath))
        { error = "This recording is unavailable."; return false; }
        if (string.Equals(data.fileName, name, StringComparison.OrdinalIgnoreCase)) return true;
        if (File.Exists(path) || File.Exists(path + ".json"))
        { error = "A recording already has that name."; return false; }
        string previous = data.fileName;
        try
        {
            data.fileName = name;
            SaveMetadata(data, root);
            File.Move(oldPath, path);
        }
        catch (Exception)
        {
            data.fileName = previous;
            try { if (File.Exists(path + ".json")) File.Delete(path + ".json"); } catch (Exception) { }
            error = "Couldn't rename this recording. Its original file was kept.";
            return false;
        }
        // The media move succeeded; obsolete metadata must not undo that success.
        try { if (File.Exists(oldPath + ".json")) File.Delete(oldPath + ".json"); } catch (Exception) { }
        return true;
    }

    public static bool TryDelete(string name, out string error, string root = null)
    {
        error = "";
        if (!TryPath(name, out string path, root)) { error = "Invalid recording."; return false; }
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception) { error = "Couldn't delete this recording. Close its playback and try again."; return false; }
        try { if (File.Exists(path + ".json")) File.Delete(path + ".json"); } catch (Exception) { }
        return true;
    }
}
