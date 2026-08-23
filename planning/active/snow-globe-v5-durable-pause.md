# Snow Globe v5 durable pause

## Outcome contract

- Outcome: closing and reopening a `SnowGlobePersistedSession` over a v5 run restores the last successfully committed pause state.
- Owned slice: isolated Snow Globe RunStore/session persistence, strict reconstruction, framed recovery, focused compatibility tests, and contract/status documentation.
- Non-goals: no v4 rewrite or upgrade, second state file, public historical-pause inspector, extra recovery segment/count, world event or digest change, `ObserverShell` change, provider/network/credential/live-state work, `src/societies/` change, or power-loss/exactly-once claim.
- Value gate: a local observer pause is no longer silently lost across an ordinary v5 session reopen, while deterministic world authority and frozen v4 behavior remain unchanged.
- Delivery boundary: local implementation, full Release validation, and independent review are complete. Git delivery, CI, PR, and merge remain parent-owned gates.

## Version and wire contract

- Current creation schema is `snow_globe_run_store/v5`. `PreviousSchemaVersion` remains the public v3 compatibility identity, and `V4SchemaVersion` explicitly names frozen framed v4.
- V2/v3 are strict flat read-only inputs. V4/v5 use the existing bounded prepare/payload/commit/continuation framing. Public `CreateNew` accepts v5 only; `OpenForAppend` accepts v4 and v5.
- `SnowGlobeLedgerKind` has explicit stable wire values `Response=0` through `ParticipantEvaluation=5`; v5 adds `PauseTransition=6`.
- A pause frame contains exactly one checksum/header-bound record at the current tick with empty agent, action exactly `Pause` or `Resume`, quantity zero, null disposition/structure fields, and the current state/event digests. The frame has `entry_count=1` and an empty prefix manifest.
- Pause transitions do not mutate the deterministic world, events, revision, state digest, or event digest. Reconstruction starts running, requires alternating non-redundant transitions at ledger/frame boundaries, and binds each transition to the exact current tick and digests.

## Session and compatibility contract

- Public `CreateNew` and `Reopen` expose exact three- and four-argument overloads; the pause Boolean is never optional.
- Three-argument v5 creation starts running. Four-argument creation with `true` commits the initial Pause frame before returning.
- Three-argument reopen derives v5 pause from committed evidence. For v4 it preserves the historical omitted default of running. The four-argument reopen overload is v4-only; v5 rejects after read-only schema preflight and before writer ownership or artifact mutation.
- V4 Pause/Resume remains session-ephemeral. A v5 state-changing Pause/Resume appends, strictly rereads/reconstructs, and only then publishes the new state. An already-target transition returns applied without consuming bytes or capacity.
- New v5 participant evaluations reconstruct and append only while durably paused. Existing idempotent receipts remain readable and retryable after Resume without appending again. V4 participant behavior is unchanged.
- Capacity/busy failures retain state and artifacts without poisoning. Any uncertain low-level pause append poisons the live session and returns no snapshot; a clean reopen follows strict durable evidence.

## Recovery and inspection contract

- A well-framed pending v5 PauseTransition consumes the same single continuation budget as scheduled recovery and is always abandoned at the prior durable pause state. A complete uncommitted payload is fully validated before abandonment.
- Before-prepare interruption leaves no pending recovery. Partial/noncanonical pause payload or commit tails fail closed. A fully committed frame is restored directly without a continuation, even when the writer observed an injected post-write error.
- V5 scheduled recovery preserves the prior durable pause state. A second pending recovery after either pause-first or scheduled-first continuation fails closed; `MaximumRecoveryCount=1` and two total segments remain unchanged.
- `Inspect(...).Snapshot.IsPaused` remains `true` for every accepted schema because inspection is inert; it is not a historical pause claim. `InspectRecoveryProvenance` remains v4-only and returns a null receipt for accepted v5 evidence.

## Implementation and evidence state

- Production files: `RunStore.cs`, `RunStoreStorage.cs`, `RunStoreExperiment.cs`, `PersistedSession.cs`, and minimal v4-only receipt routing in `PersistedRunInspector.cs`.
- Focused v5 evidence: `RunStoreV5PauseTests.cs` and `PersistedSessionV5PauseTests.cs` cover exact wire/API shape, empty/initial pause, pause/resume/reopen, no-op bytes, participant gating/idempotency, paused Step plus running Advance, capacity/busy/uncertainty, inner/outer corruption, all pause write interruption boundaries, scheduled recovery under both states, shared recovery order, inert inspection, and exact artifact bounds.
- Frozen compatibility evidence explicitly creates v4 fixtures in the existing RunStore, session recovery, and inspector tests rather than accidentally relabeling them v5.
- Current local evidence: 25/25 new v5 tests, 186/186 persistence compatibility tests, the 239/239 aggregate persistence/provider-enum selection, and the full 969/969 Snow Globe suite pass in Release; the Snow Globe library builds with zero warnings/errors and `git diff --check` is clean. Independent migration/determinism/public-interface review is FINAL GO with no P0-P3 findings. CI, commit, PR, and merge are not yet claimed.

## Risks and continuation

- The internal v4 fixture seam is compatibility-test-only; public creation remains v5-only.
- Recovery evidence proves the existing deterministic record-boundary model. It does not certify power-loss atomicity, hardware durability, cross-host coordination, or exactly-once behavior.
- Next action: commit and publish the reviewed slice through the required PR check, merge it, and record the exact delivery boundary.
