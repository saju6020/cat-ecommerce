import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  COUNTRY_NAMES,
  badRequest,
  shopFixtureIds,
  type MockProduct,
  type MockSku,
} from './fixtures';
import { db, findById, findSku, money, persist } from './store';

/**
 * `mocks/handlers/shop/staff.ts` — HO-SHP-002 §12. The staff endpoints of HO-SHP-001 §12.4.
 * Mirrors the REAL staff detail read, including food, images, health focus and the SKU label
 * (HO-SHP-001-FU1, backlog S1). Publication rules are the server's (§8.4), duplicated here only so the mock refuses the
 * same way. Role enforcement is not modelled (HO-ADM-002 precedent): a `403` a test needs comes
 * from `server.use(...)`.
 */

const noContent = () => new HttpResponse(null, { status: 204 });
/** HO-SHP-001-FU3: absent/null = not set; otherwise a whole number 1..100000 (else `InvalidShippingWeight`). */
function shippingWeightRefusal(value: unknown): Response | null {
  if (value === undefined || value === null) return null;
  if (typeof value === 'number' && Number.isInteger(value) && value >= 1 && value <= 100000)
    return null;
  return badRequest(
    'ShippingWeightGrams',
    'InvalidShippingWeight',
    'Enter a shipping weight between 1 and 100000 g.',
  );
}

const notFound = () => new HttpResponse(null, { status: 404 });

const label = (s: MockSku) =>
  [s.code, s.sizeOrWeight, s.flavour, s.colour].filter(Boolean).join(' · ');

function conflict(p: MockProduct, expected: number) {
  return HttpResponse.json(
    {
      errors: [
        {
          propertyName: 'ExpectedVersion',
          errorCode: 'ProductVersionConflict',
          errorMessage: `Expected version ${expected} but the product is at ${p.version}.`,
          resourceName: 'Product',
        },
      ],
      isValid: false,
    },
    { status: 409 },
  );
}

/** Runs a product write: 404 / 409 / mutation / version bump / persist. */
async function write(
  productId: string,
  request: Request,
  apply: (p: MockProduct, body: Record<string, unknown>) => Response | void,
): Promise<Response> {
  const p = findById(productId);
  if (!p) return notFound();
  const body = ((await request.json().catch(() => ({}))) ?? {}) as Record<string, unknown>;
  const expected = Number(body.expectedVersion);
  if (expected !== p.version) return conflict(p, expected);
  const refusal = apply(p, body);
  if (refusal) return refusal;
  p.version += 1;
  persist();
  return noContent();
}

export function checklistFor(p: MockProduct) {
  const active = p.skus.filter((s) => s.status === 'Active');
  const unpriced = active.filter((s) => !s.price).map((s) => s.code);
  const isFood = p.category === 'CatFood' && p.kind === 'Standard';
  const items = [
    {
      item: 'ActiveSku',
      isBlocking: true,
      isMet: active.length > 0,
      message: active.length > 0 ? 'At least one active SKU.' : 'Add at least one active SKU.',
    },
    {
      item: 'PricedSkus',
      isBlocking: true,
      isMet: unpriced.length === 0,
      message:
        unpriced.length === 0
          ? 'Every active SKU has a price.'
          : `Set a price for SKU ${unpriced.join(', ')}.`,
    },
    { item: 'TaxClassActive', isBlocking: true, isMet: true, message: 'The tax class is active.' },
  ];
  if (isFood) {
    items.push(
      {
        item: 'CountryOfOrigin',
        isBlocking: true,
        isMet: !!p.country,
        message: p.country
          ? 'Country of origin is set.'
          : 'Add the country of origin before publishing this food product.',
      },
      {
        item: 'FoodDetails',
        isBlocking: true,
        isMet: !!p.lifeStage,
        message: p.lifeStage
          ? 'Food details are set.'
          : 'Add the food details, including the life stage.',
      },
    );
  }
  if (p.kind === 'Bundle') {
    const units = (p.bundle ?? []).reduce((sum, c) => sum + c.quantity, 0);
    items.push({
      item: 'BundleComposition',
      isBlocking: true,
      isMet: units >= 2,
      message:
        units >= 2 ? 'The kit has enough components.' : 'A kit needs at least 2 component units.',
    });
  }
  items.push({
    item: 'PrimaryImage',
    isBlocking: false,
    isMet: false,
    message: 'Products without a photo convert poorly.',
  });
  if (isFood) {
    const complete = !!(
      p.manufacturer &&
      p.ingredientList &&
      p.analysis &&
      p.feedingGuide &&
      p.storageInstructions
    );
    items.push({
      item: 'LabellingComplete',
      isBlocking: false,
      isMet: complete,
      message: complete
        ? 'Labelling is complete.'
        : 'Add the manufacturer, ingredient list, analysis, feeding guide and storage instructions.',
    });
  }
  return items;
}

