# Claude Code Agents — How AI Was Used to Build This

> **Slide talking points:** This is the "how did you actually use AI?" section. Most interesting for a Copilot-using team.

---

## What Is Claude Code?

- Claude Code is Anthropic's AI coding agent — runs in your terminal, has access to your entire repo
- It's not autocomplete (that's Copilot) — it's an **agent** that reads files, writes code, runs commands, and makes decisions
- Think: pair programmer that can do a whole task end-to-end, not just complete the line you're typing

---

## The Difference: Copilot vs Claude Code

| | GitHub Copilot | Claude Code |
|--|---------------|-------------|
| **Interface** | IDE inline suggestions | Terminal (or IDE sidebar) |
| **Scope** | Current file/function | Entire codebase |
| **Can run commands?** | No | Yes (builds, tests, git) |
| **Memory** | Current file context | Full repo + memory files |
| **Can open PRs?** | No | Yes, with GitHub CLI |
| **Best for** | "Complete this function" | "Implement this feature" |

---

## Specialized Agents Defined in This Repo

Claude Code supports defining **custom agents** in `.claude/agents/` — each with a specialized prompt, tools, and scope. This repo used three:

### 1. `react-component-expert`
**Scope:** Frontend work only
- Creates/refactors React components in `MusicShare.Frontend/`
- Knows Next.js App Router patterns, Tailwind CSS, React Query
- Invoked when: "Add a new component", "Fix this UI issue", "Create a result card"

### 2. `infra-devops-owner`
**Scope:** Infrastructure and deployment
- Modifies `.NET Aspire AppHost`, GitHub Actions workflows, `Program.cs` wiring
- Manages environment variables, secrets, Azure configuration
- Invoked when: "Add a new service", "Fix the CI pipeline", "Configure a new env var"

### 3. `project-coordinator`
**Scope:** GitHub issue → implementation plan → delegation
- Reads GitHub issue details, analyzes requirements, plans the approach
- Hands off to the right specialist agent (react-component-expert, infra-devops-owner, etc.)
- Invoked when: "Work on issue #37"

---

## The CLAUDE.md — Instructions for the Agent

Every repo has a `CLAUDE.md` — it's the system prompt for the AI working in this codebase.

### What's documented in it:
- **Architecture overview** — so the agent understands the system before touching anything
- **Code conventions** — naming, file organization, DI patterns, test patterns
- **Key files map** — "here's where to find things"
- **Data flow** — the full saga journey documented in prose
- **Agent definitions** — which agent to use for which type of work
- **Development commands** — how to build, test, run, deploy

### Why it matters:
- The agent reads CLAUDE.md at the start of every session
- Consistent conventions across 55+ commits — because the agent was always working from the same rules
- When something is documented wrong, the agent does it wrong too → **forced the documentation to be accurate**

---

## MCP Tools — Semantic Code Analysis

Beyond file reading/writing, Claude Code was configured with **Model Context Protocol (MCP) tools** that connect to the .NET Roslyn compiler:

| Instead of | MCP Tool | Why Better |
|-----------|---------|-----------|
| Grep for a class name | `FindSymbols` | Semantic — finds the actual symbol, not a string match |
| Browse project files | `GetSolutionTree` | Understands project/namespace structure |
| Read a file to find a method | `FindSymbolDefinition` | Jumps directly to the definition |
| Search for who calls a method | `GetMethodCallers` | Full call graph, not text search |
| Manual rename across files | `RenameSymbol` | Roslyn renames — updates all references correctly |

This meant the agent was navigating the codebase **the same way a developer would** in Visual Studio — with semantic understanding, not text search.

---

## How Issues Were Handled

The workflow for most features:

1. **Create a GitHub issue** describing the feature
2. **Invoke project-coordinator agent:** `"Work on issue #37"`
3. Agent reads the issue from GitHub, plans the implementation
4. Agent delegates to the right specialist (e.g., `dotnet-backend-engineer` for confidence scoring)
5. Agent writes the code, runs the tests, opens a PR referencing the issue

**Commits like `feat: confidence scoring for cross-platform song matching (#37)` were opened as full PRs with descriptions, linked to issues, and ready for review.**

---

## What the AI Was Good At

- Implementing well-known patterns (CQRS, Repository, Decorator) correctly the first time
- Writing boilerplate quickly (entity classes, interfaces, consumers that follow the existing pattern)
- Writing tests with the right conventions (AutoMock, FluentAssertions, `ItWill*` naming)
- Keeping the CLAUDE.md up to date as architecture changed
- Remembering conventions across sessions (via memory files in `.claude/`)

---

## What Still Required Human Judgment

- Architecture decisions ("should we consolidate the Worker into the API?")
- Tuning the confidence thresholds (YouTube was too high — real data revealed this)
- Deciding what features to build and in what order
- Reviewing PRs and deciding when "good enough" was actually good enough
- Infrastructure debugging when Azure behaved unexpectedly (9 infra commits on Feb 14)

---

## The Memory System

Claude Code maintains persistent memory files in `.claude/`:
- Remembers your preferences, feedback, and project context **across sessions**
- If you tell it "don't mock the database in tests," it remembers forever
- If it tries something that fails, it saves that as feedback to avoid in future

This is what made consistent code style possible across 55+ commits over 7 weeks.

---

## The Bottom Line

> "Copilot helps you type faster. Claude Code helps you build faster."

The difference isn't speed of autocomplete — it's the scope of what you can delegate. With Claude Code, the question isn't "can the AI finish this line?" It's "can the AI take this GitHub issue and come back with a PR?"

The answer, for most of the features in this repo, was yes.
