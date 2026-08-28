# Test and Evidence Agent Contract

These rules apply under `tests/` in addition to the root `AGENTS.md`.

## Purpose

Tests protect named contracts and integration seams. They do not manufacture confidence through volume and do not establish product acceptance.

## Rules

- A behavior change needs a test that would fail before the change or a clear reason why another evidence class is required.
- Test at the authority level where the invariant lives: domain, runtime, persistence, Godot integration, CLI, provider adapter, or human gate.
- Preserve deterministic seeds, exact ordering, failure atomicity, checkpoint/resume, migration, stale-input rejection, and replay where relevant.
- Do not weaken an assertion, increase a timeout, skip a test, or narrow discovery merely to make a task green without documenting and authorizing the changed contract.
- Keep product, lab, live-provider, performance, and extended characterization tiers distinguishable.
- Never guess expected test counts. Reconcile `test-manifest.json` from actual discovery and explain additions, removals, and tier membership.
- A passing headless or screenshot test cannot prove movement feel, visual coherence, dialogue groundedness, autonomy, or desire to continue.
- Red attempts, environment limitations, and unrun suites remain visible in the handoff.

## Manifest discipline

`tests/test-manifest.json` is an executable declaration, not a historical diary. Update it in a focused change when discovered counts, filters, paths, or required versions change. Validation scripts and CI should fail when declared contracts drift.
