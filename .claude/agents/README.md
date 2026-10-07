# Software Development Subagent Team

Twelve Claude Code subagents covering a dev workflow end to end, plus the two review
gates this project's handover process requires. Claude Code picks up everything in
`.claude/agents/` automatically — there is nothing to enable.

## The build team

| Agent | Role | Tools | Notes |
|---|---|---|---|
| `business-analyst` | Breaks a raw requirements doc (SRS) into user stories + UI flow specs | read/write (docs only) | Runs before `architect`, on the requirements themselves |
| `architect` | Plans system design before code is written | read-only + web | Opus, for higher-quality tradeoff reasoning |
| `backend-developer` | Implements server-side code, APIs, data layer | read/write + bash | |
| `frontend-developer` | Implements UI components and client logic | read/write + bash | |
| `code-reviewer` | Reviews diffs for correctness, security, readability | read-only + bash | Never edits — reports only |
| `test-engineer` | TEST gate. Writes `docs/test-cases/TC-<id>.md` first, then executes and updates each row's status | read/write + bash | |
| `debugger` | Root-causes and fixes bugs/failures | read/write + bash | |
| `security-reviewer` | Audits for exploitable security issues | read-only + bash | Opus, for auth/payment/data-touching changes |
| `devops-engineer` | CI/CD, Docker, deployment, infra config | read/write + bash | Never touches secrets directly |
| `docs-writer` | Keeps READMEs/API docs/comments in sync with code | read/write | |

## The gate reviewers

These exist because `docs/handover/` requires five named gates before work is accepted. Three
were already covered by the build team; these two close the remaining gap.

| Agent | Gate | Notes |
|---|---|---|
| `architecture-reviewer` | ARCHITECTURE | Opus, read-only. Checks compliance with the *approved* architecture in `.ai/` and with the handover's allowed/forbidden scope. Does not propose better architecture |
| `acceptance-reviewer` | ACCEPTANCE | Opus, read-only. Final authority. Requires concrete evidence per acceptance criterion, and that all four prior gates passed |

Note `architect` and `architecture-reviewer` are different jobs: the first designs a system before
code exists, the second judges whether written code complied. Don't substitute one for the other.

Gate mapping used by the handovers:

| Gate | Agent |
|---|---|
| 1. ARCHITECTURE | `architecture-reviewer` |
| 2. SECURITY | `security-reviewer` |
| 3. CODE_REVIEW | `code-reviewer` |
| 4. TEST | `test-engineer` |
| 5. ACCEPTANCE | `acceptance-reviewer` |

Run them in that order — architecture first, tests fourth. Each gate agent needs the handover ID, the commits, and the changed-file
list; the acceptance gate additionally needs the four prior gate results. They return `BLOCKED`
rather than guessing when input is missing.

## How it fits together

A typical flow, driven by natural delegation (just describe the task and Claude
picks the right agent) or explicit `@agent-name` mentions:

```
business-analyst      → break the SRS into user stories + UI flow specs
architect            → design the feature, produce an implementation plan
backend-developer     → implement the server-side pieces
frontend-developer    → implement the UI pieces
test-engineer         → write test cases first, then execute and mark each one
code-reviewer         → review the diff
security-reviewer     → audit if it touches auth/payments/user data
debugger              → called in if anything fails along the way
devops-engineer       → wire up CI/CD or deployment changes
docs-writer           → update docs once the feature is done
```

Example prompts:

```text
Use the architect agent to design a rate-limiting system for our API

Use the backend-developer agent to implement the plan above

Use the code-reviewer agent to review my recent changes

Have the security-reviewer agent audit the new auth endpoints before I merge

Use the test-engineer agent to add coverage for the rate limiter
```

You can also chain them in one instruction:

```text
Use the backend-developer agent to implement this, then the code-reviewer
agent to review it, then the test-engineer agent to add tests
```

## Customizing

- Every file is a plain Markdown + YAML frontmatter file — edit directly, no special tooling needed.
- Swap `model: sonnet` → `model: haiku` on any agent to cut cost for simpler routine work, or → `opus` for agents doing the highest-stakes reasoning.
- Tighten or loosen `tools:` per agent to match your risk tolerance (e.g. drop `Bash` from `frontend-developer` if you don't want it running arbitrary shell commands).
- Add `skills:` to preload team conventions (e.g. an `api-conventions` skill) into any agent's context at startup.
- Check this whole folder into version control so your team shares the same agents.

Docs: https://code.claude.com/docs/en/sub-agents
