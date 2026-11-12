#!/usr/bin/env python3
"""G0 display projection reference; inputs already passed upstream verification.

Does not decode journal frames, verify hashes/entropy, simulate nature, create
causal edges, or execute Unity. Never writes files.
"""
from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path


VERSION = "round-observation.v0.8"
FIXTURE = Path(__file__).resolve().parents[1] / "content" / "observation-fixtures.v0.8.json"
METRIC_COLUMNS = [
    "surfaceWaterTotal", "soilWaterTotal", "growthTotal", "growthSinceStart",
    "growthThisRound", "plantCount", "flowerCount",
]
COMPARISON_COLUMNS = [
    "recordOrdinal", "eligibleOrdinal", "observedMatch", "chanceNumerator",
    "chanceDenominator", "cumulativeMatches", "cumulativeChanceNumerator",
]
COUNT_COLUMNS = [
    "preparationCount", "acquisitionStartedCount", "knownOutcomeCount",
    "resolvedCount", "drawUnresolvedTerminalCount", "drawPendingCount",
    "abandonedBeforeAcquisitionCount", "preparationInterruptedCount",
    "comparableCount", "zeroProbabilityIntentCount", "certainProbabilityIntentCount",
    "unstructuredIntentCount", "structuredUnknownCount", "noIntentCommittedCount",
    "filteredOutCount",
]


def metrics(snapshot: dict, initial_growth: int, before_growth: int | None) -> dict:
    growth = sum(p["growth"] for p in snapshot["plants"])
    return dict(zip(METRIC_COLUMNS, [
        sum(c["surfaceWater"] for c in snapshot["cells"]),
        sum(c["soilWater"] for c in snapshot["cells"]),
        growth, growth - initial_growth,
        None if before_growth is None else growth - before_growth,
        len(snapshot["plants"]),
        sum(p["stage"] == "flower" for p in snapshot["plants"]),
    ]))


def project(case: dict, snapshots: dict) -> dict:
    """Project summaries already scoped to cutoff; not a raw-record reducer.

    Intent/outcome/resolved/card fields must already exclude later source records.
    The preparationSeq filter below is not full journal cutoff verification.
    """
    query = copy.deepcopy(case["query"])
    initial = snapshots[case["initialSnapshot"]]
    initial_growth = sum(p["growth"] for p in initial["plants"])
    initial_metrics = metrics(initial, initial_growth, None)
    prefix = sorted(
        (r for r in case["rounds"] if r["preparationSeq"] <= query["cutoffSeq"]),
        key=lambda r: r["preparationSeq"],
    )
    assert len({r["roundId"] for r in prefix}) == len(prefix)
    assert len({r["preparationSeq"] for r in prefix}) == len(prefix)
    ordinal_range = query["recordOrdinalRange"]
    filters = query["comparisonFilters"]
    counts = dict.fromkeys(COUNT_COLUMNS, 0)
    cumulative_matches = cumulative_numerator = 0
    rows = []
    matching_filter = []
    for ordinal, round_ in enumerate(prefix, start=1):
        if ordinal_range is not None and not ordinal_range[0] <= ordinal <= ordinal_range[1]:
            continue
        counts["preparationCount"] += 1
        intent = round_["intent"]
        outcome = round_["outcome"]
        known = outcome["status"] in ("committed", "saved_material")
        assert known == (outcome["faceId"] is not None)
        if known:
            assert 1 <= outcome["faceId"] <= 6
            counts["knownOutcomeCount"] += 1
        if round_["acquisitionStarted"]:
            counts["acquisitionStartedCount"] += 1
        terminal = round_["terminal"]
        if terminal == "interrupted_unresolved":
            counts["drawUnresolvedTerminalCount"] += 1
        elif terminal == "abandoned_before_acquisition":
            counts["abandonedBeforeAcquisitionCount"] += 1
        elif terminal == "preparation_interrupted":
            counts["preparationInterruptedCount"] += 1
        elif terminal is None and round_["acquisitionStarted"]:
            counts["drawPendingCount"] += 1

        garden = None
        resolved = round_["resolved"]
        if resolved is not None:
            assert terminal == "resolved" and known
            counts["resolvedCount"] += 1
            before_growth = sum(p["growth"] for p in snapshots[resolved["beforeSnapshot"]]["plants"])
            garden = metrics(snapshots[resolved["postSnapshot"]], initial_growth, before_growth)

        comparison = None
        method = None if intent is None else intent["method"]
        included = (
            (filters["method"] is None or method == filters["method"])
            and (filters["outcomeSource"] is None or outcome["source"] == filters["outcomeSource"])
        )
        if not included:
            classification = "filteredOutCount"
        else:
            matching_filter.append(round_)
            if intent is None:
                classification = "noIntentCommittedCount"
            elif intent["kind"] not in ("face", "element"):
                classification = "unstructuredIntentCount"
            else:
                faces = intent["matchingFaceIds"]
                assert len(set(faces)) == len(faces) and all(1 <= f <= 6 for f in faces)
                numerator = len(faces)
                if numerator == 0:
                    classification = "zeroProbabilityIntentCount"
                elif numerator == 6:
                    classification = "certainProbabilityIntentCount"
                elif not known:
                    classification = "structuredUnknownCount"
                else:
                    classification = "comparableCount"
                    observed = int(outcome["faceId"] in faces)
                    cumulative_matches += observed
                    cumulative_numerator += numerator
                    comparison = dict(zip(COMPARISON_COLUMNS, [
                        ordinal, counts["comparableCount"] + 1, observed,
                        numerator, 6, cumulative_matches, cumulative_numerator,
                    ]))
        counts[classification] += 1
        rows.append({
            "recordOrdinal": ordinal,
            "roundId": round_["roundId"],
            "preparationSeq": round_["preparationSeq"],
            "worldRevision": None if resolved is None else resolved["worldRevision"],
            "weather": {
                "weatherId": round_["weatherId"],
                "application": "applied" if resolved is not None else (
                    "planned" if round_["prepared"] else "not_prepared"
                ),
            },
            "garden": garden,
            "outcomeKnown": known,
            "outcomeFaceId": outcome["faceId"],
            "outcomeEvidenceKind": outcome["status"],
            "cardStatus": round_["cardStatus"],
            "comparison": comparison,
            "comparisonClassification": classification,
        })

    # The seven comparison classes partition the selected record positions.
    assert sum(counts[k] for k in COUNT_COLUMNS[8:]) == counts["preparationCount"]
    if counts["comparableCount"]:
        empty_reason = None
    elif not matching_filter and (rows or ordinal_range is not None):
        empty_reason = "no_matching_records"
    elif not any(r["outcome"]["status"] in ("committed", "saved_material") for r in matching_filter):
        empty_reason = "no_known_outcome" if any(r["acquisitionStarted"] for r in matching_filter) else "no_draw_yet"
    else:
        empty_reason = "no_eligible_known_target"
    return {
        "projectionVersion": VERSION,
        "query": query,
        "initialPoint": {"recordOrdinal": 0, "metrics": initial_metrics},
        "initialPointVisible": ordinal_range is None,
        "rows": rows,
        "counts": counts,
        "comparisonEmptyReason": empty_reason,
    }


