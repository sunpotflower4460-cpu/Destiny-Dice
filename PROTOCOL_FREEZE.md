# Intention Dice — Protocol Freeze v2.1

Status: **NORMATIVE / FROZEN BEFORE P1**  
Date: 2026-08-28  
Applies to: v1 experiment protocol and all implementation phases P1+

This document is the normative v2.1 addendum to `DESIGN.md` v2.0. Where this document and `DESIGN.md` conflict, **this document wins**. The purpose of Gate 0 is to remove protocol, cryptographic, timing, and analysis ambiguities before implementation begins. After a user has created a registration entry, these rules are immutable for that experiment ID.

## 0. Why this freeze exists

The app is not merely a dice UI. Its scientific value depends on keeping the causal and audit order stable:

`pre-registered protocol -> prediction commit -> random acquisition -> immutable event log -> frozen analysis`

If hashing, RNG inclusion rules, target generation, outcome handling, or timing semantics change after data collection starts, the final report becomes difficult to interpret. Gate 0 therefore freezes those semantics before P1.

---

## 1. Evidence language and scope

### 1.1 Layer A

Layer A tests whether sessions performed under an intention condition are associated with a departure of the **target-hit rate** from the pre-registered null model.

The app must not claim that a single personal experiment proves a universal paranormal mechanism. Final labels are therefore:

- `positive_pre_registered_result`: the pre-registered positive decision rule was met.
- `negative_evidence`: the pre-registered negative Bayes-factor rule was met.
- `inconclusive`: neither threshold was met.

User-facing prose may explain these in natural Japanese, but must not upgrade `positive_pre_registered_result` into a universal claim such as “超常現象を証明した”.

### 1.2 Layer B

Layer B is a non-blinded within-person measurement of short-term mood / energy change around the ritual. Placebo, expectation, demand characteristics, and ordinary psychological effects are part of what this layer measures.

### 1.3 Layer C

Layer C is an individual randomized comparison of registered wishes. Randomization strengthens the counterfactual comparison, but outcome judgment is not blinded in v1. The final report must continue to label this limitation.

### 1.4 Tamper wording

The local hash chain is **tamper-evident**, not absolutely tamper-proof. A partial edit, deletion, insertion, or reordering is detectable by `verifyChain()`. A complete rewrite of the entire local history cannot be ruled out by a local chain alone. External publication/notarization of chain heads is what makes later full-history rewriting externally detectable for anchored periods.

---

## 2. RNG source policy

### 2.1 Provider order

P1 implements providers behind one interface. The intended production order is:

1. `anu` — ANU Quantum Numbers / QRNG service via the app's proxy/configured endpoint.
2. `randomorg` — RANDOM.ORG physical true-random fallback.
3. `local` — platform cryptographic randomness (`crypto.getRandomValues`) as last-resort fallback.

Every acquisition records its actual `rngSource`. The app must never relabel a fallback as quantum.

### 2.2 Current external-service reality

The legacy ANU QRNG JSON API is being scaled back in favor of ANU Quantum Numbers hosted through AWS. Therefore P1 must not hard-code assumptions that the legacy endpoint or the old 70-second rule will remain valid.

Provider configuration (endpoint, authentication, timeout, retry/backoff, and optional quota information) belongs in the provider adapter, not in domain/statistics code.

Tests and `pnpm simulate` never call any external RNG service.

### 2.3 Layer A confirmatory inclusion

The **primary Layer A confirmatory analysis includes only valid sessions whose measured bitstream source is `anu`**.

Reason: the registered Layer A question is specifically about a quantum random source. Mixing `randomorg` or `local` into the primary sample would silently change the estimand.

Fallback sessions are never discarded from the ledger. They appear in:

- source counts / QC,
- sensitivity analyses,
- exploratory analyses,
- the final report's missing-primary-data accounting.

A fallback session is therefore “validly recorded” but not part of the quantum-primary confirmatory sample.

If insufficient `anu` data remain to satisfy the planned evidence threshold, the result is `inconclusive`; the app must not silently substitute fallback data to rescue power.

### 2.4 Layer C assignment validity

Wish assignment only requires a pre-specified unbiased/unpredictable randomization mechanism; it does not require a quantum mechanism for the RCT logic to remain valid. `anu -> randomorg -> local` fallback is therefore allowed for Layer C assignment, with the actual source stored and displayed in audit/export views.

Marketing copy may say “quantum assignment” only for wishes that were actually assigned with `rngSource='anu'`.

---

## 3. Target-direction independence

The session target (`HIGH` / `LOW`, encoded `1` / `0`) must be independent of the measured Layer A bitstream.

### 3.1 Frozen rule

Targets are pre-generated at registration from a **separate target seed** and deterministic counter-based generator. They are not drawn from the same request/bitstream that is later tested for intention effects.

Registration stores:

