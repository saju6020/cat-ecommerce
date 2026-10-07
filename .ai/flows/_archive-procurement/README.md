# Archived — procurement flows from Bits.Procurement

These eight flow documents describe a **different project** (`Bits.Procurement`: vendors,
purchase requisitions, RFQs, purchase orders, goods receipt, invoice verification, contracts and
approval workflows) built on the **superseded architecture** — event-sourced CQRS over RabbitMQ
and MongoDB with three hosts.

They are kept here only as a reference for how a flow document is structured and how much
implementation detail is worth recording. **Nothing in them describes this system.**

Do not follow them for:
- architecture (see `.ai/architecture.md` — modular monolith, in-memory CQRS, PostgreSQL)
- the command path (no `IBusMessageDispatcher`, no `202 Accepted`, no rejection events)
- the read path (no projections, no `IReadRepository`)
- persistence (no MongoDB, no `IAggregateRootRepository<T>`)

The current flows for this project are in the parent folder.
