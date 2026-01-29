# Game Analysis Research Guide

This guide provides structured research tasks for analyzing reference games that influence Societies' design.

## Target Games

### Primary References
1. **Eco** - Environmental simulation, player-run government, skill systems
2. **Dwarf Fortress** - Agent simulation, emergent storytelling, complex systems
3. **Paradox Games** (Crusader Kings, Victoria, Stellaris) - Political systems, UI patterns

### Secondary References
- Factorio - Automation and production chains
- RimWorld - AI agent behavior
- Minecraft - Building and progression
- Civilization - Tech trees and pacing

---

## Research Focus Areas

### 1. Eco - Environmental Simulation

**Key Systems to Analyze**:
- Pollution propagation mechanics
- Skill specialization and progression
- Law creation and enforcement flow
- Economic data visualization
- Multiplayer coordination tools
- Meteor threat implementation

**Specific Questions**:
1. How does pollution spread spatially? (Distance, wind, water flow)
2. How do players visualize environmental data? (Heat maps, graphs, indicators)
3. What's the law creation UX flow? (Drafting → Voting → Enforcement)
4. How do skills unlock and progress? (XP system, specialization trees)
5. How does the economy handle supply/demand? (Price discovery, inflation)
6. What makes the meteor threat compelling? (Timer, preparation, consequences)

**Deliverables Needed**:
- Screenshots of key UI elements
- Description of pollution algorithm
- Skill tree structure
- Law system workflow diagram
- Economic interface analysis

---

### 2. Dwarf Fortress - Agent Simulation

**Key Systems to Analyze**:
- Agent scheduling and decision-making
- Memory and relationship systems
- Emergent narrative generation
- Job assignment and prioritization
- World persistence architecture
- Mood and needs systems

**Specific Questions**:
1. How do dwarves decide what to do? (Priority system, needs-based)
2. How are relationships formed and maintained? (Social network)
3. What creates emergent stories? (Combination of systems)
4. How are jobs assigned and managed? (Labor preferences, auto-assign)
5. How does the game remember world history? (Legends mode)
6. What makes agents feel alive? (Personality, preferences, quirks)

**Deliverables Needed**:
- Agent decision-making flowchart
- Relationship system architecture
- Examples of emergent narratives
- Job management interface description
- Memory system data structures

---

### 3. Paradox Games - Political Systems

**Key Systems to Analyze**:
- Voting and election mechanics
- Law creation complexity vs. accessibility
- Political faction systems
- Information presentation at scale
- Tutorial and onboarding
- Multi-layered governance (local/regional/global)

**Specific Questions**:
1. How do voting systems work? (UI, counting, transparency)
2. How are laws presented to be understandable? (Plain language, effects)
3. How do factions form and interact? (Interest groups, parties)
4. How does the UI handle complexity? (Tooltips, filters, layers)
5. How are new players taught complex systems? (Tutorials, tooltips, advisors)
6. How does jurisdiction work across levels? (Conflicts, hierarchy)

**Deliverables Needed**:
- Voting interface screenshots
- Law creation workflow
- Faction system description
- UI complexity management analysis
- Tutorial system evaluation

---

## Agent Research Prompts

### Prompt 1: Eco Game Analysis

