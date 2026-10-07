# Flows

End-to-end business flows for this project, derived from `docs/SRS.md`. Each names the modules
involved, the aggregates, the invariants that must hold, and the cross-module rules.

| Flow | SRS | Covers |
|---|---|---|
| `clinic-visit-flow.md` | §4.3 | Booking, triage, vitals, SOAP consultation, diagnostics, discharge |
| `surgery-flow.md` | §4.4 | Spay/neuter, consent gate, anaesthesia log, controlled drugs, post-op |
| `vaccination-and-reminders-flow.md` | §4.2, §4.12 | Vaccination records, due status, reminders |
| `boarding-stay-flow.md` | §4.6 | Availability, vaccination gate, care tasks, incidents, check-out |
| `order-fulfilment-flow.md` | §4.9 | Cart, prescription gate, FEFO allocation, dispatch, returns |
| `import-to-shelf-flow.md` | §4.8 | Consignments, batches, expiry lifecycle, landed cost |
| `batch-recall-flow.md` | FR-IMP-11 | Quarantine and trace across all three consumption paths |
| `prescription-diet-flow.md` | BR-2 | The reference cross-module feature |

## Read `prescription-diet-flow.md` first

It is the shortest flow and the clearest illustration of how modules cooperate without crossing
boundaries: ownership decided by who creates the fact, a contract call for the query, an
integration event for the reaction.

## Archived

`_archive-procurement/` holds eight flow documents from a different project (`Bits.Procurement`)
built on the superseded architecture. They describe nothing in this system. See the README there.
