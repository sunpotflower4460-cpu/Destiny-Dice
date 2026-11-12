#!/usr/bin/env python3
"""Read-only G0 scalar reference for local-growth-witness.v0.8.

Uses only the Python standard library and the pinned repository profile/fixtures.
Checks explicit golden integers and synthetic local evidence bindings. It is not
the Unity simulation, event accumulator, oracle, journal, or graph implementation.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path


HERE = Path(__file__).resolve().parent
FIXTURE_PATH = HERE.parent / "content" / "growth-witness-fixtures.v0.8.json"
POLICY = "local-growth-witness.v0.8"


class InvalidWitness(ValueError):
    """A narrow local consistency failure, not a journal integrity result."""


def require(condition, reason):
    if not condition:
        raise InvalidWitness(reason)


def integer(value, low, high):
    require(type(value) is int and low <= value <= high, "integer_range")
    return value


def same_typed_value(left, right):
    if type(left) is not type(right):
        return False
    if isinstance(left, dict):
        return left.keys() == right.keys() and all(same_typed_value(left[k], right[k]) for k in left)
    if isinstance(left, list):
        return len(left) == len(right) and all(same_typed_value(a, b) for a, b in zip(left, right))
    return left == right


def scalar_step(before, profile):
    """Evaluate only the evaporation and growth of one stated cell/tick."""
    q = profile["numeric"]["ratioScale"]
    ep = profile["evaporation"]
    gp = profile["growth"]
    capacity = integer(before["soilCapacity"], 1, profile["limits"]["soilCapacityMax"])
    surface = integer(before["beforeEvaporation"]["surfaceWater"], 0,
                      profile["limits"]["surfaceWaterMax"])
    soil = integer(before["beforeEvaporation"]["soilWater"], 0, capacity)
    er = integer(before["beforeEvaporation"]["remainder"], 0, ep["denominator"] - 1)
    light = integer(before["effectiveLightQ"], 0, q)
    wind = integer(before["effectiveWindQ"], 0, q)
    growth = integer(before["growthBefore"]["growth"], 0, gp["growthCap"])
    gr = integer(before["growthBefore"]["remainder"], 0, gp["denominator"] - 1)
    require(growth < gp["growthCap"] or gr == 0, "mature_remainder")
    # The profile pin fixes the coefficient 4; do not evaluate formula strings.
    if surface + soil == 0:
        demand, er_after, surface_amount, soil_amount = 0, 0, 0, 0
    else:
        demand, er_after = divmod(er + light + 4 * wind, ep["denominator"])
        actual = min(demand, surface + soil)
        surface_amount = min(actual, surface)
        soil_amount = actual - surface_amount
        if actual == surface + soil:
            er_after = 0
    surface_after, soil_after = surface - surface_amount, soil - soil_amount
    moisture = q * soil_after // capacity
    dry, low = gp["moistureDryStop"], gp["moistureOptimalLow"]
    high, wet = gp["moistureOptimalHigh"], gp["moistureWetStop"]
    if moisture <= dry or moisture >= wet:
        factor = 0
    elif moisture < low:
        factor = (moisture - dry) * q // (low - dry)
    elif moisture <= high:
        factor = q
    else:
        factor = (wet - moisture) * q // (wet - high)
    increment, remainder = divmod(gr + gp["perTick"] * factor * light, gp["denominator"])
    growth_after = min(gp["growthCap"], growth + increment)
    if growth_after == gp["growthCap"]:
        remainder = 0
    delta = growth_after - growth
    require((surface + soil) - (surface_after + soil_after) == surface_amount + soil_amount,
            "local_water_balance")
    return {
        "soilWater": soil_after,
        "surfaceWaterAfterEvaporation": surface_after,
        "evaporation": {"demand": demand, "surfaceAmount": surface_amount,
                        "soilAmount": soil_amount, "remainderAfter": er_after},
        "moistureQ": moisture, "moistureFactorQ": factor, "growthDelta": delta,
        "growthAfter": {"growth": growth_after, "remainder": remainder},
        "positiveGrowthWitnessAllowed": delta > 0,
        "stageTransitions": [row["event"] for row in gp["transitions"]
                             if growth < row["threshold"] <= growth_after],
    }


def make_witness(arithmetic, refs):
    # Assemble the proposed subset from the explicit fixture, not a simulated run.
    witness = copy.deepcopy(arithmetic["input"])
    expected = arithmetic["expected"]
    for key in ("soilWater", "moistureFactorQ", "growthDelta", "evaporation", "growthAfter"):
        witness[key] = copy.deepcopy(expected[key])
    witness["fieldEvidenceRefs"] = copy.deepcopy(refs)
    return witness


def check_witness(witness, event, sources, profile):
    require(event["kind"] == "GrowthObserved", "event_kind")
    require(witness["cellId"] == event["cellId"] and witness["plantId"] == event["plantId"],
            "subject_mismatch")
    require(witness["tick"] == event["firstTick"], "first_tick_mismatch")
    integer(witness["tick"], 1, profile["numeric"]["maxSerializedInteger"])
    integer(witness["cellId"], 0, profile["grid"]["width"] * profile["grid"]["height"] - 1)
    require(type(witness["plantId"]) is str and bool(witness["plantId"]), "plant_id")
    actual = scalar_step(witness, profile)
    for key in ("soilWater", "moistureFactorQ", "growthDelta", "evaporation", "growthAfter"):
        require(same_typed_value(witness[key], actual[key]), "arithmetic_mismatch")
    require(actual["growthDelta"] > 0, "nonpositive_growth")
    require(event["beforeGrowth"] <= witness["growthBefore"]["growth"] <
            witness["growthAfter"]["growth"] <= event["postGrowth"], "event_growth_bounds")

    def source(ref, evidence_kind, source_kind):
        require(type(ref) is str and ref in sources, "source_missing")
        value = sources[ref]
        integer(value["cellId"], 0, profile["grid"]["width"] * profile["grid"]["height"] - 1)
        integer(value["firstTick"], 1, profile["numeric"]["maxSerializedInteger"])
        integer(value["lastTick"], 1, profile["numeric"]["maxSerializedInteger"])
        require(value["roundId"] == event["roundId"], "source_round_mismatch")
        require(value["cellId"] == witness["cellId"], "source_subject_mismatch")
        require(value["firstTick"] <= witness["tick"] <= value["lastTick"], "source_tick_mismatch")
        require(value["evidenceKind"] == evidence_kind and value["sourceKind"] == source_kind,
                "source_kind_mismatch")
        return value

    refs = witness["fieldEvidenceRefs"]
    require(set(refs) == {"weather", "diceLight", "diceWind"}, "field_reference_keys")
    weather = source(refs["weather"], "WeatherApplicationEvidence", "weather")
    light = integer(weather["baseEffectiveLightQ"], 0, profile["numeric"]["ratioScale"])
    wind = integer(weather["baseEffectiveWindQ"], 0, profile["numeric"]["ratioScale"])
    for key, element in (("diceLight", "light"), ("diceWind", "wind")):
        if refs[key] is None:
            continue
        value = source(refs[key], "InputApplicationEvidence", "dice")
        require(value["elementId"] == element, "source_element_mismatch")
        delta = integer(value["additionalEffectiveQ"], 0, profile["numeric"]["ratioScale"])
        require(delta > 0, "nonpositive_dice_field")
        if element == "light":
            light += delta
        else:
            wind += delta
    require(light == witness["effectiveLightQ"] and wind == witness["effectiveWindQ"],
            "field_total_mismatch")
    # A single selected recipe cannot add both light and wind in the current model.
    require(refs["diceLight"] is None or refs["diceWind"] is None, "multiple_dice_elements")


def apply_patches(objects, patches):
    for patch in patches:
        require(patch["target"] in objects and bool(patch["path"]), "fixture_patch")
        target = objects[patch["target"]]
        for key in patch["path"][:-1]:
            target = target[key]
        require(patch["path"][-1] in target, "fixture_patch_key")
        target[patch["path"][-1]] = copy.deepcopy(patch["value"])


def verify():
    fixture = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))
    require(fixture["schemaVersion"] == "growth-witness-fixtures.v0.8" and
            fixture["status"] == "designCandidate" and
            fixture["implementationStatus"] == "notImplementedInUnity", "fixture_scope")
    require(fixture["simulationEvidenceVersion"] == POLICY, "policy_version")
    profile_path = FIXTURE_PATH.parent / fixture["profile"]["file"]
    profile_bytes = profile_path.read_bytes()
    require(hashlib.sha256(profile_bytes).hexdigest() == fixture["profile"]["sha256"], "profile_hash")
    profile = json.loads(profile_bytes)
    require(profile["evaporation"]["numeratorFormula"] == "effectiveLightQ+4*effectiveWindQ",
            "pinned_evaporation_formula")
    cases = {case["caseId"]: case for case in fixture["arithmeticCases"]}
    require(len(cases) == len(fixture["arithmeticCases"]), "duplicate_arithmetic_case")
    positives = 0
    for case in cases.values():
        require(same_typed_value(scalar_step(case["input"], profile), case["expected"]),
                "golden_mismatch:" + case["caseId"])
        positives += case["expected"]["positiveGrowthWitnessAllowed"]

    accepted = rejected = 0
    case_ids = set()
    for case in fixture["bindingCases"]:
        require(case["caseId"] not in case_ids, "duplicate_binding_case")
        case_ids.add(case["caseId"])
        arithmetic = cases[case["arithmeticCaseId"]]
        before, after = arithmetic["input"], arithmetic["expected"]
        event = {"kind": "GrowthObserved", "roundId": "fixture-round",
                 "plantId": before["plantId"], "cellId": before["cellId"], "firstTick": before["tick"],
                 "beforeGrowth": before["growthBefore"]["growth"], "postGrowth": after["growthAfter"]["growth"]}
        objects = {"witness": make_witness(arithmetic, case["fieldEvidenceRefs"]),
                   "event": event, "sources": copy.deepcopy(case["sourceEvidence"])}
        apply_patches(objects, case["patches"])
        error = None
        try:
            check_witness(objects["witness"], objects["event"], objects["sources"], profile)
        except InvalidWitness as exc:
            error = str(exc)
        expected = case["expected"]
        require((error is None) == expected["validPositiveGrowthWitness"] and
                error == expected["rejection"], "binding_mismatch:" + case["caseId"])
        accepted += error is None
        rejected += error is not None
    counts = {"arithmeticCases": len(cases), "positiveArithmeticCases": positives,
              "nonpositiveArithmeticCases": len(cases) - positives,
              "bindingCases": len(case_ids), "acceptedBindings": accepted, "rejectedBindings": rejected}
    require(counts == fixture["expectedCounts"], "fixture_counts")
    return counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="Verify the saved design examples (default).")
    parser.add_argument("--json", action="store_true", help="Print the verification counts as JSON.")
    args = parser.parse_args()
    counts = verify()
    if args.json:
        print(json.dumps({"simulationEvidenceVersion": POLICY, **counts,
                          "filesWritten": 0, "scope": "scalar arithmetic and synthetic local bindings"}))
    else:
        print(f"Verified growth witness v0.8: {counts['arithmeticCases']} scalar goldens; "
              f"{counts['acceptedBindings']} accepted and {counts['rejectedBindings']} rejected synthetic bindings.")
        print("No files written. Full simulation, first-positive selection, graph projection, journal, C# and Unity remain unexecuted.")


if __name__ == "__main__":
    main()
