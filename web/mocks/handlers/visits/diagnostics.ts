import { http, type HttpHandler } from 'msw';
import {
  branchOf,
  branchToday,
  callerRoles,
  noContent,
  notFound,
  ok,
  requireAction,
  staffNameFor,
  violation,
} from './fixtures';
import {
  classify,
  filePayload,
  findOrder,
  findVisit,
  getDb,
  persist,
  type StoredOrder,
  type StoredResultValue,
} from './store';

/**
 * `mocks/handlers/visits/diagnostics.ts` — HO-EMR-002 §12, `StaffDiagnosticsController` +
 * `DiagnosticViewModels.cs`. Numbers are strings at the stored precision; every result value carries
 * its flag and the range it was judged against (`ReferenceRangeClassifier`); a result set may
 * supersede an earlier one; attachments are signed URLs (a DICOM file is returned like any other —
 * the client never renders it).
 */

function orderInBranch(request: Request, id: string): StoredOrder | undefined {
  const order = findOrder(id);
  return order && order.branchId === branchOf(request) ? order : undefined;
}

function orderView(order: StoredOrder) {
  return {
    id: order.id,
    version: order.version,
    visitId: order.visitId,
    catId: order.catId,
    catName: order.catName,
    kind: order.kind,
    description: order.description,
    status: order.status,
    orderedOn: order.orderedOn,
    orderedBy: order.orderedBy,
    cancelledReason: order.cancelledReason,
    branchTimeZoneId: 'Asia/Dhaka',
    requestedAnalytes: order.analyteIds
      .map((id) => getDb().analytes.find((a) => a.id === id))
      .filter((a): a is NonNullable<typeof a> => !!a)
      .map((a) => ({ analyteId: a.id, code: a.code, name: a.name })),
    resultSets: order.resultSets,
    attachments: order.attachments.map((a) => ({
      id: a.id,
      resultDate: a.resultDate,
      note: a.note,
      attachedBy: a.attachedBy,
      attachedOn: a.attachedOn,
      file: filePayload(a.fileId, a.fileName, a.contentType),
    })),
  };
}

export function orderViewFor(order: StoredOrder) {
  return orderView(order);
}

const num = (value: number | null | undefined) =>
  value === null || value === undefined ? null : String(value);

