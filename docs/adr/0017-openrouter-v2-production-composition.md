# ADR 0017: OpenRouter v2 production composition

- Status: Offline composition and one-time anchor initialization complete
- Date: 2026-08-22
- Scope: Snow Globe lab only; no live/provider action

## Decision

Compose the v2 store with a fixed read-only 32-byte domain-separated HMAC anchor source at a distinct Windows Credential Manager target. The source is open-existing only and cannot provision, replace, delete, rotate, import, or scan. Disposable leases and owned buffers are cleaned. Anchor validation precedes root, capability, credential, metadata, and exchange access.

Use exactly `%LOCALAPPDATA%\\Societies\\SnowGlobe\\OpenRouterPremiumOneShot\\v2`; never observe or fall back to v1. Expose exactly three v2 state operations. Keep credential administration separate so it does not open the state anchor or v2 root. Validate exact digest/O(1) lookup, reopen the frozen journal after the durable execution claim, and publish canonical restart readback plus authenticated final journal/evidence binding. Post-claim indeterminate state is permanent and never retried. Ancestor `GENERIC_READ`/`FileShare.Read` and fixed-directory leases block mutation through junction/reparse/path-swap windows; ordinary pin behavior remains unchanged.

## Evidence and boundary

CLI `plan` remains pre-factory zero-I/O and reports `trust_anchor_status=not_inspected`, `production_factory_constructed=false`, `additional_attempt_authorized=false`, and `live_readiness=false`. Manifest v2 digest is `eea26a92d318a2ba102c7979d0cb44563d8bef967ae00b627bc6263ff59d759d`; historical Attempt 4 v1 digest is `d1e653468fd7a39e33ad355297adf96c48bde97e40a73f6b6c6812623553f737`; state-contract digest is `f9d12a2c0bcfb60cc874dc49bc60462197700d681a42ae98ce4bcefd28ac8511`.

The reviewed composition baseline passed 55/55 focused, 868/868 full SnowGlobe Release, and 67/67 CLI/security with clean Release builds. The later provisioning implementation gate passed 881/881 full SnowGlobe and 70/70 full CLI/security. A dedicated real Global mutex abandonment regression in `OpenRouterPremiumWindowsStateTrustAnchorTests` proves fail-closed `state_trust_anchor_provisioning_indeterminate`, releases recovered ownership, and permits later reacquisition. Current validation after this test-only change is focused anchor 20/20, full SnowGlobe Release 884/884, full CLI/security 70/70, and both lab and CLI Release builds 0 warnings/0 errors. Independent paid-evidence review remains GO with no P0-P2; the separate plan-conformance residual P3 remains. The exact provisioning command was invoked once and exited 0, initializing the fixed v2 root. The later fifth paid run is documented in ADR 0015; this composition review claims no additional provider behavior. Local files and the anchor do not claim owning-user/admin or whole-volume rollback resistance; rotation and recovery remain absent.

Attempt 4 remains consumed by the sole `preflight_already_attempted` before credentials/provider/charge; no retry occurred and v1 is untouched. The provisioning authority is separately consumed; `WI-GLOBAL-2026-126` is separate.

The fifth paid-run authority and all stages are consumed and recorded in ADR 0015; no stage may be rerun. The reviewed milestone was committed as `dfbeb81` and published through PR #125; GitHub `build-test-smoke` passed. Merge completes this Git delivery without authorizing provider, accounting, credential, or live-state action.
