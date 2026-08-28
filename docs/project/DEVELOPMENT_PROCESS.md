# Societies Development Process

## Purpose

This process lets the owner contribute primarily through vision, planning, tradeoffs, and human judgment while agents perform the technical heavy lifting. It is designed to prevent the two recurring failure modes of the project:

1. fragmented feature production without convergence; and
2. coherent infrastructure expansion that outruns the next product proof.

## Role model

### Project owner

Owns product meaning, priorities, accepted tradeoffs, and human gates. The owner should be asked questions such as:

- What should the player understand, feel, and be able to influence?
- Which consequence best expresses the concept?
- Does this citizen feel coherent and autonomous?
- Is the experience accepted, rejected, or in need of bounded revision?

The owner should not be required to choose classes, data structures, refactor seams, test filters, or merge tactics.

### Planning and architecture agent

Maintains the charter, current state, active milestone, decisions, and technical translation. It presents real options, costs, risks, and a recommendation. It selects the next bounded evidence from an owner-accepted milestone, not from novelty or ease of implementation.

### Implementation agent

Receives one task packet and owns its bounded outcome. It may choose implementation details inside accepted architecture. It must stop rather than expand scope when the packet is complete or a decision is required.

### Review agent

Reads the actual diff, tests, failure paths, architecture rules, and task packet independently. It checks overclaims, unrelated changes, weak tests, state-authority leaks, provider leakage, and whether the result actually clears the named value gate.

### CI and deterministic tooling

Provide mechanical evidence. They never declare visual quality, social coherence, fun, or human acceptance.

## Authority flow

```text
owner vision and judgment
        |
        v
charter + accepted decision
        |
        v
one active milestone
        |
        v
bounded task packet
        |
        v
implementation -> tests -> independent review
        |
        v
runtime/human gate when required
        |
        v
merge, rework, stop, or archive
```

No execution-agent completion recommendation bypasses this flow.

## Milestone requirements

Only `planning/active/MILESTONE.md` may authorize work. A milestone must contain:

- the product or engineering outcome;
- why it is the highest-value next proof;
- accepted baseline and dependencies;
- scope and explicit non-goals;
- ordered evidence to obtain;
- mechanical, runtime, and human gates;
- performance and provider boundaries where relevant;
- stop conditions;
- the owner decision required at completion.

A milestone may contain several task packets, but only one tightly coupled implementation path should be active at a time unless ownership and files are demonstrably independent.

## Task packet template

```markdown
# <ID> — <bounded outcome>

## Purpose
What product or architecture fact will become true, and why it matters now.

## Accepted baseline
Branch, exact SHA, relevant accepted behavior, and preserved unrelated work.

## Owned boundary
Files/modules the task may change.

## Non-goals
Adjacent systems, cleanup, abstractions, content, provider work, and roadmap changes that are prohibited.

## Required behavior
Observable behavior and deterministic/state invariants.

## Acceptance
Focused tests, integrated validation, persistence/replay, runtime observation, performance, security, and human gate as applicable.

## Stop conditions
Conditions requiring the agent to stop and report instead of guessing or broadening.

## Delivery
Expected branch, commit, PR base, evidence, and handoff.
```

## Work cycle

### 1. Orientation

- inspect branch, exact commit, worktree status, and remote relationship;
- read the governance file, charter, current state, active milestone, architecture, and nearest scoped instructions;
- inspect relevant code, tests, evidence, and historical decisions;
- state what is proven, assumed, red, and outside scope.

### 2. Technical plan

The implementation agent writes a short plan tied to the task packet. It may decompose internal steps but may not redefine the outcome. Any conflict with architecture or current reality is surfaced immediately.

### 3. Focused implementation

- preserve unrelated changes;
- make the smallest coherent vertical change;
- keep domain truth out of scenes, presenters, adapters, and diagnostics;
- add or update tests at the same authority level as the behavior;
- avoid speculative generalization and opportunistic cleanup.

### 4. Progressive validation

Run the cheapest relevant checks during iteration, then every gate triggered by the change. Typical order:

1. governance and static checks;
2. focused unit tests;
3. affected integration and Godot tests;
4. full managed/product/lab suites required by the manifest or task;
5. build configurations;
6. persistence, replay, migration, cancellation, security, or performance routes;
7. runtime observation;
8. human product gate.

Report every failure. Do not rerun until green and omit the red attempt from the handoff.

### 5. Independent review

The reviewer checks:

- task-packet compliance and non-goals;
- state ownership and dependency direction;
- deterministic ordering, exactly-once mutation, and failure atomicity;
- persistence/replay and stale-input behavior;
- tests that would fail before the change;
- player-facing clarity and hidden debug/provider leakage;
- file-size or responsibility growth that creates a new hotspot;
- evidence and wording for overclaim.

### 6. Human gate

The milestone defines when the owner must play, view, compare, or decide. Instructions must be concrete and short. Capture the owner's words and any supplied scores exactly; do not infer missing scores. A failed gate keeps the milestone incomplete even when all tests pass.

### 7. Integration

A PR contains one bounded outcome and uses the repository template. Required checks must pass. Merge method should leave a readable integration history; consolidation or noisy implementation branches may be squash-merged when the PR body preserves source identities and evidence.

### 8. Closure

Update current state, risks, decisions, and the milestone only where facts changed. Move a completed, stopped, or superseded plan to the archive. Activate no follow-on feature until the owner accepts the next outcome.

## Decision escalation

Stop and present options when:

- product meaning or player/citizen authority is ambiguous;
- two implementation paths have materially different future costs;
- a required gate conflicts with available tools or evidence;
- resolving a defect requires changing accepted scope;
- provider/network/paid execution would occur;
- data migration or compatibility would be broken;
- a human product gate fails twice;
- the next task is not obvious from the active milestone.

Present at most a few real options, explain consequences in product terms, and give a recommendation. Do not ask the owner to make low-level choices that the agent can resolve from evidence.

## Evidence classes

- **Contract evidence:** unit and property tests for deterministic rules.
- **Integration evidence:** cross-module, Godot-hosted, persistence, replay, and migration tests.
- **Operational evidence:** build, CLI, security, provider, artifact, and recovery results.
- **Performance evidence:** clean-source identity, scenario, hardware/route, distributions, and target classification.
- **Runtime evidence:** observed executable route and captured state.
- **Human evidence:** explicit owner judgment against a named product gate.

Keep these classes separate. One does not imply another.

## Documentation discipline

Canonical documents are rewritten to current truth; they are not chronological logs. Decisions and ADRs are append-only records with explicit status. Evidence is stored with provenance. Completed plans are archived. Agent transcripts and long completion narratives stay out of root authority documents unless they contain unique evidence that is summarized and linked.

## Prohibited shortcuts

- “Continue based on the planning” without identifying the one active milestone and next missing evidence.
- Treating the last agent's recommendation as roadmap authority.
- Calling implemented infrastructure product progress without showing the player/citizen consequence it enables.
- Starting a new branch from an arbitrary stacked head.
- Hiding failed attempts or unsupported claims inside a giant status update.
- Solving a human acceptance problem with more automated diagnostics alone.
- Requiring the owner to continuously supervise source-level execution.
