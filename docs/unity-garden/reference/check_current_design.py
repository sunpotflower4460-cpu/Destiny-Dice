#!/usr/bin/env python3
"""Read-only G0 consistency checks for the v0.7 design source index.

Checks source pins, permitted profile composition, the limited catalog change,
synthetic evidence examples, reference-report totals, and local document links.
Does not run Unity, simulate journal recovery, or implement a complete selector.
No dependencies outside the Python standard library; no file writes.
"""
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit


DESIGN = Path(__file__).resolve().parents[1]
REPO = DESIGN.parents[1]
CONTENT = DESIGN / "content"
MANIFEST = CONTENT / "design-manifest.v0.7.json"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_json(path):
    def reject_constant(value):
        raise ValueError(f"Non-JSON numeric constant in {path.name}: {value}")
    return json.loads(path.read_text(encoding="utf-8"), parse_constant=reject_constant)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolve_source(base, relative):
    path = (base / relative).resolve()
    require(path.is_relative_to(DESIGN), f"Source escapes design directory: {relative}")
    require(path.is_file(), f"Missing source: {relative}")
    return path


def validate_manifest():
    manifest = read_json(MANIFEST)
    require(manifest["schemaVersion"] == "design-manifest.v0.7", "Manifest schema")
    require(manifest["status"] == "designCandidate", "Manifest must remain candidate")
    require(manifest["implementationStatus"] == "notImplementedInUnity", "Implementation boundary")
    require(manifest["pathBase"] == "docs/unity-garden", "Manifest path base")
    roles = ["runtimeCandidates", "verificationReferences", "historicalReferences"]
    entries = [entry for role in roles for entry in manifest[role]]
    require(len({entry["id"] for entry in entries}) == len(entries), "Duplicate artifact ID")
    require(len({entry["path"] for entry in entries}) == len(entries), "Duplicate artifact path")
    for entry in entries:
        path = resolve_source(DESIGN, entry["path"])
        require(path.stat().st_size == entry["byteLength"], f"Byte length: {entry['path']}")
        require(digest(path) == entry["sha256"], f"Hash: {entry['path']}")
        if path.suffix == ".json":
            data = read_json(path)
            require(data["schemaVersion"] == entry["schemaVersion"], f"Schema: {entry['path']}")
            require(data["status"] == "designCandidate", f"Candidate status: {entry['path']}")
    indexed_json = {(DESIGN / entry["path"]).resolve() for entry in entries if entry["path"].endswith(".json")}
    require(indexed_json == {path.resolve() for path in CONTENT.glob("*.json") if path != MANIFEST},
            "Every content JSON must have an explicit current/reference/history role")
    runtime = {entry["id"]: entry for entry in manifest["runtimeCandidates"]}
    require(set(runtime) == {"simulation", "map", "weather", "targets", "oracle", "appearance"},
            "Unexpected runtime candidate set")
    require({key for key, entry in runtime.items() if not entry["required"]} == {"appearance"},
            "Only appearance is optional in the current candidate bundle")
    require(manifest["appearanceNotAdopted"] == {
        "runAppearanceProjectionVersion": None, "includeAppearanceProfileInRunContentManifest": False},
        "Disabled appearance must not become a required run reference")
    require(manifest["applicationOrder"] == ["simulation", "map", "weather", "targets", "oracle", "appearanceIfAdopted"],
            "Profile application order")
    require(manifest["notRuntimeAssets"] == ["verificationReferences", "historicalReferences"],
            "Reference artifacts must not supply normal gameplay results")
    data = {key: read_json(DESIGN / entry["path"]) for key, entry in runtime.items()}
    policies = manifest["policies"]
    for role, field in [("weather", "weatherPolicyVersion"), ("targets", "targetPolicyVersion"),
                        ("targets", "mappingPolicyVersion"), ("oracle", "selectionPolicyVersion")]:
        require(data[role][field] == policies[field], f"Policy mismatch: {field}")
    require(data["appearance"]["appearanceProjectionVersion"] == policies["optionalAppearanceProjectionVersion"],
            "Optional appearance version")
    require(data["weather"]["mappingPolicyVersion"] == "dynamic-faces.v0.4" and
            policies["mappingPolicyVersion"] == "dynamic-faces.v0.6", "Explicit mapping override")
    require(policies["groupingVersion"] == "observation-groups.v0.4", "Group policy")
    require(policies["runLifecycleVersion"] == "run-switch.v0.7", "Lifecycle policy")
    contract = (DESIGN / "DATA_CONTRACT.md").read_text(encoding="utf-8")
    require("run-switch.v0.7" in contract and "runLifecycleVersion" in contract, "Lifecycle contract missing")
    for role, fields in [("weather", ["baseProfile", "mapReuse"]),
                         ("targets", ["baseProfile", "mapReuse", "weatherProfile"])]:
        for field in fields:
            pin = data[role][field]
            require(digest(resolve_source(CONTENT, pin["file"])) == pin["sha256"], f"Inherited pin: {role}.{field}")
    return manifest, data, len(entries)


