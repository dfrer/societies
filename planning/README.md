# Planning

## Active Plan

- [Societies V3: Two-Week Development Plan](active/v3-two-week-development-plan.md) — July 13–24, 2026
- **Draft/Conditional:** [Societies V3: Weeks 3-4 Development Plan](active/v3-weeks-3-4-development-plan.md) - July 27-August 7, 2026; it activates only if W2-06 concludes **Continue V3**.


Short-horizon plans under `planning/active/` are grounded in the current build and take priority over older aspirational documents during their execution window. The code and `CURRENT_BUILD.md` remain authoritative for what is implemented.
The product-level intent and deterministic/LLM boundary are maintained in [PRODUCT-THESIS.md](PRODUCT-THESIS.md). It is a north star, not evidence of implemented systems.

## What Is This?

The `planning/` tree contains design documents, session outputs, research, and spreadsheets for the Societies project. These are **aspirational** — they describe systems we want to build, not systems that are currently implemented.

## Planning vs. Code

**Planning documents are not authoritative.** If a planning document conflicts with the code, the code wins. The current authoritative implementation lives in `src/societies/` — see `CURRENT_BUILD.md` at repo root for what actually exists today.

Many planning documents (especially Sessions 1–7) describe systems that are not yet built: multiplayer networking, GOAP/Utility AI, voxel terrain, markets, governance, event sourcing, PostgreSQL persistence. The current prototype is a **local deterministic settlement simulation** with 12–18 citizens in shipped scenarios (plus smaller test fixtures and a 24-citizen stress override), heightfield terrain, JSON persistence, and no networking.

> **Staleness notice:** Some planning docs describe systems that don't exist in code yet. Others reference files that were renamed or moved during reorganizations. Read with caution. When in doubt, check the code first.

## How to Navigate

1. **Start here:** `CURRENT_BUILD.md` — what is actually implemented right now
2. **For current execution:** `planning/active/README.md` — active short-horizon development plans
3. **Then:** `planning/sessions/[AGENTS-READ-FIRST]-index.md` — full planning session index
4. **For constants:** `planning/meta/technical-constants.md` — aspirational numerical reference (not yet a shared C# class)
5. **For research:** `planning/research/[MASTER-RESEARCH-INDEX].md` — game analysis and technical research
6. **For reference:** `planning/meta/[META-INDEX].md` — cross-cutting project documentation

## Directory Structure

```
planning/
├── active/            # Current short-horizon execution plans
├── sessions/          # Core planning sessions 1–7
├── research/          # Game analysis, technical research, reference PDFs
├── meta/              # Cross-cutting reference docs and constants
│   └── navigation/    # Meta-docs about the planning structure itself
├── spreadsheets/      # Data tables (CSV)
├── archives/          # Superseded/legacy content
└── week1-deep-planning/  # Legacy (being phased into sessions/)
```

## Convention Notes

- Files in `[BRACKETS]` are navigation hubs — read these first
- `README.md` files within subdirectories explain their contents
- Session documents use zero-padded numbering: `01-architecture-overview.md`
- Research files use `r##-` prefixes: `r1-eco-performance-research.md`
- Archive reorganization trail is in `planning/meta/navigation/reorg-history/`

*Last updated: 2026-07-09*
