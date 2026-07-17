using System.Linq;
using Godot;
using Godot.Collections;

public sealed class QuickMatchSimulator
{
    public MatchTeamState SimulateMinute(
        MatchTeamState home,
        MatchTeamState away,
        int minute,
        RandomNumberGenerator random,
        Array<FootballMatchEvent> events)
    {
        MatchTeamState attacking = ChoosePossessionTeam(home, away, random);
        MatchTeamState defending = attacking == home ? away : home;
        IncrementStat(attacking, "possession_ticks");
        SimulateAttack(home, away, attacking, defending, minute, random, events);
        SimulateFoul(home, away, minute, random, events);
        return attacking;
    }

    private static MatchTeamState ChoosePossessionTeam(
        MatchTeamState home,
        MatchTeamState away,
        RandomNumberGenerator random)
    {
        Dictionary homeStrengths = home.strengths();
        Dictionary awayStrengths = away.strengths();
        float homeWeight = homeStrengths["midfield"].AsSingle() * PossessionModifier(home.mentality) + 3f;
        float awayWeight = awayStrengths["midfield"].AsSingle() * PossessionModifier(away.mentality);
        return random.Randf() < homeWeight / Mathf.Max(homeWeight + awayWeight, 1f) ? home : away;
    }

    private static void SimulateAttack(
        MatchTeamState home,
        MatchTeamState away,
        MatchTeamState attacking,
        MatchTeamState defending,
        int minute,
        RandomNumberGenerator random,
        Array<FootballMatchEvent> events)
    {
        Dictionary attackStrengths = attacking.strengths();
        Dictionary defenseStrengths = defending.strengths();
        float difference = attackStrengths["attack"].AsSingle() - defenseStrengths["defense"].AsSingle();
        float chanceProbability = Mathf.Clamp(
            0.145f + difference * 0.0035f + AttackModifier(attacking.mentality),
            0.07f,
            0.25f);
        if (random.Randf() >= chanceProbability)
        {
            return;
        }

        IncrementStat(attacking, "shots");
        FootballPlayer? shooter = PickPlayer(attacking, true, random);
        string shooterName = shooter?.display_name ?? "Một cầu thủ";
        float onTargetProbability = Mathf.Clamp(0.38f + difference * 0.005f, 0.24f, 0.58f);
        if (random.Randf() < onTargetProbability)
        {
            IncrementStat(attacking, "shots_on_target");
            float goalProbability = Mathf.Clamp(0.23f + difference * 0.003f, 0.12f, 0.38f);
            if (random.Randf() < goalProbability)
            {
                IncrementStat(attacking, "goals");
                events.Add(new FootballMatchEvent().setup(
                    minute,
                    "goal",
                    $"VÀO! {shooterName} ghi bàn cho {attacking.team.short_name}. " +
                    $"Tỷ số là {ScoreText(home, away)}.",
                    attacking.team.id,
                    shooter?.id ?? new StringName()));
                return;
            }

            events.Add(new FootballMatchEvent().setup(
                minute,
                "shot_on_target",
                $"{shooterName} dứt điểm trúng đích nhưng thủ môn đã cản phá.",
                attacking.team.id,
                shooter?.id ?? new StringName()));
            return;
        }

        if (random.Randf() < 0.23f)
        {
            IncrementStat(attacking, "corners");
            events.Add(new FootballMatchEvent().setup(
                minute,
                "corner",
                $"{attacking.team.short_name} có một quả phạt góc sau cú sút của {shooterName}.",
                attacking.team.id));
            return;
        }

        events.Add(new FootballMatchEvent().setup(
            minute,
            "shot_off_target",
            $"{shooterName} dứt điểm chệch khung thành.",
            attacking.team.id,
            shooter?.id ?? new StringName()));
    }

    private static void SimulateFoul(
        MatchTeamState home,
        MatchTeamState away,
        int minute,
        RandomNumberGenerator random,
        Array<FootballMatchEvent> events)
    {
        if (random.Randf() >= 0.034f)
        {
            return;
        }

        MatchTeamState fouling = random.Randf() < 0.5f ? home : away;
        IncrementStat(fouling, "fouls");
        if (random.Randf() >= 0.58f)
        {
            return;
        }

        IncrementStat(fouling, "yellow_cards");
        FootballPlayer? player = PickPlayer(fouling, false, random);
        events.Add(new FootballMatchEvent().setup(
            minute,
            "yellow_card",
            $"Thẻ vàng cho {player?.display_name ?? "một cầu thủ"} ({fouling.team.short_name}).",
            fouling.team.id,
            player?.id ?? new StringName()));
    }

    private static FootballPlayer? PickPlayer(
        MatchTeamState state,
        bool preferAttackers,
        RandomNumberGenerator random)
    {
        var preferredIds = new Array<StringName>();
        foreach (Dictionary slot in state.formation.slots)
        {
            string role = slot["role"].AsString();
            bool isAttacker = role is "AM" or "LW" or "RW" or "ST";
            if (isAttacker != preferAttackers)
            {
                continue;
            }

            StringName slotId = slot["id"].AsStringName();
            if (state.squad.starter_slots.TryGetValue(slotId, out Variant value))
            {
                preferredIds.Add(value.AsStringName());
            }
        }

        if (preferredIds.Count == 0)
        {
            preferredIds = new Array<StringName>(state.squad.starter_ids);
        }
        if (preferredIds.Count == 0)
        {
            return null;
        }

        return state.team.get_player(preferredIds[random.RandiRange(0, preferredIds.Count - 1)]);
    }

    private static string ScoreText(MatchTeamState home, MatchTeamState away)
    {
        return $"{Stat(home, "goals")}  –  {Stat(away, "goals")}";
    }

    private static int Stat(MatchTeamState state, string key)
    {
        return state.stats[key].AsInt32();
    }

    private static void IncrementStat(MatchTeamState state, string key)
    {
        state.stats[key] = Stat(state, key) + 1;
    }

    private static float AttackModifier(StringName mentality)
    {
        return mentality.ToString() switch
        {
            "attacking" => 0.035f,
            "defensive" => -0.025f,
            _ => 0f
        };
    }

    private static float PossessionModifier(StringName mentality)
    {
        return mentality.ToString() switch
        {
            "attacking" => 1.04f,
            "defensive" => 0.96f,
            _ => 1f
        };
    }
}
