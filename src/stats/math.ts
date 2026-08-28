const LANCZOS_COEFFICIENTS = [
  0.9999999999998099,
  676.5203681218851,
  -1259.1392167224028,
  771.3234287776531,
  -176.6150291621406,
  12.507343278686905,
  -0.13857109526572012,
  9.984369578019572e-6,
  1.5056327351493116e-7,
] as const;

export function logGamma(value: number): number {
  if (!Number.isFinite(value) || value <= 0) {
    throw new RangeError('logGamma value must be finite and > 0');
  }

  if (value < 0.5) {
    return Math.log(Math.PI) - Math.log(Math.sin(Math.PI * value)) - logGamma(1 - value);
  }

  const z = value - 1;
  let x = LANCZOS_COEFFICIENTS[0];
  for (let index = 1; index < LANCZOS_COEFFICIENTS.length; index += 1) {
    x += LANCZOS_COEFFICIENTS[index] / (z + index);
  }
  const t = z + LANCZOS_COEFFICIENTS.length - 1.5;
  return 0.5 * Math.log(2 * Math.PI) + (z + 0.5) * Math.log(t) - t + Math.log(x);
}

/** Standard normal CDF using a stable erf approximation. */
export function standardNormalCdf(z: number): number {
  if (Number.isNaN(z)) throw new TypeError('z must not be NaN');
  if (z === Number.POSITIVE_INFINITY) return 1;
  if (z === Number.NEGATIVE_INFINITY) return 0;

  const sign = z < 0 ? -1 : 1;
  const x = Math.abs(z) / Math.sqrt(2);
  const t = 1 / (1 + 0.3275911 * x);
  const a1 = 0.254829592;
  const a2 = -0.284496736;
  const a3 = 1.421413741;
  const a4 = -1.453152027;
  const a5 = 1.061405429;
  const erf = sign * (1 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.exp(-x * x));
  return 0.5 * (1 + erf);
}

export function pearsonCorrelation(xs: readonly number[], ys: readonly number[]): number | null {
  if (xs.length !== ys.length) throw new RangeError('correlation arrays must have equal length');
  if (xs.length < 2) return null;
  if (xs.some((value) => !Number.isFinite(value)) || ys.some((value) => !Number.isFinite(value))) {
    throw new TypeError('correlation values must be finite');
  }

  const meanX = xs.reduce((sum, value) => sum + value, 0) / xs.length;
  const meanY = ys.reduce((sum, value) => sum + value, 0) / ys.length;
  let covariance = 0;
  let varianceX = 0;
  let varianceY = 0;

  for (let index = 0; index < xs.length; index += 1) {
    const dx = xs[index]! - meanX;
    const dy = ys[index]! - meanY;
    covariance += dx * dy;
    varianceX += dx * dx;
    varianceY += dy * dy;
  }

  if (varianceX === 0 || varianceY === 0) return null;
  return covariance / Math.sqrt(varianceX * varianceY);
}

export function ordinaryLeastSquaresSlope(xs: readonly number[], ys: readonly number[]): number | null {
  if (xs.length !== ys.length) throw new RangeError('regression arrays must have equal length');
  if (xs.length < 2) return null;
  if (xs.some((value) => !Number.isFinite(value)) || ys.some((value) => !Number.isFinite(value))) {
    throw new TypeError('regression values must be finite');
  }

  const meanX = xs.reduce((sum, value) => sum + value, 0) / xs.length;
  const meanY = ys.reduce((sum, value) => sum + value, 0) / ys.length;
  let numerator = 0;
  let denominator = 0;
  for (let index = 0; index < xs.length; index += 1) {
    const dx = xs[index]! - meanX;
    numerator += dx * (ys[index]! - meanY);
    denominator += dx * dx;
  }
  return denominator === 0 ? null : numerator / denominator;
}
