import { http, HttpResponse, type HttpHandler } from 'msw';
import { findAppointment } from '../appointments/store';
import {
  branchOf,
  callerRoles,
  noContent,
  notFound,
  ok,
  requireAction,
  staffNameFor,
  violation,
} from './fixtures';
import { filePayload, getDb, persist, zoneForBranch, type StoredConsent } from './store';

/**
 * `mocks/handlers/visits/consents.ts` — HO-EMR-002 §12, `StaffConsentsController` +
 * `ConsentViewModels.cs`. The signature is stroke data in, a polyline-only SVG out (the coordinates
 * are never returned as JSON); a consent of another branch is a `404`; a consent form that has not
 * been published is a `404`; recording with an old form version is `ConsentFormVersionNotCurrent`.
 */

const KINDS = ['Surgery', 'Anaesthesia', 'Euthanasia'];

function consentInBranch(request: Request, id: string): StoredConsent | undefined {
  const consent = getDb().consents.find((c) => c.id === id);
  return consent && consent.branchId === branchOf(request) ? consent : undefined;
}

function consentView(c: StoredConsent) {
  return {
    id: c.id,
    version: c.version,
    catId: c.catId,
    appointmentId: c.appointmentId,
    kind: c.kind,
    procedureDescription: c.procedureDescription,
    method: c.method,
    signedByName: c.signedByName,
    signerRelationship: c.signerRelationship,
    signedByCustomerId: c.signedByCustomerId,
    signedOn: c.signedOn,
    policyKey: c.policyKey,
    policyVersionNumber: c.policyVersionNumber,
    capturedBy: c.capturedBy,
    witness: c.witness,
    status: c.status,
    withdrawnOn: c.withdrawnOn,
    withdrawnBy: c.withdrawnBy,
    withdrawalReason: c.withdrawalReason,
    branchTimeZoneId: zoneForBranch(c.branchId),
    signatureUrl:
      c.method === 'OnScreenSignature'
        ? `/api/staff/consents/${c.id}/signature.svg`
        : c.fileId
          ? filePayload(c.fileId, c.fileName ?? undefined, c.fileContentType ?? undefined).url
          : null,
    file: c.fileId
      ? filePayload(c.fileId, c.fileName ?? undefined, c.fileContentType ?? undefined)
      : null,
  };
}

function svgFor(strokes: number[][][]): string {
  const lines = strokes
    .map(
      (stroke) =>
        `<polyline fill="none" stroke="#000" stroke-width="2" points="${stroke.map(([x, y]) => `${(x! * 300).toFixed(1)},${(y! * 120).toFixed(1)}`).join(' ')}"/>`,
    )
    .join('');
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 120" width="300" height="120">${lines}</svg>`;
}

