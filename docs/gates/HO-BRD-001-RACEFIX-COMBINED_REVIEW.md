GATE: CODE_REVIEW (combined: architecture, security, code)
HANDOVER: HO-BRD-001 (race-fix follow-up, backlog rows 49, 50, 51, 54)
COMMITS: 1043e597..26aa5f94
DATE: 2026-10-15
STATUS: PASS

FINDINGS
- [MEDIUM] BoardingCareCommandHandlers.cs:260 — the second UnitClaim caller, FlagSuspectedInfectiousCommandHandler, is not gated. Two parallel flags of different stays pick the same deterministic first free isolation unit; the loser is answered 409 although another isolation unit is free (the row-49 symptom). Bounded: staff-only, isolation units only, rare. Fix: take BoardingClaimGate.EnterAsync("unit-claim") in that handler, as Reserve does (isolation units are a different type from Reserve's, so a separate key such as "unit-claim-isolation" would avoid needless contention). Backlog row.
- [MEDIUM] BoardingClaimGate.cs:15 — WaitAsync() has no timeout and no CancellationToken. The Reserve key is one global stripe held across several DB round trips, so a slow DB call, or a burst of authenticated reserves, queues every other reserve (and every confirm/daycare key hashing to the same stripe) with no bound and no way for a caller who has disconnected to leave the queue. It is authenticated customer traffic and the critical section is short, so this is not exploitable as a DoS today, but it is an availability coupling. Fix: pass the request's token and a bounded wait (for example 5 s, then answer 503 or retry); consider keying Reserve per branch and unit type instead of globally. Backlog row.
- [LOW] BoardingClaimGate.cs:13 — the 64 stripes are shared by all key families, so "confirm:<stay>" and "daycare-slot:..." can queue behind the global "unit-claim" stripe about 1 time in 64. Harmless for correctness (a collision only serialises more); stated for the record. Key concatenation "daycare-slot:" + BranchId + LocalDate has no separator, which is also harmless because a collision only shares a stripe.
- [LOW] The gate is process-local. This is acceptable: .ai/architecture.md names a single host, no scale-out is assumed, the worker (BoardingWorkCycle) and HTTP share one process and one static gate, and the exclusion constraint and ix_daycare_slot remain the cross-process control (a loser there is still 409). No ADR is strictly required, but the "single-instance assumption for claim fairness" is worth one line in the handover or ADR log so a future scale-out revisits it.

CHECKED, NO FINDING
- Primitive: SemaphoreSlim(1,1) with await, not lock; correct for async handlers.
- Release: callers use `using var`, so the semaphore is released on exception; Releaser is idempotent (Interlocked). Cancellation cannot leak a permit because the permit is only taken after WaitAsync returns.
- Deadlock: SemaphoreSlim is non-reentrant, but none of the three gated handlers dispatches another command while holding the gate (no dispatcher injected in them; the three commands are sent only from controllers and BoardingWorkCycle). One gate per handler, never nested.
- Memory: fixed 64 semaphores, no growth. string.GetHashCode is per-process randomised but stable inside the process, which is all a process-local stripe needs.
- Pattern fit: the lock sits in the handler, which is still one aggregate per command; no dispatcher or repository convention is changed. No superseded-architecture types introduced.
- Tests: T_R1b and I_H3b are un-skipped and are real parallel tests. Baseline with the gate: both pass. With the gate mutated to a no-op (no WaitAsync, no Release): T_R1b failed in 2 of 2 runs ("200/409" in 4 and 5 of 5 rounds); I_H3b failed ("Expected 2, Actual 1"). Gate restored with git checkout, git status clean (only untracked scratchpad/). Row 54 (confirm race) and row 51 (U_G24 rename) have no mutation evidence here; the rename is name-only.
- Ran against catecommerce_brd with the full connection string; no full suite.

SCOPE CHECK
- files in allowed scope: 7/7 (Boarding handlers, gate, two tests, backlog)
- files outside allowed scope: none
