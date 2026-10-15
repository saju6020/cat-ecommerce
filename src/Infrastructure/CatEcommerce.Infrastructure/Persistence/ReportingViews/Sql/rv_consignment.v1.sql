-- HO-RPT-001 section 8.4 (HO-IMP-001, FR-IMP-3): an import consignment. The clearance documents are present when their NUMBER is recorded -- the same test HO-IMP-001 applies before it lets a
-- consignment clear. landed_cost_total is NULL until the costs have been allocated across the batches (it is not guessed from the purchase orders).
CREATE VIEW report.rv_consignment AS
SELECT
    c."Id"                                   AS consignment_id,
    c.reference                              AS reference,
    c.shipment_reference                     AS shipment_reference,
    c.branch_id                              AS branch_id,
    c.origin_country_code                    AS origin_country,
    (SELECT string_agg(DISTINCT s.name, ', ' ORDER BY s.name)
       FROM inventory.purchase_order po
       JOIN inventory.supplier s ON s."Id" = po.supplier_id
      WHERE po."Id" = ANY (c.purchase_order_ids))   AS supplier_name,
    cardinality(c.purchase_order_ids)        AS po_count,
    c.status                                 AS clearance_status,
    (SELECT sum(l.quantity * l.landed_unit_cost_base)
       FROM inventory.consignment_landed_cost_line l
      WHERE l.consignment_id = c."Id")       AS landed_cost_total,
    (c.import_permit_number IS NOT NULL AND btrim(c.import_permit_number) <> '')               AS has_permit,
    (c.health_certificate_number IS NOT NULL AND btrim(c.health_certificate_number) <> '')     AS has_health_certificate,
    (c.customs_declaration_number IS NOT NULL AND btrim(c.customs_declaration_number) <> '')   AS has_customs_declaration,
    (SELECT count(*)::int FROM inventory.batch b WHERE b.consignment_id = c."Id")              AS batches_received,
    c."CreatedDate"                          AS created_on,
    c.cleared_on                             AS cleared_on
FROM inventory.consignment c;
