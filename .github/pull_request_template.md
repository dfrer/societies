# Outcome

- **Product or architecture result:**
- **Active milestone/task ID:**
- **Value gate cleared:**

# Authority and baseline

- **Base branch and exact SHA:**
- **Accepted behavior preserved:**
- **Nearest scoped `AGENTS.md` read:**

# Scope

- **Owned files/modules:**
- **Non-goals:**
- **Adjacent work explicitly not authorized:**

# What changed

- _Describe the bounded change._

# Evidence

| Evidence class | Command/artifact/observation | Result |
|---|---|---|
| Governance/static | | |
| Focused tests | | |
| Integrated/full tests | | |
| Build/runtime | | |
| Persistence/replay/migration | | |
| Performance/security/provider | | |
| Human gate | | |

# Human acceptance

- **Required for this PR:** yes / no
- **Exact owner verdict or reason not required:**
- **Scores supplied by owner (do not infer):**

# Risks and honesty

- **Still red or unverified:**
- **Assumptions/environment limits:**
- **New debt introduced:**

# Repository state

- **Final commit:**
- **Working tree:**
- **Diff reviewed independently:** yes / no
- **Current-state/risk/decision docs updated where facts changed:** yes / no / not applicable

# Follow-on authority

- **Does merging this PR authorize the next feature?** no, unless an owner-accepted milestone explicitly says otherwise.
- **Next owner decision, if any:**

## Required checks

- [ ] The diff is bounded to the stated outcome.
- [ ] Tests would catch the relevant pre-change failure or another evidence class is explained.
- [ ] Deterministic authority, persistence, replay, and fallback remain intact where applicable.
- [ ] No provider, credential, billing, or debug internals leak into product presentation.
- [ ] Automated evidence is not presented as human acceptance.
- [ ] Failures and unrun gates are reported.
- [ ] `python scripts/check-project-governance.py` passes.
