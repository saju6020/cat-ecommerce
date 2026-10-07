# Gate verdicts

One file per gate run: `<HANDOVER-ID>-<GATE>.md`, where `<GATE>` is one of
`ARCHITECTURE`, `SECURITY`, `CODE_REVIEW`, `TEST`, `ACCEPTANCE`.

Written by the gate **before** it reports. A gate that leaves no artifact did not happen, as far
as anyone downstream can tell — ACCEPTANCE refuses to certify a handover whose earlier gates left
nothing behind.

## Format

```markdown
GATE: <ARCHITECTURE|SECURITY|CODE_REVIEW|TEST|ACCEPTANCE>
HANDOVER: <id>
COMMITS: <sha list>
DATE: <YYYY-MM-DD>
STATUS: PASS | FAIL | BLOCKED

FINDINGS
- [BLOCKER|HIGH|MEDIUM|LOW] <file>:<line> — <what rule, and what the code does instead>

SCOPE CHECK
- files in allowed scope: <n>/<n>
- files outside allowed scope: <list, or "none">
```

Write the file on **every** run, including a clean PASS. A PASS with no findings is still the
record that the gate ran, and it is the cheapest file to write.
