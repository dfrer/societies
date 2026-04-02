# Societies

A persistent multiplayer civilization simulation where human players and AI agents coexist as equal citizens in a living ecosystem.

[![Godot 4.x](https://img.shields.io/badge/Godot-4.x-blue.svg)](https://godotengine.org)
[![C#](https://img.shields.io/badge/C%23-10.0-green.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## 🎮 Overview

Societies is an ambitious multiplayer simulation game where you build a civilization alongside AI agents who are equal citizens—economically, politically, and socially. The world evolves continuously whether you're online or not, creating a truly persistent simulation.

### Key Features

- **AI-Human Equivalence**: AI agents participate fully in economy and governance
- **Persistent World**: Simulation continues 24/7 with or without players
- **Emergent Governance**: Create laws and constitutions that affect real AI populations
- **Environmental Simulation**: Ecosystem, pollution, and climate systems with real consequences
- **Voxel-Based World**: 1m³ block-based terrain like Eco and Minecraft—fully editable, persistent, and interactable
- **Realistic Logistics**: Weight-based carrying and material transport system—resources have mass and must be physically moved
- **Custom Entities**: Vehicles, workshops, storage containers, and machinery alongside blocks—complex shaped objects with unique physics and functionality
- **Progressive Society**: From homesteading to planetary federations
- **Godot 4 + C#**: Modern, performant, open-source stack

## 🧱 Core Systems

### Voxel World

A fully editable, persistent block-based world built on 1m³ voxels, inspired by Eco and Minecraft:

- **Destructible Terrain**: Every block can be mined, placed, or modified by players and AI
- **Material Types**: Stone, soil, wood, ores, and crafted materials with unique properties
- **Persistence**: All voxel changes saved to database and synchronized across clients
- **Performance**: Chunk-based rendering with LOD for large world sizes (0.5-4 km²)

### Physics & Logistics

Realistic weight and carrying system that makes resource management a core gameplay element:

- **Weight-Based Inventory**: Every item has mass—players and agents have carrying capacity limits
- **Material Transport**: Heavy resources must be moved using carts, vehicles, or conveyor systems
- **Physical Storage**: Items exist in the world in chests, stockpiles, and containers (not just abstract inventory)
- **Logistics Challenges**: Efficient resource flow requires planning roads, vehicles, and storage networks

### Custom Entities

Beyond blocks—complex shaped objects that enable advanced automation and industry:

- **Vehicles**: Carts, trucks, and transport vehicles for moving heavy materials
- **Workshops**: Crafting stations, smelters, and production machines with custom models
- **Storage**: Specialized containers like silos, stockpiles, and warehouses
- **Machinery**: Pumps, generators, and industrial equipment with unique physics
- **Entity Physics**: Custom collision shapes and interaction systems alongside the voxel grid

## 📁 Project Structure

```
societies/
├── README.md                    # This file
├── planning/                    # All planning documents
│   ├── sessions/                # 7-session planning structure
│   │   ├── session-1-technical-architecture/
│   │   ├── session-2-ai-system-design/
│   │   ├── session-3-core-gameplay-loops/
│   │   ├── session-4-progression-and-balance/
│   │   ├── session-5-governance-mechanics/
│   │   ├── session-6-prototyping-roadmap/
│   │   └── session-7-integration-master-plan/
│   ├── research/                # Research materials
│   │   ├── game-analysis-research-guide.md
│   │   ├── technical-postmortems-research-guide.md
│   │   └── agent-research-prompts.md
│   ├── meta/                    # Vision and methodology
│   │   ├── societies-meta-planning.md
│   │   └── societies-comprehensive-breakdown.md
│   └── spreadsheets/            # Excel templates
│       ├── tech-stack-comparison.md
│       ├── resource-economy-balance.md
│       ├── progression-timeline.md
│       └── risk-assessment.md
│
├── src/                         # Source code
│   └── societies/               # Godot project
│       ├── project.godot        # Godot project config
│       ├── Societies.csproj     # C# project file
│       ├── scenes/              # Godot scene files
│       ├── scripts/             # C# scripts
│       │   ├── core/            # Core systems
│       │   ├── multiplayer/     # Networking
│       │   ├── simulation/      # World simulation
│       │   ├── agents/          # AI behavior
│       │   ├── economy/         # Trading/markets
│       │   ├── governance/      # Laws/politics
│       │   └── utils/           # Helpers
│       ├── assets/              # Game assets
│       └── resources/           # Godot resources
│
└── docs/                        # Documentation
    └── (generated from planning)
```

## 🚀 Quick Start

### Prerequisites

- **Godot 4.x** with C# support ([Download](https://godotengine.org/download))
- **.NET 8.0 SDK** ([Download](https://dotnet.microsoft.com/download))
- **IDE**: Visual Studio, VS Code, or Rider
- **PostgreSQL** (for large-scale production servers like Eco's 50-100 player servers, NOT for development)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/societies.git
   cd societies
   ```

2. **Open in Godot**
   - Launch Godot 4.x
   - Import project from `src/societies/`
   - Let Godot import assets and generate C# solution

3. **Build the project**
   - In Godot: Click "Build" button
   - Or run: `dotnet build src/societies/Societies.csproj`

4. **Run the game**
   - Press F5 in Godot
   - Or run the compiled executable

### Running Modes

**Single Player (Offline)**
```csharp
// Game automatically starts local server
GameManager.Instance.StartSinglePlayer();
```

**Multiplayer Server**
```csharp
// Start dedicated server
GameManager.Instance.StartServer(port: 7777);
```

**Multiplayer Client**
```csharp
// Connect to server
GameManager.Instance.ConnectToServer("127.0.0.1", 7777);
```

## 📖 Documentation

### Planning Documents

All planning is in `/planning/` directory:

1. **Technical Architecture** (`day1-technical-architecture.md`)
   - System design, Godot + ENet networking
   - PostgreSQL/SQLite database design
   - Performance budgets and scalability

2. **AI System Design** (`day2-ai-system-design.md`)
   - Agent decision-making architecture
   - Memory and goal systems
   - Population elasticity

3. **Core Gameplay Loops** (`day3-core-gameplay-loops.md`)
   - Moment-to-moment activities
   - Player archetypes
   - Session flows

4. **Progression & Balance** (`day4-progression-and-balance.md`)
   - Technology tree
   - Resource economy
   - Threat timeline

5. **Governance Mechanics** (`day5-governance-mechanics.md`)
   - Law systems
   - Voting and elections
   - Anti-griefing protections

6. **Prototyping Roadmap** (`day6-prototyping-roadmap.md`)
   - 6-month build plan
   - 5 prototypes leading to Alpha
   - Validation criteria

7. **Master Development Plan** (`day7-master-development-plan.md`)
   - Integration map
   - Risk management
   - Resource requirements

### Research Guides

Ready-to-use research prompts in `/planning/research/`:

- **Game Analysis**: Deep dives into Eco, Dwarf Fortress, Paradox games
- **Technical Postmortems**: GDC talks and case studies
- **Agent Prompts**: Delegation templates for research tasks

### Spreadsheets

Excel templates in `/planning/spreadsheets/` (convert .md to .xlsx):

- **Tech Stack Comparison**: Decision matrices with scoring
- **Resource Economy Balance**: Production chains, consumption rates
- **Progression Timeline**: Tech tree, milestones, server lifecycle
- **Risk Assessment**: Risk matrices, mitigation strategies

## 🛠️ Tech Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Engine** | Godot 4.x + C# | Free, excellent multiplayer, native C# |
| **Networking** | Godot ENet | UDP-based, low latency, built-in RPC |
| **Database (Production)** | PostgreSQL | Large-scale servers (50+ players like Eco) |
| **Database (Dev/Single-Player)** | SQLite | Zero setup, sufficient for MVP (8 players, 20 agents) |
| **Server OS** | Linux (Ubuntu) | Stable, headless Godot support |
| **Version Control** | Git + GitHub | Collaboration, CI/CD |

## 📅 Development Timeline

```
Month 1:   Prototype 1 - Basic World & Simulation
Month 2:   Prototype 2 - AI Agents & Economy
Month 3:   Prototype 3 - Basic Governance
Month 4:   Prototype 4 - Progression & Threats
Month 5:   Prototype 5 - Environmental Systems
Month 6:   Alpha - Integration & Testing
Month 7-12: Beta - Balancing & Polish
Month 13+: Release
```

See `planning/sessions/session-6-prototyping-roadmap/day6-prototyping-roadmap.md` for detailed plan.

## 🤝 Contributing

This is currently a solo project, but contributions will be welcome post-Alpha.

### For Now

1. **Playtest** - Try prototypes and provide feedback
2. **Research** - Use the research guides to analyze reference games
3. **Spread the word** - Share the project with interested developers

### Future

- Open source after release
- Community mods supported
- Developer documentation

## 🎯 Current Status

**Phase**: Day 0 - Project Setup Complete ✅

**Completed**:
- ✅ Folder structure
- ✅ 7 planning document templates
- ✅ 4 Excel spreadsheet templates
- ✅ 3 research guides with agent prompts
- ✅ Godot project initialization
- ✅ Multiplayer foundation code

**Next**:
- Week 1: Fill out planning documents
- Delegate research tasks
- Begin Prototype 1 (Month 1)

## 📊 Key Metrics

**Scope**:
- World Size: 0.5-4 km² (voxel-based, fully editable)
- Voxel Resolution: 1m³ blocks
- AI Agents: 20 (MVP target) → 50-100 (post-MVP)
- Players: 8 (MVP) → 20+ (post-MVP)
- Max Carrying Weight: ~50kg per character (realistic logistics)
- Dev Timeline: 2-3 years (realistic for solo development)

**Performance Targets**:
- Server: 20 ticks/second
- Client: 60+ FPS
- Network: <100ms latency
- Memory: <4GB server

## 📝 License

MIT License - See [LICENSE](LICENSE) file

## 🙏 Acknowledgments

**Inspirations**:
- **Eco** by Strange Loop Games - Voxel world, environmental simulation, and logistics
- **Minecraft** by Mojang - Voxel-based building and world editing
- **Dwarf Fortress** by Bay 12 Games - Agent simulation and complex systems
- **Factorio** by Wube Software - Automation, logistics, and material transport
- **Paradox Games** - Political systems and governance

**Tools**:
- Godot Engine and community
- .NET ecosystem
- PostgreSQL team

## 📞 Contact

- **Issues**: [GitHub Issues](https://github.com/yourusername/societies/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/societies/discussions)

---

## Navigation Quick Reference

**Getting Started**:
1. Read `planning/meta/societies-comprehensive-breakdown.md` for vision
2. Review `planning/sessions/session-1-technical-architecture/` for technical architecture
3. Check `planning/sessions/session-7-integration-master-plan/` for integration roadmap

**Research Tasks**:
- Delegate using `planning/research/agent-research-prompts.md`
- Track progress with research guides

**Development**:
- Code in `src/societies/`
- Use Godot 4.x with C#
- Follow planning documents

**Questions?**:
- Check planning documents first
- Look for TODO comments in code
- Create GitHub issue for discussion

---

**Ready to build the future of multiplayer simulation games.** 🚀
