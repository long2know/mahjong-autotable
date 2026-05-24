# Phase L L1 design memo (DevOps angle)

> Phase K Wave 15 — Apone (DevOps). The Phase L pre-plan
> (`docs/phase-l-devops-readiness.md`, NEW W14) sketched four
> surfaces with a preliminary 10–12 wave estimate. This memo
> is the **L1 wave's design-input** for the DevOps angle — a
> set of decisions that the Phase L L1 wave needs to make
> BEFORE other lanes can sequence their L1 deliverables.
>
> Scope: the four surfaces from `docs/phase-l-devops-readiness.md
> §2`, restated as **L1 design decisions** with success criteria
> + blockers. NOT a full Phase L charter — that lives at Phase L
> bring-up. This is the **pre-charter design input** that the
> Phase K close-out waves (W15, W16, W17) refine.

## 1. Why this memo exists (vs the W14 pre-plan)

The W14 pre-plan answered "**which surfaces** are in Phase L scope?"
This memo answers "**what does the L1 wave decide** to make the
later waves executable?" Three reasons the L1-specific framing
matters:

* **Cross-lane negotiation cost.** Each L1 decision unblocks
  another lane's L1 deliverable. Surfacing the decision shape
  in W15 (vs Phase L bring-up day) gives Hicks + Bishop + Hudson
  + Vasquez time to surface their own L1 dependencies.
* **Pre-charter risk classification.** L1 decisions that need
  Stephen's call (e.g. Aurora vs session-affinity) MUST land in
  the squad inbox before Phase L bring-up so the L1 wave doesn't
  blow up on a 2-week-question.
* **Estimate refinement.** The W14 preliminary 10–12 wave
  estimate is broad. The L1 design decisions are the highest-
  leverage refinements; nailing them tightens the estimate to
  ±2 waves.

## 2. Per-surface L1 design decisions

### 2.1 WebRTC TURN cluster scaling

**L1 wave allocation.** §2.1 L1 from the W14 pre-plan: vertical
scale (4 → 8 vCPU + 8 → 16 GiB; 6 → 10–12 nodes per region) +
Hudson load-test re-baseline.

**L1 design decisions.**

* **DD-1: Resource limits vs requests.** Today: requests =
  limits at the W12 sizing (4 vCPU / 8 GiB). Phase L target: do
  we keep request=limit (Guaranteed QoS, predictable but
  expensive) or split (Burstable, cheaper but admission-
  pressure-sensitive)? **Apone recommends Guaranteed** — TURN
  CPU is real-time-sensitive and a noisy-neighbour evict
  surfaces as call-quality regression. The cost delta is small
  (~+20 % at the 12-node scale).
* **DD-2: Re-baseline test harness.** Hudson's W11 load-test
  uses `webrtcperf` against the apex ALB. Phase L L1 needs the
  same harness against the new sizing AND against the W13
  Janus integration (per `docs/janus-deployment.md`). **Apone
  recommends extending the W11 harness** — adding a Janus-
  through path as a new test scenario rather than re-writing.
* **DD-3: Node-affinity for coturn pods.** Today coturn pods
  schedule freely. Phase L sizing pushes to 10–12 nodes; a
  topology spread constraint per AZ prevents an AZ outage from
  taking 4+ nodes. **Apone recommends adding a
  `topologySpreadConstraints` rule** to the coturn StatefulSet
  patch.

**Success criteria for L1.**

* Hudson `coturn-allocations-prod` panel shows ≥ 1000
  concurrent allocations per node at the new sizing without p99
  latency regression > 5 % vs W13 baseline.
* `webrtcperf` re-baseline run completes against all four AZs
  (cluster TopologySpread holds under load).
* `kubectl drain` of a single coturn pod surfaces zero call
  drops (graceful relay-candidate handoff).

**Blockers.** Hudson's load-test harness availability is the
critical path. If Hudson is not back in scope by Phase L L1,
Apone owns the harness re-run with Hudson's W11 panel set as
the post-hoc validation. Add 1 wave if Hudson is OOO.

