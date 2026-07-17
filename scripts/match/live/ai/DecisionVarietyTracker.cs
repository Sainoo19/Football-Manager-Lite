using System.Collections.Generic;
using Godot;

public sealed class DecisionVarietyTracker
{
    private const int MemorySize = 6;
    private readonly Queue<StringName> _recentTargets = new();
    private readonly Dictionary<StringName, int> _targetCounts = new();

    public void Reset()
    {
        _recentTargets.Clear();
        _targetCounts.Clear();
    }

    public void RecordPassTarget(StringName playerId)
    {
        _recentTargets.Enqueue(playerId);
        _targetCounts[playerId] = RecentUseCount(playerId) + 1;
        if (_recentTargets.Count <= MemorySize)
        {
            return;
        }

        StringName expiredId = _recentTargets.Dequeue();
        int remaining = RecentUseCount(expiredId) - 1;
        if (remaining <= 0)
        {
            _targetCounts.Remove(expiredId);
        }
        else
        {
            _targetCounts[expiredId] = remaining;
        }
    }

    public float PassScoreAdjustment(StringName playerId, float seededRoll, bool preferSafe)
    {
        float varietyAmplitude = preferSafe ? 0.025f : 0.06f;
        float variation = (seededRoll - 0.5f) * 2f * varietyAmplitude;
        float repetitionPenalty = RecentUseCount(playerId) * 0.04f;
        return variation - repetitionPenalty;
    }

    public int RecentUseCount(StringName playerId)
    {
        return _targetCounts.TryGetValue(playerId, out int count) ? count : 0;
    }
}
