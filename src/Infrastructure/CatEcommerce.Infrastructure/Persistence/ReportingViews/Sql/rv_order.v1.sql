-- HO-RPT-001 section 8.4 (HO-ORD-001, FR-ORD-10, FR-ORD-18): the order lifecycle instants, for the fulfilment report and the dashboard. packed_on is when packing started.
CREATE VIEW report.rv_order AS
SELECT
    o."Id"                     AS order_id,
    o.order_number             AS order_number,
    o.branch_id                AS branch_id,
    o.customer_id              AS customer_id,
    o.channel                  AS channel,
    o.status                   AS status,
    o.payment_method           AS payment_method,
    o.placed_on                AS placed_on,
    o.confirmed_on             AS confirmed_on,
    o.packing_started_on       AS packed_on,
    o.shipment_dispatched_on   AS dispatched_on,
    o.delivered_on             AS delivered_on,
    o.cancelled_on             AS cancelled_on,
    o.grand_total              AS grand_total,
    o.currency_code            AS currency_code
FROM ordering.sales_order o;