def validate_catalog(catalog):
    old = read_json(CONTENT / "oracle-catalog.v0.5.json")
    old_entries = {entry["id"]: entry for entry in old["entries"]}
    entries = {entry["id"]: entry for entry in catalog["entries"]}
    require(len(entries) == len(catalog["entries"]) == 28 and set(entries) == set(old_entries), "Catalog IDs/count")
    require(catalog["cardOrder"] == old["cardOrder"] and len(catalog["cardOrder"]) == 6, "Card order unchanged")
    changed = [key for key in entries if entries[key] != old_entries[key]]
    require(changed == ["message.plant.shadowed_bud"], "Only the unchanging-bud entry may change")
    new_entry = entries[changed[0]]
    old_entry = old_entries[changed[0]]
    for key, value in old_entry.items():
        if key != "requiresAll":
            require(new_entry[key] == value, f"Preserved entry field: {key}")
    require(set(new_entry["requiresAll"]) == set(old_entry["requiresAll"]) | {"light_reached_focus"},
            "Missing same-cell positive-light predicate")
    require(new_entry["focusBinding"] == {
        "eventKind": "LightReceived", "subject": "sameCellAsShadowedBud",
        "requiresPositiveAdditionalEffectiveLight": True}, "Explicit same-cell light focus")
    require(set(new_entry) == set(old_entry) | {"focusBinding"}, "Unexpected changed-entry fields")
    require(catalog["selectionPolicyInheritance"]["rankingFrom"] == "oracle-focus.v0.5",
            "Ranking inheritance")
    require(catalog["selectionPolicyVersion"] == "oracle-focus.v0.7", "Catalog policy")
    require(sum(entry["kind"] == "fallback" for entry in entries.values()) == 4, "Fallback count")
    require(sum(entry["kind"] == "factual" for entry in entries.values()) == 24, "Factual count")


def validate_synthetic_bindings():
    fixture = read_json(CONTENT / "oracle-binding-fixtures.v0.7.json")
    require(digest(CONTENT / fixture["catalogFile"]) == fixture["catalogSha256"], "Binding fixture catalog pin")
    require(fixture["selectionPolicyVersion"] == "oracle-focus.v0.7" and
            fixture["entryId"] == "message.plant.shadowed_bud", "Binding fixture contract")
    cases = fixture["cases"]
    require(len(cases) == 6 and len({case["caseId"] for case in cases}) == 6, "Six distinct synthetic cases")
    # This checks the consistency of the deliberately small design examples.
    # It does not consume actual SimulationEvents, generate groups, or rank text.
    for case in cases:
        subject, rock = case["subject"], case["mapEvidence"]
        cell = subject["cellId"]
        valid_state = (case["inputElement"] == "light" and cell in case["lightRangeCellIds"]
                       and subject["beforeStage"] == subject["afterStage"] == "bud"
                       and rock["cellId"] == cell and 0 <= rock["lightExposureQ"] < 1000
                       and rock["lightOccluderCellId"] in rock["solidCellIds"])
        events = [event for event in case["lightReceivedEvidence"]
                  if event["kind"] == "LightReceived" and event["cellId"] == cell
                  and event["additionalEffectiveLightQ"] > 0
                  and event["cumulativeAdditionalEffectiveLight"] > 0
                  and event["firstTick"] <= event["lastTick"]]
        event_ids = {event["eventId"] for event in events}
        groups = sorted(group["groupId"] for group in case["observationGroups"]
                        if group["kind"] == "LightReceived" and cell in group["cellIds"]
                        and event_ids.intersection(group["eventIds"]))
        eligible = valid_state and bool(groups)
        expected = case["expected"]
        require(eligible == expected["candidateEligible"], f"Synthetic eligibility: {case['caseId']}")
        require((groups[0] if eligible else None) == expected["focusGroupId"], f"Synthetic focus: {case['caseId']}")
    return len(cases)


