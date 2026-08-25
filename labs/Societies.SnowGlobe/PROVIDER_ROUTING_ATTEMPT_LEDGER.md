# Snow Globe provider-routing attempt ledger contract

## Bounded outcome

This isolated Module creates authenticated evidence that one fresh provider-neutral routing attempt
was initially `not_started`, then permits exactly one durable claim of its first dispatch. It closes
the persistence and race gap needed by a future routing composition without selecting or invoking a
provider. The Module has no provider transport, credential, endpoint, request body, payment,
generation, retry, fallback, gameplay, or deterministic-world authority.

No default or real retained root exists in this interface. The Windows file Adapter is internal and
requires a caller-injected, already existing, verified absolute root plus a provider-neutral
integrity-anchor Adapter. This milestone exercises it only in isolated test directories.

## Deep Module interface

`ProviderRoutingAttemptLedgerModule` has an internal constructor over the storage, integrity-anchor,
and opaque-ID seams. Its public instance interface contains only:

- `Create`: snapshot and validate one canonical current-readiness assessment, create one fresh
  attempt, and durably retain its authenticated `not_started` record;
- `Inspect`: resolve one exact attempt through authenticated restart/recovery rules and return only
  its detached canonical current record; missing or poisoned evidence is a closed failure and never
  becomes an inferred `not_started` fact;
- `ClaimDispatch`: snapshot and validate one routing decision, require the exact current-record
  digest and closed selected provider, durably retain a claim tombstone, then retain and read back
  `dispatch_started` before returning success;
- `Validate`: authenticate and canonically validate detached retained record bytes.

Create binds the exact accepted comparison SHA-256
`b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c`, the validated
`snow_globe_provider_routing_readiness_assessment/v2` digest and expiry, the closed routing intent,
its exact closed OpenRouter and Ollama readiness projections, creation time, and a fresh opaque
lowercase 64-hex attempt identifier. Claim binds that identifier, the exact expected record digest,
`openrouter` or `ollama`, the validated canonical routing-decision digest, and claim time. It also
requires the decision's two readiness codes to equal the authenticated assessment projections;
unknown, asymmetric, or swapped facts cannot be relabeled for dispatch. Caller-owned canonical
memory is bounded before access, copied once, used only through that owned snapshot, and zeroed
afterward.

## Versioned authenticated evidence

- record schema: `snow_globe_provider_routing_attempt_record/v2`;
- internal claim-tombstone schema:
  `snow_globe_provider_routing_dispatch_claim_tombstone/v1`;
- contract schema: `snow_globe_provider_routing_attempt_ledger_contract/v2`;
- contract SHA-256:
  `99694dd77536b92b537d3f95417138b35982d7304c9c20f10f48da5c9d5c2e47`;
- maximum canonical record: 8 KiB, JSON depth 5;
- maximum canonical claim tombstone: 4 KiB, JSON depth 4.

Each record contains only fixed schema/contract identities, state and chain fields, times, evidence
digests, the exact closed `ready`/`not_ready`/`unknown` readiness pair, intent, an optional closed
provider code, the optional routing-decision digest, the injected integrity-anchor identity digest,
fixed limitation codes, a payload digest, and an authenticator. Terminal records preserve the exact
readiness pair from the initial authenticated record.
The provider-neutral anchor authenticates the exact canonical payload and is required again after
restart. Canonical validation rejects duplicate properties, unknown values, noncanonical ordering or
encoding, malformed/truncated/deep/oversized input, payload or authenticator tampering, an incorrect
anchor, invalid time domains, and impossible state bindings.

The anchor is external to the ledger root. It protects retained records against undetected mutation
when the same trusted anchor is supplied, but it is not a remote signature, hardware attestation,
or whole-volume rollback defense. Anchor provisioning, persistence, replacement, and recovery are
outside this milestone.

## State and append-only transition

```text
create succeeds
    -> record-00000000.json: not_started

claim begins
    -> dispatch-claim-tombstone.json: durable expected-record/provider/decision binding

claim completes and readback validates
    -> record-00000001-dispatch-started.json: dispatch_started

claim persistence becomes partial or ambiguous
    -> record-00000001-submission-unknown.json: submission_unknown
       or the attempt is poisoned when the tombstone itself cannot be authenticated
```

There is no transition out of `dispatch_started`, `submission_unknown`, or poisoned state. There is
no deletion, overwrite, rollback, repair, retry, second claim, provider substitution, or inference
that an attempt is `not_started` merely because a later record is absent. `not_started` is returned
only after the exact initial record validates and the exact claim, dispatch, and unknown paths are
verified absent while the attempt directory is pinned.

The claim tombstone is the durable terminal guard. It is created and flushed before the success
record. A successful claim returns only after the success record is flushed, read back byte-for-byte,
authenticated, and rebound to the initial record and tombstone. The internal storage claim-failure
contract distinguishes `definitely_pre_tombstone` from
`terminal_material_created_or_unknown`. A definite pre-tombstone lease/storage failure reports the
closed non-ambiguous storage code and remains inspectable as `not_started`; it does not falsely claim
terminal state. Once tombstone material may exist, outward ambiguity is terminal: recovery either
accepts an already valid `dispatch_started` record, appends authenticated `submission_unknown`, or
reports poisoned evidence. It never exposes the attempt as `not_started` again.

Every internal storage Adapter must classify every unexpected `ClaimOnce` failure using that exact
exposure contract, including identity, read, validation, allocation, lease, write, flush, readback,
and disposal failures. Known missing, terminal, expected-digest mismatch, and poisoned outcomes stay
closed ledger codes. An Adapter that violates the seam contract with an unclassified exception is
halted as generic `attempt_ledger_failed`; the Module never upgrades that violation into terminal
ambiguity evidence.

## Storage Adapters

The in-memory and Windows file Adapters implement the same internal storage seam and are exercised
through the same Module. The Windows Adapter:

- accepts only an existing absolute root and exposes no root discovery or default factory;
- holds one exclusive writer lease for its lifetime;
- pins root, attempts, and per-attempt directory identities;
- rejects reparse points, hard-linked files, identity changes, traversal-shaped attempt IDs, and a
  second writer;
- uses fixed child names, CreateNew/no-overwrite writes, write-through streams, synchronous durable
  flush, exact bounded readback, and authenticated canonical validation;
- resolves attempts by exact opaque identifier without treating directory enumeration as authority;
- preserves initial, tombstone, success, and unknown records as append-only evidence across restart.

The bounded fault seam covers definite identity/read preparation failure, lease loss immediately
before tombstone creation, and failure while appending the recovery-unknown record. It exists only
for deterministic offline tests. A failed recovery write is poisoned for that inspection; a later
healthy restart may authenticate the tombstone and append the same deterministic
`submission_unknown` record.

File integrity is cooperative against other writers that respect the lease. Same-user or
administrator deletion, replacement, and whole-volume rollback remain outside what local files can
prevent; authenticated chain and identity validation make detected interference fail closed.

## Raw-free and authority limits

Canonical records never retain credentials, account identity, provider metadata, prompt, proposal,
reasoning, request or response body, endpoint, host path, PID, dynamic exception, secret, price, or
model output. The only provider values are the closed codes `openrouter` and `ollama`.

`dispatch_started` is durable evidence that a future caller claimed the pre-transport gate. It is
not an execution permit, transport object, credential capability, provider request, routing-policy
input, payment authorization, retry/fallback authority, or simulation command. A later separately
reviewed orchestration Module must bind fresh readiness, the routing policy, this ledger, and the
actual pre-byte transport boundary before any real dispatch is allowed.
