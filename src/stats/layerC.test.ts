import { describe, expect, it } from 'vitest';
import { DEFAULT_DECISION_RULE } from '../registration/types';
import { EXPLORATORY_WARNING } from './exploratory';
import {
  analyzeExploratoryLayerC,
  analyzeFinalLayerC,
  analyzeInterimLayerC,
  fisherExactTwoSided,
  twoGroupBayesFactor10,
  type LayerCWishObservation,
} from './layerC';

function wish(
  arm: LayerCWishObservation['arm'],
  outcome: LayerCWishObservation['outcome'],
  options: Partial<Pick<LayerCWishObservation, 'likelihood' | 'influence' | 'pathway'>> = {},
): LayerCWishObservation {
  return {
    arm,
    outcome,
    likelihood: options.likelihood ?? 2,
    influence: options.influence ?? 'mixed',
    ...(outcome === 'realized' ? { pathway: options.pathway ?? 'unknown' } : {}),
  };
}

describe('P8 Layer C exact inference', () => {
  it('matches the frozen 2x2 BF golden n1=n2=2, y1=2, y2=0 -> 10/3', () => {
    expect(twoGroupBayesFactor10(2, 2, 2, 0)).toBeCloseTo(10 / 3, 12);
  });

  it('matches Fisher exact known examples', () => {
    expect(fisherExactTwoSided(2, 2, 2, 0)).toBeCloseTo(1 / 3, 12);
    expect(fisherExactTwoSided(10, 1, 14, 11)).toBeCloseTo(0.0027594561852200836, 12);
  });

  it('returns no Fisher p when either randomized arm is empty', () => {
    expect(fisherExactTwoSided(0, 0, 4, 2)).toBeNull();
    expect(fisherExactTwoSided(4, 2, 0, 0)).toBeNull();
  });
});

describe('P8 Layer C outcome policy and exploration', () => {
  it('counts withdrawn and undecidable as not realized in primary, while sensitivity excludes only undecidable', () => {
    const observations: LayerCWishObservation[] = [
      wish('practice', 'realized', { pathway: 'own_action' }),
      wish('practice', 'withdrawn'),
      wish('practice', 'undecidable'),
      wish('sealed', 'realized', { pathway: 'chance_encounter' }),
      wish('sealed', 'not_realized'),
      wish('sealed', 'undecidable'),
    ];

    const interim = analyzeInterimLayerC(observations);
    expect(interim.comparison.practice).toMatchObject({ n: 3, realized: 1, notRealized: 2, withdrawn: 1, undecidable: 1 });
    expect(interim.comparison.sealed).toMatchObject({ n: 3, realized: 1, notRealized: 2, withdrawn: 0, undecidable: 1 });
    expect(interim.sensitivityExcludingUndecidable.practice).toMatchObject({ n: 2, realized: 1, notRealized: 1, withdrawn: 1, undecidable: 0 });
    expect(interim.sensitivityExcludingUndecidable.sealed).toMatchObject({ n: 2, realized: 1, notRealized: 1, undecidable: 0 });
  });

  it('keeps likelihood/influence strata and pathways in a separately tagged exploratory result', () => {
    const observations: LayerCWishObservation[] = [
      wish('practice', 'realized', { likelihood: 1, influence: 'self', pathway: 'own_action' }),
      wish('practice', 'realized', { likelihood: 3, influence: 'external', pathway: 'other_person' }),
      wish('practice', 'not_realized', { likelihood: 3, influence: 'external' }),
      wish('sealed', 'realized', { likelihood: 1, influence: 'self', pathway: 'chance_encounter' }),
      wish('sealed', 'not_realized', { likelihood: 3, influence: 'external' }),
    ];

    const interim = analyzeInterimLayerC(observations);
    const exploratory = analyzeExploratoryLayerC(observations);
    expect('strata' in interim).toBe(false);
    expect('pathways' in interim).toBe(false);
    expect('fisherTwoSidedP' in interim).toBe(false);
    expect(exploratory.analysisKind).toBe('exploratory');
    expect(exploratory.warning).toBe(EXPLORATORY_WARNING);
    expect(exploratory.strata.likelihood).toHaveLength(3);
    expect(exploratory.strata.influence).toHaveLength(3);
    expect(exploratory.strata.likelihood[0]?.comparison.practice.realizationRate).toBe(1);
    expect(exploratory.strata.likelihood[2]?.comparison.practice).toMatchObject({ n: 2, realized: 1 });
    expect(exploratory.pathways).toEqual([
      { pathway: 'own_action', practice: 1, sealed: 0, total: 1, practiceShare: 0.5, sealedShare: 0 },
      { pathway: 'other_person', practice: 1, sealed: 0, total: 1, practiceShare: 0.5, sealedShare: 0 },
      { pathway: 'chance_encounter', practice: 0, sealed: 1, total: 1, practiceShare: 0, sealedShare: 1 },
      { pathway: 'unknown', practice: 0, sealed: 0, total: 0, practiceShare: 0, sealedShare: 0 },
    ]);
  });

  it('applies the frozen positive rule only when practice realization is higher', () => {
    const positive: LayerCWishObservation[] = [
      ...Array.from({ length: 12 }, () => wish('practice', 'realized', { pathway: 'own_action' })),
      ...Array.from({ length: 12 }, () => wish('sealed', 'not_realized')),
    ];
    const reversed: LayerCWishObservation[] = [
      ...Array.from({ length: 12 }, () => wish('practice', 'not_realized')),
      ...Array.from({ length: 12 }, () => wish('sealed', 'realized', { pathway: 'chance_encounter' })),
    ];

    const positiveResult = analyzeFinalLayerC(positive, DEFAULT_DECISION_RULE);
    expect(positiveResult.fisherTwoSidedP).not.toBeNull();
    expect(positiveResult.fisherTwoSidedP!).toBeLessThan(DEFAULT_DECISION_RULE.pThresh);
    expect(positiveResult.comparison.bf10).toBeGreaterThan(DEFAULT_DECISION_RULE.bfPos);
    expect(positiveResult.label).toBe('positive_pre_registered_result');
    expect(positiveResult.evidenceGrade).toBe('★★');
    expect(positiveResult.limitation).toBe('randomized_non_blinded_self_judgment');

    expect(analyzeFinalLayerC(reversed, DEFAULT_DECISION_RULE).label).toBe('inconclusive');
  });

  it('rejects semantically invalid realized observations without a pathway', () => {
    const malformed = {
      arm: 'practice',
      outcome: 'realized',
      likelihood: 2,
      influence: 'mixed',
    } as LayerCWishObservation;
    expect(() => analyzeInterimLayerC([malformed])).toThrow('requires pathway');
  });
});