export const diagnosticsHandlers: HttpHandler[] = [
  http.post('*/api/staff/visits/:visitId/diagnostics', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const visit = findVisit(params.visitId as string);
    if (!visit || visit.branchId !== branchOf(request)) return notFound();
    const body = (await request.json()) as {
      kind: string;
      description?: string;
      analyteIds?: string[];
    };
    if (visit.status === 'Finalised') {
      return violation(null, {
        propertyName: 'VisitId',
        errorCode: 'VisitFinalised',
        errorMessage:
          'This visit has been finalised and can no longer be changed. Add an addendum instead.',
      });
    }
    if ((body.kind === 'Imaging' || body.kind === 'Other') && !body.description?.trim()) {
      return violation(null, {
        propertyName: 'Description',
        errorCode: 'DiagnosticDescriptionRequired',
        errorMessage: `Describe the ${body.kind} study to be done.`,
      });
    }
    const id = crypto.randomUUID();
    getDb().orders.push({
      id,
      version: 1,
      visitId: visit.id,
      catId: visit.catId,
      catName: visit.catName,
      branchId: visit.branchId,
      kind: body.kind,
      description: body.description?.trim() || null,
      status: 'Pending',
      orderedOn: new Date().toISOString(),
      orderedBy: staffNameFor(callerRoles(request)),
      cancelledReason: null,
      analyteIds: body.analyteIds ?? [],
      resultSets: [],
      attachments: [],
    });
    visit.orderIds.push(id);
    visit.version += 1;
    persist();
    return ok({ diagnosticOrderId: id }, 201);
  }),

  http.get('*/api/staff/diagnostics', ({ request }) => {
    const refused = requireAction(request, 'Visits.View');
    if (refused) return refused;
    const status = new URL(request.url).searchParams.get('status') ?? 'Pending';
    const rows = getDb()
      .orders.filter((o) => o.branchId === branchOf(request) && o.status === status)
      .sort((a, b) => a.orderedOn.localeCompare(b.orderedOn))
      .map((o) => ({
        id: o.id,
        visitId: o.visitId,
        catId: o.catId,
        catName: o.catName,
        kind: o.kind,
        status: o.status,
        orderedOn: o.orderedOn,
        orderedBy: o.orderedBy,
      }));
    return ok(rows);
  }),

  http.get('*/api/staff/diagnostics/:orderId', ({ request, params }) => {
    const refused = requireAction(request, 'Visits.View');
    if (refused) return refused;
    const order = orderInBranch(request, params.orderId as string);
    return order ? ok(orderView(order)) : notFound();
  }),

  http.post('*/api/staff/diagnostics/:orderId/results', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const order = orderInBranch(request, params.orderId as string);
    if (!order) return notFound();
    const body = (await request.json()) as {
      reportedOn: string;
      note?: string;
      supersedesResultSetId?: string;
      values: Array<{
        analyteId?: string;
        name?: string;
        valueKind: 'Numeric' | 'Qualitative';
        numericValue?: number;
        textValue?: string;
        unit?: string;
        referenceLow?: number;
        referenceHigh?: number;
        normalText?: string;
      }>;
    };
    if (order.status === 'Cancelled') {
      return violation(null, {
        propertyName: 'DiagnosticOrderId',
        errorCode: 'DiagnosticOrderCancelled',
        errorMessage: 'This order was cancelled.',
      });
    }
    if (!body.values?.length) {
      return violation(null, {
        propertyName: 'Values',
        errorCode: 'InvalidDiagnosticResult',
        errorMessage: 'A result set has between 1 and 100 values.',
      });
    }
    if (body.reportedOn > branchToday()) {
      return violation(null, {
        propertyName: 'ReportedOn',
        errorCode: 'ClinicalDateInFuture',
        errorMessage: 'The report date cannot be in the future.',
      });
    }
    const values: StoredResultValue[] = [];
    for (const raw of body.values) {
      const name = raw.name?.trim();
      if (!name) {
        return violation(null, {
          propertyName: 'Name',
          errorCode: 'InvalidDiagnosticResult',
          errorMessage: 'Each value needs a name of up to 100 characters.',
        });
      }
      if (
        raw.referenceLow !== undefined &&
        raw.referenceHigh !== undefined &&
        raw.referenceLow > raw.referenceHigh
      ) {
        return violation(null, {
          propertyName: 'ReferenceLow',
          errorCode: 'InvalidReferenceRange',
          errorMessage: `The reference range for '${name}' is back to front: the low bound is above the high bound.`,
        });
      }
      if (raw.valueKind === 'Numeric') {
        if (raw.numericValue === undefined || !raw.unit?.trim()) {
          return violation(null, {
            propertyName: 'NumericValue',
            errorCode: 'InvalidDiagnosticResult',
            errorMessage: `A numeric value for '${name}' needs a number and a unit.`,
          });
        }
        values.push({
          id: crypto.randomUUID(),
          name,
          valueKind: 'Numeric',
          numericValue: String(raw.numericValue),
          textValue: null,
          unit: raw.unit,
          referenceLow: num(raw.referenceLow),
          referenceHigh: num(raw.referenceHigh),
          normalText: null,
          flag: classify(raw.numericValue, raw.referenceLow ?? null, raw.referenceHigh ?? null),
        });
      } else {
        if (!raw.textValue?.trim()) {
          return violation(null, {
            propertyName: 'TextValue',
            errorCode: 'InvalidDiagnosticResult',
            errorMessage: `A qualitative value for '${name}' needs a result.`,
          });
        }
        const normal = raw.normalText?.trim();
        values.push({
          id: crypto.randomUUID(),
          name,
          valueKind: 'Qualitative',
          numericValue: null,
          textValue: raw.textValue,
          unit: null,
          referenceLow: null,
          referenceHigh: null,
          normalText: normal || null,
          flag: !normal
            ? 'NotAssessed'
            : raw.textValue.trim().toLowerCase() === normal.toLowerCase()
              ? 'Normal'
              : 'Abnormal',
        });
      }
    }
    const resultSetId = crypto.randomUUID();
    order.resultSets.push({
      id: resultSetId,
      reportedOn: body.reportedOn,
      note: body.note ?? null,
      supersedesResultSetId: body.supersedesResultSetId ?? null,
      recordedBy: staffNameFor(callerRoles(request)),
      recordedOn: new Date().toISOString(),
      values,
    });
    order.status = 'Resulted';
    order.version += 1;
    persist();
    return ok(
      {
        resultSetId,
        values: values.map((v) => ({
          valueId: v.id,
          name: v.name,
          valueKind: v.valueKind,
          numericValue: v.numericValue === null ? null : Number(v.numericValue),
          textValue: v.textValue,
          unit: v.unit,
          referenceLow: v.referenceLow === null ? null : Number(v.referenceLow),
          referenceHigh: v.referenceHigh === null ? null : Number(v.referenceHigh),
          normalText: v.normalText,
          flag: v.flag,
        })),
      },
      201,
    );
  }),

  http.post('*/api/staff/diagnostics/:orderId/attachments', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const order = orderInBranch(request, params.orderId as string);
    if (!order) return notFound();
    const body = (await request.json()) as { fileId: string; resultDate: string; note?: string };
    const info = getDb().files[body.fileId];
    if (!info)
      return violation(null, {
        propertyName: 'FileId',
        errorCode: 'FileNotFound',
        errorMessage: 'That file could not be found. Upload it again.',
      });
    const id = crypto.randomUUID();
    order.attachments.push({
      id,
      resultDate: body.resultDate,
      note: body.note ?? null,
      attachedBy: staffNameFor(callerRoles(request)),
      attachedOn: new Date().toISOString(),
      fileId: body.fileId,
      fileName: info.name,
      contentType: info.type,
      sizeInBytes: info.size,
    });
    order.version += 1;
    persist();
    return ok({ attachmentId: id }, 201);
  }),

  http.post('*/api/staff/diagnostics/:orderId/cancel', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const order = orderInBranch(request, params.orderId as string);
    if (!order) return notFound();
    const body = (await request.json()) as { reason: string };
    if (order.resultSets.length > 0 || order.attachments.length > 0) {
      return violation(null, {
        propertyName: 'DiagnosticOrderId',
        errorCode: 'DiagnosticOrderHasResults',
        errorMessage: 'This order already has results or files and cannot be cancelled.',
      });
    }
    order.status = 'Cancelled';
    order.cancelledReason = body.reason;
    order.version += 1;
    persist();
    return noContent();
  }),
];
