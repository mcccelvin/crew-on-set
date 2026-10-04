using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public interface IProductionLogTransport
{
    void Read(Action<string> success, Action<string> failure);
    void Write(string json, Action success, Action<string> failure);
}

// Leaves unknown properties, old entries and an existing envelope intact. Never guesses
// at an unrecognised remote format or deletes old logs to make an upload fit.
public static class ProductionLogDocument
{
    private static IList Records(object root)
    {
        if (root is IList list) return list;
        if (root is IDictionary<string, object> envelope)
        {
            IList found = null;
            foreach (string key in new[] { "production_logs", "logs", "records" })
                if (envelope.TryGetValue(key, out var value) && value is IList items)
                {
                    if (found != null) throw new InvalidDataException("Ambiguous production_logs envelope; existing data was kept.");
                    found = items;
                }
            if (found != null) return found;
        }
        throw new InvalidDataException("Unrecognised production_logs format; existing data was kept. Send its JSON format to the game developer.");
    }
    public static string Merge(string json, IList<ProductionLogRecord> batch, Func<string, object> read, Func<object, string> write)
    {
        object root = string.IsNullOrWhiteSpace(json) ? new List<object>() : read(json);
        var entries = Records(root);
        foreach (var record in batch)
        {
            IDictionary<string, object> existing = null;
            foreach (object item in entries)
                if (item is IDictionary<string, object> map && map.TryGetValue("id", out var id) && Equals(id, record.id))
                {
                    if (existing != null) throw new InvalidDataException("Duplicate production log ID; existing data was kept.");
                    existing = map;
                }
            var addition = record.ToWire();
            if (existing == null) entries.Add(addition);
            else Overlay(existing, addition);
        }
        return write(root);
    }
    private static void Overlay(IDictionary<string, object> existing, IDictionary<string, object> addition)
    {
        foreach (var pair in addition)
            if (pair.Value is IDictionary<string, object> sub && existing.TryGetValue(pair.Key, out var old) && old is IDictionary<string, object> oldMap)
                Overlay(oldMap, sub);
            else existing[pair.Key] = pair.Value;
    }
    public static bool Contains(string json, ProductionLogRecord record, Func<string, object> read)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        foreach (object entry in Records(read(json)))
            if (entry is IDictionary<string, object> map && map.TryGetValue("id", out var id) && Equals(id, record.id) &&
                Matches(map, record.ToWire())) return true;
        return false;
    }
    private static bool Matches(object actual, object expected)
    {
        if (expected == null) return actual == null;
        if (expected is IDictionary<string, object> map)
        {
            if (!(actual is IDictionary<string, object> actualMap)) return false;
            foreach (var pair in map)
                if (!actualMap.TryGetValue(pair.Key, out var value) || !Matches(value, pair.Value)) return false;
            return true;
        }
        if (expected is IList list)
        {
            if (!(actual is IList actualList) || actualList.Count != list.Count) return false;
            for (int i = 0; i < list.Count; i++) if (!Matches(actualList[i], list[i])) return false;
            return true;
        }
        if (expected is string || expected is bool) return Equals(actual, expected);
        if (actual == null || actual is string || actual is bool) return false;
        try { return Math.Abs(Convert.ToDouble(actual) - Convert.ToDouble(expected)) <= .00001; }
        catch (Exception) { return Equals(actual, expected); }
    }
}

// Transport-independent so failure, account switching and read-back verification can be tested.
public sealed class ProductionLogUpload : IDisposable
{
    private readonly string owner;
    private readonly IProductionLogTransport transport;
    private readonly Func<string, object> read;
    private readonly Func<object, string> write;
    private readonly Action persist;
    private readonly Action<ProductionLogRecord> verified;
    private readonly IList<ProductionLogRecord> pending;
    private bool disposed;
    public bool Busy { get; private set; }
    public string Status { get; private set; } = "Production results saved locally.";
    public event Action Finished;
    public ProductionLogUpload(string owner, IList<ProductionLogRecord> pending, IProductionLogTransport transport,
        Func<string, object> read, Func<object, string> write, Action persist, Action<ProductionLogRecord> verified)
    { this.owner = owner; this.pending = pending; this.transport = transport; this.read = read; this.write = write; this.persist = persist; this.verified = verified; }

    public bool TrySync()
    {
        if (disposed || Busy || pending.Count == 0) return false;
        var batch = new List<ProductionLogRecord>();
        foreach (var record in pending) if (record.IsOwnedBy(owner)) batch.Add(record);
        if (batch.Count == 0) { Status = "Production logs have no matching authenticated owner."; return false; }
        Busy = true; Status = "Uploading production results…";
        try
        {
            transport.Read(remote => {
                if (disposed) return;
                try
                {
                    string json = ProductionLogDocument.Merge(remote, batch, read, write);
                    transport.Write(json, () => {
                        if (disposed) return;
                        // A successful write is NOT enough: check all required fields in a fresh read.
                        transport.Read(uploaded => {
                            if (disposed) return;
                            try
                            {
                                foreach (var record in batch)
                                    if (!ProductionLogDocument.Contains(uploaded, record, read))
                                    { Fail("PlayFab read-back did not match the saved result. Kept locally for retry."); return; }
                                foreach (var record in batch) verified(record);
                                foreach (var record in batch) pending.Remove(record);
                                persist();
                                Busy = false; Status = "Production results verified in PlayFab."; Finished?.Invoke();
                            }
                            catch (Exception ex) { Fail(ex.Message); }
                        }, Fail);
                    }, Fail);
                }
                catch (Exception ex) { Fail(ex.Message); }
            }, Fail);
        }
        catch (Exception ex) { Fail(ex.Message); }
        return true;
    }
    private void Fail(string message)
    {
        if (disposed) return;
        Busy = false; Status = message; Finished?.Invoke();
    }
    public void Dispose() { disposed = true; Busy = false; }
}
