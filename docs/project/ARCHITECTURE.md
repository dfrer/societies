# Societies Architecture

## Architectural purpose

The architecture exists to preserve one causal world while allowing deterministic systems, humans, and bounded model-assisted citizens to participate through different interfaces. It must support a complete Snow Globe experience without turning the game into a provider framework or allowing presentation code to become hidden authority.

## Runtime topology

```text
Godot product client — src/societies
  input, camera, interaction, audiovisual presentation, UI
             |
             | player intents / immutable projections
             v
Authoritative runtime and domain
  PrototypeRuntimeSession, commands, events, world, citizens,
  ecology, resources, structures, work, commitments, persistence
             |
             | bounded citizen-known observation / closed vocabulary
             v
Gameplay cognition interface — not yet integrated
  request identity, cancellation, stale binding, normalized receipt
             |
       +-----+------------------+
       |                        |
       v                        v
Recorded/deterministic adapter  Governed live Snow Globe adapter
first production path          separately authorized
       |                        |
       +-----------+------------+
                   v
Authoritative validator
  accepts/rejects proposal; records communication and outcome;
  only validated commands/events can mutate the world
```

The isolated laboratory under `labs/` develops and tests cognition, scheduling, persistence, recording, provider, and recovery mechanisms. Product code may adopt a reviewed interface or implementation, but must not depend on lab CLIs, credentials, raw provider response types, billing journals, or experimental storage roots.

## Module boundaries

### Product presentation — `src/societies/scenes`, `presentation`, `ui`

Owns rendering, camera, input interpretation, animation, sound triggers, interaction affordances, and readable projections. It may cache disposable view state. It may not own canonical inventory, citizen, structure, ecological, commitment, or policy truth.

### Authoritative product runtime — `src/societies/scripts/core`, `simulation`, `world`

Owns session state, deterministic time, commands, events, catalogs, world changes, resources, work, citizens, persistence, replay, and validation. Public operations return explicit results. Restore is prepare-before-commit and must fail without partially mounting invalid state.

### Snow Globe laboratory — `labs/Societies.SnowGlobe`

Owns isolated experiments and contracts for agent observations, deliberation scheduling, normalized proposals, fallbacks, persistence, recorded response, quality comparison, provider readiness, durable attempt state, and recovery. It does not own product state or automatically authorize integration.

### Laboratory CLIs

- `Societies.SnowGlobe.BenchmarkCli` — bounded benchmark and comparison entry points.
- `Societies.SnowGlobe.RecordingCli` — governed recording workflows.
- `Societies.SnowGlobe.OpenRouterCli` — bounded OpenRouter execution/evidence workflows.

CLIs are operator tools, not game services. They require explicit authorization for network, credential, or paid operations.

### Tests — `tests/` and Godot test scenes

Tests prove named contracts at the appropriate layer. Product tests and lab tests remain separate enough to locate failures, but integration tests must exist for every adopted seam. Test count is not product value.

### Evidence and decisions

- `artifacts/` and `planning/active/evidence/` store bounded checked-in evidence and compatibility paths.
- `docs/adr/` records accepted technical decisions.
- `docs/project/DECISION_LOG.md` records project-level direction.
- Evidence never grants roadmap authority.

## World mutation path

Every mutation follows this shape:

```text
intent or proposal
  -> canonicalize and bind to current observation/state
  -> validate identity, eligibility, quantity, target, and ordering
  -> produce accepted/rejected command result
  -> append authoritative event(s)
  -> mutate domain state exactly once
  -> publish detached projection/diagnostic
  -> persist/replay from authoritative records
```

No UI callback, provider response, memory summary, animation, or diagnostic may skip this path.

### Packet 02A Causeway authority boundary

The Packet 02A substrate freezes and binds the actual Causeway definition schema/version and SHA-256 digest. Catalog mismatch fails closed before authoritative use. Canonical persistence is strict about exact fields, duplicates, ordering, and valid anchors. The accepted deterministic route transition is `ContributeCommunityTimber`, advancing revision `0->1`; edit, reload, and fixed replay must preserve Causeway equality. This remains a deterministic product substrate, not citizen cognition or visual acceptance.

## Cognition interface requirements

The first product interface must be versioned and small. A request contains only citizen-known state, stable identity/traits, current needs and obligations, recent observed events, the player's exact bounded interaction, allowed proposals, and stale-binding digests. A receipt separates:

1. a closed action proposal that requires authoritative validation; and
2. a bounded communication act that can explain or negotiate but cannot create facts.

Fresh execution may call one adapter at a defined high-value moment. Save, resume, and replay use the normalized recorded receipt and validation result rather than recalling a model. Late, stale, malformed, unknown, cancelled, or unavailable output resolves to typed rejection or deterministic fallback.

## Dependency rules

- Product runtime may not reference provider-specific or CLI assemblies.
- Presentation may reference product projections and command interfaces, not mutable domain internals.
- Lab adapters depend inward on provider-neutral lab contracts, never outward on Godot.
- Evidence codecs and storage adapters do not decide product policy.
- A generalized abstraction is added only when at least two accepted uses require it or the active milestone explicitly proves the need.
- Moving code between product and lab requires an ADR or decision explaining ownership, dependency direction, tests, and rollback.

## Refactor policy

Do not reorganize code merely because a file is large. Refactor when a bounded product task reveals one of these facts:

- mixed authority or domain/presentation ownership;
- repeated change collisions;
- a failure path that cannot be tested in isolation;
- provider or storage detail leaking across the product seam;
- a class preventing the smallest next vertical slice;
- measurable performance or correctness harm.

Refactors must preserve behavior with characterization tests, reduce a named risk, and avoid combining broad cleanup with new product behavior. The consolidation milestone deliberately leaves accepted runtime behavior unchanged.