/** As the real `Sku.LabelFor`: the axes joined with " · ", the product name only when none is set. */
const skuLabelFor = (p: MockProduct, s: MockSku) => {
  const axes = [
    s.sizeOrWeight,
    s.flavour,
    s.colour,
    s.packQuantity !== null ? `Pack of ${s.packQuantity}` : null,
  ].filter((x): x is string => !!x && x.trim() !== '');
  return axes.length > 0 ? axes.join(' · ') : p.name;
};

/** `null` when no food details were ever set — as the real read. */
function staffFood(p: MockProduct) {
  const any =
    p.manufacturer ||
    p.lifeStage ||
    p.protein ||
    p.ingredientList ||
    p.analysis ||
    p.feedingGuide ||
    p.storageInstructions;
  if (!any) return null;
  return {
    manufacturer: p.manufacturer,
    lifeStage: p.lifeStage,
    proteinSource: p.protein,
    ingredientList: p.ingredientList,
    guaranteedAnalysis: p.analysis,
    feedingGuide: p.feedingGuide,
    storageInstructions: p.storageInstructions,
  };
}

function staffDetail(p: MockProduct) {
  return {
    productId: p.productId,
    version: p.version,
    kind: p.kind,
    name: p.name,
    brand: p.brand,
    description: p.description,
    category: p.category,
    subcategory: p.subcategory,
    tags: p.tags,
    slug: p.slug,
    status: p.status,
    firstPublishedOn: p.firstPublishedOn,
    taxClassId: p.taxClassId,
    countryOfOriginCode: p.country,
    prescriptionRequired: p.prescriptionRequired,
    isExpiryTracked: p.isExpiryTracked,
    listedOnStorefront: p.listed,
    skus: p.skus.map((s) => ({
      skuId: s.skuId,
      code: s.code,
      barcode: s.barcode,
      sizeOrWeight: s.sizeOrWeight,
      flavour: s.flavour,
      colour: s.colour,
      packQuantity: s.packQuantity,
      shippingWeightGrams: s.shippingWeightGrams ?? null,
      label: skuLabelFor(p, s),
      status: s.status,
      currentPrice: s.price ? money(s.price) : null,
      scheduledPrice: s.scheduledPrice ? money(s.scheduledPrice.amount) : null,
      // `null` = never reported (the unfinished draft); a real zero is `sellableQuantity: 0`.
      stock:
        s.code.startsWith('UNMET') || s.code.startsWith('DRAFT')
          ? null
          : s.inStock
            ? {
                sellableQuantity: 12,
                nextAllocatableExpiryOn: s.expiresOn,
                asOf: '2026-10-04T04:42:00Z',
              }
            : { sellableQuantity: 0, nextAllocatableExpiryOn: null, asOf: '2026-10-04T04:42:00Z' },
    })),
    publishChecklist: checklistFor(p),
    bundleComponents: (p.bundle ?? []).map((c) => {
      const found = findSku(c.skuId);
      return {
        componentSkuId: c.skuId,
        skuCode: found?.sku.code ?? null,
        productName: found?.product.name ?? null,
        quantity: c.quantity,
        isOnSale: !!found && found.sku.status === 'Active' && found.product.status === 'Active',
      };
    }),
    relatedProducts: p.related.map((r) => {
      const target = db.products.find((x) => x.slug === r.slug);
      return {
        relatedProductId: target?.productId ?? '',
        kind: r.kind,
        sortOrder: r.sortOrder,
        name: target?.name ?? null,
        status: target?.status ?? null,
      };
    }),
    translations: p.translations,
    food: staffFood(p),
    images: [...p.images]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((i) => ({ ...i, url: null })),
    healthFocus: p.healthFocus,
  };
}