export const consentHandlers: HttpHandler[] = [
  http.get('*/api/staff/consent-forms/:kind', ({ request, params }) => {
    const refused = requireAction(request, 'Consents.View');
    if (refused) return refused;
    const form = getDb().forms[params.kind as string];
    return form ? ok(form) : notFound();
  }),

  http.post('*/api/staff/consents', async ({ request }) => {
    const refused = requireAction(request, 'Consents.Record');
    if (refused) return refused;
    const body = (await request.json()) as {
      catId: string;
      appointmentId: string;
      kind: string;
      procedureDescription: string;
      method: 'OnScreenSignature' | 'SignedUpload';
      signature?: number[][][];
      fileId?: string;
      signedByName: string;
      signerRelationship: string;
      signedByCustomerId?: string;
      policyVersionNumber: number;
      witnessStaffId?: string;
    };
    if (!KINDS.includes(body.kind)) {
      return violation(null, {
        propertyName: 'Kind',
        errorCode: 'ConsentFormVersionNotCurrent',
        errorMessage: 'Choose a consent kind.',
      });
    }
    const form = getDb().forms[body.kind];
    if (!form) return notFound();
    if (form.versionNumber !== body.policyVersionNumber) {
      return violation(null, {
        propertyName: 'PolicyVersionNumber',
        errorCode: 'ConsentFormVersionNotCurrent',
        errorMessage:
          'The consent wording has changed since this page opened. Reload to read the current version before signing.',
      });
    }
    const appointment = findAppointment(body.appointmentId);
    if (!appointment || appointment.branchId !== branchOf(request)) {
      return violation(null, {
        propertyName: 'AppointmentId',
        errorCode: 'ConsentAppointmentInvalid',
        errorMessage: 'That appointment cannot take a consent.',
      });
    }
    if (appointment.catId !== body.catId) {
      return violation(null, {
        propertyName: 'CatId',
        errorCode: 'ConsentEvidenceMismatch',
        errorMessage: 'That appointment is for a different cat.',
      });
    }
    if (!body.procedureDescription?.trim() || !body.signedByName?.trim()) {
      return violation(null, {
        propertyName: body.signedByName?.trim() ? 'ProcedureDescription' : 'SignedByName',
        errorCode: 'ConsentEvidenceMismatch',
        errorMessage: 'Fill in the procedure and the signer.',
      });
    }
    if (body.method === 'OnScreenSignature') {
      const strokes = body.signature ?? [];
      const points = strokes.reduce((n, s) => n + s.length, 0);
      const valid =
        strokes.length > 0 &&
        strokes.length <= 200 &&
        points >= 10 &&
        points <= 5000 &&
        strokes.every(
          (s) => s.length >= 2 && s.every(([x, y]) => x! >= 0 && x! <= 1 && y! >= 0 && y! <= 1),
        );
      if (!valid) {
        return violation(null, {
          propertyName: 'Signature',
          errorCode: 'InvalidSignature',
          errorMessage: 'The signature could not be used. Clear it and sign again.',
        });
      }
    } else if (!body.fileId || !getDb().files[body.fileId]) {
      return violation(null, {
        propertyName: 'FileId',
        errorCode: 'FileNotFound',
        errorMessage: 'That file could not be found. Upload it again.',
      });
    }
    const id = crypto.randomUUID();
    const file = body.fileId ? getDb().files[body.fileId] : undefined;
    getDb().consents.push({
      id,
      version: 1,
      catId: body.catId,
      branchId: appointment.branchId,
      appointmentId: body.appointmentId,
      kind: body.kind as StoredConsent['kind'],
      procedureDescription: body.procedureDescription,
      method: body.method,
      signedByName: body.signedByName,
      signerRelationship: body.signerRelationship,
      signedByCustomerId: body.signedByCustomerId ?? null,
      signedOn: new Date().toISOString(),
      policyKey: form.policyKey,
      policyVersionNumber: form.versionNumber,
      capturedBy: staffNameFor(callerRoles(request)),
      witness: body.witnessStaffId ? 'Nadia Nurse' : null,
      status: 'Recorded',
      withdrawnOn: null,
      withdrawnBy: null,
      withdrawalReason: null,
      strokes: body.method === 'OnScreenSignature' ? (body.signature ?? null) : null,
      fileId: body.fileId ?? null,
      fileName: file?.name ?? null,
      fileContentType: file?.type ?? null,
    });
    persist();
    return ok({ consentId: id }, 201);
  }),

  http.get('*/api/staff/consents/status', ({ request }) => {
    const refused = requireAction(request, 'Consents.View');
    if (refused) return refused;
    const url = new URL(request.url);
    const match = getDb().consents.find(
      (c) =>
        c.catId === url.searchParams.get('catId') &&
        c.appointmentId === url.searchParams.get('appointmentId') &&
        c.kind === url.searchParams.get('kind') &&
        c.status === 'Recorded',
    );
    return ok({
      hasValidConsent: !!match,
      consentId: match?.id ?? null,
      signedOn: match?.signedOn ?? null,
    });
  }),

  http.get('*/api/staff/consents/:consentId/signature.svg', ({ request, params }) => {
    const refused = requireAction(request, 'Consents.View');
    if (refused) return refused;
    const consent = consentInBranch(request, params.consentId as string);
    if (!consent?.strokes) return notFound();
    return new HttpResponse(svgFor(consent.strokes), {
      status: 200,
      headers: { 'Content-Type': 'image/svg+xml', 'Cache-Control': 'private, no-store' },
    });
  }),

  http.get('*/api/staff/consents/:consentId', ({ request, params }) => {
    const refused = requireAction(request, 'Consents.View');
    if (refused) return refused;
    const consent = consentInBranch(request, params.consentId as string);
    return consent ? ok(consentView(consent)) : notFound();
  }),

  http.get('*/api/staff/cats/:catId/consents', ({ request, params }) => {
    const refused = requireAction(request, 'Consents.View');
    if (refused) return refused;
    const rows = getDb()
      .consents.filter((c) => c.catId === params.catId && c.branchId === branchOf(request))
      .sort((a, b) => b.signedOn.localeCompare(a.signedOn))
      .map((c) => ({
        id: c.id,
        appointmentId: c.appointmentId,
        kind: c.kind,
        method: c.method,
        status: c.status,
        signedOn: c.signedOn,
        policyVersionNumber: c.policyVersionNumber,
        signedByName: c.signedByName,
      }));
    return ok(rows);
  }),

  http.post('*/api/staff/consents/:consentId/withdraw', async ({ request, params }) => {
    const refused = requireAction(request, 'Consents.Record');
    if (refused) return refused;
    const consent = consentInBranch(request, params.consentId as string);
    if (!consent) return notFound();
    const body = (await request.json()) as { reason: string };
    if (consent.status === 'Withdrawn') {
      return violation(null, {
        propertyName: 'ConsentId',
        errorCode: 'ConsentAlreadyWithdrawn',
        errorMessage: 'This consent has already been withdrawn.',
      });
    }
    consent.status = 'Withdrawn';
    consent.withdrawnOn = new Date().toISOString();
    consent.withdrawnBy = staffNameFor(callerRoles(request));
    consent.withdrawalReason = body.reason;
    consent.version += 1;
    persist();
    return noContent();
  }),
];