def validate_stress_report():
    report = read_json(CONTENT / "water-stress-reference-report.v0.7.json")
    for file_key, hash_key in [("referenceScriptFile", "referenceScriptSha256"),
                               ("numericKernelFile", "numericKernelSha256")]:
        require(digest(resolve_source(CONTENT, report[file_key])) == report[hash_key], f"Stress {file_key}")
    for pin in report["profilePins"]:
        require(digest(resolve_source(CONTENT, pin["file"])) == pin["sha256"], f"Stress profile {pin['file']}")
    require(report["executedScope"]["rounds"] == 1200 and report["executedScope"]["ticks"] == 48000,
            "Stress scope")
    columns = report["roundRowColumns"]
    expected_pairs = {(site, recipe) for site in ["inlet", "shade", "meadow"] for recipe in ["water_drop", "water_rain"]}
    require(len(report["runs"]) == 6 and {(run["siteId"], run["recipeId"]) for run in report["runs"]} == expected_pairs,
            "All six independent stress cases must be reported")
    total_rows = 0
    for run in report["runs"]:
        rows = run["roundRows"]
        require(len(rows) == run["rounds"] == 200, "Stress rounds per case")
        before_water = run["initialWater"]
        totals = {key: 0 for key in run["totals"]}
        for number, values in enumerate(rows, 1):
            require(len(values) == len(columns), "Stress row width")
            row = dict(zip(columns, values))
            require(row["round"] == number and row["tickStart"] == (number - 1) * 40 and row["tickEnd"] == number * 40,
                    "Stress round/tick continuity")
            require(row["weatherInjected"] == 2530 and row["diceInjected"] == 1200 and row["totalInjected"] == 3730,
                    "Stress source inputs")
            after_water = row["surfaceWaterAfter"] + row["soilWaterAfter"]
            residual = after_water - before_water - row["totalInjected"] + row["surfaceEvaporated"] + row["soilEvaporated"] + row["drained"]
            require(residual == row["waterBalanceResidual"] == 0, "Stress row water conservation")
            require(after_water == row["totalWaterAfter"], "Stress row total water")
            require(row["plantCount"] == 4, "Stress initial plants retained")
            for key in totals:
                totals[key] += row[key]
            before_water = after_water
        require(totals == run["totals"], "Stress cumulative totals")
        require(before_water == run["finalState"]["totalWater"], "Stress final state total")
        total_rows += len(rows)
    return total_rows


def headings(path):
    # The documents use plain Markdown headings. Keep Japanese letters and
    # underscores; drop punctuation as GitHub does for these heading forms.
    ids, seen = set(), {}
    source = re.sub(r"```[\s\S]*?```", "", path.read_text(encoding="utf-8"))
    for line in source.splitlines():
        match = re.match(r"^#{1,6}\s+(.+?)\s*#*$", line)
        if not match:
            continue
        slug = re.sub(r"[^\w\- ]", "", match[1].lower()).replace(" ", "-")
        count = seen.get(slug, 0)
        ids.add(slug if count == 0 else f"{slug}-{count}")
        seen[slug] = count + 1
    return ids


def validate_documents():
    docs = sorted(DESIGN.glob("*.md")) + [REPO / "README.md"]
    link_count = table_count = 0
    for doc in docs:
        source = re.sub(r"```[\s\S]*?```", "", doc.read_text(encoding="utf-8"))
        for target in re.findall(r"!?\[[^\]\n]*\]\(([^)\n]+)\)", source):
            target = target.strip().strip("<>")
            parts = urlsplit(target)
            if parts.scheme or parts.netloc:
                continue
            path = (doc.parent / unquote(parts.path)).resolve() if parts.path else doc
            require(path.is_relative_to(REPO) and path.exists(), f"Broken local link in {doc.name}: {target}")
            if parts.fragment and path.suffix == ".md":
                require(unquote(parts.fragment) in headings(path), f"Broken heading in {doc.name}: {target}")
            link_count += 1
        width = None
        for line in source.splitlines():
            if line.startswith("|") and line.endswith("|"):
                clean = re.sub(r"`[^`]*`", "code", line).replace(r"\|", "escaped-pipe")
                columns = len(clean.split("|")) - 2
                if width is None:
                    width = columns
                    table_count += 1
                require(columns == width, f"Uneven table in {doc.name}: {line[:90]}")
            else:
                width = None
    return len(docs), link_count, table_count


def main():
    _, data, pins = validate_manifest()
    validate_catalog(data["oracle"])
    synthetic_cases = validate_synthetic_bindings()
    stress_rows = validate_stress_report()
    docs, links, tables = validate_documents()
    print(f"Verified design v0.7 source index: {pins} pinned artifacts; 5 required + 1 optional runtime candidates.")
    print(f"Catalog: 28 entries, 27 unchanged, 1 restricted; {synthetic_cases} synthetic evidence examples consistent.")
    print(f"Stress report arithmetic: {stress_rows} saved rows checked; natural simulation is verified separately.")
    print(f"Documents: {docs} Markdown files, {links} local links/anchors, {tables} tables.")
    print("No files written. Unity, journal recovery, full predicates, rendering and device checks remain unexecuted.")


if __name__ == "__main__":
    main()
