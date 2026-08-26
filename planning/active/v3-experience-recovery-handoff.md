# New-Session Handoff: Continue Societies with ER-01

Use the prompt below in a new Codex session.

---

Continue overall Societies development with the next bounded milestone:
**ER-01 First Believable Settlement Loop**.

Repository: `E:\AIExperiments\games\societies`

Important starting truth:

- Live `origin/master` was `7b9e588e695dccac971f80d312fb1c133bfb388d` when this handoff was written.
- W3-05 engineering delivery is merged and automated checks were green, but the user-led play
  assessment on 2026-08-26 failed substantially.
- The experience was judged exceptionally basic/non-functional relative to the goal, on rails,
  visually repetitive, and unfinished in its HUD, interactions, and world objects.
- Treat that as authoritative product evidence. Do not reinterpret it as a minor polish request.
- W3-06+, the former Week 4 feature path, live LLM/provider work, Snow Globe integration, broad
  governance, markets, multiplayer, and voxel-terrain replacement remain inactive.
- Do not use Computer Use or take control of the user's desktop. User acceptance will be performed
  manually by the user after the build is ready.

Safety and repository handling:

- Read root `AGENTS.md`, `CURRENT_BUILD.md`,
  `planning/active/v3-experience-recovery-plan.md`, and
  `planning/active/v3-weeks-3-4-development-plan.md` first.
- Refresh Git/GitHub truth before making current claims.
- The primary checkout is dirty with unrelated user work on `feature/v3-w2-vis-baseline`; do not edit,
  clean, stage, reset, or commit anything there.
- Work in a fresh clean isolated worktree from the latest appropriate base. If the local planning branch
  `codex/experience-trajectory-planning` is not yet on `origin/master`, preserve it and base the
  implementation on its planning commit or reconcile it deliberately without overwriting other work.
- Preserve all unrelated, untracked, concurrent, Snow Globe, provider, and lab work.

Outcome contract:

- Build one coherent 8-10 minute player path: orientation -> harvest -> contribute -> deliberate
  two-policy choice -> readable citizen interest/response -> visible shared consequence.
- Restructure the player-facing HUD around one primary need/decision, one contextual interaction,
  concise action feedback, and an optional diagnostic layer.
- Make focused resources and the central depot feel interactive through clear affordance and distinct
  focus/success/rejection/depletion states derived from authoritative results.
- Supply exactly two curated deterministic starting profiles using the existing catalog/seed seam.
  They must visibly differ in resource approach, immediate pressure, and an environmental/world cue.
- Keep `PrototypeRuntimeSession` and the existing validated command/event paths as the sole authority.
  `GameManager` remains the intent-to-command seam; player, HUD, resource, and scene code are
  presentation/input Adapters only. Do not duplicate simulation state in the UI.
- Number keys may remain shortcuts, but the civic tradeoff must have an understandable player-facing
  choice surface and cannot depend on a developer key list.

Acceptance:

- Add focused coverage for both profiles, replay, HUD hierarchy, interaction states, and preservation
  of harvest/contribution/civic command ownership.
- Run proportional focused checks, then the authoritative local validation wrapper and warning-free
  Release/ExportRelease builds. Report performance safety honestly.
- Do not claim visual/play acceptance from headless tests, screenshots, or automated input.
- Prepare a short manual acceptance route for the user to run in both profiles. The user must be able
  to understand the goal, interactions, policy tradeoff, citizen reason, consequence, and meaningful
  contrast without a facilitator or developer key list.
- Allow one narrow ER-01 refinement if the first assessment exposes clarity defects. If it still fails
  substantially, stop instead of adding more systems.

Delivery:

- Keep the slice tightly bounded to ER-01 and use one implementation owner if substantial work is
  delegated, with no overlapping ownership.
- Update `CURRENT_BUILD.md`, the ER-01 plan/evidence, and `WORKFLOW.md` at the milestone boundary.
- Distinguish local green checks, CI/delivery, and user-led acceptance. Do not claim the milestone
  complete until the user-led product gate passes or is explicitly recorded as failed/blocked.
- At handoff, state what changed, validation results, exact repository/commit/PR state, remaining
  uncertainty, and one practical next action.

Do not broaden scope to make the prototype look busy. The purpose is to prove that one existing
deterministic society loop can feel intentional, embodied, variable, and worth continuing.

---
