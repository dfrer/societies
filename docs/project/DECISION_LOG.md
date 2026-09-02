# Societies Project Decision Log

Project-level decisions change product identity, authority, roadmap, or operating rules. Technical implementation decisions with narrower scope belong in `docs/adr/`.

## D-001 — Deterministic simulation owns reality

**Status:** Accepted

World facts and every state-changing outcome belong to deterministic domain/runtime code. Humans, deterministic planners, and model-assisted citizens all act through validated commands and events. Presentation and inference cannot mutate authority directly.

## D-002 — LLMs are bounded cognition, not simulation authority

**Status:** Accepted

LLMs may interpret citizen-known state, deliberate, communicate, negotiate, summarize bounded memory, and propose closed actions. Invalid, stale, late, or unavailable output resolves through typed rejection or deterministic fallback. Replay does not recall a model.

## D-003 — Snow Globe is the first complete miniature of Societies

**Status:** Accepted

Snow Globe is not merely an isolated lab. The lab proves mechanisms; the Godot product must realize the full human/citizen/consequence loop at small scale before civilization-scale expansion.

## D-004 — The player is an embodied resident-founder

**Status:** Accepted 2026-08-26

The player has hands, a home, a voice, and limited social standing—not omnipotent command authority. Influence comes from work, promises, persuasion, and outcomes. Citizens may counter, refuse, withdraw, or proceed.

## D-005 — First mature consequence is the wetland water-control commitment

**Status:** Accepted direction 2026-08-26

The first complete proof centers on a failing causeway, ecological and shelter tradeoffs, Mara/Ivo/Sena, and a negotiated outcome that changes physical, resource, labor, trust, and next-day state.

## D-006 — Human acceptance is a required evidence class

**Status:** Accepted

Automated correctness, screenshots, diagnostics, and agent review cannot establish player feel, visual coherence, citizen credibility, or desire to continue. The owner verdict is recorded exactly and may keep a mechanically green milestone incomplete.

## D-007 — EB-01R is the accepted bounded worldcraft baseline

**Status:** Accepted 2026-08-27

Commit `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9` is the preserved source baseline. The owner accepted world readability, HUD hierarchy, inventory usability, construction clarity, and interaction feel. This does not accept performance, accessibility, release readiness, citizen integration, or the complete product.

## D-008 — One repository and one active milestone

**Status:** Accepted 2026-08-27

The product, Snow Globe lab, cognition-quality work, evidence, research, and planning remain in `dfrer/societies` with explicit directory boundaries. Only `planning/active/MILESTONE.md` authorizes work. Historical plans are archived rather than allowed to compete.

## D-009 — Planning is the owner's primary development interface

**Status:** Accepted 2026-08-27

The owner contributes mainly through product planning, critique, tradeoffs, and human acceptance. Agents translate that into technical packets, implementation, validation, and review. The owner is not expected to manage source-level decisions from message to message.

## D-010 — Execution agents do not own the roadmap

**Status:** Accepted 2026-08-27

An agent may recommend follow-on work, but cannot activate it. The next task comes from the ordered missing evidence in an owner-accepted milestone. Adjacent infrastructure or feature expansion requires an explicit decision.

## D-011 — Consolidate before further feature work

**Status:** Active 2026-08-27

Preserve and integrate the accepted stack, replace conflicting authority documents, archive historical active plans, add governance checks and scoped instructions, and close obsolete delivery paths before selecting the next product proof.

## D-012 — Do not perform a broad code rewrite during consolidation

**Status:** Accepted 2026-08-27

The accepted runtime and lab code remain behaviorally unchanged during repository governance work. Large files and mixed responsibilities are recorded as debt. Refactor them only through bounded tasks with characterization and triggered validation.

## D-013 — Societies is the product; Snow Globe is its bounded social/cognition proving ground

**Status:** Accepted 2026-08-28

Societies remains the embodied player-facing product and deterministic world authority. Snow Globe names its first miniature social proof and the bounded cognition/laboratory work that supports it; it is not a separate frontend, provider dashboard, alternate simulation authority, or automatic roadmap. Product integration adopts only the smallest reviewed request/receipt contract needed by an accepted interaction.

Laboratory CLIs, provider types, credentials, billing, raw responses, routing, and operator stores remain outside product runtime and player presentation. Recorded and deterministic adapters are the first product path; any live-model pilot requires a separate owner decision.

## D-014 — Select the three-citizen Causeway Before Nightfall proof

**Status:** Accepted 2026-08-28

The next bounded product proof is `SNOW-GLOBE-SOCIAL-KERNEL-V1 — Causeway Before Nightfall`. Exactly Mara, Ivo, and Sena must participate as embodied, persistent citizens with bounded knowledge and distinct material interests in one failing-wetland-causeway situation. One citizen is an internal integration checkpoint only, not the product pass gate.