**L1 estimate.** 1 wave (within the W14 pre-plan's 3-wave
§2.1 total).

### 2.2 Mobile native app CI bootstrap

**L1 wave allocation.** §2.2 L1 from the W14 pre-plan: Apple +
Google enrolment + credential provisioning + production-rails
activation.

**L1 design decisions.**

* **DD-4: Versioning convention.** Shared SemVer
  (`mobile-0.24.0` → CHANGELOG row matching backend) vs
  mobile-only counter (`mobile-1.0.0` independent of backend).
  The W14 pre-plan flagged this as decision-required; Apone
  + Hicks have an outstanding preference split. **L1 wave MUST
  resolve** — Apone recommends shared SemVer for auditability;
  if Hicks insists on independent counter, accept that AS-IS
  but document the mapping in `docs/mobile-release.md`.
* **DD-5: Signing key custody.** Apple App Store Connect API
  key + Google Play Console JSON key both need a custodian.
  **Apone recommends ESO + AWS Secrets Manager** — same
  custody path as the W4 JWT keys. Avoids a second
  secret-management surface.
* **DD-6: Rehearsal cadence.** Quarterly (same as JWT rehearsal
  cadence) vs monthly. **Apone recommends monthly initially**
  (Phase L L1+L2 ramp-up; quarterly steady-state after Phase L
  L4). Monthly catches store-side certificate expiry windows
  earlier — Apple's iOS distribution certs have a 1-year window
  and a missed renewal blocks the upload step.

**Success criteria for L1.**

* `mobile-production-release.yml` end-to-end runs successfully
  in `workflow_dispatch` mode against a real iOS + Android
  build artefact, with the upload step landing on the
  TestFlight / Play Console internal tracks.
* Credential rotation rehearsal lands per the §2.2 L2 plan
  (rehearsal workflow modelled after `jwt-rotation-rehearsal-
  scheduled.yml`).

**Blockers.** Apple Developer enrolment is operator-driven
(Stephen approves; ID.me verification is a 24–48h queue).
Google Play Console enrolment is faster. **Pre-Phase-L L1
prep:** Stephen kicks off the Apple Developer enrolment
during W17 close-out so the credentials are ready for the
Phase L L1 wave.

**L1 estimate.** 1 wave (within the W14 pre-plan's 2-wave
§2.2 total).

### 2.3 Multi-region active-active

**L1 wave allocation.** §2.3 L1 from the W14 pre-plan: active-
active design memo (Aurora vs session-affinity decision).

**L1 design decisions.**

* **DD-7: Aurora Global vs session-affinity — DECISION GATE.**
  This is the highest-leverage Phase L decision. The W14
  pre-plan §2.3 recommended session-affinity (Aurora Global is
  technically active-passive with ~3–5 s cross-region
  replication lag that would surface as game-state drift if a
  session migrated mid-game). **Apone formally recommends
  session-affinity in this memo.** Stephen's call on the
  inbox memo by Phase L bring-up day.
* **DD-8: Session-affinity routing layer placement.** Three
  candidate layers:
  * **L4 (NLB-level).** Hash on source IP. Simple but breaks
    on NAT (multiple clients sharing an IP land on the same
    region — load-imbalance risk).
  * **L7 (ALB + sticky cookies).** Hash on a session cookie.
    Handles NAT cleanly. ALB-level sticky-session is already
    Hudson-monitored (the W7 `alb-stickysession-rate` panel
    exists).
  * **CloudFront Function (edge-side).** Hash on a custom
    header set by the JS bundle. Maximum control, maximum
    config surface.
  **Apone recommends L7 (ALB sticky cookies)** — the W7 panel
  is the smoke-test surface and the cookie semantics align
  with the W4 frontend session model.