def compact(projection: dict) -> dict:
    """Fixed column arrays keep the hand-authored expected values readable."""
    return {
        "initialMetrics": [projection["initialPoint"]["metrics"][k] for k in METRIC_COLUMNS],
        "initialPointVisible": projection["initialPointVisible"],
        "recordOrdinals": [r["recordOrdinal"] for r in projection["rows"]],
        "gardenPoints": [None if r["garden"] is None else [r["garden"][k] for k in METRIC_COLUMNS] for r in projection["rows"]],
        "comparisonPoints": [None if r["comparison"] is None else [r["comparison"][k] for k in COMPARISON_COLUMNS] for r in projection["rows"]],
        "weatherApplications": [r["weather"]["application"] for r in projection["rows"]],
        "counts": [projection["counts"][k] for k in COUNT_COLUMNS],
        "comparisonEmptyReason": projection["comparisonEmptyReason"],
    }


def main() -> None:
    if not __debug__:
        raise SystemExit("Verification requires assertions enabled; do not use python -O or PYTHONOPTIMIZE.")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="compare all saved expected projections (default)")
    parser.add_argument("--case", help="print one case's derived projection to stdout after verification")
    args = parser.parse_args()
    data = json.loads(FIXTURE.read_text(encoding="utf-8"))
    assert data["schemaVersion"] == "observation-fixtures.v0.8"
    assert data["status"] == "designCandidate"
    assert data["implementationStatus"] == "notImplementedInUnity"
    assert data["projectionVersion"] == VERSION
    assert data["documentKind"] == "synthetic-prevalidated-observation-fixtures-not-journal"
    assert data["metricColumns"] == METRIC_COLUMNS
    assert data["comparisonColumns"] == COMPARISON_COLUMNS
    assert data["countColumns"] == COUNT_COLUMNS
    case_ids = [case["caseId"] for case in data["cases"]]
    assert case_ids, "no observation fixtures"
    assert len(case_ids) == len(set(case_ids)), "duplicate fixture caseId"
    results = {}
    for case in data["cases"]:
        actual = project(case, data["snapshots"])
        assert compact(actual) == case["expected"], (
            case["caseId"], {"expected": case["expected"], "actual": compact(actual)}
        )
        assert actual["query"] == case["query"]
        # Reader collection order is not part of the ordering contract.
        reordered = copy.deepcopy(case)
        reordered["rounds"].reverse()
        assert project(reordered, data["snapshots"]) == actual, case["caseId"]
        results[case["caseId"]] = actual
    if args.case:
        if args.case not in results:
            parser.error(f"unknown case: {args.case}")
        print(json.dumps(results[args.case], ensure_ascii=False, indent=2))
    else:
        print(f"Verified observation v0.8: {len(results)} synthetic cases; exact numerator/6, stable positions, null/zero and counts agree.")
        print("Inputs assume upstream verified facts. No journal, entropy, nature, graph edges or Unity executed. No files written.")


if __name__ == "__main__":
    main()
