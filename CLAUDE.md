# MoneyTransferService — Claude Instructions

Caveman mode: short. Clear. Why > what. Keep intent.

Project status is tracked in `Docs/PROJECT_STATUS.md`.
Treat `Docs/PROJECT_STATUS.md` as living organism.
Update it when architecture, intent, decision, backlog, or project status changes.
Keep it aligned with current project goal.
Remove stale/wrong notes.
Do not let it become graveyard.

## Work Style

- Build only at meaningful checkpoints. Avoid continuous builds.
- Understand the problem first. If it is complex, create a plan before execution.
- Always decompose work into independent, testable units.
- Default mode: subagent first, main agent verifies.

## Subagent-first Execution Rule

- For every non-trivial task, spawn at least one subagent before making changes.
- The main agent should not directly execute implementation work unless the task is truly trivial.
- The main agent is responsible for:
  - understanding the request,
  - identifying constraints and hidden dependencies,
  - defining expected outcome,
  - decomposing work into independent, testable units,
  - delegating work to subagent(s),
  - reviewing subagent output,
  - integrating accepted changes,
  - verifying final state.

- A task is trivial only when it is a single deterministic change with no meaningful design, dependency, or validation risk.
  Examples:
  - fix a typo,
  - add or adjust a short comment,
  - answer a simple read-only question,
  - run one obvious command requested by the user.

- Never accept subagent output blindly. Always verify it against the original request and project rules.
- After each subagent completes:
  - validate output against expected behavior,
  - if invalid, re-decompose or re-run with corrected scope.

## Task Decomposition

- Every task must be split into independent components when possible.
- If dependencies exist, construct a dependency graph before execution and respect it.
- Shared state, shared schema, shared files, generated artifacts, or ordering-sensitive logic require sequential delegation.

### Parallel Subagent Rule

- Parallel subagents are allowed only for truly independent work.
- Do not parallelize only because there are multiple files or multiple edits.
- Parallel work must not share mutable files, database schema, generated artifacts, or ordering-sensitive logic.
- Shared state or shared files require sequential delegation and validation after each result.

## Workflow

1. Understand the requirement.
2. Identify constraints and hidden dependencies.
3. Decide if the task is trivial.
4. If trivial, execute directly.
5. If non-trivial, decompose and spawn at least one subagent.
6. Decide sequential vs parallel delegation.
7. Validate subagent output.
8. Integrate accepted changes.
9. Verify final state.
10. Report outcome and next recommended task.

## Model Routing

### Opus (Planner / Architect)

Use for:

- System design and architecture decisions
- Cross-module refactoring plans
- Ambiguous or incomplete requirements
- Concurrency, distributed systems, and state consistency problems
- High-level reasoning about correctness and approach

Output should be limited to plans, reasoning, and decisions (not full implementation).

### Sonnet (Default Worker)

Use for:

- Feature implementation
- Bug fixing (local or medium complexity scope)
- API and service layer development
- Database logic (SQL / EF Core / MongoDB)
- Integration tasks

This is the default execution model.

### Haiku (Fast Worker)

Use for:

- Boilerplate generation
- DTO mapping
- Parsing, transformation, and formatting tasks
- Log analysis
- Small refactors
- File scanning and extraction tasks

### Haiku (Router Mode)

Use for:

- Task classification (deciding between Opus / Sonnet / Haiku)
- Context reduction and filtering
- Breaking work into subtasks

This is the cost optimization layer.

### Small Fast Model

Use for:

- Trivial read-only queries
- Single-step deterministic commands
- Tasks requiring no reasoning or planning

## Constraints

- Opus must never be used as an execution worker.
- Sonnet is the default execution layer.
- Haiku is used for routing, parallel lightweight tasks, and repetitive work.
- Parallel execution is only allowed for stateless, independent tasks.
- Any shared state, shared schema, or shared file requires sequential execution.

## Key Principle

Optimize for correctness first, then cost, then speed. Never sacrifice correctness for parallelism or model cost reduction.

## Project Status Usage

- Read `Docs/PROJECT_STATUS.md` when task needs architecture, current project state, decisions, backlog, naming rules, or project guardrails.
- Update `Docs/PROJECT_STATUS.md` after completing work that changes architecture, intent, decision, backlog, or current status.
- Keep `CLAUDE.md` limited to agent instructions.
- Keep project facts, decisions, and backlog in `Docs/PROJECT_STATUS.md`.

## Reporting Guardrails

- Mention next task after completing any work.
