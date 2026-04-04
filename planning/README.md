# Planning

## What Is This?

The `planning/` tree contains design documents, session outputs, research, and spreadsheets for the Societies project. These are **aspirational** — they describe systems we want to build, not systems that are currently implemented.

## Planning vs. Code

**Planning documents are not authoritative.** If a planning document conflicts with the code, the code wins. The current authoritative implementation lives in `src/societies/` — see `CURRENT_BUILD.md` at repo root for what actually exists today.

Many planning documents (especially Sessions 1–7) describe systems that are not yet built: multiplayer networking, GOAP/Utility AI, voxel terrain, markets, governance, event sourcing, PostgreSQL persistence. The current prototype is a **local deterministic settlement simulation** with 3–18 AI agents, heightfield terrain, JSON persistence, and no networking.

> **Staleness notice:** Some planning docs describe systems that don't exist in code yet. Others reference files that were renamed or moved during reorganizations. Read with caution. When in doubt, check the code first.

## How to Navigate

1. **Start here:** `CURRENT_BUILD.md` — what is actually implemented right now
2. **Then:** `planning/sessions/[AGENTS-READ-FIRST]-index.md` — full planning session index
3. **For constants:** `planning/meta/technical-constants.md` — aspirational numerical reference (not yet a shared C# class)
4. **For research:** `planning/research/[MASTER-RESEARCH-INDEX].md` — game analysis and technical research
5. **For reference:** `planning/meta/[META-INDEX].md` — cross-cutting project documentation

## Directory Structure

```
planning/
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

*Last updated: 2026-04-04*