The scenario must support state-driven player support for Mara or Ivo, keeping or sacrificing the player's reserved dry timber, a staged costly compromise with immediate and dated obligations, citizen-to-citizen action, no action through the following morning, contribution, counter/refusal, promise and breach, resource consumption/withholding, stale and failed cognition, deterministic fallback, save/resume/replay, and next-morning physical, resource, labor, ecological, fulfillment/delivery, commitment, and relationship consequences. There is no universal cost-free ending, majority tally, privileged player choice, or magical veto.

The planning PR authorizes no implementation while unmerged. Once merged, work proceeds through the milestone's nine ordered one-PR packets, beginning with verified baseline/performance characterization. Live provider, network, credential, paid, billing, deployment, release, and adjacent feature work remain unauthorized.

## D-015 — Consolidation transition completed

**Status:** Completed 2026-08-28

D-011 reached its completion boundary when consolidation, hosted validation, governance workflow, branch-protection repair, and documentation closeout were integrated on `master`. The exact accepted runtime remains `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`, and the completed milestone is archived unchanged at `planning/archives/2026-08-28-consolidation-v1/MILESTONE.md`.

This transition closes the consolidation freeze without rewriting D-011's original record. It does not itself authorize product work; merge-conditioned authorization is defined by D-014, `project-governance.json`, and the active milestone.

## D-016 — Recover Causeway visual production before social breadth

**Status:** Owner decision accepted/selected 2026-09-01; delivered through PR #194

The owner rejected the first Packet 02 visual presentation as incomplete: “basically none of this is near actual visual completleness, needs massive work”. This supersedes D-014 only on sequencing and visual-production detail; it does not reject authoritative mechanics or necessarily the broad block-based substrate. The owner selected **A — weathered civic-folk wetland**: blocky voxel terrain/world substrate with richer authored non-terrain models, using an Eco-like structural reference without copying its assets or style.

The ordered recovery is Packet 02A deterministic substrate recovery, Packet 02V one near-final authored Causeway Before Nightfall district, then Packet 03. Packet 02A preserves the causeway domain/scenario/event/session/schema/persistence/replay/artifact/performance seams and hardened behavior while dropping primitive presenter/`Label3D`, direct review shortcuts, source-string UI tests, and rejected completion evidence. Packet 02V must pass an actual interactive in-engine human visual gate before social development resumes. Packet 03 remains unstarted. One isolated non-shipping character scale/silhouette/rig-import/locomotion feasibility study is permitted; it uses no Mara/Ivo/Sena identity, does not enter runtime, and makes no citizen-readiness claim. The salvage source is the immutable local branch `codex/snow-globe-social-kernel-packet-02`, base `760c39e22219c41a92ec021fbf16490df380d004`, checkpoint `7aad77dc10f2edf84e44f5f43e0082568a1ab8e9` (tree `fe0c91ca05d6d3f24647967ae4876b3d8265e131`), rejection record `269c80e16766094378afd4809bca40e045ab686b` (tree `c18d419e53dee2a60f90ffa44baa89ffc19b51ed`), evidence `planning/active/evidence/snow-globe-social-kernel-packet-02-validation.json`; no GitHub link/publication exists and later rejection records are not recovery source.

No purchases, downloads, accounts, providers, credentials, paid calls, deployment, publication, release, or automatic implementation are authorized by this decision. PR #194 delivered planning head `52557d0a517b841421aa60a7b7b295375908c35f` to `master` as merge `ccb651c67689c25eb0f3adec78abbfcf870699f5` at `2026-09-02T03:28:09Z`. Required hosted evidence passed: `build-test-smoke` run `33531175853`, job `99934428113`; Snow Globe lab detector job `99934427058`, suite job `99934496859`, and `lab-tests` job `99935427335`.

## D-017 — Packet 02A substrate recovery is the current implementation boundary

**Status:** Branch-local implementation awaiting technical review and hosted delivery 2026-09-01

Packet 02A is based on `master` `1745896535124bd39ca6321fe6430d93de81bf43` on `feature/social-kernel-02a-causeway-substrate`. It retains deterministic causeway state/catalog/events/session, schema-v12 persistence/replay/artifact/summary, v5 route/profile, `ExecuteCausewayIntent`, and zero-tick HUD refresh suppression/coalescing. Schema-v12 freezes and binds the actual Causeway definition schema/version/SHA-256 digest and fails closed on catalog mismatch; exact-field, duplicate, order, and valid-anchor checks are strict. The accepted deterministic route transition is `ContributeCommunityTimber`, revision `0->1`, with Causeway equality across edit/reload/fixed replay. It drops the rejected presenter/`Label3D`, keyboard review controls, source-string UI scaffold/tests, and historical evidence/status. This branch-local state has no hosted or human acceptance and does not authorize Packet 02V until technical review and hosted delivery are complete.
