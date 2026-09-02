# Current Build

This file is a stable compatibility entry point. The canonical, maintained implementation report is [`docs/project/CURRENT_STATE.md`](docs/project/CURRENT_STATE.md). Do not append session logs or historical delivery records here.

## Baseline

- Accepted runtime source: `847c86b1c379e6a1dd8d4b7b641c3c89646e28c9`
- Preservation branch: `archive/pre-consolidation-2026-08-27`
- Product project: `src/societies/`
- Snow Globe laboratory: `labs/Societies.SnowGlobe/`
- Active authority: `planning/active/MILESTONE.md` — `SNOW-GLOBE-SOCIAL-KERNEL-V1 — Causeway Before Nightfall`
- Planning authority merged: PR #191 at `31ea1d6012d6fd932d0bfe0dbc621e668fd58c80`
- Packet 01 local checkpoint: `936771285fd1fd2ebb92054dc3630a656a78b941` on `feature/social-kernel-01-baseline` (tree `2bd4a709a54ae72fde84fc3e24968e5fec49e49b`)
- Packet 01 delivery: [PR #192](https://github.com/dfrer/societies/pull/192), head `ea538a506318f99946f858c7c4b50508b65fb44c`, merged as `f471efe5fe5f5b62aef37e48f27bc1fbffd0104e`
- Visual-recovery amendment: [PR #194](https://github.com/dfrer/societies/pull/194), planning head `52557d0a517b841421aa60a7b7b295375908c35f`, merged as `ccb651c67689c25eb0f3adec78abbfcf870699f5` at `2026-09-02T03:28:09Z`

## Proven now

The EB-01R bounded founder-worldcraft slice has explicit user acceptance for world readability, HUD hierarchy, inventory usability, construction clarity, and interaction feel. The previously merged hosted baseline reports 507 managed tests, 387 fast tests, 28 Godot tests, and warning-free Release/ExportRelease builds. The runtime includes authoritative gather, bounded inventory, floor/wall/post construction, placement rejection, dismantling, persistence, and replay.

## Canonical Packet 01 baseline and delivery

Packet 01 validates and characterizes the accepted empty Snow Globe scene and its edit/reload/replay route. The canonical evidence is [`planning/active/evidence/snow-globe-social-kernel-packet-01-validation.json`](planning/active/evidence/snow-globe-social-kernel-packet-01-validation.json). Local checks report 519/519 managed tests and 399/399 fast tests. Required hosted `build-test-smoke` and `lab-tests` both passed before PR #192 merged at `2026-08-31T04:52:40Z`; the hosted product gate passed 399/399 fast and 28/28 Godot tests, while the relevant hosted lab gate passed 1,186 with 5 documented skips plus its 56/56, 94/94, and 104/104 companion suites. Worst realtime physics p95 remains **23.109 ms**, a **16.67 ms target miss** and a **33.33 ms hard-safety pass**. The fixed-60 runs provide identity evidence only, not timing measurements.

## Not proven now

The accepted scene still has zero participating citizens, resources, stock, structures, build queue, stress, and crisis state. The first Packet 02 human visual gate was rejected as not near visual completeness. It does not yet contain a Packet 02V near-final district, Mara, Ivo, Sena, the gameplay-facing Snow Globe cognition interface, negotiated wetland consequence, acceptance of the 16.67 ms product performance target, accessibility acceptance, hosted release evidence, or release readiness. Packet 01 retains the accepted-scene characterization of 64 collision bodies, 12,777 initial collision shapes, and 12,781 shapes after the expected edit. The historical 51.9392 ms safety miss remains context; Packet 01 does not erase it or turn its new 16.67 ms target miss into a pass.

## Active delivery boundary

Planning PR #191 is merged, PR #194 delivered the visual-recovery amendment, and PR #196 delivered Packet 02A to `master` `155fa9dac62eebc516bcff189fd5692071f366d9` (tree `5c2e8b53a4b18802ead74b1ba57647c2846763cf`) from reviewed feature head `c268207501817335d54e31cf432ade17f93ca78a` (same tree). Hosted product run `33682822948` / job `100423159671` passed (fast 462/462, Godot build-solutions, headless 29/29, governance/diff); hosted lab run `33682822608` passed (detector `100423158477`, suite `100423215546`, final `lab-tests` `100424336278`: core 1,186 passed, 5 documented historical byte-pinning skips, 0 failed; companions 56/56, 94/94, 104/104). Retain the local signal-11 build-solutions attempt as a known failure; displayed/GPU timing remains unmeasured, and headless physics misses 16.67 ms while 33.33 ms hard safety passes. Packet 02A retains deterministic causeway state/catalog/events/session, schema-v12 persistence/replay/artifact/summary, v5 route/profile, `ExecuteCausewayIntent`, and zero-tick HUD refresh suppression/coalescing; it explicitly drops the rejected presenter/`Label3D`, keyboard review controls, source-string UI scaffold/tests, and historical evidence/status. Packet 02V is next authorized; Packet 03 is **NOT STARTED** and unauthorized until 02V human acceptance. No human visual, citizen, cognition, accessibility, deployment, or release acceptance is claimed.
