import type { Condition } from '../registration/types';
import type { RitualInput, RitualRecord } from './types';

function validateSeconds(seconds: number): void {
  if (!Number.isFinite(seconds) || seconds < 0) {
    throw new RangeError('ritual seconds must be a finite non-negative number');
  }
}

function withText(
  kind: RitualRecord['kind'],
  seconds: number,
  text: string,
  valid: boolean,
): RitualRecord {
  return {
    kind,
    seconds,
    text,
    textLen: text.length,
    valid,
  };
}

/**
 * Frozen P4 compliance rules from DESIGN.md §3.2.
 * Invalid rituals are recorded as invalid rather than silently repaired or dropped.
 */
export function buildRitualRecord(condition: Condition, input: RitualInput): RitualRecord {
  validateSeconds(input.seconds);
  const text = input.text ?? '';

  switch (condition) {
    case 0:
      return { kind: 'pull_only', seconds: input.seconds, valid: input.seconds >= 60 };
    case 1:
      return withText('intention_writing', input.seconds, text, text.length >= 30);
    case 2:
      return { kind: 'affirmation', seconds: input.seconds, valid: input.seconds >= 300 };
    case 3:
      return { kind: 'prayer', seconds: input.seconds, valid: input.seconds >= 180 };
    case 4:
      return withText('full_combo', input.seconds, text, input.seconds >= 480 && text.length >= 30);
  }
}
