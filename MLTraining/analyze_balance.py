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


# ─────────────────────────────────────────────────────────────────────────
# 計測ガードレール第1弾: ミラー実験（同一ロースターの Blue/Red 入れ替えペア）対応。
# 旧 JSONL には rosterSeed/mirrored/gitHash が無いため .get() で寛容に読み、
# ペアが1組も見つからなければ従来出力に影響を与えず黙ってスキップする。
# ─────────────────────────────────────────────────────────────────────────

def build_mirror_pairs(matches):
    """rosterSeed で通常試合(mirrored=False)とミラー試合(mirrored=True)を組にする。
    どちらか片方しか無い rosterSeed は不完全ペアとして除外する。"""
    by_seed = defaultdict(dict)
    for m in matches:
        if "rosterSeed" not in m or "mirrored" not in m:
            continue
        seed = m["rosterSeed"]
        key = "mirrored" if m["mirrored"] else "normal"
        # 同じ (seed, mirrored) が複数あっても最初の1件のみを採用する(想定外の重複への保険)。
        by_seed[seed].setdefault(key, m)

    pairs = []
    for seed in sorted(by_seed.keys()):
        entry = by_seed[seed]
        if "normal" in entry and "mirrored" in entry:
            pairs.append((entry["normal"], entry["mirrored"]))
    return pairs


def print_git_hash_summary(matches):
    hashes = sorted({m.get("gitHash", "unknown") for m in matches})
    if not hashes or hashes == ["unknown"]:
        return
    print("\n=== Implementation version (gitHash) ===")
    for h in hashes:
        count = sum(1 for m in matches if m.get("gitHash", "unknown") == h)
        print("  {} -> {} matches".format(h, count))


def print_side_effect(pairs):
    print("\n=== Mirror experiment: side effect (same roster, Blue vs Red win rate) ===")
    if not pairs:
        print("no complete mirror pairs found (need matching rosterSeed with mirrored=false/true)")
        return

    blue_wins = 0
    total = 0
    for normal, mirrored in pairs:
        for m in (normal, mirrored):
            winner = m.get("winnerTeam", "timeout")
            if winner not in ("Blue", "Red"):
                continue
            total += 1
            if winner == "Blue":
                blue_wins += 1

    if total == 0:
        print("no decisive matches in mirror pairs")
        return

    rate = 100.0 * blue_wins / total
    deviation = rate - 50.0
    print("mirror pairs: {}  (matches counted: {})".format(len(pairs), total))
    print("Blue-side winrate across identical rosters: {:.1f}% (deviation from 50%: {:+.1f}pt)".format(
        rate, deviation
    ))
    print("-> this deviation is attributable to side (Blue/Red), not champion composition.")


def print_side_vs_composition_first_events(pairs):
    print("\n=== Mirror experiment: does first-kill/first-tower follow the side or the roster? ===")
    if not pairs:
        print("no complete mirror pairs found (need matching rosterSeed with mirrored=false/true)")
        return

    def first_team(events):
        if not events:
            return None
        return min(events, key=lambda ev: ev.get("t", 0.0)).get("team", None)

    kill_side_follows = 0
    kill_comp_follows = 0
    kill_total = 0
    tower_side_follows = 0
    tower_comp_follows = 0
    tower_total = 0

    for normal, mirrored in pairs:
        # normal: blueRoster/redRoster がそのまま。mirrored: Blue/Red が入れ替わっただけで
        # blueRoster(mirrored) == redRoster(normal) のはず。
        n_kill = first_team(normal.get("killEvents", []))
        m_kill = first_team(mirrored.get("killEvents", []))
        if n_kill in ("Blue", "Red") and m_kill in ("Blue", "Red"):
            kill_total += 1
            if n_kill == m_kill:
                kill_side_follows += 1  # 同じサイドが両方で先制 → サイド由来
            else:
                kill_comp_follows += 1  # サイドが変わっても先制チームの「構成」が同じ → 構成由来

        n_tower = first_team(normal.get("towerEvents", []))
        m_tower = first_team(mirrored.get("towerEvents", []))
        if n_tower in ("Blue", "Red") and m_tower in ("Blue", "Red"):
            tower_total += 1
            if n_tower == m_tower:
                tower_side_follows += 1
            else:
                tower_comp_follows += 1

    if kill_total > 0:
        print("first-kill pairs comparable: {}".format(kill_total))
        print("  same side won first-kill in both (side-attributable):      {} ({:.1f}%)".format(
            kill_side_follows, 100.0 * kill_side_follows / kill_total))
        print("  same roster won first-kill in both (composition-attributable): {} ({:.1f}%)".format(
            kill_comp_follows, 100.0 * kill_comp_follows / kill_total))
    else:
        print("no comparable first-kill pairs")

    if tower_total > 0:
        print("first-tower pairs comparable: {}".format(tower_total))
        print("  same side destroyed first tower in both (side-attributable):      {} ({:.1f}%)".format(
            tower_side_follows, 100.0 * tower_side_follows / tower_total))
        print("  same roster destroyed first tower in both (composition-attributable): {} ({:.1f}%)".format(
            tower_comp_follows, 100.0 * tower_comp_follows / tower_total))
    else:
        print("no comparable first-tower pairs")


def print_champion_winrate_pair_averaged(pairs):
    print("\n=== Champion winrate, pair-averaged (side effect cancelled out) ===")
    if not pairs:
        print("no complete mirror pairs found (need matching rosterSeed with mirrored=false/true)")
        return

    # champId -> [games, wins] accumulated across both matches of each pair.
    acc = defaultdict(lambda: {"games": 0, "wins": 0})
    for normal, mirrored in pairs:
        for m in (normal, mirrored):
            winner = m.get("winnerTeam", "timeout")
            for champ_id in m.get("blueRoster", []):
                acc[champ_id]["games"] += 1
                if winner == "Blue":
                    acc[champ_id]["wins"] += 1
            for champ_id in m.get("redRoster", []):
                acc[champ_id]["games"] += 1
                if winner == "Red":
                    acc[champ_id]["wins"] += 1

    header = "{:<12}{:>6}{:>10}".format("champ", "games", "winrate")
    print(header)
    print("-" * len(header))
    for champ_id in sorted(acc.keys()):
        s = acc[champ_id]
        if s["games"] == 0:
            continue
        winrate = 100.0 * s["wins"] / s["games"]
        print("{:<12}{:>6}{:>9.1f}%".format(champ_id, s["games"], winrate))


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
    print_git_hash_summary(matches)

    pairs = build_mirror_pairs(matches)
    print_side_effect(pairs)
    print_side_vs_composition_first_events(pairs)
    print_champion_winrate_pair_averaged(pairs)


if __name__ == "__main__":
    main()
