```markdown
---
name: writing-skills
description: Use when creating new skills, editing existing skills, or verifying skills work before deployment in the Antigravity environment
---

# Writing Antigravity Skills

## Overview

**Writing skills IS Test-Driven Development applied to process documentation.**

**Personal skills live in agent-specific directories (e.g., `~/.antigravity/skills`)**

You write test cases (pressure scenarios with subagents), watch them fail (baseline behavior), write the skill (documentation), watch tests pass (agents comply), and refactor (close loopholes).

**Core principle:** If you didn't watch an Antigravity agent fail without the skill, you don't know if the skill teaches the right thing.

**REQUIRED BACKGROUND:** You MUST understand `antigravity:test-driven-development` before using this skill. That skill defines the fundamental RED-GREEN-REFACTOR cycle. This skill adapts TDD to documentation.

**Official guidance:** For Antigravity's official skill authoring best practices, see `antigravity-best-practices.md`. This document provides additional patterns and guidelines that complement the TDD-focused approach in this skill.

## What is a Skill?

A **skill** is a reference guide for proven techniques, patterns, or tools. Skills help future Antigravity instances find and apply effective approaches.

**Skills are:** Reusable techniques, patterns, tools, reference guides

**Skills are NOT:** Narratives about how you solved a problem once

## TDD Mapping for Skills

| TDD Concept | Skill Creation |
|-------------|----------------|
| **Test case** | Pressure scenario with subagent |
| **Production code** | Skill document (SKILL.md) |
| **Test fails (RED)** | Agent violates rule without skill (baseline) |
| **Test passes (GREEN)** | Agent complies with skill present |
| **Refactor** | Close loopholes while maintaining compliance |
| **Write test first** | Run baseline scenario BEFORE writing skill |
| **Watch it fail** | Document exact rationalizations agent uses |
| **Minimal code** | Write skill addressing those specific violations |
| **Watch it pass** | Verify agent now complies |
| **Refactor cycle** | Find new rationalizations → plug → re-verify |

The entire skill creation process follows RED-GREEN-REFACTOR.

## When to Create a Skill

**Create when:**
- Technique wasn't intuitively obvious to you or the agent
- You'd reference this again across Antigravity projects
- Pattern applies broadly (not project-specific)
- Others would benefit

**Don't create for:**
- One-off solutions
- Standard practices well-documented elsewhere
- Project-specific conventions (put in `ANTIGRAVITY.md` or similar config)
- Mechanical constraints (if it's enforceable with regex/validation, automate it—save documentation for judgment calls)

## Skill Types

### Technique
Concrete method with steps to follow (condition-based-waiting, root-cause-tracing)

### Pattern
Way of thinking about problems (flatten-with-flags, test-invariants)

### Reference
API docs, syntax guides, tool documentation (office docs)

## Directory Structure


```

skills/
skill-name/
SKILL.md              # Main reference (required)
supporting-file.* # Only if needed

```

**Flat namespace** - all skills in one searchable namespace

**Separate files for:**
1. **Heavy reference** (100+ lines) - API docs, comprehensive syntax
2. **Reusable tools** - Scripts, utilities, templates

**Keep inline:**
- Principles and concepts
- Code patterns (< 50 lines)
- Everything else

## SKILL.md Structure

**Frontmatter (YAML):**
- Only two fields supported: `name` and `description`
- Max 1024 characters total
- `name`: Use letters, numbers, and hyphens only (no parentheses, special chars)
- `description`: Third-person, describes ONLY when to use (NOT what it does)
  - Start with "Use when..." to focus on triggering conditions
  - Include specific symptoms, situations, and contexts
  - **NEVER summarize the skill's process or workflow** (see ASO section for why)
  - Keep under 500 characters if possible

```markdown
---
name: Skill-Name-With-Hyphens
description: Use when [specific triggering conditions and symptoms]
---

# Skill Name

## Overview
What is this? Core principle in 1-2 sentences.

## When to Use
[Small inline flowchart IF decision non-obvious]

Bullet list with SYMPTOMS and use cases
When NOT to use

## Core Pattern (for techniques/patterns)
Before/after code comparison

## Quick Reference
Table or bullets for scanning common operations

## Implementation
Inline code for simple patterns
Link to file for heavy reference or reusable tools

## Common Mistakes
What goes wrong + fixes

## Real-World Impact (optional)
Concrete results

```

## Antigravity Search Optimization (ASO)

**Critical for discovery:** Future Antigravity agents need to FIND your skill

### 1. Rich Description Field

**Purpose:** The agent reads the description to decide which skills to load for a given task. Make it answer: "Should I read this skill right now?"

**Format:** Start with "Use when..." to focus on triggering conditions

**CRITICAL: Description = When to Use, NOT What the Skill Does**

The description should ONLY describe triggering conditions. Do NOT summarize the skill's process or workflow in the description.

**The trap:** Descriptions that summarize workflow create a shortcut the agent will take. The skill body becomes documentation the agent skips.

```yaml
# ❌ BAD: Summarizes workflow - Agent may follow this instead of reading skill
description: Use when executing plans - dispatches subagent per task with code review between tasks

# ✅ GOOD: Just triggering conditions, no workflow summary
description: Use when executing implementation plans with independent tasks in the current session

