# ADR 0016: Authenticated append-only OpenRouter state-generation store

- Status: Accepted offline milestone; production composition and one-time anchor initialization complete
- Date: 2026-08-22
- Scope: `labs/Societies.SnowGlobe` only; no `src/societies/`, credential, provider, or live-root wiring

## Decision

Use `OpenRouterPremiumStateGenerationStore` as the offline v2 state-root contract. Each generation is created cooperatively with `CreateNew`/create-new semantics and authenticated canonical envelopes. Authorization resolution is an exact O(1) lookup; restart requires the same caller-supplied external trust anchor. Claims are written before capability opening, and evidence plus validation receipts are canonical and store-generated. The FileJournal receives narrow identity and publication-freeze hardening and remains append-only and fail closed.

The store defends its bounded local surface against corruption, path escape, reparse points, hardlinks, profile mismatch, concurrent writers, and generation/anchor confusion. It does not claim immutable or permanent tamper resistance: local files alone cannot prevent an owning user/admin from rewriting state or a whole-volume rollback. The trust-anchor implementation and key persistence are intentionally external to v2.

## Evidence

The store-only implementation was followed by an offline composition using a fixed read-only 32-byte domain-separated HMAC anchor source at a distinct Credential Manager target. It is open-existing only, with no provision/replace/delete/rotate/import/scan; disposable leases and owned buffers are cleaned; anchor validation precedes root/capability/credential/metadata/exchange; fixed v2 LocalAppData containment never falls back to v1; exactly three v2 state operations are exposed; credential administration remains separate from state-anchor/root access; exact digest/O(1) validation, frozen-journal reopen after durable claim, canonical restart/final bindings, permanent post-claim indeterminate/no-retry, and mutation-blocking ancestor/fixed-directory leases are enforced.

The reviewed composition baseline passed 55/55 focused, 868/868 full SnowGlobe Release, and 67/67 CLI/security with clean Release builds. The later provisioning implementation gate passed 881/881 full SnowGlobe and 70/70 full CLI/security. A dedicated real Global mutex abandonment regression in `OpenRouterPremiumWindowsStateTrustAnchorTests` proves fail-closed `state_trust_anchor_provisioning_indeterminate`, releases recovered ownership, and permits later reacquisition. The later lease-zero conformance follow-up closes the former residual P3 with focused 10/10, full SnowGlobe Release 885/885, full CLI/security 70/70, clean Release builds, and independent deep security GO with no P0-P3. The exact provisioning command then ran once and exited 0, initializing the fixed v2 root; the secret remains in Windows Credential Manager and was not displayed. Local files and the anchor do not claim owning-user/admin or whole-volume rollback resistance; rotation and recovery remain absent.

## Boundary and current next action

Attempt 4 remains historical and terminal: exactly one `preflight_already_attempted` before credentials/provider/charge, authority consumed and no retry; v1 is untouched. The later fifth run is historical. The sixth run created exactly one durable generation/execution/validation claim and stopped after one exchange as `provider_response_rejected_response_finish_invalid`; its raw-free bindings and limitations are recorded in ADR 0015 and the focused contract. Every sixth-run stage and authority are consumed, with no rerun or additional provider action authorized. The reviewed store milestone was merged through PR #125 as `f021cdc`; the fake-only lease-zero follow-up is commit `399adac` in PR #126.
