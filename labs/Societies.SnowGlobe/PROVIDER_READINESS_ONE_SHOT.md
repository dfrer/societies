# Snow Globe provider-readiness one-shot publication contract

## Completed governed evidence boundary

The reviewed invocation and retention surface merged as exact command source
`a43b81a4429072d14bf751ab848dce02ef7a8a38`, rebuilt in Release with 0 warnings/errors, and was
invoked exactly once with exit code 0. The terminal claim remains retained with SHA-256
`48a03f86756e61e41890ded234dd815c8efbf6e0460f93f706aa157beb1b63a8` and
`additional_attempt_authorized=false`. No rerun is authorized.

The versioned command is:

```text
provider-readiness-record-once-v1 --ollama-pid <positive-int32> --ollama-start-utc-ticks <positive-utc-ticks>
```

Those two values are non-secret process-binding inputs and are never printed or retained. The
interface accepts no repository path, output path, credential, endpoint, account, model, provider
selector, proxy, retry, fallback, generation, payment, or routing control. The repository root and
all evidence locations are fixed by production code.

## One deep sequential command

After exact argument validation, the command performs these steps once:

1. Publish and durably read back a canonical `invocation_consumed` claim before constructing either
   provider Adapter. The claim embeds the exact lower-case Git SHA from the command assembly's
   informational version; it does not trust a caller-supplied source claim.
2. Read the fixed cognition comparison through a bounded, no-follow, single-link file handle and
   require the accepted SHA-256
   `b3574d0b4cf94ed25a3c9e152a751dc748d4a4dcdf2fb381e5a3a0c094ddf64c` with
   `accepted_openrouter_default` selection evidence.
3. Invoke the merged OpenRouter readiness Adapter once through the common observation Module.
4. Only after that invocation returns, invoke the merged Ollama readiness Adapter once through the
   same Module using the supplied runtime identity.
5. Revalidate both canonical observations at one assessment time, reject an expired or future
   observation, create the canonical v2 assessment, and revalidate it. The assessment must retain
   primary-attempt state `unknown`, routing issuance `not_issued`, and canonical null routing input.
6. Publish the three exact canonical files together through the fixed atomic-directory boundary.

The command has no retry or fallback loop. Missing credentials, provider unavailability, metadata
rejection, timeout, and cancellation are retained only when the existing Adapter represents them as
a valid raw-free observation. An unexpected construction, validation, or publication failure stops
the sequence. Neither Adapter is invoked a second time.

## Provider boundary inherited unchanged

OpenRouter uses the merged hardened verifier and one owned credential snapshot for exactly three
sequential authenticated `GET` requests on a ready path: current-key metadata, Azure/ZDR-filtered
models, and ZDR endpoints. It issues no POST or completion request. The endpoint, Bearer-auth, and
response contracts remain bound to the official references recorded by the verifier:

- <https://openrouter.ai/docs/api/api-reference/api-keys/get-current-key>
- <https://openrouter.ai/docs/api/api-reference/models/get-models>
- <https://openrouter.ai/docs/api/api-reference/endpoints/list-endpoints-zdr>
- <https://openrouter.ai/docs/guides/routing/provider-selection>

Ollama uses the merged pinned Windows runtime/listener verifier and exactly one loopback HTTP/1.1
`GET http://127.0.0.1:11435/api/tags`, following the official tags schema at
<https://docs.ollama.com/api/tags>. It sends no authorization header and performs no generate, pull,
or update.

The existing Adapters retain redirect-off, proxy-off, cookie-off, decompression-off, exact-method
and effective-URI checks, bounded headers/bodies, strict JSON depth/shape/value validation,
post-body trailer rejection, one-shot closure, timeout/cancellation classification, source identity
binding, raw-buffer clearing, and closed diagnostic codes. Managed framework strings may remain
subject to the limitations already documented in
[the observation contract](PROVIDER_READINESS_OBSERVATION.md); this publication layer makes no
stronger zeroization claim.

## Fixed canonical retention

The retained paths are exactly:

```text
artifacts/snowglobe/provider-readiness/one-shot-consumed-v1.json
artifacts/snowglobe/provider-readiness/evidence-v1/openrouter-observation-v1.json
artifacts/snowglobe/provider-readiness/evidence-v1/ollama-observation-v1.json
artifacts/snowglobe/provider-readiness/evidence-v1/routing-readiness-assessment-v2.json
```

