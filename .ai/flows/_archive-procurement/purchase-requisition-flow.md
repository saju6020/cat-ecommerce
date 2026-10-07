# Purchase Requisition (PR) Flow

## Status: Not Started

## What it does
Employees request procurement via requisitions (item, quantity, UOM, required date, budget code, attachment), save as draft, submit through a configurable multi-level approval workflow, amend with re-approval, and validate against available budget before approval. SRS §3.2 (FR-PR-001..005), Business Rules BR-001, BR-002.

## Key aggregates / components
- `PurchaseRequisitionAggregate` (src/Domain/Aggregates)

## Key files
-

## Decisions made
-

## Remaining work
- Create PR (draft + submit)
- Approval workflow integration (see approval-workflow-flow.md)
- PR amendment / re-approval
- Budget validation integration
