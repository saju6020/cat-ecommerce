# Approval Workflow Flow

## Status: Planned — HO-WF-001 approved, not yet implemented

## What it does

The shared approval engine consumed by Purchase Requisition (FR-PR-003), Purchase Order Management
(FR-PO-005) and Contract Procurement (FR-CT-001). A System Admin configures workflows — ordered
steps, each naming an approver role, each optionally conditional on amount / department / category /
location — and a document is routed through the steps its attributes select. SRS §3.7
(FR-WF-001..004).

## Key aggregates / components

- **`WorkflowDefinitionAggregate`** — admin configuration. Long-lived, revised occasionally.
- **`ApprovalRequestAggregate`** — one running approval. Short-lived, decided once.
- **`WorkflowStepResolver`** — pure domain service, no I/O. Turns a definition's steps plus a
  document's attributes into the frozen chain that request will walk.

Two aggregates rather than one because their lifecycles are unrelated: a single aggregate would make
every configuration edit a write against every in-flight approval.

## The one thing to understand before touching this

**An approval request snapshots its resolved steps at the moment it starts, and never reads the
definition again.**

That is what implements stakeholder decision #26 — *a document mid-approval finishes under the
definition that was active when it started.* The request does not hold a definition id it might later
dereference, and does not hold a version number to match; it holds the actual steps, carried on
`ApprovalRequestStartedEvent`.

Two consequences follow, and both are easy to break by accident:

1. **Conditions are evaluated once, at start.** Evaluating them lazily as each step is reached would
   re-read the live definition and violate #26 silently — the approval would finish under whatever
   the rules say *now*, not what they said when it began.
2. **Revising a definition needs no coordination with in-flight requests.** There is nothing to
   migrate and no version to reconcile. If you find yourself writing that code, the snapshot has been
   lost somewhere.

## Key files

Not yet implemented. HO-WF-001 §11 and §13 name every file to be created.

- Plans: `docs/plans/approval-workflow/configure-dynamic-approval-workflow.md`,
  `docs/plans/approval-workflow/conditional-approval-routing.md`
- Handover: `docs/handover/HO-WF-001-approval-workflow-engine.md`

## Decisions made

- **#23** — approval thresholds and escalation SLAs are admin-configurable, and FR-WF-002 is the
  single place threshold configuration lives. No PR, PO or Contract file defines a threshold.
- **#24** — delegation is date-bounded with auto-expiry (FR-WF-003, not in HO-WF-001).
- **#25** — escalation goes to the next level up the same chain, not a separate target (FR-WF-004,
  not in HO-WF-001).
- **#26** — a definition change does not affect in-flight approvals. See above.
- **Architect** — decision history is a child collection (ADR-006); the request carries a bounded
  applied-id ring so a redelivered decision is success, not a violation (ADR-007); step authorization
  lives in the command handler rather than `RequireActionAttribute`, because the required role is
  per-request state the Api boundary cannot see — a deliberate departure from ADR-009, to be recorded
  as **ADR-010** when HO-WF-001 lands.

## Remaining work

- HO-WF-001 — the backend engine: both aggregates, the resolver, commands, projections, two
  controllers.
- HO-WF-002 — the Workflow Configuration screen, including the per-step condition builder.
- FR-WF-003 Delegation and FR-WF-004 Escalation — their own plans, deliberately deferred until the
  core lifecycle has run. Escalation additionally needs a scheduler this solution does not have.

## Known constraints

- **The engine ships with no consumer.** PR, PO and Contract do not exist. The integration contract
  is deliberately document-agnostic — a string `DocumentType`, a `Guid DocumentId`, and an
  `ApprovalAttributes` bag — so a consumer added later supplies its own values without the engine
  changing.
- **D-PR-01 blocks the consumer, not the engine.** The amount arrives as an input attribute; the
  engine never owns one. Do not add an amount field to `PurchaseRequisitionAggregate` to make the
  amount conditions demonstrable.
- **`Department`, `Category` and `Location` have no source entity** anywhere in this solution. They
  are opaque nullable strings by decision, and a null attribute never satisfies a condition.
