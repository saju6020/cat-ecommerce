import { http, type HttpHandler } from 'msw';
import { noContent, notFound, ok, requireAction, violation } from './fixtures';
import { findCode, getDb, persist, SERVICE_CONSULTATION } from './store';

/**
 * `mocks/handlers/visits/catalogue.ts` — HO-EMR-002 §12, `StaffClinicalCatalogueController`:
 * diagnosis codes, lab analytes and note templates. Lists are `{ items, pageNumber, pageSize,
 * totalCount }` ordered by code; `search` is a case-insensitive contains over code and name;
 * the built-in `SYS-NAD` code cannot be changed; one active template per service.
 */

function paging(request: Request) {
  const url = new URL(request.url);
  return {
    pageNumber: Number(url.searchParams.get('pageNumber') ?? 1),
    pageSize: Number(url.searchParams.get('pageSize') ?? 20),
    search: url.searchParams.get('search')?.trim().toLowerCase() ?? '',
    activeOnly: url.searchParams.get('activeOnly') === 'true',
  };
}

function page<T>(rows: T[], p: { pageNumber: number; pageSize: number }) {
  return {
    items: rows.slice((p.pageNumber - 1) * p.pageSize, p.pageNumber * p.pageSize),
    pageNumber: p.pageNumber,
    pageSize: p.pageSize,
    totalCount: rows.length,
  };
}

const SERVICES = [
  { id: SERVICE_CONSULTATION.id, name: SERVICE_CONSULTATION.name },
  { id: 's0000000-0000-0000-0000-00000000a0b2', name: 'Spay' },
  { id: 's0000000-0000-0000-0000-00000000a0b3', name: 'Dental scale and polish' },
];