```
# Research Task: Eco Game Deep Analysis

## Objective
Analyze the game Eco for design patterns relevant to Societies, focusing on environmental simulation, governance, and economic systems.

## Background
Eco is a multiplayer environmental simulation game where players build a civilization and must balance economic growth with environmental protection. A meteor threatens to destroy the world on day 30, forcing collaboration.

## Research Questions

### Environmental Systems
1. How does pollution propagate in the game world?
   - What types of pollution exist?
   - How does pollution spread spatially?
   - What are the consequences of pollution?
   - How can players visualize pollution data?

2. How does the ecosystem simulation work?
   - What species are modeled?
   - How do species interact?
   - What happens when species go extinct?
   - How is biodiversity tracked?

### Governance Systems
3. How does the law system work?
   - What types of laws can be created?
   - What's the process from proposal to enactment?
   - How are laws enforced?
   - What's the UI for creating and viewing laws?

4. How does voting work?
   - Who can vote?
   - What can be voted on?
   - How is vote counting handled?
   - What's the player experience of voting?

### Economic Systems
5. How does the skill system work?
   - How are skills unlocked?
   - How does specialization work?
   - What are the benefits of specializing?
   - How is skill progression displayed?

6. How does the economy function?
   - What currency system is used?
   - How are prices determined?
   - How do players trade?
   - What economic data is available to players?

### Meteor Threat
7. How is the meteor threat implemented?
   - When is it introduced?
   - What preparation is required?
   - What happens if players fail?
   - How does it drive player behavior?

### UI/UX Patterns
8. What are the key UI patterns?
   - How is complex data visualized?
   - How are notifications handled?
   - What's the information architecture?
   - What makes the UI effective or frustrating?

## Deliverables

For each research question above, provide:
1. Detailed description (200-500 words)
2. Screenshots or diagrams where applicable
3. Specific examples from gameplay
4. Strengths and weaknesses of the implementation
5. Recommendations for Societies (what to adopt, avoid, or improve)

## Format

Submit findings as a structured report with:
- Executive summary (1 paragraph)
- Detailed findings by section
- Visual aids (screenshots, diagrams)
- Comparative analysis (how it compares to Societies needs)
- Actionable recommendations

## Success Criteria
- [ ] All 8 research questions answered in detail
- [ ] At least 5 screenshots or visual examples
- [ ] Specific recommendations for Societies
- [ ] Critical analysis (not just description)
- [ ] Word count: 2000-4000 words

## Time Budget
3-4 hours focused gameplay and analysis

## Resources
- Game: Eco (available on Steam)
- Focus: Play for at least 5-10 hours to understand systems
- Document: Take notes during play sessions
```

---

### Prompt 2: Dwarf Fortress Analysis

```
# Research Task: Dwarf Fortress Agent Systems Analysis

## Objective
Analyze Dwarf Fortress's agent simulation systems for insights on creating believable AI citizens in Societies.

## Background
Dwarf Fortress is legendary for its complex agent simulation where dwarves have needs, preferences, relationships, and create emergent stories through their interactions.

## Research Questions

### Agent Decision-Making
1. How do dwarves decide what to do?
   - What drives their behavior? (Needs, preferences, moods)
   - How are priorities determined?
   - What makes them choose one task over another?
   - How do they handle conflicting needs?

2. How does the needs system work?
   - What needs do dwarves have?
   - How are needs satisfied?
   - What happens when needs aren't met?
   - How do needs affect behavior?

### Memory and Learning
3. How do dwarves remember things?
   - What do they remember?
   - How long do memories last?
   - How do memories affect future behavior?
   - Can they forget things?

4. How do relationships form?
   - What creates relationships?
   - How do relationships evolve?
   - What types of relationships exist?
   - How do relationships affect gameplay?

### Emergent Storytelling
5. What creates emergent narratives?
   - What systems contribute to stories?
   - Can you provide specific examples?
   - How do players learn about these stories?
   - What makes a good emergent story?

### Job and Labor Management
6. How is labor organized?
   - How are jobs assigned?
   - How do dwarves choose professions?
   - What happens when no one wants to do a job?
   - How are skills developed?

### World Persistence
7. How does world history work?
   - What is tracked over time?
   - How does history affect current gameplay?
   - What's the Legends mode?
   - How much data is stored?

### What Makes Agents Feel Alive
8. What gives dwarves personality?
   - What individual traits exist?
   - How do preferences manifest?
   - What quirks or behaviors stand out?
   - How do players connect with individual dwarves?

## Deliverables

Provide for each question:
1. Technical explanation of how the system works
2. Examples from actual gameplay
3. Screenshots or descriptions of relevant interfaces
4. Analysis of what works well and what doesn't
5. Recommendations for Societies implementation

Include:
- System architecture diagrams (if possible)
- Specific anecdotes of emergent behavior
- Comparison to other games
- Implementation complexity assessment

## Format

Structured report with:
- Introduction to Dwarf Fortress agent systems
- Section for each research question
- Visual aids and examples
- Synthesis section connecting insights
- Recommendations prioritized by importance

## Success Criteria
- [ ] All 8 questions thoroughly answered
- [ ] At least 3 specific emergent story examples
- [ ] Technical depth appropriate for implementation
- [ ] Clear recommendations for Societies
- [ ] Word count: 2500-4500 words

## Time Budget
4-5 hours (2-3 hours playing, 2 hours research/writing)

## Resources
- Game: Dwarf Fortress (free at bay12games.com)
- Wiki: dwarffortresswiki.org
- Community: Reddit r/dwarffortress
- Focus: Pay attention to individual dwarves, their needs, and relationships
```