- `targetSeed`
- `targetAlgorithmVersion`
- the resulting target schedule or enough frozen information to reproduce it exactly

Future targets remain hidden in normal UI; only the current session target is revealed.

### 3.2 Deterministic generator

For v1, deterministic schedule/target generation uses SHA-256 counter expansion rather than `Math.random()`.

Conceptually:

`block_i = SHA256(UTF8(domainSeparator + ':' + seed + ':' + counter))`

Separate domain separators are mandatory:

- `condition-schedule-v1`
- `target-schedule-v1`

Condition shuffling uses unbiased rejection sampling when mapping bytes to a bounded integer for Fisher-Yates. Target bits are consumed directly from the independent target stream.

Same seed + same algorithm version must always produce the same output on Web, Node, and iOS.

---

## 4. Experiment day and time semantics

### 4.1 Frozen timezone

Registration stores an IANA timezone name, e.g. `Asia/Tokyo`.

The experiment timezone is frozen for the lifetime of the experiment ID. Device timezone changes or travel do not redefine historical or future experiment-day boundaries.

### 4.2 Experiment-day boundary

`dayBoundaryHour` remains a registration parameter (default `3`).

`experimentDate(now)` is calculated in the frozen experiment timezone. All daily idempotency checks use this experiment date rather than the device's raw local calendar date.

### 4.3 Clock injection

Domain/application services must not scatter direct `new Date()` calls. Time-sensitive logic receives a Clock / explicit timestamp so tests can be deterministic.

---

## 5. Daily control semantics

The machine control is **at most one control entry per experiment day**.

At the first app activation for an experiment day:

1. derive the frozen experiment date,
2. check whether a control entry already exists for that date,
3. if none exists, acquire and append one,
4. if one exists, do not acquire another.

Repeated app opens must not multiply controls or API consumption.

Control entries always record source. Source-specific QC must not hide fallbacks.

---

## 6. Layer B timing

The v2.0 order placed `moodPost` after result reveal, which would mix ritual effects with emotional reaction to success/failure. v2.1 freezes the daily core order as:

1. ensure daily control exists,
2. reveal today's condition,
3. reveal the current target,
4. capture `moodPre`,
5. perform the assigned ritual,
6. capture `moodPost`,
7. capture confidence / optional prophecy,
8. **append prediction and receive committed seq/hash**,
9. only then acquire measured RNG bits,
10. compute hits/z and append session,
11. reveal result,
12. run wish moment,
13. show permitted feedback.

Thus Layer B's pre/post pair brackets the ritual rather than the random-result reaction.

The prediction remains after the ritual and before RNG acquisition, preserving the intended question “does felt confidence predict the upcoming result?”

---

## 7. Prediction-before-RNG enforcement

This invariant must exist below the UI layer.

A session RNG acquisition is rejected unless a committed prediction reference exists for the same:

- experiment ID,
- experiment date,
- `seqInDay`,
- condition,
- target direction.

The session entry stores `predictionSeq` and verification checks `predictionSeq < session.seq`.

Disabling a button is not sufficient enforcement.

---

## 8. Canonical ledger hash specification

### 8.1 Canonical JSON

Canonicalization follows **RFC 8785 JSON Canonicalization Scheme (JCS)** semantics for all hash inputs.

A single implementation is shared by append and verification. No feature may implement its own ad-hoc `JSON.stringify` hashing path.

### 8.2 Entry hash

For every ledger row, define the logical hash object:

```json
{
  "createdAt": "<exact created_at string>",
  "payload": { "...": "parsed payload object" },
  "prevHash": "<64-char lowercase hex>",
  "type": "<ledger entry type>"
}
```

Then:

`entry_hash = lowercaseHex(SHA-256(UTF8(JCS(logicalHashObject))))`

The database's `payload_json` stores the JCS canonical payload string used by the writer.

### 8.3 Genesis / first entry

The first ledger entry is the `registration` entry.

Its `prev_hash` is exactly:

`0000000000000000000000000000000000000000000000000000000000000000`

The experiment's `genesisHash` is the first registration entry's `entry_hash`.

No special second hashing algorithm exists for genesis.

### 8.4 What verification must detect

P2 tests must detect at least:

- payload mutation,
- `type` mutation,
- `created_at` mutation,
- `prev_hash` mutation,
- deletion of an interior entry,
- insertion of an unchained entry,
- reordering,
- incorrect genesis `prev_hash`,
- non-canonical stored payload when canonical equality is required.

### 8.5 Single writer

All ledger writes pass through one append service / serialization queue. UI, RNG providers, wishes, and statistics code may not issue direct ledger `INSERT`s.

The append critical section is:

`read current head -> canonicalize -> hash -> insert`

and must be serialized to prevent two concurrent appends from using the same previous head.

---

## 9. Ledger as source of truth

The ledger remains the authoritative event store.

Derived caches/projections are allowed, but they must be reproducible from the ledger and disposable without data loss.