export const catalogueHandlers: HttpHandler[] = [
  // ---- diagnosis codes
  http.get('*/api/staff/diagnosis-codes', ({ request }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.View');
    if (refused) return refused;
    const p = paging(request);
    const rows = getDb()
      .codes.filter(
        (c) =>
          (!p.activeOnly || c.isActive) &&
          (!p.search ||
            c.code.toLowerCase().includes(p.search) ||
            c.name.toLowerCase().includes(p.search)),
      )
      .sort((a, b) => a.code.localeCompare(b.code));
    return ok(page(rows, p));
  }),
  http.get('*/api/staff/diagnosis-codes/:id', ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.View');
    if (refused) return refused;
    const code = findCode(params.id as string);
    return code ? ok(code) : notFound();
  }),
  http.post('*/api/staff/diagnosis-codes', async ({ request }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const body = (await request.json()) as {
      code: string;
      name: string;
      category?: string;
      externalSystem?: string;
      externalCode?: string;
    };
    if (!body.code?.trim() || !body.name?.trim()) {
      return violation(null, {
        propertyName: body.code?.trim() ? 'Name' : 'Code',
        errorCode: 'DiagnosisCodeAlreadyExists',
        errorMessage: 'Enter a code and a name.',
      });
    }
    if (getDb().codes.some((c) => c.code.toLowerCase() === body.code.trim().toLowerCase())) {
      return violation(null, {
        propertyName: 'Code',
        errorCode: 'DiagnosisCodeAlreadyExists',
        errorMessage: `A diagnosis code '${body.code.trim()}' already exists.`,
      });
    }
    const id = crypto.randomUUID();
    getDb().codes.push({
      id,
      code: body.code.trim().toUpperCase(),
      name: body.name.trim(),
      category: body.category || null,
      externalSystem: body.externalSystem || null,
      externalCode: body.externalCode || null,
      isActive: true,
      isSystem: false,
      version: 1,
    });
    persist();
    return ok({ diagnosisCodeId: id }, 201);
  }),
  http.put('*/api/staff/diagnosis-codes/:id', async ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const code = findCode(params.id as string);
    if (!code) return notFound();
    if (code.isSystem) {
      return violation(null, {
        propertyName: 'Code',
        errorCode: 'SystemDiagnosisCodeReadOnly',
        errorMessage: 'This is a built-in code and cannot be changed.',
      });
    }
    const body = (await request.json()) as {
      name: string;
      category?: string;
      externalSystem?: string;
      externalCode?: string;
    };
    Object.assign(code, {
      name: body.name,
      category: body.category || null,
      externalSystem: body.externalSystem || null,
      externalCode: body.externalCode || null,
      version: code.version + 1,
    });
    persist();
    return noContent();
  }),
  http.put('*/api/staff/diagnosis-codes/:id/active', async ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const code = findCode(params.id as string);
    if (!code) return notFound();
    if (code.isSystem) {
      return violation(null, {
        propertyName: 'Code',
        errorCode: 'SystemDiagnosisCodeReadOnly',
        errorMessage: 'This is a built-in code and cannot be changed.',
      });
    }
    code.isActive = ((await request.json()) as { isActive: boolean }).isActive;
    code.version += 1;
    persist();
    return noContent();
  }),

  // ---- lab analytes
  http.get('*/api/staff/lab-analytes', ({ request }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.View');
    if (refused) return refused;
    const p = paging(request);
    const rows = getDb()
      .analytes.filter(
        (a) =>
          (!p.activeOnly || a.isActive) &&
          (!p.search ||
            a.code.toLowerCase().includes(p.search) ||
            a.name.toLowerCase().includes(p.search)),
      )
      .sort((a, b) => a.code.localeCompare(b.code));
    return ok(page(rows, p));
  }),
  http.get('*/api/staff/lab-analytes/:id', ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.View');
    if (refused) return refused;
    const row = getDb().analytes.find((a) => a.id === params.id);
    return row ? ok(row) : notFound();
  }),
  http.post('*/api/staff/lab-analytes', async ({ request }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const body = (await request.json()) as {
      code: string;
      name: string;
      valueKind: 'Numeric' | 'Qualitative';
      diagnosticKind: string;
      unit?: string;
      referenceLow?: number;
      referenceHigh?: number;
      normalText?: string;
    };
    if (getDb().analytes.some((a) => a.code.toLowerCase() === body.code.trim().toLowerCase())) {
      return violation(null, {
        propertyName: 'Code',
        errorCode: 'LabAnalyteAlreadyExists',
        errorMessage: `A lab analyte '${body.code.trim()}' already exists.`,
      });
    }
    if (
      body.referenceLow !== undefined &&
      body.referenceHigh !== undefined &&
      body.referenceLow > body.referenceHigh
    ) {
      return violation(null, {
        propertyName: 'ReferenceLow',
        errorCode: 'InvalidReferenceRange',
        errorMessage: 'The low bound is above the high bound.',
      });
    }
    const id = crypto.randomUUID();
    getDb().analytes.push({
      id,
      code: body.code.trim().toUpperCase(),
      name: body.name.trim(),
      valueKind: body.valueKind,
      diagnosticKind: body.diagnosticKind,
      unit: body.unit || null,
      referenceLow: body.referenceLow === undefined ? null : String(body.referenceLow),
      referenceHigh: body.referenceHigh === undefined ? null : String(body.referenceHigh),
      normalText: body.normalText || null,
      isActive: true,
      version: 1,
    });
    persist();
    return ok({ labAnalyteId: id }, 201);
  }),
  http.put('*/api/staff/lab-analytes/:id', async ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const row = getDb().analytes.find((a) => a.id === params.id);
    if (!row) return notFound();
    const body = (await request.json()) as {
      name: string;
      valueKind: 'Numeric' | 'Qualitative';
      diagnosticKind: string;
      unit?: string;
      referenceLow?: number;
      referenceHigh?: number;
      normalText?: string;
    };
    if (
      body.referenceLow !== undefined &&
      body.referenceHigh !== undefined &&
      body.referenceLow > body.referenceHigh
    ) {
      return violation(null, {
        propertyName: 'ReferenceLow',
        errorCode: 'InvalidReferenceRange',
        errorMessage: 'The low bound is above the high bound.',
      });
    }
    Object.assign(row, {
      name: body.name,
      valueKind: body.valueKind,
      diagnosticKind: body.diagnosticKind,
      unit: body.unit || null,
      referenceLow: body.referenceLow === undefined ? null : String(body.referenceLow),
      referenceHigh: body.referenceHigh === undefined ? null : String(body.referenceHigh),
      normalText: body.normalText || null,
      version: row.version + 1,
    });
    persist();
    return noContent();
  }),
  http.put('*/api/staff/lab-analytes/:id/active', async ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const row = getDb().analytes.find((a) => a.id === params.id);
    if (!row) return notFound();
    row.isActive = ((await request.json()) as { isActive: boolean }).isActive;
    row.version += 1;
    persist();
    return noContent();
  }),

  // ---- note templates
  http.get('*/api/staff/note-templates', ({ request }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.View');
    if (refused) return refused;
    const url = new URL(request.url);
    const p = paging(request);
    const serviceId = url.searchParams.get('serviceId');
    const rows = getDb()
      .templates.filter(
        (t) => (!p.activeOnly || t.isActive) && (!serviceId || t.serviceId === serviceId),
      )
      .sort((a, b) => a.serviceName.localeCompare(b.serviceName));
    return ok(page(rows, p));
  }),
  http.get('*/api/staff/note-templates/:id', ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.View');
    if (refused) return refused;
    const row = getDb().templates.find((t) => t.id === params.id);
    return row ? ok(row) : notFound();
  }),
  http.post('*/api/staff/note-templates', async ({ request }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const body = (await request.json()) as {
      serviceId: string;
      name: string;
      subjective: string;
      objective: string;
      assessment: string;
      plan: string;
      dischargeInstructions: string;
    };
    if (getDb().templates.some((t) => t.serviceId === body.serviceId && t.isActive)) {
      return violation(null, {
        propertyName: 'ServiceId',
        errorCode: 'NoteTemplateAlreadyExistsForService',
        errorMessage: 'That service already has an active note template.',
      });
    }
    const id = crypto.randomUUID();
    getDb().templates.push({
      id,
      serviceId: body.serviceId,
      serviceName: SERVICES.find((s) => s.id === body.serviceId)?.name ?? 'Service',
      name: body.name,
      subjective: body.subjective,
      objective: body.objective,
      assessment: body.assessment,
      plan: body.plan,
      dischargeInstructions: body.dischargeInstructions,
      templateVersion: 1,
      isActive: true,
      version: 1,
    });
    persist();
    return ok({ noteTemplateId: id }, 201);
  }),
  http.put('*/api/staff/note-templates/:id', async ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const row = getDb().templates.find((t) => t.id === params.id);
    if (!row) return notFound();
    const body = (await request.json()) as Partial<typeof row>;
    Object.assign(row, {
      name: body.name ?? row.name,
      subjective: body.subjective ?? '',
      objective: body.objective ?? '',
      assessment: body.assessment ?? '',
      plan: body.plan ?? '',
      dischargeInstructions: body.dischargeInstructions ?? '',
      templateVersion: row.templateVersion + 1,
      version: row.version + 1,
    });
    persist();
    return noContent();
  }),
  http.put('*/api/staff/note-templates/:id/active', async ({ request, params }) => {
    const refused = requireAction(request, 'ClinicalCatalogue.Manage');
    if (refused) return refused;
    const row = getDb().templates.find((t) => t.id === params.id);
    if (!row) return notFound();
    row.isActive = ((await request.json()) as { isActive: boolean }).isActive;
    row.version += 1;
    persist();
    return noContent();
  }),
];