---

### Prompt 3: Paradox Games Analysis

```
# Research Task: Paradox Games Political Systems Analysis

## Objective
Analyze Paradox Interactive games (Crusader Kings, Victoria, Stellaris) for governance UI patterns, political mechanics, and complexity management.

## Background
Parox games are known for complex political simulations. We need to understand how they make deep systems accessible and engaging.

## Research Questions

### Voting Systems
1. How do elections work?
   - What's the player experience of voting?
   - How are candidates presented?
   - How is vote counting displayed?
   - What makes elections engaging?

2. How is political power distributed?
   - Who can make decisions?
   - How are decisions made?
   - What limits power?
   - How do players gain/lose power?

### Law and Policy
3. How are laws/policies created?
   - What's the creation process?
   - How are effects communicated?
   - What's the UI for law management?
   - How do laws interact?

4. How is complexity managed?
   - How much information is shown at once?
   - What uses tooltips vs. full screens?
   - How are related systems connected?
   - What helps players understand impact?

### Factions and Politics
5. How do political factions work?
   - How do factions form?
   - What do factions want?
   - How do players interact with factions?
   - How do factions affect gameplay?

6. How is political conflict handled?
   - What creates conflict?
   - How is conflict resolved?
   - What are the consequences?
   - How do players navigate conflict?

### Tutorial and Onboarding
7. How are new players taught?
   - What tutorial systems exist?
   - How is complexity introduced?
   - What helps players learn?
   - What are common confusion points?

### UI Patterns
8. What UI patterns work well?
   - Information organization
   - Navigation between systems
   - Alert and notification systems
   - Data visualization techniques

## Deliverables

For each question:
1. Description of the mechanic/system
2. Screenshots of relevant UI
3. Analysis of accessibility vs. depth
4. What works well and what doesn't
5. Specific recommendations for Societies

Include:
- Comparative analysis across different Paradox games
- UI pattern library (catalog of effective patterns)
- Complexity management strategies
- Accessibility recommendations

## Format

Comparative analysis with:
- Introduction to each game analyzed
- Section per research question with cross-game comparison
- UI pattern catalog
- Synthesis of best practices
- Implementation recommendations for Societies

## Success Criteria
- [ ] Analysis of at least 2 Paradox games
- [ ] At least 10 UI screenshots
- [ ] Pattern catalog with 5+ patterns
- [ ] Actionable UI/UX recommendations
- [ ] Word count: 2000-4000 words

## Time Budget
3-4 hours gameplay across games + 2 hours analysis

## Resources
- Games: Crusader Kings 3, Victoria 3, or Stellaris
- Focus: UI/UX and political systems
- Look for: Tutorial sequences, law creation, voting interfaces
```

---

## Research Tracking

### Task Assignment

| Game | Assigned To | Due Date | Status | Priority |
|------|-------------|----------|--------|----------|
| Eco | [Agent 1] | [Date] | Pending | High |
| Dwarf Fortress | [Agent 2] | [Date] | Pending | High |
| Paradox Games | [Agent 3] | [Date] | Pending | Medium |

### Quality Checklist

Before submitting research:
- [ ] Played game for minimum required hours
- [ ] Answered all research questions
- [ ] Included screenshots/examples
- [ ] Provided critical analysis (not just description)
- [ ] Made specific recommendations for Societies
- [ ] Within word count guidelines
- [ ] Properly formatted and organized

### Integration Plan

After research completion:
1. Review all submissions
2. Synthesize findings
3. Update planning documents with insights
4. Prioritize recommendations
5. Create implementation tickets

---

## Expected Outcomes

### From Eco Analysis
- Pollution propagation algorithm
- Law creation UX flow
- Skill system structure
- Economic visualization patterns
- Meteor threat design lessons

### From Dwarf Fortress
- Agent decision-making architecture
- Memory system design
- Relationship mechanics
- Emergent storytelling factors
- Personality system structure

### From Paradox Games
- Voting UI patterns
- Complexity management strategies
- Political faction systems
- Tutorial best practices
- Governance interface design

---

**Instructions for Use**:
1. Assign research tasks to agents or team members
2. Set deadlines and expectations
3. Review submissions against quality checklist
4. Synthesize findings into planning documents
5. Update game design based on research insights
