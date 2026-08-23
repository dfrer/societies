# ADR 0011: Offline Pinned Ollama Recording Fixture

## Decision

Commit `8f95875` adds the registry-closed `OfflinePinnedOllamaRecordingFixture` as a testable local-lane recording Adapter. The fixture is entirely in memory and copies exactly twelve caller-supplied response buffers. Frozen qwen3.5:4b metadata is bound through the Adapter contract digest and execution provenance: canonical endpoint, runtime identity/hash, benchmark adapter/prompt/contract/evidence identities, artifact metadata, context 4096, and output limit 96.

## Conformance and limits

The candidate-neutral harness proves its exact eleven ordered checks and full offline result; it does not claim fixture-specific slot indexing, owned-buffer cleanup, or timeout coverage. `OfflinePinnedOllamaRecordingFixtureTests` prove the exact count/index is read once, fixture-owned response/request buffers are zeroed, caller buffers are preserved, concurrent capability safety, and pre/mid cancellation through the deterministic internal seam. Generic recording-session timeout semantics remain predecessor evidence, not pinned-fixture timeout evidence. There is no path, PID, file, environment, socket, process, provider, model, credential, payment, or live authority. The fixture does not attest transport delivery, model execution, or quality.

Focused conformance plus fixture validation is 12/12; full Release is 445/445; build is 0 warnings/errors; benchmark CLI is 56/56 before the final narrow correction; deep review is CODE GO. Fixture, harness, and test hashes are `B308723D8253222458BAD80A6B14041178CDED8829DC6C1B276F156EF91FA0B3`, `4AF2C88F5199ACAD1E9CA2EB78DB9877E145C1DDEA239797DE09D0085F7E7C3A`, and `C96C92B01294C37444612C859D2B3D0F562E9DE1867318BE4F6809A4F7D1D0EC`.

## Status and next action

The fixture action is historical at `8f95875`. Exactly one current action remains: design and implement an entirely **OFFLINE** bounded Ollama recording request/response codec plus fake transport port against this fixture, with no sockets, server/model calls, credentials, or live authority. Actual loopback transport requires separate authorization and security review.
