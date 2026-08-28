# Planning Agent Contract

These rules apply under `planning/` in addition to the root `AGENTS.md`.

## Planning is the owner's interface

Use planning to help the owner reason critically about product meaning, tradeoffs, sequencing, and acceptance. Translate accepted outcomes into technical constraints. Do not force the owner to supervise source-level work or choose implementation details agents can resolve.

## One active plan

Only `planning/active/MILESTONE.md` authorizes work. `planning/active/README.md` is an index. `planning/active/evidence/` is retained for compatibility and proves bounded facts; it does not activate tasks.

Before replacing the active milestone:

1. record whether it completed, stopped, or was superseded;
2. update current state, decisions, and risks where evidence changed;
3. move the old milestone into a dated archive;
4. obtain explicit owner acceptance of the next product outcome;
5. create the new milestone with scope, non-goals, evidence, human gate, and stop conditions.

## Historical material

Do not edit archived plans to make them current. Do not treat old dates, unchecked boxes, “next action” sections, standing authorization, or branch names as present authority. Historical material may inform a new decision only after reconciliation with source and current evidence.

## Planning quality

- Distinguish decisions, recommendations, assumptions, candidates, and unresolved questions.
- Use product language first and technical language where needed.
- Recommend a path; do not merely present an unranked backlog.
- Kill or defer work that does not produce the next meaningful proof.
- State what would falsify a plan.
- Keep canonical plans concise; move execution logs and raw evidence elsewhere.
- Never mark a human gate passed without the owner's explicit judgment.
