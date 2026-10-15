-- HO-RPT-001 section 8.4 (added by the build, dashboard only): a customer return request with the branch of its order.
CREATE VIEW report.rv_order_return AS
SELECT
    r."Id"           AS return_id,
    r.order_id       AS order_id,
    o.branch_id      AS branch_id,
    r.status         AS status,
    r.requested_on   AS requested_on,
    r.received_on    AS received_on
FROM ordering.return_request r
JOIN ordering.sales_order o ON o."Id" = r.order_id;