```

**Content:**

* Use concrete triggers, symptoms, and situations that signal this skill applies
* Describe the *problem* (race conditions, inconsistent behavior) not just *language-specific symptoms*
* Write in third person (injected into system prompt)
* **NEVER summarize the skill's process or workflow**

### 2. Keyword Coverage

Use words the agent would search for:

* Error messages: "Hook timed out", "ENOTEMPTY", "race condition"
* Symptoms: "flaky", "hanging", "zombie", "pollution"
* Tools: Actual commands, library names, file types

### 3. Descriptive Naming

**Use active voice, verb-first:**

* ✅ `creating-skills` not `skill-creation`
* ✅ `condition-based-waiting` not `async-test-helpers`

### 4. Token Efficiency (Critical)

**Problem:** Frequently referenced skills load into EVERY conversation. Every token counts.

**Target word counts:**

* getting-started workflows: <150 words each
* Frequently-loaded skills: <200 words total
* Other skills: <500 words (still be concise)

### 5. Cross-Referencing Other Skills

**When writing documentation that references other skills:**

Use skill name only, with explicit requirement markers:

* ✅ Good: `**REQUIRED SUB-SKILL:** Use antigravity:test-driven-development`
* ✅ Good: `**REQUIRED BACKGROUND:** You MUST understand antigravity:systematic-debugging`
* ❌ Bad: `See skills/testing/test-driven-development` (unclear if required)

**Why no @ links:** `@` syntax force-loads files immediately, consuming context before you need them.

## Flowchart Usage

**Use flowcharts ONLY for:**

* Non-obvious decision points
* Process loops where you might stop too early
* "When to use A vs B" decisions

See `graphviz-conventions.dot` for style rules.

## The Iron Law (Same as TDD)

```
NO SKILL WITHOUT A FAILING TEST FIRST

```

This applies to NEW skills AND EDITS to existing skills.

Write skill before testing? Delete it. Start over.
Edit skill without testing? Same violation.

**No exceptions:**

* Not for "simple additions"
* Not for "documentation updates"
* Don't keep untested changes as "reference"
* Delete means delete

**REQUIRED BACKGROUND:** The `antigravity:test-driven-development` skill explains why this matters. Same principles apply to documentation.

## Testing All Skill Types

Different skill types need different test approaches:

### Discipline-Enforcing Skills (rules/requirements)

**Test with:**

* Academic questions: Do they understand the rules?
* Pressure scenarios: Do they comply under stress?
* Multiple pressures combined: time + sunk cost + exhaustion

### Technique Skills (how-to guides)

**Test with:**

* Application scenarios: Can they apply the technique correctly?
* Variation scenarios: Do they handle edge cases?

## Bulletproofing Skills Against Rationalization

Skills that enforce discipline need to resist rationalization. Agents are smart and will find loopholes when under pressure.

### Close Every Loophole Explicitly

Don't just state the rule - forbid specific workarounds:

<Good>

```markdown
Write code before test? Delete it. Start over.

**No exceptions:**

  - Don't keep it as "reference"
  - Don't "adapt" it while writing tests
  - Delete means delete

<!-- end list -->


```

### Address "Spirit vs Letter" Arguments

Add foundational principle early:

```markdown
**Violating the letter of the rules is violating the spirit of the rules.**

```

## RED-GREEN-REFACTOR for Skills

Follow the TDD cycle:

### RED: Write Failing Test (Baseline)

Run pressure scenario with subagent WITHOUT the skill. Document exact behavior:

* What choices did they make?
* What rationalizations did they use (verbatim)?

### GREEN: Write Minimal Skill

Write skill that addresses those specific rationalizations. Don't add extra content for hypothetical cases.
Run same scenarios WITH skill. Agent should now comply.

### REFACTOR: Close Loopholes

Agent found new rationalization? Add explicit counter. Re-test until bulletproof.

## STOP: Before Moving to Next Skill

**After writing ANY skill, you MUST STOP and complete the deployment process.**

**Do NOT:**

* Create multiple skills in batch without testing each
* Move to next skill before current one is verified

**The deployment checklist below is MANDATORY for EACH skill.**

## Skill Creation Checklist (TDD Adapted)

**IMPORTANT: Use TodoWrite to create todos for EACH checklist item below.**

**RED Phase - Write Failing Test:**

* [ ] Create pressure scenarios (3+ combined pressures for discipline skills)
* [ ] Run scenarios WITHOUT skill - document baseline behavior verbatim
* [ ] Identify patterns in rationalizations/failures

**GREEN Phase - Write Minimal Skill:**

* [ ] Name uses only letters, numbers, hyphens
* [ ] YAML frontmatter with only name and description
* [ ] Description starts with "Use when..." and includes specific triggers/symptoms
* [ ] Description written in third person (ASO compliant)
* [ ] Keywords throughout for search
* [ ] Clear overview with core principle
* [ ] Address specific baseline failures identified in RED
* [ ] Code inline OR link to separate file
* [ ] Run scenarios WITH skill - verify agents now comply

**REFACTOR Phase - Close Loopholes:**

* [ ] Identify NEW rationalizations from testing
* [ ] Add explicit counters (if discipline skill)
* [ ] Build rationalization table from all test iterations
* [ ] Create red flags list
* [ ] Re-test until bulletproof

**Deployment:**

* [ ] Commit skill to git and push
* [ ] Verify discovery via ASO check

## The Bottom Line

**Creating skills IS TDD for process documentation.**

Same Iron Law: No skill without failing test first.
Same cycle: RED (baseline) → GREEN (write skill) → REFACTOR (close loopholes).

If you follow TDD for code, follow it for skills. It's the same discipline applied to documentation.

```

```