* **DD-9: Session-affinity break recovery.** What happens when
  a region goes down? Two options:
  * **Re-pin on new ALB.** Session loses state, user re-joins
    via the lobby. Bishop's W4 design supports this (game
    state is lobby-bootstrapped).
  * **Cross-region state migration.** Aurora Global gives this
    for "free" but with replication lag. Out-of-scope per DD-7.
  **Apone recommends re-pin** — simpler, matches the W4 design.
  Document the user-visible "your game disconnected; re-join"
  flow in `docs/voice-sfu-design.md`.

**Success criteria for L1.**

* Decision memo at `.squad/decisions/inbox/apone-phase-l-wave-1-
  multi-region-active-active.md` lands by Phase L L1 bring-up.
* Stephen signs off on DD-7 + DD-8 + DD-9 before the
  session-affinity routing layer work (Phase L §2.3 L2) starts.

**Blockers.** Stephen's call on the inbox memo. Until Phase L
L1 closes, downstream Phase L §2.3 work is blocked.

**L1 estimate.** 1 wave (within the W14 pre-plan's 4–5-wave
§2.3 total; L1 is design-only).

### 2.4 Container scanning shift-left strategy

**L1 wave allocation.** §2.4 L1 from the W14 pre-plan:
Trivy PR trigger + CRITICAL/HIGH severity gate + `.trivy.ignore`
allow-list convention.

**L1 design decisions.**

* **DD-10: Scan trigger.** Three options:
  * **Per-PR (shift-left).** Every PR runs Trivy. Catches
    issues earliest; ~30s added to PR feedback loop.
  * **Pre-merge gate.** Required check on `main`-targeted PRs
    only. Same coverage; tighter feedback loop.
  * **Scheduled nightly.** What we do today. Catches issues
    24h late.
  **Apone recommends per-PR** — the latency cost (~30 s) is
  worth it; matches the SLSA + cosign-verify per-PR gate
  pattern (W6+).
* **DD-11: Severity gate.** Fail on CRITICAL only,
  CRITICAL+HIGH, or all severities? **Apone recommends
  CRITICAL+HIGH fail**; MEDIUM+LOW informational. Matches
  the W6 SLSA verifier-on-PR pattern. Allow-list via
  `.trivy.ignore` for known false-positives.
* **DD-12: Allow-list governance.** The `.trivy.ignore` file
  is Hudson + Apone-owned (security review on every PR that
  amends it). **Apone recommends a CODEOWNERS rule**:
  `.trivy.ignore @apone @hudson`. Bishop / Hicks / Vasquez
  cannot amend without sign-off.

**Success criteria for L1.**

* PR-blocking Trivy scan workflow lands.
* `.trivy.ignore` baseline with current allow-list (zero
  entries at L1 start; entries added as needed).
* CODEOWNERS rule for `.trivy.ignore` lands in the same PR.

**Blockers.** None — this surface is independent of the
other three.

**L1 estimate.** 1 wave (matches the W14 pre-plan's 1-wave
§2.4 total).

## 3. Cross-surface L1 decision matrix

The L1 decisions cluster around three axes; cross-surface
considerations:

| Axis                    | Decisions                       | Cross-surface implication                                                                          |
|-------------------------|----------------------------------|------------------------------------------------------------------------------------------------------|
| Cost vs reliability     | DD-1 (Guaranteed QoS), DD-5 (ESO+SM custody), DD-11 (sev gate) | All three lean toward reliability over cost. Stephen's call on the cost ceiling at Phase L bring-up. |
| Cadence                 | DD-6 (mobile rehearsal monthly), DD-10 (Trivy per-PR)         | Both lean toward faster cadence. Net: more workflow runs per month; Hudson's CI-quota panel needs to expand. |
| Decision authority      | DD-4 (Apone vs Hicks), DD-7 (Stephen's call)                  | Phase L L1 closes ONLY if both surface a sign-off path before L1 bring-up.                          |

## 4. L1 wave-by-wave estimate refinement

W14 pre-plan: **10–12 waves total**, distributed across four
surfaces. Post-L1 design decisions, the estimate refines:

| Phase L Wave | Surface | Scope                                                              | Confidence  |
|--------------|---------|---------------------------------------------------------------------|-------------|
| L1           | §2.3    | Active-active design memo (DD-7 + DD-8 + DD-9 resolution).         | HIGH        |
| L1           | §2.4    | Trivy shift-left.                                                  | HIGH        |
| L1           | §2.2    | Mobile store enrolment (DD-4 + DD-5 + DD-6 resolution).            | MED (Apple verification ETA dependency). |
| L2           | §2.1    | TURN vertical scale (DD-1 + DD-2 + DD-3).                          | HIGH        |
| L2           | §2.2    | Mobile store production-rails activation.                          | HIGH        |
| L3           | §2.3    | Session-affinity routing layer (DD-8 implementation).              | HIGH        |
| L3           | §2.1    | TURN us-west-2 horizontal.                                         | HIGH        |
| L4           | §2.3    | Per-region Redis + Bishop runtime region awareness.                 | MED (Bishop dependency). |
| L4           | §2.2    | Mobile rehearsal workflow.                                         | HIGH        |
| L5           | §2.3    | Frontend region-stable endpoint + active-active cutover.            | MED (Hicks dependency). |
| L6 (optional)| §2.3    | EU + APAC activation.                                              | LOW (regional cluster availability). |
| L6 (optional)| §2.1    | EU + APAC TURN clusters (paired with §2.3 L6).                     | LOW (paired with above). |

Refined estimate: **10 waves baseline + 2 optional** (L6 EU /
APAC activation). Confidence holds AT 10 waves; the 2 optional
waves depend on EU / APAC regional cluster lifecycle work
landing during Phase K W15+ close-out.

## 5. Phase K → L bridging actions (W15 → W17)

Three pre-Phase-L L1 actions land during Phase K close-out
to unblock L1:

1. **W15 (this wave).** This memo + Apone's `kyverno-enforce-
   rollout.md` + `hpa-min-replicas-tuning.md` close the
   Phase K §6.3 + §6.4 hardening calendar slots. Phase L L1
   does NOT inherit those items.
2. **W16.** Apone resolves DD-4 (mobile versioning) via inbox
   memo with Hicks. Closing decision by W16 close so L1 mobile
   work isn't blocked on a 2-week-question.
3. **W17.** Stephen kicks off Apple Developer enrolment per
   §2.2 DD-5 blocker note. ID.me verification queue is 24–48 h;
   we want credentials in-hand by Phase L L1 bring-up day.

## 6. Open questions for Stephen

* **DD-7 (Aurora vs session-affinity).** Apone recommends
  session-affinity; Stephen's call by Phase L bring-up.
* **DD-4 (mobile versioning).** Apone recommends shared
  SemVer; Hicks's input is the deciding factor; Stephen
  arbitrates if Apone + Hicks don't converge by W16 close.
* **L6 optional waves.** Does Phase L extend to EU/APAC
  activation, or close at L5 with us-east-1 + us-west-2
  active-active and EU/APAC remaining DR-ready? Stephen's
  call at Phase L L4 close (mid-Phase-L) based on traffic
  signal.

## 7. Cross-references

* `docs/phase-l-devops-readiness.md` — W14 pre-plan (this
  memo's parent).
* `docs/regional-eks-bringup.md` — Phase K regional cluster
  readiness (§2.3 prerequisite).
* `docs/voice-sfu-design.md` — W11 SFU design (§2.1 input).
* `docs/janus-deployment.md` — W12 Janus integration (§2.1
  input).
* `docs/mobile-release.md` — Phase K mobile release runbook
  (§2.2 input).
* `docs/jwt-rotation-rehearsal.md §5.3` — quarterly rehearsal
  cadence (§2.2 DD-6 reference).
* `docs/slsa-provenance.md` — supply-chain baseline
  (§2.4 SLSA + cosign reference).
* `docs/admission-policy.md` — Kyverno admission baseline
  (§2.4 context).
* `Phase_K_W15/Apone/history.md` — W15 wave history (memo's
  authoring context).
