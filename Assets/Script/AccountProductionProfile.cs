using System;
using System.Collections.Generic;
using UnityEngine;

// Read-only account projection of the same career data carried by the existing
// PlayFab checkpoint sync. It never activates a career or creates spendable coins.
public static class AccountProductionProfile
{
    public sealed class Snapshot
    {
        public readonly CareerProfileProgress.Record record = new CareerProfileProgress.Record();
        public readonly List<PlayerAnalytics.Attempt> history = new List<PlayerAnalytics.Attempt>();
        public readonly List<GameSaveSlot> sources = new List<GameSaveSlot>();
        public string owner;
        public int bCoins;
        public bool hasBudget, currentBudget, conflictingHistory;
        public int Preference(string key)
        {
            foreach (var slot in sources) if (slot.Int(key, 0) == 1) return 1;
            return 0;
        }
    }
    private sealed class Contribution
    {
        public GameSaveSlot slot;
        public CareerProfileProgress.Record record;
    }
    public static Snapshot Capture(string owner, IEnumerable<GameSaveSlot> stored, GameSaveSlot active, List<GameSaveValue> live)
    {
        var result = new Snapshot();
        if (string.IsNullOrWhiteSpace(owner)) owner = "guest";
        result.owner = owner;
        var candidates = new List<GameSaveSlot>();
        if (stored != null) foreach (var slot in stored)
            if (slot != null && slot.owner == owner && slot.values != null && !string.IsNullOrEmpty(slot.id)) candidates.Add(slot);
        if (active != null && active.owner == owner && live != null)
        {
            candidates.RemoveAll(s => s.id == active.id);
            candidates.Add(new GameSaveSlot { id=active.id, owner=owner, name=active.name, revision=active.revision,
                updatedUtc=active.updatedUtc, values=live }); // no mutation of the selected preferences
            var budget = live.Find(v => v.key == "PlayerMoney" && v.kind == 0);
            result.hasBudget = result.currentBudget = budget != null;
            if (budget != null) result.bCoins = budget.integer;
        }
        var origins = new Dictionary<string, Contribution>(StringComparer.Ordinal);
        GameSaveSlot latestBudget = null;
        foreach (var slot in candidates)
        {
            // Deleted checkpoints still retain earned profile history, but not a usable budget.
            if (slot.Int("SaveDeleted",0)==0 && slot.values.Exists(v=>v.key=="PlayerMoney" && v.kind==0) &&
                (latestBudget==null || string.CompareOrdinal(slot.updatedUtc,latestBudget.updatedUtc)>0)) latestBudget=slot;
            result.sources.Add(slot);
            string origin = Origin(slot);
            var contribution = new Contribution { slot=slot, record=CareerProfileProgress.ReadStored(slot) };
            if (origins.TryGetValue(origin, out var previous))
            {
                if (JsonUtility.ToJson(previous.record) != JsonUtility.ToJson(contribution.record)) result.conflictingHistory=true;
                // Offline forks share prior history: never add both sets of counters.
                // Keep a real whole record, prioritizing the longer tracked history.
                if (Compare(contribution,previous)>0) origins[origin]=contribution;
            }
            else origins.Add(origin,contribution);
        }
        if (!result.hasBudget && latestBudget!=null)
        { result.hasBudget=true;result.bCoins=latestBudget.Money; }
        var submissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in origins.Values)
        {
            Add(result.record,item.record);
            int historyIndex = 0;
            foreach (var attempt in PlayerAnalytics.ProfileHistory(item.slot))
            {
                historyIndex++;
                if (attempt == null || !attempt.closed) continue;
                // Old attempts have no submission ID. Two attempts on the same day
                // are still different; one whole history per origin is already selected.
                string id = string.IsNullOrEmpty(attempt.submissionId) ? Origin(item.slot)+":legacy:"+historyIndex : attempt.submissionId;
                if (submissions.Add(id)) result.history.Add(attempt);
            }
        }
        // Preserve observed portfolio/milestone evidence from a fork without adding overlapping counts.
        foreach (var slot in result.sources) Milestones(result.record,CareerProfileProgress.ReadStored(slot));
        result.record.partialHistory |= result.conflictingHistory;
        result.history.Sort((a,b)=>string.CompareOrdinal(string.IsNullOrEmpty(a.playedUtc)?a.date:a.playedUtc,
            string.IsNullOrEmpty(b.playedUtc)?b.date:b.playedUtc));
        if(result.history.Count>50)result.history.RemoveRange(0,result.history.Count-50);
        return result;
    }
    public static string Origin(GameSaveSlot slot)
    {
        string origin=slot.values.Find(v=>v.key==GameSaveRepository.ProfileOriginKey && v.kind==2)?.text;
        if(string.IsNullOrEmpty(origin))origin=slot.values.Find(v=>v.key=="CCoins.CareerRewardId" && v.kind==2)?.text;
        return string.IsNullOrEmpty(origin)?slot.id:origin;
    }
    private static int Compare(Contribution a,Contribution b)
    {
        int order=a.record.attempts.CompareTo(b.record.attempts);
        if(order==0)order=a.record.recordings.CompareTo(b.record.recordings);
        if(order==0)order=string.CompareOrdinal(a.slot.updatedUtc,b.slot.updatedUtc);
        return order;
    }
    private static int Sum(int a,int b)=>(int)Math.Min(int.MaxValue,(long)a+b);
    private static void Add(CareerProfileProgress.Record target,CareerProfileProgress.Record source)
    {
        target.recordings=Sum(target.recordings,source.recordings);target.attempts=Sum(target.attempts,source.attempts);
        target.passed=Sum(target.passed,source.passed);target.failed=Sum(target.failed,source.failed);
        target.recordedSeconds+=source.recordedSeconds;target.scoreTotal+=source.scoreTotal;
        target.income+=source.income;target.spent+=source.spent;
        target.partialHistory|=source.partialHistory;target.partialDuration|=source.partialDuration;
        for(int i=0;i<5;i++){target.attemptsByLevel[i]=Sum(target.attemptsByLevel[i],source.attemptsByLevel[i]);target.failedByLevel[i]=Sum(target.failedByLevel[i],source.failedByLevel[i]);}
        Milestones(target,source);
    }
    private static void Milestones(CareerProfileProgress.Record target,CareerProfileProgress.Record source)
    {
        target.completedMask|=source.completedMask;target.uncertainLevelsMask|=source.uncertainLevelsMask;
        target.bestRank=Math.Max(target.bestRank,source.bestRank);target.bestScore=Mathf.Max(target.bestScore,source.bestScore);
        target.topRank|=source.topRank;target.polished|=source.polished;target.balanced|=source.balanced;
        target.comeback|=source.comeback;target.firstTry|=source.firstTry;target.frugal|=source.frugal;
        for(int i=0;i<5;i++)target.bestByLevel[i]=Mathf.Max(target.bestByLevel[i],source.bestByLevel[i]);
    }
}
