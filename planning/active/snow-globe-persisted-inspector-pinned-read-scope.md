# Snow Globe persisted-inspector pinned read scope

## Outcome contract

- Outcome: one call to any `SnowGlobePersistedRunInspector` surface validates twice through one bounded set of already-open artifact objects, so a pathname replacement cannot silently substitute the second evidence source.
- Scope: lexical `Path.GetFullPath` canonicalization; root-to-leaf rejection of existing reparse/symbolic-link ancestors before descendant metadata or access, followed by run-directory and direct-entry validation; exact finite v2/v3 or v4/v5 layout capture; one read-only handle per consumed artifact; two offset-zero reads through the unchanged strict RunStore readers; and layout/link-policy revalidation after handle opening and after the second parse.
- Interface: all public method signatures, result types, receipts, schema identities, deterministic reconstruction, and stable rejection strings remain unchanged. The internal `IRunStoreReadFileSystem` seam excludes create, append, and lease operations; the existing writer filesystem inherits it.
- Finite layout: v2/v3 consume only `run.json` and `ledger.jsonl`; v4/v5 also consume `commits.jsonl` and may consume the exact `ledger.0001.jsonl` / `commits.0001.jsonl` pair. `.writer.lock` is allowed and policy-checked but never opened.
- Failure mapping: an initially invalid layout or link is `run_store_invalid`; an initial open/access failure is `run_store_unavailable`; observed races, second-pass parse/read failures, byte drift, layout drift, or link-policy drift are `run_store_unstable`. Identity mismatch and reconstruction outcomes are unchanged.

## Non-goals and compatibility boundaries

- The scope is not a writer lease and gives no proof that a pathname still names an opened object at return. It adds no directory-object binding, native file identity, P/Invoke, hard-link exclusion, protection against link swap-and-restore between checks, same-object content ABA protection, after-return stability, cryptographic authenticity, recovery, mutation, or storage-format change.
- `SnowGlobeRunStore.Read`, `OpenForAppend`, framed recovery, mutable sessions, v4 ephemeral pause, v5 durable pause, recovery-provenance receipts, durable-control receipts, and inert `Inspect(...).Snapshot.IsPaused` semantics remain unchanged.
- There is no power-loss, hardware-durability, cross-host-coordination, exactly-once, deployment, release, provider, cost, or quality claim. No provider, credential, network, live-state, retained-evidence, dependency, gameplay, or `src/societies/` work is in scope.

## Acceptance evidence

- Existing v2-v5 inspection, recovery-provenance, durable-control, pending-frame, continuation, receipt, identity, bounds, and inert-snapshot behavior remains green across all three public surfaces.
- Focused tests prove every consumed artifact is opened once and read twice from offset zero; accepted and rejected paths dispose all handles; byte-identical path replacement cannot switch the second evidence source; in-place header, ledger, and marker mutation fails unstable; layout add/remove and link-policy insertion fail unstable; and initial physical file/directory/ancestor symbolic links reject invalid when the platform permits their creation. A deterministic adapter proves root-to-leaf ordering and no descendant access after an ancestor reports reparse/link policy.
- Exact-property malformed typed v2, v3, v4, and v5 headers return `run_store_invalid` through all three surfaces and dispose their sole opened header handle; `JsonException` cannot escape or bypass cleanup.
- A concurrent v5 `AdvanceAsync`, `PauseAsync`, and `ResumeAsync` succeeds while the inspector holds pinned read handles, proving the scope neither opens `.writer.lock` nor blocks valid appends.
- Final local Release evidence: 46/46 focused inspector tests, 234/234 inspector/persistence compatibility tests, and the full 992/992 Snow Globe suite pass; both the Snow Globe library and test project build with 0 warnings and 0 errors, and `git diff --check` is clean. Independent security/determinism/public-interface re-review is FINAL GO with no P0-P3 findings after closing two P1s in ancestor-validation order and malformed typed-header cleanup. The main task owns Git/PR delivery.
- Linux physical symbolic-link coverage executes normally. Windows physical symbolic-link coverage is conditional on host privilege/filesystem support; this local milestone does not claim a separately verified junction case.

## Delivery boundary

The reviewed implementation is ready for delivery. The main task owns commits, push, pull request, required-check monitoring, merge, and the final clean synchronized `master` continuation record.
