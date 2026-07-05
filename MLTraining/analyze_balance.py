"""
Balance sim aggregation report.

Reads a JSONL file produced by Enigma.Learning.BalanceSimRunner (one line per
simulated match) and prints tables for: per-champion appearances/winrate/
average KDA/CS, team winrate (Blue/Red bias), average match length, timeout
rate, core-capture distribution, and firstTower vs match-win correlation.

Usage: python analyze_balance.py path/to/batch_YYYYMMDD_HHMM.jsonl
"""

import json
import sys
from collections import defaultdict

# Champions with fewer than this many appearances get flagged as "inconclusive"
# rather than given a winrate verdict (too few samples to be meaningful).
MIN_APPEARANCES_FOR_VERDICT = 20


def load_matches(path):
    matches = []
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            matches.append(json.loads(line))
    return matches


def champion_stats(matches):
    # champId -> dict(appearances, wins, kills, deaths, cs)
    stats = defaultdict(lambda: {"appearances": 0, "wins": 0, "kills": 0, "deaths": 0, "cs": 0})

    for m in matches:
        winner = m.get("winnerTeam", "timeout")
        blue_roster = m.get("blueRoster", [])
        red_roster = m.get("redRoster", [])
        per_champ = m.get("perChampion", {})

        for champ_id in blue_roster:
            s = stats[champ_id]
            s["appearances"] += 1
            if winner == "Blue":
                s["wins"] += 1
        for champ_id in red_roster:
            s = stats[champ_id]
            s["appearances"] += 1
            if winner == "Red":
                s["wins"] += 1

        for champ_id, kda in per_champ.items():
            s = stats[champ_id]
            s["kills"] += kda.get("kills", 0)
            s["deaths"] += kda.get("deaths", 0)
            s["cs"] += kda.get("cs", 0)

    return stats


def print_champion_table(stats):
    print("\n=== Per-champion stats ===")
    header = "{:<12}{:>6}{:>9}{:>8}{:>8}{:>8}{:>10}".format(
        "champ", "games", "winrate", "avgK", "avgD", "avgCS", "verdict"
    )
    print(header)
    print("-" * len(header))

    for champ_id in sorted(stats.keys()):
        s = stats[champ_id]
        games = s["appearances"]
        if games == 0:
            continue
        winrate = 100.0 * s["wins"] / games
        avg_k = s["kills"] / games
        avg_d = s["deaths"] / games
        avg_cs = s["cs"] / games
        verdict = "OK" if games >= MIN_APPEARANCES_FOR_VERDICT else "INCONCLUSIVE (<{} games)".format(
            MIN_APPEARANCES_FOR_VERDICT
        )
        winrate_str = "{:.1f}%".format(winrate)
        print("{:<12}{:>6}{:>9}{:>8.2f}{:>8.2f}{:>8.2f}  {}".format(
            champ_id, games, winrate_str, avg_k, avg_d, avg_cs, verdict
        ))


def print_team_bias(matches):
    print("\n=== Team winrate (Blue/Red bias check) ===")
    total = len(matches)
    blue_wins = sum(1 for m in matches if m.get("winnerTeam") == "Blue")
    red_wins = sum(1 for m in matches if m.get("winnerTeam") == "Red")
    timeouts = sum(1 for m in matches if m.get("winnerTeam") == "timeout")

    if total == 0:
        print("no matches")
        return

    print("total matches: {}".format(total))
    print("Blue winrate:  {:.1f}% ({} wins)".format(100.0 * blue_wins / total, blue_wins))
    print("Red winrate:   {:.1f}% ({} wins)".format(100.0 * red_wins / total, red_wins))
    print("Timeout rate:  {:.1f}% ({} matches)".format(100.0 * timeouts / total, timeouts))


def print_match_length(matches):
    print("\n=== Match length ===")
    durations = [m.get("durationSec", 0.0) for m in matches]
    if not durations:
        print("no matches")
        return
    avg = sum(durations) / len(durations)
    print("average duration: {:.1f}s ({:.1f} min)".format(avg, avg / 60.0))
    print("min: {:.1f}s  max: {:.1f}s".format(min(durations), max(durations)))