function list(url: URL) {
  const sp = url.searchParams;
  const q = (sp.get('q') ?? '').toLowerCase();
  let items = db.products.filter((p) => {
    if (
      q &&
      !`${p.name} ${p.brand} ${p.skus.map((s) => s.code).join(' ')}`.toLowerCase().includes(q)
    )
      return false;
    if (sp.get('status') && p.status !== sp.get('status')) return false;
    if (sp.get('category') && p.category !== sp.get('category')) return false;
    if (sp.get('brand') && p.brand !== sp.get('brand')) return false;
    if (sp.get('kind') && p.kind !== sp.get('kind')) return false;
    if (sp.get('listed') && String(p.listed) !== sp.get('listed')) return false;
    if (sp.get('barcode') && !p.skus.some((s) => s.barcode === sp.get('barcode'))) return false;
    return true;
  });
  items = [...items].sort((a, b) =>
    sp.get('sort') === 'name_desc' ? b.name.localeCompare(a.name) : a.name.localeCompare(b.name),
  );
  const pageSize = Math.min(Number(sp.get('pageSize') ?? '25') || 25, 100);
  const pageNumber = Math.max(1, Number(sp.get('pageNumber') ?? '1') || 1);
  return {
    items: items.slice((pageNumber - 1) * pageSize, pageNumber * pageSize).map((p) => ({
      productId: p.productId,
      name: p.name,
      brand: p.brand,
      category: p.category,
      status: p.status,
      kind: p.kind,
      skuCount: p.skus.length,
      listedOnStorefront: p.listed,
      lastUpdatedOn: '2026-10-03T10:00:00Z',
    })),
    pageNumber,
    pageSize,
    totalCount: items.length,
  };
}

