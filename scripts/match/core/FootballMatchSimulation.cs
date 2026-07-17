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
    public bool use_live_pitch_events { get; set; }
    public long MatchSeed { get; private set; }

    private readonly RandomNumberGenerator _rng = new();
    private readonly QuickMatchSimulator _quickMatchSimulator = new();

    public FootballMatchSimulation setup(FootballTeam homeTeam, FootballTeam awayTeam, long seedValue = 1)
    {
        home = new MatchTeamState().setup(homeTeam);
        away = new MatchTeamState().setup(awayTeam);
        current_minute = 0;
        is_finished = false;
        last_possession_team_id = homeTeam.id;
        events.Clear();
        MatchSeed = seedValue;
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
        MatchTeamState attacking;
        if (use_live_pitch_events)
        {
            attacking = get_state(last_possession_team_id) ?? home;
            IncrementStat(attacking, "possession_ticks");
        }
        else
        {
            attacking = _quickMatchSimulator.SimulateMinute(home, away, current_minute, _rng, minuteEvents);
            foreach (FootballMatchEvent matchEvent in minuteEvents)
            {
                Record(matchEvent);
            }
        }
        last_possession_team_id = attacking.team.id;

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

    public void set_live_possession(StringName teamId)
    {
        if (use_live_pitch_events && get_state(teamId) is not null)
            last_possession_team_id = teamId;
    }

    public FootballMatchEvent? register_live_shot(
        StringName teamId,
        StringName shooterId,
        StringName outcome,
        StringName goalkeeperId = null!,
        StringName blockerId = null!)
    {
        if (!use_live_pitch_events || is_finished)
            return null;
        MatchTeamState? attacking = get_state(teamId);
        if (attacking is null)
            return null;
        MatchTeamState defending = attacking == home ? away : home;
        FootballPlayer? shooter = attacking.team.get_player(shooterId);
        FootballPlayer? goalkeeper = defending.team.get_player(goalkeeperId ?? new StringName());
        FootballPlayer? blocker = defending.team.get_player(blockerId ?? new StringName());
        string shooterName = shooter?.display_name ?? "Một cầu thủ";
        IncrementStat(attacking, "shots");

        FootballMatchEvent matchEvent;
        switch (outcome.ToString())
        {
            case "goal":
                IncrementStat(attacking, "shots_on_target");
                IncrementStat(attacking, "goals");
                matchEvent = new FootballMatchEvent().setup(
                    current_minute, "goal",
                    $"VÀO! {shooterName} dứt điểm đánh bại {goalkeeper?.display_name ?? "thủ môn"}. Tỷ số là {score_text()}.",
                    attacking.team.id, shooterId);
                break;
            case "saved":
            case "parried":
            case "parried_corner":
                IncrementStat(attacking, "shots_on_target");
                matchEvent = new FootballMatchEvent().setup(
                    current_minute, "shot_on_target",
                    outcome == "saved"
                        ? $"{shooterName} dứt điểm trúng đích, {goalkeeper?.display_name ?? "thủ môn"} bắt gọn."
                        : $"{shooterName} dứt điểm trúng đích, {goalkeeper?.display_name ?? "thủ môn"} đẩy bóng ra.",
                    attacking.team.id, shooterId);
                break;
            case "blocked":
            case "blocked_corner":
                matchEvent = new FootballMatchEvent().setup(
                    current_minute, "shot_blocked",
                    $"{shooterName} dứt điểm nhưng {blocker?.display_name ?? "hậu vệ"} đã chắn bóng.",
                    attacking.team.id, shooterId);
                break;
            default:
                matchEvent = new FootballMatchEvent().setup(
                    current_minute, "shot_off_target",
                    $"{shooterName} dứt điểm chệch khung thành.",
                    attacking.team.id, shooterId);
                break;
        }
        Record(matchEvent);
        return matchEvent;
    }

    public FootballMatchEvent? register_live_foul(
        StringName foulingTeamId,
        StringName offenderId,
        StringName victimId,
        StringName card)
    {
        if (!use_live_pitch_events || is_finished)
            return null;
        MatchTeamState? fouling = get_state(foulingTeamId);
        if (fouling is null)
            return null;
        MatchTeamState victimState = fouling == home ? away : home;
        FootballPlayer? offender = fouling.team.get_player(offenderId);
        FootballPlayer? victim = victimState.team.get_player(victimId);
        IncrementStat(fouling, "fouls");

        if (card == "yellow" || card == "red")
        {
            return RegisterCardEvent(fouling, offenderId, victim, card, false);
        }
        var matchEvent = new FootballMatchEvent().setup(
            current_minute,
            "foul",
            $"{offender?.display_name ?? "Một cầu thủ"} phạm lỗi với {victim?.display_name ?? "đối phương"}.",
            fouling.team.id,
            offenderId);
        Record(matchEvent);
        return matchEvent;
    }

    public FootballMatchEvent? RegisterLiveAdvantage(
        StringName foulingTeamId,
        StringName offenderId,
        StringName victimId)
    {
        if (!use_live_pitch_events || is_finished)
        {
            return null;
        }
        MatchTeamState? fouling = get_state(foulingTeamId);
        if (fouling is null)
        {
            return null;
        }

        MatchTeamState attacking = fouling == home ? away : home;
        FootballPlayer? victim = attacking.team.get_player(victimId);
        IncrementStat(fouling, "fouls");
        var matchEvent = new FootballMatchEvent().setup(
            current_minute,
            "advantage",
            $"Trọng tài cho lợi thế, {victim?.display_name ?? "đội tấn công"} tiếp tục lên bóng.",
            attacking.team.id,
            victimId);
        Record(matchEvent);
        return matchEvent;
    }

    public FootballMatchEvent? RegisterLiveDelayedCard(
        StringName foulingTeamId,
        StringName offenderId,
        StringName card)
    {
        if (!use_live_pitch_events)
        {
            return null;
        }
        MatchTeamState? fouling = get_state(foulingTeamId);
        if (fouling is null || (card != "yellow" && card != "red"))
        {
            return null;
        }

        return RegisterCardEvent(fouling, offenderId, null, card, true);
    }

    public FootballMatchEvent? RegisterLiveOffside(StringName attackingTeamId, StringName playerId)
    {
        if (!use_live_pitch_events || is_finished)
            return null;
        MatchTeamState? attacking = get_state(attackingTeamId);
        if (attacking is null)
            return null;
        FootballPlayer? player = attacking.team.get_player(playerId);
        var matchEvent = new FootballMatchEvent().setup(
            current_minute,
            "offside",
            $"{player?.display_name ?? "Một cầu thủ"} rơi vào vị trí việt vị.",
            attackingTeamId,
            playerId);
        Record(matchEvent);
        return matchEvent;
    }

    public FootballMatchEvent? register_live_restart(StringName teamId, StringName restartType)
    {
        if (!use_live_pitch_events || is_finished)
            return null;
        MatchTeamState? state = get_state(teamId);
        if (state is null)
            return null;
        string text;
        StringName eventType = restartType;
        switch (restartType.ToString())
        {
            case "corner":
                IncrementStat(state, "corners");
                text = $"{state.team.short_name} được hưởng phạt góc.";
                break;
            case "goal_kick":
                text = $"{state.team.short_name} phát bóng lên.";
                break;
            case "free_kick":
                text = $"{state.team.short_name} được hưởng đá phạt.";
                break;
            case "penalty":
                IncrementStat(state, "penalties");
                text = $"{state.team.short_name} được hưởng phạt đền.";
                break;
            case "throw_in":
                text = $"{state.team.short_name} được hưởng ném biên.";
                break;
            default:
                eventType = "kickoff";
                text = $"{state.team.short_name} giao bóng trở lại.";
                break;
        }
        var matchEvent = new FootballMatchEvent().setup(current_minute, eventType, text, state.team.id);
        Record(matchEvent);
        return matchEvent;
    }

    public void RegisterLivePassAttempt(StringName teamId)
    {
        if (!use_live_pitch_events || is_finished)
        {
            return;
        }

        MatchTeamState? state = get_state(teamId);
        if (state is not null)
        {
            IncrementStat(state, "passes_attempted");
        }
    }

    public void RegisterLivePassCompletion(StringName teamId)
    {
        if (!use_live_pitch_events || is_finished)
        {
            return;
        }

        MatchTeamState? state = get_state(teamId);
        if (state is not null)
        {
            IncrementStat(state, "passes_completed");
        }
    }

    public void RegisterLiveFirstTouchError(StringName teamId)
    {
        if (!use_live_pitch_events || is_finished)
        {
            return;
        }

        MatchTeamState? state = get_state(teamId);
        if (state is not null)
        {
            IncrementStat(state, "first_touch_errors");
        }
    }

    private FootballMatchEvent RegisterCardEvent(
        MatchTeamState fouling,
        StringName offenderId,
        FootballPlayer? victim,
        StringName card,
        bool isDelayed)
    {
        FootballPlayer? offender = fouling.team.get_player(offenderId);
        DisciplinaryActionResult result = fouling.ApplyCard(offenderId, card);
        StringName eventType = result.CardKind is MatchCardKind.SecondYellowRed or MatchCardKind.DirectRed
            ? "red_card"
            : "yellow_card";
        string incident = victim is null
            ? $"{offender?.display_name ?? "Một cầu thủ"} bị trọng tài xử lý ở lần bóng chết."
            : $"{offender?.display_name ?? "Một cầu thủ"} phạm lỗi với {victim.display_name}.";
        string sanction = result.CardKind switch
        {
            MatchCardKind.SecondYellowRed => " Thẻ vàng thứ hai, đồng nghĩa thẻ đỏ!",
            MatchCardKind.DirectRed => " Trọng tài rút thẻ đỏ trực tiếp!",
            _ => isDelayed ? " Trọng tài quay lại rút thẻ vàng." : " Trọng tài rút thẻ vàng."
        };
        var matchEvent = new FootballMatchEvent().setup(
            current_minute,
            eventType,
            incident + sanction,
            fouling.team.id,
            offenderId);
        Record(matchEvent);
        return matchEvent;
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

    private void AddEvent(Array<FootballMatchEvent> minuteEvents, FootballMatchEvent matchEvent)
    {
        Record(matchEvent);
        minuteEvents.Add(matchEvent);
    }

    private void Record(FootballMatchEvent matchEvent) => events.Add(matchEvent);

    private static int Stat(MatchTeamState state, string key) => state.stats[key].AsInt32();

    private static void IncrementStat(MatchTeamState state, string key) =>
        state.stats[key] = Stat(state, key) + 1;

}