def print_core_capture_distribution(matches):
    print("\n=== Core capture distribution ===")
    blue_counts = defaultdict(int)
    red_counts = defaultdict(int)
    for m in matches:
        blue_counts[m.get("coreCapturesBlue", 0)] += 1
        red_counts[m.get("coreCapturesRed", 0)] += 1

    print("Blue core captures per match (count -> num matches):")
    for count in sorted(blue_counts.keys()):
        print("  {} -> {}".format(count, blue_counts[count]))
    print("Red core captures per match (count -> num matches):")
    for count in sorted(red_counts.keys()):
        print("  {} -> {}".format(count, red_counts[count]))


def print_first_tower_correlation(matches):
    print("\n=== First tower vs match win correlation ===")
    total_with_tower = 0
    first_tower_team_won = 0
    for m in matches:
        first_tower = m.get("firstTowerTeam", "")
        winner = m.get("winnerTeam", "timeout")
        if not first_tower:
            continue
        total_with_tower += 1
        if first_tower == winner:
            first_tower_team_won += 1

    if total_with_tower == 0:
        print("no matches with a recorded first tower")
        return

    rate = 100.0 * first_tower_team_won / total_with_tower
    print("matches with a first tower recorded: {}".format(total_with_tower))
    print("first-tower team went on to win: {:.1f}% ({}/{})".format(
        rate, first_tower_team_won, total_with_tower
    ))


# OT (overtime) kicks in at 900s (15 min). Used to split tower-destruction
# timing into pre-OT vs OT-or-later buckets when investigating whether one
# side's towers fall disproportionately during the OT decay window.
OT_START_SEC = 900.0


def print_tower_event_timing(matches):
    print("\n=== Tower destruction timing (towerEvents) ===")
    pre_ot = []
    ot_or_later = []
    for m in matches:
        for ev in m.get("towerEvents", []):
            t = ev.get("t", 0.0)
            if t < OT_START_SEC:
                pre_ot.append(t)
            else:
                ot_or_later.append(t)

    total = len(pre_ot) + len(ot_or_later)
    if total == 0:
        print("no tower events recorded")
        return

    print("tower destructions before {:.0f}s (pre-OT): {}".format(OT_START_SEC, len(pre_ot)))
    print("tower destructions at/after {:.0f}s (OT or later): {}".format(OT_START_SEC, len(ot_or_later)))

    all_times = sorted(pre_ot + ot_or_later)
    print("min: {:.1f}s  median: {:.1f}s".format(all_times[0], _median(all_times)))


def print_first_tower_timing_by_team(matches):
    print("\n=== First tower destroyed per match: Blue vs Red ===")
    blue_times = []
    red_times = []
    for m in matches:
        events = m.get("towerEvents", [])
        if not events:
            continue
        first = min(events, key=lambda ev: ev.get("t", 0.0))
        team = first.get("team", "")
        t = first.get("t", 0.0)
        if team == "Blue":
            blue_times.append(t)
        elif team == "Red":
            red_times.append(t)

    print("Blue destroyed the first tower in {} matches (avg {:.1f}s)".format(
        len(blue_times), (sum(blue_times) / len(blue_times)) if blue_times else 0.0
    ))
    print("Red destroyed the first tower in {} matches (avg {:.1f}s)".format(
        len(red_times), (sum(red_times) / len(red_times)) if red_times else 0.0
    ))


def print_first_kill_by_team(matches):
    print("\n=== First kill per match: Blue vs Red ===")
    blue_count = 0
    red_count = 0
    for m in matches:
        events = m.get("killEvents", [])
        if not events:
            continue
        first = min(events, key=lambda ev: ev.get("t", 0.0))
        team = first.get("team", "")
        if team == "Blue":
            blue_count += 1
        elif team == "Red":
            red_count += 1

    print("Blue got the first kill in {} matches".format(blue_count))
    print("Red got the first kill in {} matches".format(red_count))


def _median(values):
    n = len(values)
    if n == 0:
        return 0.0
    mid = n // 2
    if n % 2 == 1:
        return values[mid]
    return (values[mid - 1] + values[mid]) / 2.0


def main():
    if len(sys.argv) < 2:
        print("usage: python analyze_balance.py path/to/batch.jsonl")
        sys.exit(1)

    path = sys.argv[1]
    matches = load_matches(path)

    print("Loaded {} matches from {}".format(len(matches), path))

    stats = champion_stats(matches)
    print_champion_table(stats)
    print_team_bias(matches)
    print_match_length(matches)
    print_core_capture_distribution(matches)
    print_first_tower_correlation(matches)
    print_tower_event_timing(matches)
    print_first_tower_timing_by_team(matches)
    print_first_kill_by_team(matches)


if __name__ == "__main__":
    main()