Application code accesses storage through a port such as `LedgerStore`; `pnpm simulate` may use an in-memory implementation while the app uses SQLite.

Node simulation must not require Capacitor, DOM, or an external network.

---

## 10. Registration version freeze

`RegistrationPayload` v2.1 extends the v2.0 definition with frozen provenance fields:

```ts
{
  protocolVersion: '2.1';
  canonicalizationVersion: 'rfc8785-jcs-v1';
  scheduleAlgorithmVersion: 'sha256-counter-fy-v1';
  targetAlgorithmVersion: 'sha256-counter-target-v1';
  targetSeed: string;
  timeZone: string;                 // IANA name, frozen
  rngPolicyVersion: 'rng-policy-v1';
  analysisPlanVersion: string;
  statsVersion: string;
  appVersion: string;
  buildId?: string;
}
```

These values are part of the registration payload and therefore the genesis hash.

After registration, changing any of them requires a new experiment ID.

---

## 11. Layer C outcome policy

A registered wish is immutable after its `wish` entry is committed.

Primary Layer C analysis uses a pre-specified conservative intention-to-treat-like policy:

- `realized` -> realized
- `not_realized` -> not realized
- `withdrawn` -> not realized
- `undecidable` -> not realized in the primary analysis
- deadline not yet reached by experiment end -> not in the primary denominator; count/report separately

A sensitivity analysis may additionally show results with `undecidable` excluded, but it is explicitly secondary and may not replace the frozen primary result.

Counts of `withdrawn` and `undecidable` are always shown by arm.

---

## 12. Wish assignment crash recovery

The required order remains:

`wish -> assignment`

Because a process can crash between those two appends, the application must support recovery without allowing human choice to enter the gap.

Rules:

- an unassigned wish is never shown as practice/sealed in normal UI,
- startup/resume detects `wish` entries with no assignment,
- assignment is idempotent: an existing assignment forbids a second assignment,
- recovery completes the random assignment using the configured fallback chain,
- the original wish is never edited or deleted.

---

## 13. Sealed-wish information boundary

Before deadline, a sealed wish's text must not be returned to normal practice UI code.

Visibility is enforced in domain/application projections, not merely by hiding a React component.

Separate privileged audit/export functions may include sealed content because the ledger must remain complete; normal wish-time/list projections must not.

---

## 14. Statistics dependency order

P4 needs `bits -> hits -> z` even though the broader statistics engine is P5. To remove that dependency inversion, the implementation order is frozen as:

`P0 -> Gate 0 -> P1 -> P2 -> P3 -> P4a -> P4 -> P5 -> P6 -> P7 -> P8 -> P9 -> P10 -> P11`

### P4a — Stats Core

P4a is deliberately tiny and may contain only UI-independent pure functions required by session recording, including:

- bit decoding/validation,
- hit counting against target,
- z-score,
- cumulative deviation primitive if required by P4 tests.

P5 extends statistics with CI, p-values, Holm correction, Bayes factors, calibration and trends without changing the frozen P4a definitions silently.

---

## 15. Interim vs final analysis API

The application must structurally separate interim and final confirmatory statistics.

Interim UI may expose:

- n,
- hit rate,
- z / cumulative deviation,
- confidence intervals where appropriate,
- Bayes factor,
- chance expectation / honest meter,
- RNG source counts.

It must not expose a repeatedly peeked confirmatory frequentist p-value.

Final confirmatory p-values / Holm-adjusted decisions are generated once against the frozen final dataset according to the registered plan.

---

## 16. Report reproducibility

The P10 report must be reproducible from exported ledger data plus the frozen protocol metadata without relying on mutable UI state.

Target end state:

```sh
pnpm verify-export <experiment.json>
pnpm generate-report <experiment.json>
```

The verifier and report generator use the same canonicalization/statistics libraries as the app where technically possible.

Confirmatory and exploratory report sections remain visually and computationally separated.

---

## 17. Implementation stack alignment

The repository currently contains React 19 dependencies. v2.1 treats the installed, tested React 19 line as the implementation baseline rather than downgrading solely to match the v2.0 prose.

Capacitor remains on the existing v6 line until a dedicated migration phase is approved. Dependency upgrades must not be mixed into scientific-protocol phases.

`jeep-sqlite` / `sql.js` compatibility constraints recorded in `PROGRESS.md` remain active.

---

## 18. Gate 0 Definition of Done

Gate 0 is complete when:

- this normative protocol freeze exists in the repository,
- `AGENTS.md` instructs implementers to read it and defines precedence,
- `README.md` exposes the new implementation order,
- `PROGRESS.md` records the freeze and unresolved external-service operational concerns,
- no runtime implementation for P1+ is introduced in the Gate 0 PR,
- existing P0 typecheck/tests/build remain green.

After Gate 0 merges, the next implementation phase is **P1 RNG module only**.
