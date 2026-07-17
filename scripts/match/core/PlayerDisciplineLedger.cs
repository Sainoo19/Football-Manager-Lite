using System.Collections.Generic;
using Godot;

public enum MatchCardKind
{
    None,
    Yellow,
    SecondYellowRed,
    DirectRed
}

public readonly struct DisciplinaryActionResult
{
    public DisciplinaryActionResult(MatchCardKind cardKind, int yellowCardCount, bool sendsOffPlayer)
    {
        CardKind = cardKind;
        YellowCardCount = yellowCardCount;
        SendsOffPlayer = sendsOffPlayer;
    }

    public MatchCardKind CardKind { get; }
    public int YellowCardCount { get; }
    public bool SendsOffPlayer { get; }
}

public sealed class PlayerDisciplineLedger
{
    private readonly Dictionary<StringName, int> _yellowCards = new();
    private readonly HashSet<StringName> _sentOffPlayers = new();

    public void Reset()
    {
        _yellowCards.Clear();
        _sentOffPlayers.Clear();
    }

    public int YellowCardCount(StringName playerId)
    {
        return _yellowCards.GetValueOrDefault(playerId);
    }

    public bool IsSentOff(StringName playerId)
    {
        return _sentOffPlayers.Contains(playerId);
    }

    public DisciplinaryActionResult ApplyYellowCard(StringName playerId)
    {
        if (_sentOffPlayers.Contains(playerId))
        {
            return new DisciplinaryActionResult(MatchCardKind.None, YellowCardCount(playerId), false);
        }

        int yellowCardCount = YellowCardCount(playerId) + 1;
        _yellowCards[playerId] = yellowCardCount;
        if (yellowCardCount < 2)
        {
            return new DisciplinaryActionResult(MatchCardKind.Yellow, yellowCardCount, false);
        }

        _sentOffPlayers.Add(playerId);
        return new DisciplinaryActionResult(MatchCardKind.SecondYellowRed, yellowCardCount, true);
    }

    public DisciplinaryActionResult ApplyDirectRedCard(StringName playerId)
    {
        if (!_sentOffPlayers.Add(playerId))
        {
            return new DisciplinaryActionResult(MatchCardKind.None, YellowCardCount(playerId), false);
        }

        return new DisciplinaryActionResult(MatchCardKind.DirectRed, YellowCardCount(playerId), true);
    }
}
