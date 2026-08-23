# Offline Pinned Ollama Recording Fixture

## Current evidence

Commit `8f95875` adds `OfflinePinnedOllamaRecordingFixture`, a registry-closed, entirely in-memory recording Adapter fixture. It copies exactly twelve caller-supplied response buffers and binds the frozen qwen3.5:4b cell pins: canonical endpoint, runtime identity/hash, benchmark adapter/prompt/contract/evidence identities, artifact metadata, context window 4096, and output limit 96. Contract digest and provenance binding make those identities explicit, while candidate-neutral conformance keeps the harness independent of a provider implementation.

The fixture tests prove each exact slot/index is read once and twelve ordered one-shot calls occur with no retry, fallback, alternate, or thirteenth call. They also prove concurrent capability safety, pre-cancel and midflight cancellation through the deterministic internal seam, disposal closure, fixture-owned request/response zeroing, and caller preservation. Generic recording-session timeout semantics remain predecessor evidence, not pinned-fixture timeout evidence. The fixture has no path, PID, file, environment, socket, process, provider, model, credential, payment, or live authority. It does not attest transport delivery or model execution and makes no quality claim.

Validation is focused conformance plus fixture 12/12, full Snow Globe Release 445/445, build 0 warnings/errors, and benchmark CLI 56/56 before the final narrow correction; independent deep review is CODE GO. Fixture hash is `B308723D8253222458BAD80A6B14041178CDED8829DC6C1B276F156EF91FA0B3`; harness hash `4AF2C88F5199ACAD1E9CA2EB78DB9877E145C1DDEA239797DE09D0085F7E7C3A`; tests hash `C96C92B01294C37444612C859D2B3D0F562E9DE1867318BE4F6809A4F7D1D0EC`.

## Boundary

This fixture is historical evidence, not an Ollama client. The sole current action is an entirely offline bounded Ollama recording request/response codec plus fake transport port against this fixture. It must use no sockets, server/model calls, credentials, or live authority. Actual loopback transport remains separately authorized and security-reviewed.