const guard = (cell: string) =>
  `"${(/^\s*[=+\-@]/.test(cell) ? `'${cell}` : cell).replaceAll('"', '""')}"`;

export const staffHandlers: HttpHandler[] = [
  http.get('*/api/staff/shop/products/export.csv', ({ request }) => {
    const { items } = list(new URL(request.url));
    const rows = items.map((i) =>
      [
        i.productId,
        i.name,
        i.brand,
        i.category,
        i.status,
        i.kind,
        String(i.skuCount),
        String(i.listedOnStorefront),
        i.lastUpdatedOn,
      ]
        .map(guard)
        .join(','),
    );
    return new HttpResponse(
      [
        'ProductId,Name,Brand,Category,Status,Kind,SkuCount,ListedOnStorefront,LastUpdatedOn',
        ...rows,
      ].join('\n'),
      {
        headers: { 'Content-Type': 'text/csv' },
      },
    );
  }),

  http.get('*/api/staff/shop/products', ({ request }) =>
    HttpResponse.json(list(new URL(request.url))),
  ),

  http.post('*/api/staff/shop/products', async ({ request }) => {
    const body = (await request.json()) as {
      kind: 'Standard' | 'Bundle';
      name: string;
      brand: string;
      category: string;
      subcategory?: string | null;
      taxClassId: string;
      slug: string;
      bundleSkuCode?: string | null;
    };
    if (db.products.some((p) => p.slug === body.slug)) {
      return badRequest('Slug', 'SlugAlreadyInUse', 'This slug is already in use.');
    }
    const productId = crypto.randomUUID();
    db.products.push({
      productId,
      slug: body.slug,
      version: 1,
      name: body.name,
      brand: body.brand,
      description: '',
      category: body.category,
      subcategory: body.subcategory ?? null,
      kind: body.kind,
      status: 'Draft',
      listed: true,
      country: null,
      lifeStage: null,
      protein: null,
      prescriptionRequired: false,
      tags: [],
      ingredientList: null,
      manufacturer: null,
      analysis: null,
      feedingGuide: null,
      storageInstructions: null,
      healthFocus: [],
      images: [],
      skus:
        body.kind === 'Bundle'
          ? [
              {
                skuId: crypto.randomUUID(),
                code: body.bundleSkuCode ?? 'KIT',
                barcode: null,
                sizeOrWeight: 'Kit',
                flavour: null,
                colour: null,
                packQuantity: null,
                shippingWeightGrams: null,
                status: 'Active',
                price: null,
                scheduledPrice: null,
                inStock: false,
                expiresOn: null,
              },
            ]
          : [],
      ratingAverage: null,
      ratingCount: 0,
      createdAt: new Date().toISOString(),
      sold: 0,
      bundle: body.kind === 'Bundle' ? [] : null,
      related: [],
      translations: [],
      taxClassId: body.taxClassId,
      isExpiryTracked: body.category === 'CatFood',
      firstPublishedOn: null,
    });
    persist();
    return HttpResponse.json({ productId }, { status: 201 });
  }),

  http.get('*/api/staff/shop/products/:productId', ({ params }) => {
    const p = findById(String(params.productId));
    return p ? HttpResponse.json(staffDetail(p)) : notFound();
  }),

  http.put('*/api/staff/shop/products/:productId', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      if (!String(b.name ?? '').trim())
        return badRequest('Name', 'InvalidProductName', 'Enter a product name.');
      Object.assign(p, {
        name: b.name,
        brand: b.brand,
        description: (b.description as string | null) ?? '',
        subcategory: (b.subcategory as string | null) ?? null,
        tags: (b.tags as string[]) ?? [],
        country: (b.countryOfOriginCode as string | null) ?? null,
        listed: b.listedOnStorefront !== false,
        isExpiryTracked: p.category === 'CatFood' ? true : Boolean(b.isExpiryTracked),
        taxClassId: b.taxClassId,
        healthFocus: (b.healthFocus as string[] | undefined) ?? [],
      });
      if (b.category && b.category !== p.category && p.status === 'Active') {
        return badRequest(
          'Category',
          'CategoryLockedWhileActive',
          'The category can’t change while the product is active.',
        );
      }
    }),
  ),

  http.put('*/api/staff/shop/products/:productId/slug', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      if (p.firstPublishedOn)
        return badRequest(
          'Slug',
          'SlugLockedAfterPublication',
          'The slug can’t change once the product has been published.',
        );
      if (db.products.some((x) => x.slug === b.slug && x.productId !== p.productId)) {
        return badRequest('Slug', 'SlugAlreadyInUse', 'This slug is already in use.');
      }
      p.slug = String(b.slug);
    }),
  ),

  http.put('*/api/staff/shop/products/:productId/food', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      const analysis = b.guaranteedAnalysis as {
        proteinPercent: number;
        fatPercent: number;
        fibrePercent: number;
        moisturePercent: number;
      } | null;
      if (analysis && Object.values(analysis).some((v) => v < 0 || v > 100)) {
        return badRequest(
          'GuaranteedAnalysis',
          'InvalidGuaranteedAnalysis',
          'Each analysis value must be between 0 and 100 %.',
        );
      }
      Object.assign(p, {
        manufacturer: b.manufacturer ?? null,
        lifeStage: b.lifeStage ?? null,
        protein: b.proteinSource ?? null,
        ingredientList: b.ingredientList ?? null,
        analysis: analysis ?? null,
        feedingGuide: b.feedingGuide ?? null,
        storageInstructions: b.storageInstructions ?? null,
      });
    }),
  ),

  http.put('*/api/staff/shop/products/:productId/prescription', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      if (p.category !== 'CatFood') {
        return badRequest(
          'PrescriptionRequired',
          'PrescriptionOnlyForFood',
          'Only food can require a prescription.',
        );
      }
      p.prescriptionRequired = Boolean(b.prescriptionRequired);
    }),
  ),

  http.put('*/api/staff/shop/products/:productId/images', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      const images = (b.images as MockProduct['images']) ?? [];
      if (images.some((i) => !i.altText?.trim())) {
        return badRequest('AltText', 'ImageAltTextRequired', 'Every image needs alt text.');
      }
      p.images = images.map((i) => ({
        fileId: i.fileId,
        altText: i.altText,
        sortOrder: i.sortOrder,
        isPrimary: i.isPrimary,
        skuId: i.skuId ?? null,
      }));
    }),
  ),

  http.put('*/api/staff/shop/products/:productId/bundle-components', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      if (p.kind !== 'Bundle')
        return badRequest(
          'components',
          'InvalidBundleComponent',
          'Only a bundle product has components.',
        );
      const components =
        (b.components as Array<{ componentSkuId: string; quantity: number }>) ?? [];
      for (const c of components) {
        const found = findSku(c.componentSkuId);
        if (!found)
          return badRequest(
            'components',
            'InvalidBundleComponent',
            `SKU '${c.componentSkuId}' was not found.`,
          );
        if (found.product.kind === 'Bundle')
          return badRequest(
            'components',
            'InvalidBundleComponent',
            `SKU ${found.sku.code} is a bundle and cannot be a component.`,
          );
        if (found.product.prescriptionRequired)
          return badRequest(
            'components',
            'InvalidBundleComponent',
            `SKU ${found.sku.code} is a prescription product and cannot be bundled.`,
          );
        if (found.sku.status !== 'Active')
          return badRequest(
            'components',
            'InvalidBundleComponent',
            `SKU ${found.sku.code} is discontinued.`,
          );
      }
      p.bundle = components.map((c) => ({
        productSlug: findSku(c.componentSkuId)!.product.slug,
        skuId: c.componentSkuId,
        quantity: c.quantity,
      }));
    }),
  ),

  http.put('*/api/staff/shop/products/:productId/related-products', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      const links =
        (b.links as Array<{
          relatedProductId: string;
          kind: 'FrequentlyBoughtTogether' | 'UpSell';
          sortOrder: number;
        }>) ?? [];
      const mapped: MockProduct['related'] = [];
      for (const link of links) {
        const target = findById(link.relatedProductId);
        if (!target)
          return badRequest(
            'RelatedProductId',
            'RelatedProductInvalid',
            `Product '${link.relatedProductId}' was not found.`,
          );
        mapped.push({ slug: target.slug, kind: link.kind, sortOrder: link.sortOrder });
      }
      p.related = mapped;
    }),
  ),

  http.put('*/api/staff/shop/products/:productId/translations/:locale', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      const locale = String(params.locale);
      if (locale === 'en')
        return badRequest(
          'Locale',
          'DefaultLocaleNotTranslatable',
          'The default language can’t be translated.',
        );
      if (locale !== 'bn')
        return badRequest('Locale', 'LocaleNotEnabled', 'That language is not enabled.');
      const fields = {
        name: (b.name as string) || null,
        description: (b.description as string) || null,
        ingredientList: (b.ingredientList as string) || null,
        feedingGuide: (b.feedingGuide as string) || null,
      };
      p.translations = p.translations.filter((t) => t.locale !== locale);
      if (Object.values(fields).some(Boolean)) p.translations.push({ locale, ...fields });
    }),
  ),

  http.delete(
    '*/api/staff/shop/products/:productId/translations/:locale',
    ({ params, request }) => {
      const p = findById(String(params.productId));
      if (!p) return notFound();
      const expected = Number(new URL(request.url).searchParams.get('expectedVersion'));
      if (expected !== p.version) return conflict(p, expected);
      p.translations = p.translations.filter((t) => t.locale !== params.locale);
      p.version += 1;
      persist();
      return noContent();
    },
  ),

  http.post('*/api/staff/shop/products/:productId/skus', async ({ params, request }) => {
    const skuId = crypto.randomUUID();
    const response = await write(String(params.productId), request, (p, b) => {
      if (p.skus.some((s) => s.code === b.code))
        return badRequest('Code', 'SkuCodeAlreadyInUse', 'This SKU code is already in use.');
      const weightRefusal = shippingWeightRefusal(b.shippingWeightGrams);
      if (weightRefusal) return weightRefusal;
      if (!b.sizeOrWeight && !b.flavour && !b.colour) {
        return badRequest(
          'SizeOrWeight',
          'VariantAttributesRequired',
          'Add a size or weight, a flavour or a colour.',
        );
      }
      p.skus.push({
        skuId,
        code: String(b.code),
        barcode: (b.barcode as string | null) ?? null,
        sizeOrWeight: (b.sizeOrWeight as string | null) ?? null,
        flavour: (b.flavour as string | null) ?? null,
        colour: (b.colour as string | null) ?? null,
        packQuantity: (b.packQuantity as number | null) ?? null,
        shippingWeightGrams: (b.shippingWeightGrams as number | null) ?? null,
        status: 'Active',
        price: null,
        scheduledPrice: null,
        inStock: false,
        expiresOn: null,
      });
    });
    return response.status === 204 ? HttpResponse.json({ skuId }, { status: 201 }) : response;
  }),

  http.put('*/api/staff/shop/products/:productId/skus/:skuId', ({ params, request }) =>
    write(String(params.productId), request, (p, b) => {
      const s = p.skus.find((x) => x.skuId === params.skuId);
      if (!s) return notFound();
      const weightRefusal = shippingWeightRefusal(b.shippingWeightGrams);
      if (weightRefusal) return weightRefusal;
      Object.assign(s, {
        sizeOrWeight: b.sizeOrWeight ?? null,
        flavour: b.flavour ?? null,
        colour: b.colour ?? null,
        packQuantity: b.packQuantity ?? null,
        // As the real API: omitting the field CLEARS it.
        shippingWeightGrams: b.shippingWeightGrams ?? null,
      });
    }),
  ),

  http.post('*/api/staff/shop/products/:productId/skus/:skuId/discontinue', ({ params, request }) =>
    write(String(params.productId), request, (p) => {
      const s = p.skus.find((x) => x.skuId === params.skuId);
      if (!s) return notFound();
      if (p.skus.filter((x) => x.status === 'Active').length <= 1) {
        return badRequest(
          'SkuId',
          'LastActiveSkuCannotBeDiscontinued',
          'An active product needs at least one active SKU.',
        );
      }
      s.status = 'Discontinued';
    }),
  ),

  http.post('*/api/staff/shop/products/:productId/skus/:skuId/reactivate', ({ params, request }) =>
    write(String(params.productId), request, (p) => {
      const s = p.skus.find((x) => x.skuId === params.skuId);
      if (!s) return notFound();
      s.status = 'Active';
    }),
  ),

  http.post('*/api/staff/shop/products/:productId/publish', ({ params, request }) =>
    write(String(params.productId), request, (p) => {
      const unmet = checklistFor(p).filter((c) => c.isBlocking && !c.isMet);
      if (unmet.length > 0) {
        return badRequest(
          'ProductId',
          'ProductNotPublishable',
          `This product can't be published yet. ${unmet.map((u) => u.message).join(' ')}`,
        );
      }
      p.status = 'Active';
      p.firstPublishedOn = p.firstPublishedOn ?? new Date().toISOString();
    }),
  ),

  http.post('*/api/staff/shop/products/:productId/unpublish', ({ params, request }) =>
    write(String(params.productId), request, (p) => {
      if (p.status !== 'Active')
        return badRequest(
          'ProductId',
          'InvalidProductTransition',
          `A ${p.status} product can’t be unpublished.`,
        );
      p.status = 'Draft';
    }),
  ),

  http.post('*/api/staff/shop/products/:productId/archive', ({ params, request }) =>
    write(String(params.productId), request, (p) => {
      if (p.status === 'Archived')
        return badRequest(
          'ProductId',
          'InvalidProductTransition',
          'The product is already archived.',
        );
      p.status = 'Archived';
    }),
  ),

  http.get('*/api/staff/shop/skus/:skuId/prices', ({ params }) => {
    const found = findSku(String(params.skuId));
    if (!found) return notFound();
    const { sku } = found;
    const entry = (amount: string, from: string) => ({
      skuPriceId: crypto.randomUUID(),
      amount: money(amount),
      effectiveFrom: from,
      setBy: shopFixtureIds.customerId,
    });
    return HttpResponse.json({
      current: sku.price ? entry(sku.price, '2026-06-01T00:00:00Z') : null,
      scheduled: sku.scheduledPrice
        ? [entry(sku.scheduledPrice.amount, sku.scheduledPrice.effectiveFrom)]
        : [],
      past: sku.price
        ? [entry(String(Math.round(Number(sku.price) * 0.9)) + '.00', '2026-01-01T00:00:00Z')]
        : [],
    });
  }),

  http.post('*/api/staff/shop/skus/:skuId/prices', async ({ params, request }) => {
    const found = findSku(String(params.skuId));
    if (!found) return notFound();
    const body = (await request.json()) as {
      amount: number;
      currencyCode: string;
      effectiveFrom: string;
    };
    if (!(body.amount > 0))
      return badRequest(
        'Amount',
        'PriceMustBePositive',
        'The price must be more than zero.',
        'SkuPrice',
      );
    // A tolerance for "Now" (the client sends its current instant), same as a real clock skew allowance.
    if (new Date(body.effectiveFrom).getTime() < Date.now() - 5 * 60_000) {
      return badRequest(
        'EffectiveFrom',
        'PriceCannotBeBackdated',
        'A new price can’t start in the past.',
        'SkuPrice',
      );
    }
    const amount = body.amount.toFixed(2);
    if (new Date(body.effectiveFrom).getTime() <= Date.now() + 60_000) found.sku.price = amount;
    else found.sku.scheduledPrice = { amount, effectiveFrom: body.effectiveFrom };
    persist();
    return HttpResponse.json({ skuPriceId: crypto.randomUUID() }, { status: 201 });
  }),

  http.get('*/api/staff/shop/skus', ({ request }) => {
    const barcode = new URL(request.url).searchParams.get('barcode');
    if (!barcode) return HttpResponse.json({ error: 'barcode is required.' }, { status: 400 });
    for (const product of db.products) {
      const sku = product.skus.find((s) => s.barcode === barcode);
      if (sku) {
        return HttpResponse.json({
          skuId: sku.skuId,
          code: sku.code,
          label: label(sku),
          productId: product.productId,
          productName: product.name,
          status: sku.status,
        });
      }
    }
    return notFound();
  }),

  http.get('*/api/staff/shop/countries', () =>
    HttpResponse.json(Object.entries(COUNTRY_NAMES).map(([code, name]) => ({ code, name }))),
  ),

  http.get('*/api/staff/shop/origins', () =>
    HttpResponse.json(
      db.origins.map((o) => ({
        countryCode: o.code,
        countryName: COUNTRY_NAMES[o.code] ?? o.code,
        sourcingNotes: o.sourcingNotes,
        isFeatured: o.isFeatured,
        sortOrder: o.sortOrder,
        productCount: db.products.filter((p) => p.country === o.code && p.status === 'Active')
          .length,
      })),
    ),
  ),

  http.post('*/api/staff/shop/origins', async ({ request }) => {
    const body = (await request.json()) as {
      countryCode: string;
      sourcingNotes?: string | null;
      isFeatured: boolean;
      sortOrder: number;
    };
    const code = body.countryCode.toUpperCase();
    if (!COUNTRY_NAMES[code])
      return badRequest(
        'CountryCode',
        'InvalidCountryCode',
        'Choose a valid country.',
        'SourcingOrigin',
      );
    if (db.origins.some((o) => o.code === code)) {
      return badRequest(
        'CountryCode',
        'OriginAlreadyExists',
        'An origin already exists for this country.',
        'SourcingOrigin',
      );
    }
    db.origins.push({
      code,
      sourcingNotes: body.sourcingNotes ?? null,
      isFeatured: body.isFeatured,
      sortOrder: body.sortOrder,
    });
    persist();
    return HttpResponse.json({ countryCode: code }, { status: 201 });
  }),

  http.put('*/api/staff/shop/origins/:countryCode', async ({ params, request }) => {
    const origin = db.origins.find((o) => o.code === String(params.countryCode).toUpperCase());
    if (!origin) return notFound();
    const body = (await request.json()) as {
      sourcingNotes?: string | null;
      isFeatured: boolean;
      sortOrder: number;
    };
    Object.assign(origin, {
      sourcingNotes: body.sourcingNotes ?? null,
      isFeatured: body.isFeatured,
      sortOrder: body.sortOrder,
    });
    persist();
    return noContent();
  }),

  http.get('*/api/staff/shop/reviews', ({ request }) => {
    const sp = new URL(request.url).searchParams;
    const pageNumber = Math.max(1, Number(sp.get('pageNumber') ?? '1') || 1);
    const order = { PendingModeration: 0, Published: 1, Rejected: 2, Hidden: 3 } as const;
    const filtered = db.reviews
      .filter(
        (r) =>
          (!sp.get('status') || r.status === sp.get('status')) &&
          (!sp.get('productId') || r.productId === sp.get('productId')),
      )
      .sort(
        (a, b) => order[a.status] - order[b.status] || b.submittedOn.localeCompare(a.submittedOn),
      );
    return HttpResponse.json({
      items: filtered.slice((pageNumber - 1) * 25, pageNumber * 25).map((r) => ({
        reviewId: r.reviewId,
        productId: r.productId,
        productName: r.productName,
        skuLabel: r.skuLabel,
        rating: r.rating,
        title: r.title,
        body: r.body,
        customerDisplayName: r.mine ? 'Signed-in customer' : r.authorDisplayName,
        submittedOn: r.submittedOn,
        status: r.status,
      })),
      pageNumber,
      pageSize: 25,
      totalCount: filtered.length,
    });
  }),

  http.post('*/api/staff/shop/reviews/:reviewId/publish', ({ params }) =>
    moderate(String(params.reviewId), 'Published', ['PendingModeration', 'Hidden', 'Rejected']),
  ),
  http.post('*/api/staff/shop/reviews/:reviewId/hide', ({ params }) =>
    moderate(String(params.reviewId), 'Hidden', ['Published']),
  ),
  http.post('*/api/staff/shop/reviews/:reviewId/reject', async ({ params, request }) => {
    const body = (await request.json()) as { reason?: string };
    if (!body.reason)
      return badRequest(
        'Reason',
        'ReviewRejectionReasonRequired',
        'Choose a reason for rejecting this review.',
        'ProductReview',
      );
    return moderate(String(params.reviewId), 'Rejected', ['PendingModeration']);
  }),
];

function moderate(
  reviewId: string,
  to: 'Published' | 'Hidden' | 'Rejected',
  from: string[],
): Response {
  const review = db.reviews.find((r) => r.reviewId === reviewId);
  if (!review) return notFound();
  if (!from.includes(review.status)) {
    return badRequest(
      'ReviewId',
      'InvalidReviewTransition',
      `A ${review.status} review can’t be changed to ${to}.`,
      'ProductReview',
    );
  }
  review.status = to;
  if (to === 'Published') {
    const product = findById(review.productId);
    if (product) {
      const total = (product.ratingAverage ?? 0) * product.ratingCount + review.rating;
      product.ratingCount += 1;
      product.ratingAverage = Math.round((total / product.ratingCount) * 10) / 10;
    }
  }
  persist();
  return noContent();
}
