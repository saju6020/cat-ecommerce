-- HO-RPT-001 section 8.4 (HO-SHP-001, FR-SHP-1 to 4): a SKU with its product, category, country of origin and its CURRENT price. Prices are effective-dated (HO-ADM-001 ADR-049), so the current
-- one is resolved here, at query time: the latest price whose effective_from has passed.
CREATE VIEW report.rv_sku AS
SELECT
    s."Id"                                                   AS sku_id,
    s.code                                                   AS sku_code,
    NULLIF(concat_ws(' / ', s.size_or_weight, s.flavour, s.colour), '')   AS variant_label,
    p."Id"                                                   AS product_id,
    p.name                                                   AS product_name,
    p.brand                                                  AS brand,
    p.category                                               AS category,
    p.subcategory                                            AS sub_category,
    p.country_of_origin_code                                 AS country_of_origin,
    (SELECT sp.amount FROM product.sku_price sp
      WHERE sp.sku_id = s."Id" AND sp.effective_from <= now()
      ORDER BY sp.effective_from DESC LIMIT 1)               AS current_price,
    (SELECT sp.currency_code FROM product.sku_price sp
      WHERE sp.sku_id = s."Id" AND sp.effective_from <= now()
      ORDER BY sp.effective_from DESC LIMIT 1)               AS currency_code,
    p.prescription_required                                  AS requires_vet_authorisation
FROM product.sku s
JOIN product.product p ON p."Id" = s.product_id;
