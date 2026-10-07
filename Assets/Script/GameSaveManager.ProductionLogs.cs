using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public sealed partial class GameSaveManager
{
    public const string ProductionLogsKey = "production_logs";
    public string AuthenticatedPlayerId => HasCloudSession ? authenticatedId : null;
    [Serializable] private sealed class LogReceipt { public string id, decision, verifiedUtc; }
    [Serializable] private sealed class LogJournal
    {
        public string owner;
        public List<ProductionLogRecord> pending = new List<ProductionLogRecord>();
        public List<LogReceipt> verified = new List<LogReceipt>();
    }
    private LogJournal productionJournal;
    private ProductionLogUpload productionUpload;
    private float nextProductionSync;
    private string productionJournalKey;
    public string ProductionLogStatus => productionUpload?.Status ?? "Production results are saved locally; sign in to upload.";
    public bool ProductionLogsSyncing => productionUpload != null && productionUpload.Busy;
    public bool VerifiedPassedProduction => productionJournal?.verified.Exists(r => r.decision == "passed") == true;
    public bool VerifiedFailedProduction => productionJournal?.verified.Exists(r => r.decision == "failed") == true;

    private void BindProductionLogs(string owner)
    {
        productionUpload?.Dispose();
        using (var hash = SHA256.Create())
            productionJournalKey = "SaveSystem.ProductionLogs.v1." + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(owner))).Replace("-", "");
        var json = new StringBuilder();
        int count = PlayerPrefs.GetInt(productionJournalKey + ".Count", 0);
        for (int i = 0; i < count; i++) json.Append(PlayerPrefs.GetString(productionJournalKey + "." + i, ""));
        try { productionJournal = JsonUtility.FromJson<LogJournal>(json.ToString()); }
        catch (ArgumentException) { productionJournal = null; }
        // Do not silently replace a damaged queue. The completed career snapshots are also retained.
        if (count > 0 && (productionJournal == null || productionJournal.owner != owner || productionJournal.pending == null || productionJournal.verified == null))
        {
            productionUpload = null; productionJournal = null;
            Debug.LogWarning("Production log queue could not be read. Its stored data has been kept.");
            return;
        }
        if (productionJournal == null) productionJournal = new LogJournal { owner = owner };
        var serializer = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
        productionUpload = new ProductionLogUpload(owner, productionJournal.pending, new LogTransport(this, owner, session),
            value => serializer.DeserializeObject(value), value => serializer.SerializeObject(value), SaveProductionJournal,
            record => {
                if (!productionJournal.verified.Exists(r => r.id == record.id))
                    productionJournal.verified.Add(new LogReceipt { id = record.id, decision = record.decision, verifiedUtc = DateTime.UtcNow.ToString("o") });
                Debug.Log("PlayFab production log read-back verified: " + record.decision + ", level " + record.level + ", " + record.role + ".");
            });
        productionUpload.Finished += () => { nextProductionSync = Time.unscaledTime + 60f; Changed?.Invoke(); };
        nextProductionSync = Time.unscaledTime;
    }
    private void SaveProductionJournal()
    {
        string json = JsonUtility.ToJson(productionJournal);
        int oldCount = PlayerPrefs.GetInt(productionJournalKey + ".Count", 0), count = (json.Length + 7999) / 8000;
        for (int i = 0; i < count; i++) PlayerPrefs.SetString(productionJournalKey + "." + i, json.Substring(i * 8000, Math.Min(8000, json.Length - i * 8000)));
        for (int i = count; i < oldCount; i++) PlayerPrefs.DeleteKey(productionJournalKey + "." + i);
        PlayerPrefs.SetInt(productionJournalKey + ".Count", count); PlayerPrefs.Save();
    }
    public void RecordProduction(ProductionLogRecord record)
    {
        // Guest results remain in the career snapshot until that career is linked to an account.
        if (record == null || string.IsNullOrWhiteSpace(record.playerId) || record.playerId == "guest") return;
        if (productionJournal == null || productionJournal.owner != record.playerId)
        {
            if (PlayerPrefs.GetString("PlayFabId", "") != record.playerId) return;
            BindProductionLogs(record.playerId);
        }
        if (productionJournal == null || !record.IsOwnedBy(productionJournal.owner) ||
            productionJournal.verified.Exists(r => r.id == record.id) || productionJournal.pending.Exists(r => r.id == record.id)) return;
        productionJournal.pending.Add(record);
        SaveProductionJournal(); // Write ahead of any network request, outside career rollback/retry.
        nextProductionSync = Time.unscaledTime; Changed?.Invoke();
    }
    private void RecoverProductionLogs()
    {
        if (Repository == null || authenticatedId != Repository.Owner) return;
        if (productionJournal == null || productionJournal.owner != authenticatedId) BindProductionLogs(authenticatedId);
        foreach (var slot in Repository.Slots)
        {
            var repeatedLegacy = new Dictionary<string, int>();
            foreach (var attempt in PlayerAnalytics.ProfileHistory(slot))
            {
                var record = attempt.productionLog;
                if (!attempt.closed) continue;
                if (record == null)
                {
                    record = ProductionLogRecord.FromLegacyAttempt(slot, attempt, authenticatedId);
                    if (record == null) continue; // Unlocks and personal-best keys are not submitted results.
                    repeatedLegacy.TryGetValue(record.submissionId, out int occurrence);
                    repeatedLegacy[record.submissionId] = occurrence + 1;
                    // Keep distinct, identical submissions in a retained history, but
                    // deduplicate copied careers and repeat logins using stable IDs.
                    if (occurrence > 0)
                    {
                        record.submissionId += "-" + occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        record.productionId = record.submissionId;
                        record.id = record.submissionId + ":" + authenticatedId;
                    }
                }
                if (string.IsNullOrEmpty(record.playerId))
                {
                    record = JsonUtility.FromJson<ProductionLogRecord>(JsonUtility.ToJson(record));
                    record.playerId = authenticatedId; record.id = record.submissionId + ":" + authenticatedId;
                }
                RecordProduction(record);
            }
        }
    }
    private void TickProductionLogs()
    {
        if (Syncing || productionUpload == null || productionUpload.Busy || Time.unscaledTime < nextProductionSync ||
            productionJournal?.owner != authenticatedId || !HasCloudSession) return;
        nextProductionSync = float.PositiveInfinity;
        productionUpload.TrySync();
    }
    private sealed class LogTransport : IProductionLogTransport
    {
        private readonly GameSaveManager manager;
        private readonly string owner;
        private readonly int generation;
        public LogTransport(GameSaveManager manager, string owner, int generation)
        { this.manager = manager; this.owner = owner; this.generation = generation; }
        private bool Current => manager != null && manager.session == generation && manager.authenticatedId == owner && manager.HasCloudSession;
        public void Read(Action<string> success, Action<string> failure)
        {
            if (!Current) { failure("Sign in to the result's account to upload."); return; }
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { ProductionLogsKey } }, result => {
                if (!Current) return;
                success(result.Data != null && result.Data.TryGetValue(ProductionLogsKey, out var entry) ? entry.Value : null);
            }, error => {
                if (!Current) return;
                failure("PlayFab could not read production logs. Results kept locally; retrying automatically.");
                manager.HandleSessionError(error);
            });
        }
        public void Write(string json, Action success, Action<string> failure)
        {
            if (!Current) { failure("Sign in to the result's account to upload."); return; }
            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest {
                Permission = UserDataPermission.Private, Data = new Dictionary<string, string> { { ProductionLogsKey, json } }
            }, result => { if (Current) success(); }, error => {
                if (!Current) return;
                failure("PlayFab rejected the production-log upload (" + error.Error + "). Full results kept locally; retrying automatically.");
                manager.HandleSessionError(error);
            });
        }
    }
}