The claim schema is `snow_globe_provider_readiness_one_shot_claim/v1`; its contract schema is
`snow_globe_provider_readiness_one_shot_contract/v1`, and its contract digest is
`7e715b26045ee68d5ad0d44acf4532f4ad3213882b49c859fb952d816731e786`. The contract binds the
command name, exact Adapter/observation/assessment/comparison contracts, fixed paths, sequential
order, pre-Adapter claim, atomic publication policy, raw-data prohibition, and absence of retry,
fallback, generation, payment, routing, or world authority. The claim's source SHA and payload
digest vary only with the exact built source revision and resulting canonical payload.

Historical validation is timeless: it accepts an exact canonical claim containing any lowercase
40-hex retained source commit and returns that retained value after payload-digest and canonical-byte
validation. Starting a new invocation is stricter. Both the command and production `ClaimOnce`
boundary require the retained commit to equal the current command assembly source commit before any
comparison read, Adapter construction, or provider access. A structurally valid historical claim
therefore remains readable but can never be replayed as a new current-build claim.

The Windows store pins and verifies the repository and every existing ancestor, rejects reparse
points, uses no-follow `CreateNew`/open handles, requires regular files with exactly one hard link,
performs durable flush and exact readback, and checks handle/path identities. The evidence files are
written with fixed names into a newly created fixed pending directory. After all three read back and
validate byte-for-byte, a same-volume directory rename exposes the final directory atomically. The
store then verifies the directory identity, each file identity/link count/content, the exact three-
file set, and the original claim again.

The claim is the terminal one-shot tombstone. If the process stops after the claim, a file write is
partial, a rename is ambiguous, a target already exists, or post-publication identity validation
fails, the claim remains and every later invocation is refused before comparison reading or Adapter
construction. Pending or final artifacts are never reused, completed, overwritten, deleted, or
retried by this command.

These checks bind the bytes and file identities at publication time; they are integrity evidence,
not an external authenticity anchor or permanent same-user tamper barrier. A later consumer must
perform canonical validation from its own single snapshot, and repository review/history supplies
the durable source/evidence association. Likewise, the embedded assembly source SHA identifies the
built source revision but is not a signature or remote attestation.

## Retained evidence result

The governed point-in-time cycle produced these canonical facts:

- OpenRouter observation: 1,111 bytes, SHA-256
  `24b9302bf91edd894ed69125f38261c39b4d43633062f3ee3707b664415d9f74`, `complete`/`ready`,
  exactly three authenticated GETs.
- Ollama observation: 1,090 bytes, SHA-256
  `30be811f4f011b9d4a45cf670bc94bf41657f8a702f3b31d3fe17331b5520e0a`, `complete`/`ready`,
  exactly one loopback metadata GET.
- Routing-readiness assessment v2: 5,715 bytes, SHA-256
  `7f150a0043b616db29dd01c660053f6e0a30df385e820634e53e3c672dd0c41a`,
  `insufficient_current_attempt_evidence`, primary state `unknown`, routing `not_issued`, and routing
  input absent.

Both observations were current metadata-ready within the isolated contract at assessment time.
Their 60-second expiry prevents that point-in-time fact from becoming continuous or future
readiness. Canonical validation accepted the claim, both observations, and the assessment. A closed
leakage scan found no credential or secret, account identity, raw metadata or response body,
prompt/reasoning, host path, process identity, or dynamic-error value. The retained
`same_account_bound` value is only a closed binding status and carries no account identifier.

The cycle made no POST, completion, generation, payment, retry, fallback, gameplay, deterministic-
world, quality, deployment, or commercial-readiness claim or action. A routing input cannot be
issued until a separately reviewed durable attempt ledger proves a fresh `not_started` primary state
and atomically claims dispatch.

## Closed output and authority

Success prints one bounded line containing only status, request counts, the three canonical artifact
digests, assessment status, fixed repository-relative paths, `primary_attempt=unknown`,
`routing=not_issued`, `routing_input_present=false`, and
`additional_attempt_authorized=false`. Failure prints only a closed code. Output and retained
evidence exclude credentials, account identity, raw metadata, response bodies, host paths, PID/start
ticks, dynamic errors, provider content, prompts, reasoning, and secrets.

This command records evidence only. It cannot select or dispatch a provider, authorize a paid or
generation request, establish a current primary-attempt state, issue a routing-policy input, mutate a
journal, or change deterministic simulation/world state. See also
[the v2 routing-readiness contract](PROVIDER_ROUTING_READINESS_EVIDENCE.md).
