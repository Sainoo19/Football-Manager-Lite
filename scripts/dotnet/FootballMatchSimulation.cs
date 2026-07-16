using System;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class FootballMatchSimulation : RefCounted
{
    public MatchTeamState home { get; set; } = null!;
    public MatchTeamState away { get; set; } = null!;
    public int current_minute { get; set; }
    public bool is_finished { get; set; }
    public Array<FootballMatchEvent> events { get; set; } = new();
    public string last_error { get; set; } = "";
    public StringName last_possession_team_id { get; private set; } = new();

    private readonly RandomNumberGenerator _rng = new();

    public FootballMatchSimulation setup(FootballTeam homeTeam, FootballTeam awayTeam, long seedValue = 1)
    {
        home = new MatchTeamState().setup(homeTeam);
        away = new MatchTeamState().setup(awayTeam);
        current_minute = 0;
        is_finished = false;
        last_possession_team_id = homeTeam.id;
        events.Clear();
        _rng.Seed = unchecked((ulong)seedValue);
        Record(new FootballMatchEvent().setup(
            0, "kickoff", $"Trận đấu bắt đầu tại sân của {home.team.display_name}."));
        return this;
    }

    public Array<FootballMatchEvent> advance_minute()
    {
        var minuteEvents = new Array<FootballMatchEvent>();
        if (is_finished)
            return minuteEvents;

        current_minute++;
        MatchTeamState attacking = ChoosePossessionTeam();
        last_possession_team_id = attacking.team.id;
        IncrementStat(attacking, "possession_ticks");
        MatchTeamState defending = attacking == home ? away : home;
        SimulateAttack(attacking, defending, minuteEvents);
        SimulateFoul(minuteEvents);

        if (current_minute is 60 or 72 or 80)
            SimulateAiSubstitution(away, minuteEvents);

        if (current_minute == 45)
        {
            AddEvent(minuteEvents, new FootballMatchEvent().setup(
                45, "half_time",
                $"Hết hiệp một: {home.team.short_name} {Stat(home, "goals")}–{Stat(away, "goals")} {away.team.short_name}."));
        }
        else if (current_minute >= 90)
        {
            is_finished = true;
            AddEvent(minuteEvents, new FootballMatchEvent().setup(
                90, "full_time",
                $"Hết trận: {home.team.short_name} {Stat(home, "goals")}–{Stat(away, "goals")} {away.team.short_name}."));
        }
        return minuteEvents;
    }

    public Array<FootballMatchEvent> simulate_to_end()
    {
        var remainingEvents = new Array<FootballMatchEvent>();
        while (!is_finished)
        {
            foreach (FootballMatchEvent matchEvent in advance_minute())
                remainingEvents.Add(matchEvent);
        }
        return remainingEvents;
    }

    public FootballMatchEvent? change_mentality(StringName teamId, StringName newMentality)
    {
        last_error = "";
        MatchTeamState? state = get_state(teamId);
        if (state is null)
        {
            last_error = "Không tìm thấy đội bóng.";
            return null;
        }
        if (newMentality != "defensive" && newMentality != "balanced" && newMentality != "attacking")
        {
            last_error = "Tâm lý thi đấu không hợp lệ.";
            return null;
        }

        state.mentality = newMentality;
        string label = newMentality.ToString() switch
        {
            "defensive" => "Phòng ngự",
            "attacking" => "Tấn công",
            _ => "Cân bằng"
        };
        var matchEvent = new FootballMatchEvent().setup(
            current_minute, "tactic", $"{state.team.display_name} chuyển sang lối chơi {label}.", state.team.id);
        Record(matchEvent);
        return matchEvent;
    }

    public FootballMatchEvent? make_substitution(StringName teamId, StringName outgoingId, StringName incomingId)
    {
        last_error = "";
        if (is_finished)
        {
            last_error = "Trận đấu đã kết thúc.";
            return null;
        }
        MatchTeamState? state = get_state(teamId);
        if (state is null)
        {
            last_error = "Không tìm thấy đội bóng.";
            return null;
        }
        if (state.substitutions_used >= MatchTeamState.MaxSubstitutions)
        {
            last_error = "Đã sử dụng đủ 5 quyền thay người.";
            return null;
        }

        FootballPlayer? outgoing = state.team.get_player(outgoingId);
        FootballPlayer? incoming = state.team.get_player(incomingId);
        if (outgoing is null || incoming is null || !state.make_substitution(outgoingId, incomingId))
        {
            last_error = "Lựa chọn thay người không hợp lệ.";
            return null;
        }

        var matchEvent = new FootballMatchEvent().setup(
            current_minute,
            "substitution",
            $"{state.team.short_name} thay người: {incoming.display_name} vào sân, {outgoing.display_name} rời sân.",
            state.team.id,
            incoming.id);
        Record(matchEvent);
        return matchEvent;
    }

    public MatchTeamState? get_state(StringName teamId)
    {
        if (home is not null && home.team.id == teamId)
            return home;
        if (away is not null && away.team.id == teamId)
            return away;
        return null;
    }

    public int get_possession(MatchTeamState state) =>
        current_minute <= 0 ? 50 : Mathf.RoundToInt((float)Stat(state, "possession_ticks") / current_minute * 100f);

    public string score_text() => $"{Stat(home, "goals")}  –  {Stat(away, "goals")}";

    private MatchTeamState ChoosePossessionTeam()
    {
        Dictionary homeStrengths = home.strengths();
        Dictionary awayStrengths = away.strengths();
        float homeWeight = homeStrengths["midfield"].AsSingle() * PossessionModifier(home.mentality) + 3f;
        float awayWeight = awayStrengths["midfield"].AsSingle() * PossessionModifier(away.mentality);
        return _rng.Randf() < homeWeight / Mathf.Max(homeWeight + awayWeight, 1f) ? home : away;
    }

    private void SimulateAttack(
        MatchTeamState attacking,
        MatchTeamState defending,
        Array<FootballMatchEvent> minuteEvents)
    {
        Dictionary attackStrengths = attacking.strengths();
        Dictionary defenseStrengths = defending.strengths();
        float difference = attackStrengths["attack"].AsSingle() - defenseStrengths["defense"].AsSingle();
        float chanceProbability = Mathf.Clamp(
            0.145f + difference * 0.0035f + AttackModifier(attacking.mentality), 0.07f, 0.25f);
        if (_rng.Randf() >= chanceProbability)
            return;

        IncrementStat(attacking, "shots");
        FootballPlayer? shooter = PickPlayer(attacking, true);
        string shooterName = shooter?.display_name ?? "Một cầu thủ";
        float onTargetProbability = Mathf.Clamp(0.38f + difference * 0.005f, 0.24f, 0.58f);
        if (_rng.Randf() < onTargetProbability)
        {
            IncrementStat(attacking, "shots_on_target");
            float goalProbability = Mathf.Clamp(0.23f + difference * 0.003f, 0.12f, 0.38f);
            if (_rng.Randf() < goalProbability)
            {
                IncrementStat(attacking, "goals");
                AddEvent(minuteEvents, new FootballMatchEvent().setup(
                    current_minute, "goal",
                    $"VÀO! {shooterName} ghi bàn cho {attacking.team.short_name}. Tỷ số là {score_text()}.",
                    attacking.team.id, shooter?.id ?? new StringName()));
            }
            else
            {
                AddEvent(minuteEvents, new FootballMatchEvent().setup(
                    current_minute, "shot_on_target",
                    $"{shooterName} dứt điểm trúng đích nhưng thủ môn đã cản phá.",
                    attacking.team.id, shooter?.id ?? new StringName()));
            }
        }
        else if (_rng.Randf() < 0.23f)
        {
            IncrementStat(attacking, "corners");
            AddEvent(minuteEvents, new FootballMatchEvent().setup(
                current_minute, "corner",
                $"{attacking.team.short_name} có một quả phạt góc sau cú sút của {shooterName}.",
                attacking.team.id));
        }
        else
        {
            AddEvent(minuteEvents, new FootballMatchEvent().setup(
                current_minute, "shot_off_target",
                $"{shooterName} dứt điểm chệch khung thành.",
                attacking.team.id, shooter?.id ?? new StringName()));
        }
    }

    private void SimulateFoul(Array<FootballMatchEvent> minuteEvents)
    {
        if (_rng.Randf() >= 0.034f)
            return;
        MatchTeamState fouling = _rng.Randf() < 0.5f ? home : away;
        IncrementStat(fouling, "fouls");
        if (_rng.Randf() >= 0.58f)
            return;
        IncrementStat(fouling, "yellow_cards");
        FootballPlayer? player = PickPlayer(fouling, false);
        AddEvent(minuteEvents, new FootballMatchEvent().setup(
            current_minute, "yellow_card",
            $"Thẻ vàng cho {player?.display_name ?? "một cầu thủ"} ({fouling.team.short_name}).",
            fouling.team.id, player?.id ?? new StringName()));
    }

    private void SimulateAiSubstitution(MatchTeamState state, Array<FootballMatchEvent> minuteEvents)
    {
        FootballPlayer? outgoing = state.get_starter_players().OrderBy(player => player.overall).FirstOrDefault();
        FootballPlayer? incoming = state.get_substitute_players().OrderByDescending(player => player.overall).FirstOrDefault();
        if (outgoing is null || incoming is null)
            return;
        FootballMatchEvent? matchEvent = make_substitution(state.team.id, outgoing.id, incoming.id);
        if (matchEvent is not null)
            minuteEvents.Add(matchEvent);
    }

    private FootballPlayer? PickPlayer(MatchTeamState state, bool preferAttackers)
    {
        var preferredIds = new Array<StringName>();
        foreach (Dictionary slot in state.formation.slots)
        {
            string role = slot["role"].AsString();
            bool isAttacker = role is "AM" or "LW" or "RW" or "ST";
            if (isAttacker != preferAttackers)
                continue;
            StringName slotId = slot["id"].AsStringName();
            if (state.squad.starter_slots.TryGetValue(slotId, out Variant value))
                preferredIds.Add(value.AsStringName());
        }
        if (preferredIds.Count == 0)
            preferredIds = new Array<StringName>(state.squad.starter_ids);
        if (preferredIds.Count == 0)
            return null;
        return state.team.get_player(preferredIds[_rng.RandiRange(0, preferredIds.Count - 1)]);
    }

    private void AddEvent(Array<FootballMatchEvent> minuteEvents, FootballMatchEvent matchEvent)
    {
        Record(matchEvent);
        minuteEvents.Add(matchEvent);
    }

    private void Record(FootballMatchEvent matchEvent) => events.Add(matchEvent);

    private static int Stat(MatchTeamState state, string key) => state.stats[key].AsInt32();

    private static void IncrementStat(MatchTeamState state, string key) =>
        state.stats[key] = Stat(state, key) + 1;

    private static float AttackModifier(StringName mentality) => mentality.ToString() switch
    {
        "attacking" => 0.035f,
        "defensive" => -0.025f,
        _ => 0f
    };

    private static float PossessionModifier(StringName mentality) => mentality.ToString() switch
    {
        "attacking" => 1.04f,
        "defensive" => 0.96f,
        _ => 1f
    };
}
