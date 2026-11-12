#!/usr/bin/env python3
"""G0 read-only reference for six forced-rain/water stress runs.

Python standard library only. The existing v0.6 numeric kernel is imported,
never invoked through its CLI and never rewritten. Default/--verify compares
with the committed v0.7 report; --json prints a newly calculated report to
stdout. This script has no file-writing operation. It is not Unity game code.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
CONTENT = HERE.parent / 'content'
REPORT_PATH = CONTENT / 'water-stress-reference-report.v0.7.json'
KERNEL_PATH = HERE / 'diversity_v06.py'
KERNEL_SHA256 = '2e2af0ad82435698d0bb9d4be336a6b93a15071cd252f3f06ba85d2cc63d9096'
PROFILE_PINS = {
    'simulation-profile.v0.2.json': '7615b63c32d1e67c74b8c42e7a491859ca6c9e430e49e91b37d9a534f5e0f6c6',
    'garden-map.v0.3.json': 'fbf85d3c8278d4bd7eb6fdb06982923c5b039d1aad86596922742371a374fe20',
    'weather-profile.v0.4.json': '9aa3f20ec4dbc41d14d6beb4fa7437304647a9c8256f9facb4d6d1923d31d1b6',
    'target-profile.v0.6.json': 'dd72a71da076a597f34085b003ee02f05660d1801410c3df17f9517c91a7cb2a',
}
ROUNDS = 200
WATER_RECIPES = ['water_drop', 'water_rain']
MAX_JSON_INTEGER = 2**53 - 1
MAX_INT64 = 2**63 - 1
ROUND_COLUMNS = [
    'round', 'tickStart', 'tickEnd', 'weatherInjected', 'diceInjected',
    'totalInjected', 'surfaceEvaporated', 'soilEvaporated', 'drained',
    'waterBalanceResidual', 'surfaceWaterAfter', 'soilWaterAfter',
    'totalWaterAfter', 'maxCellSurfaceIncludingInjection',
    'maxCellSurfaceAfterTick', 'anchorSurfaceAfter', 'anchorSoilAfter',
    'drainSurfaceAfter', 'plantCount', 'flowerCount', 'growthDelta',
    'numericStateSha256',
]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_kernel():
    assert sha256(KERNEL_PATH) == KERNEL_SHA256, 'v0.6 kernel changed'
    for name, expected in PROFILE_PINS.items():
        assert sha256(CONTENT / name) == expected, name
    spec = importlib.util.spec_from_file_location('garden_reference_v06', KERNEL_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def check_serialized_integers(value):
    if isinstance(value, bool) or value is None:
        return
    if isinstance(value, int):
        assert abs(value) <= MAX_JSON_INTEGER
    elif isinstance(value, dict):
        for item in value.values():
            check_serialized_integers(item)
    elif isinstance(value, (list, tuple)):
        for item in value:
            check_serialized_integers(item)


def formatted_json(value, level=0):
    """Keep each measured round on one line, with readable object boundaries."""
    indent = '  ' * level
    if isinstance(value, dict):
        return '{\n' + ',\n'.join(
            '  ' * (level + 1) + json.dumps(key) + ': ' + formatted_json(item, level + 1)
            for key, item in value.items()) + '\n' + indent + '}'
    if isinstance(value, list) and any(isinstance(item, (list, dict)) for item in value):
        return '[\n' + ',\n'.join('  ' * (level + 1) + formatted_json(item, level + 1)
                                 for item in value) + '\n' + indent + ']'
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'))


def calculate():
    ref = load_kernel()
    runs = []
    initial_water = sum(c['initialSurfaceWater'] + c['initialSoilWater'] for c in ref.CELLS)
    # Nonnegative inventories and exact conservation bound every intermediate
    # cell water quantity in these 200-round cases, including after flow.
    case_water_upper_bound = initial_water + ROUNDS * (2530 + 1200)
    assert case_water_upper_bound < ref.P['limits']['surfaceWaterMax']
    max_head = max(ref.HEIGHT) + case_water_upper_bound
    max_flow_request = max_head * 125 // 1000
    arithmetic_upper_bounds = {
        'headTimesConductance': max_head * 125,
        'fourNeighbourRequestSum': 4 * max_flow_request,
        'flowRequestTimesDonorBudget': max_flow_request * case_water_upper_bound,
        'growthNumerator': 999999 + 25 * 1000 * 1000,
        'soilWaterTimesRatioScale': 1000 * 1000,
    }
    assert all(value <= MAX_INT64 for value in arithmetic_upper_bounds.values())

    for site in ref.SITE_ORDER:
        for recipe in WATER_RECIPES:
            case_id = f'water-stress.v0.7:{site}:{recipe}'
            world = ref.World(case_id=case_id, weather_id='drizzle', site_id=site)
            anchor = ref.SITE_BY[site]['centerCellId']
            totals = {key: 0 for key in ['weatherInjected', 'diceInjected',
                      'totalInjected', 'surfaceEvaporated', 'soilEvaporated', 'drained']}
            rows = []
            first_drain_round = None
            max_surface = max_post_tick_surface = 0
            for round_number in range(1, ROUNDS + 1):
                before_water = sum(world.surface) + sum(world.soil)
                tick_start = world.tick
                result = world.resolve(recipe)
                after_water = sum(world.surface) + sum(world.soil)
                residual = (after_water - before_water - result['totalInjected']
                            + result['surfaceEvaporated'] + result['soilEvaporated']
                            + result['drained'])
                assert residual == result['waterBalanceResidual'] == 0
                assert result['weatherInjected'] == 2530 and result['diceInjected'] == 1200
                assert result['totalInjected'] == 3730
                assert world.tick - tick_start == 40 and world.round == round_number
                assert all(0 <= value <= 1000000 for value in world.surface)
                assert all(0 <= value <= ref.CAP[i] for i, value in enumerate(world.soil))
                assert all(0 <= value < 1000 for value in world.evap)
                assert all(0 <= p['growth'] <= 2500 and 0 <= p['growthRemainder'] < 1000000
                           for p in world.plants.values())
                assert len(world.plants) == len(ref.MAP['initialPlants']) == 4
                assert after_water <= case_water_upper_bound
                check_serialized_integers(result)
                for key in totals:
                    totals[key] += result[key]
                max_surface = max(max_surface, result['maxSurfaceDepth'])
                max_post_tick_surface = max(max_post_tick_surface, result['maxPostTickSurfaceDepth'])
                if result['drained'] > 0 and first_drain_round is None:
                    first_drain_round = round_number
                rows.append([
                    round_number, tick_start, world.tick, result['weatherInjected'],
                    result['diceInjected'], result['totalInjected'], result['surfaceEvaporated'],
                    result['soilEvaporated'], result['drained'], residual,
                    result['surfaceWaterAfter'], result['soilWaterAfter'], after_water,
                    result['maxSurfaceDepth'], result['maxPostTickSurfaceDepth'],
                    world.surface[anchor], world.soil[anchor], world.surface[ref.OUTLET],
                    result['plantCount'], result['flowerCount'], result['growthDelta'],
                    world.snapshot_hash(),
                ])
            cumulative_residual = (sum(world.surface) + sum(world.soil) - initial_water
                                   - totals['totalInjected'] + totals['surfaceEvaporated']
                                   + totals['soilEvaporated'] + totals['drained'])
            assert cumulative_residual == 0
            runs.append({
                'caseId': case_id, 'siteId': site, 'centerCellId': anchor,
                'recipeId': recipe, 'weatherId': 'drizzle', 'rounds': ROUNDS,
                'ticksVerified': ROUNDS * 40, 'allRoundsReported': True,
                'initialWater': initial_water, 'firstDrainRound': first_drain_round,
                'totals': totals, 'waterBalanceResidual': cumulative_residual,
                'maxCellSurfaceIncludingInjection': max_surface,
                'maxCellSurfaceAfterTick': max_post_tick_surface,
                'finalState': {
                    'tickIndex': world.tick, 'round': world.round,
                    'surfaceWater': sum(world.surface), 'soilWater': sum(world.soil),
                    'totalWater': sum(world.surface) + sum(world.soil),
                    'maxCellSurface': max(world.surface),
                    'minSoilWater': min(world.soil[i] for i in range(ref.N) if ref.CAP[i] > 0),
                    'maxSoilWater': max(world.soil),
                    'anchorSurface': world.surface[anchor], 'anchorSoil': world.soil[anchor],
                    'drainSurface': world.surface[ref.OUTLET],
                    'plantCellGrowthRows': [[i, world.plants[i]['growth'], world.plants[i]['stage']]
                                           for i in sorted(world.plants)],
                    'numericStateSha256': world.snapshot_hash(),
                },
                'roundRows': rows,
            })
    report = {
        'documentKind': 'water-stress-design-reference-report',
        'schemaVersion': 'water-stress-reference-report.v0.7',
        'status': 'designCandidate', 'implementationStatus': 'notImplementedInUnity',
        'date': '2026-11-12',
        'referenceScriptFile': '../reference/water_stress_v07.py',
        'referenceScriptSha256': sha256(Path(__file__)),
        'numericKernelFile': '../reference/diversity_v06.py',
        'numericKernelSha256': KERNEL_SHA256,
        'profilePins': [{'file': name, 'sha256': digest} for name, digest in PROFILE_PINS.items()],
        'reproduction': {
            'verify': 'python docs/unity-garden/reference/water_stress_v07.py --verify',
            'emitJson': 'python docs/unity-garden/reference/water_stress_v07.py --json',
            'dependencies': 'Python standard library plus the pinned repository files only',
            'fileWritesByScript': False,
            'independentImplementation': False,
        },
        'executedScope': {
            'sites': ref.SITE_ORDER, 'recipes': WATER_RECIPES,
            'forcedWeather': 'drizzle', 'roundsPerIndependentRun': ROUNDS,
            'runs': 6, 'rounds': 1200, 'ticks': 48000,
            'initialState': 'Original map initial water and four plants for each independent run',
            'selection': 'Development forced inputs; no mapping, D6, oracle or weather lottery',
            'perTickChecksInheritedFromKernel': ['exact water conservation',
                'surface and soil water bounds', 'evaporation and growth remainder bounds',
                'growth bounds', 'water injection bounds', 'donor flow budget and request bounds'],
            'perRoundChecks': ['exact water conservation', 'state bounds',
                'serialized integer bounds', 'four original plants retained'],
        },
        'integerRangeEvidence': {
            'pythonIntegersAreUnbounded': True,
            'maxSerializedInteger': MAX_JSON_INTEGER,
            'signedInt64MaxDecimal': str(MAX_INT64),
            'caseTotalWaterUpperBoundIgnoringAllSinks': case_water_upper_bound,
            'selectedIntermediateArithmeticUpperBounds': arithmetic_upper_bounds,
            'meaning': 'Bounds for this fixed input set, not execution of C# checked arithmetic or a proof for arbitrary profiles.',
        },
        'roundRowColumns': ROUND_COLUMNS,
        'numericStateHashMeaning': 'v0.6 ordered numeric state digest including tick/round; not a WorldSnapshot/JCS journal stateHash',
        'runs': runs,
        'notCovered': ['Unity or C# implementation', 'Mono/IL2CPP checked-overflow behavior',
            'unlimited-duration stability', 'visual water depth and readability',
            'journal durability or interruption recovery', 'mapping and dice/oracle selection',
            'SimulationEvent aggregation and oracle-focus.v0.7 predicates/messages',
            'mature-garden-boundary.v0.6 54 cases', 'real physical water units'],
        'acceptanceMeaning': 'All six forced-input design calculations are reported. Integer bounds and water conservation do not establish exhibit readiness or pleasant-looking long rain.',
    }
    check_serialized_integers(report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group()
    group.add_argument('--verify', action='store_true')
    group.add_argument('--json', action='store_true', help='Print calculated report; does not write a file')
    args = parser.parse_args()
    report = calculate()
    if args.json:
        print(formatted_json(report))
        return
    saved = json.loads(REPORT_PATH.read_text(encoding='utf-8'))
    assert report == saved, 'Calculated stress report differs from saved v0.7 report'
    print('Verified water stress v0.7: 6 runs, 1200 rounds, 48000 ticks; all water balances zero; bounds valid; no files written.')


if __name__ == '__main__':
    main()
