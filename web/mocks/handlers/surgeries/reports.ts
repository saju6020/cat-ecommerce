import { http, type HttpHandler } from 'msw';
import type { SaveReportDraftRequest } from '@/lib/api/surgical-reports';
import {
  actorFor,
  bad,
  branchOf,
  callerRoles,
  conflict,
  noContent,
  notFound,
  ok,
  requireAction,
  violation,
  zoneOfBranch,
} from './fixtures';
import { findCat, findSurgery, getDb, newId, persist, staffName, type MReport } from './store';

/**
 * `mocks/handlers/surgeries/reports.ts` — `StaffSurgeryReportsController`. The first `PUT` creates the
 * draft; a stale `expectedVersion` is `409 StaleVersion`; a `PUT` after finalisation is
 * `409 ReportNotEditable`; finalising an incomplete report is `400 ReportIncomplete` naming each missing
 * field (HO-SRG-001 §8.x, ADR-165). Addenda are append-only.
 */
function viewOf(r: MReport) {
  const s = findSurgery(r.surgeryId)!;
  return {
    surgeryId: r.surgeryId,
    catId: s.catId,
    catName: findCat(s.catId)?.name ?? 'Cat',
    visitId: s.visitId,
    branchId: s.branchId,
    branchTimeZoneId: zoneOfBranch(s.branchId),
    version: r.version,
    status: r.status,
    procedurePerformed: r.procedurePerformed,
    findings: r.findings,
    technique: r.technique,
    sutureMaterial: r.sutureMaterial,
    hadComplications: r.hadComplications,
    complications: r.complications,
    surgeonId: r.surgeonId,
    surgeonName: staffName(r.surgeonId),
    assistantId: r.assistantId,
    assistantName: staffName(r.assistantId),
    finalisedOn: r.finalisedOn,
    finalisedBy: r.finalisedBy,
    finalisedByName: r.finalisedByName,
    addenda: r.addenda,
  };
}

const REQUIRED: Array<[keyof MReport, string]> = [
  ['procedurePerformed', 'procedure performed'],
  ['findings', 'findings'],
  ['technique', 'technique'],
  ['sutureMaterial', 'suture material'],
];

export const reportHandlers: HttpHandler[] = [
  http.get('*/api/staff/surgeries/:id/report', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || s.branchId !== branchOf(request)) return notFound();
    const report = getDb().reports.find((r) => r.surgeryId === s.id);
    return report ? ok(viewOf(report)) : notFound();
  }),

  http.put('*/api/staff/surgeries/:id/report', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.WriteReport');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || s.branchId !== branchOf(request)) return notFound();
    const body = (await request.json()) as SaveReportDraftRequest;
    const db = getDb();
    let report = db.reports.find((r) => r.surgeryId === s.id);
    if (report?.status === 'Finalised') {
      return conflict(
        bad(
          'SurgeryId',
          'ReportNotEditable',
          'This report has been finalised and can only be added to with an addendum.',
        ),
      );
    }
    // The real handler refuses a save against an existing draft whose version it does not know.
    if (report && body.expectedVersion !== report.version) {
      return conflict(
        bad(
          'ExpectedVersion',
          'StaleVersion',
          'Someone else changed this report. Reload to see their changes.',
        ),
      );
    }
    if (!report) {
      report = {
        surgeryId: s.id,
        version: 0,
        status: 'Draft',
        procedurePerformed: '',
        findings: '',
        technique: '',
        sutureMaterial: '',
        hadComplications: false,
        complications: null,
        surgeonId: body.surgeonId,
        assistantId: body.assistantId,
        finalisedOn: null,
        finalisedBy: null,
        finalisedByName: null,
        addenda: [],
      };
      db.reports.push(report);
    }
    report.procedurePerformed = body.procedurePerformed ?? '';
    report.findings = body.findings ?? '';
    report.technique = body.technique ?? '';
    report.sutureMaterial = body.sutureMaterial ?? '';
    report.hadComplications = body.hadComplications;
    report.complications = body.hadComplications ? (body.complications ?? '') : null;
    report.surgeonId = body.surgeonId;
    report.assistantId = body.assistantId;
    report.version += 1;
    persist();
    return ok({ version: report.version });
  }),

  http.post('*/api/staff/surgeries/:id/report/finalise', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.WriteReport');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || s.branchId !== branchOf(request)) return notFound();
    const report = getDb().reports.find((r) => r.surgeryId === s.id);
    if (!report) return notFound();
    if (report.status === 'Finalised') {
      return conflict(
        bad('SurgeryId', 'ReportAlreadyFinalised', 'This report has already been finalised.'),
      );
    }
    const body = (await request.json()) as { expectedVersion: number };
    if (body.expectedVersion !== report.version) {
      return conflict(
        bad(
          'ExpectedVersion',
          'StaleVersion',
          'Someone else changed this report. Reload to see their changes.',
        ),
      );
    }
    const missing = REQUIRED.filter(([key]) => !String(report[key] ?? '').trim()).map(
      ([, name]) => name,
    );
    if (report.hadComplications && !report.complications?.trim()) missing.push('complications');
    if (missing.length > 0) {
      // Like the real aggregate: ONE violation whose message names every missing part.
      return violation(
        bad(
          'ProcedurePerformed',
          'ReportIncomplete',
          `The report is incomplete: ${missing.join(', ')}.`,
        ),
      );
    }
    const actor = actorFor(callerRoles(request));
    report.status = 'Finalised';
    report.finalisedOn = new Date().toISOString();
    report.finalisedBy = actor.id;
    report.finalisedByName = actor.name;
    report.version += 1;
    s.version += 1;
    persist();
    return noContent();
  }),

  http.post('*/api/staff/surgeries/:id/report/addenda', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.WriteReport');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || s.branchId !== branchOf(request)) return notFound();
    const report = getDb().reports.find((r) => r.surgeryId === s.id);
    if (!report) return notFound();
    if (report.status !== 'Finalised') {
      return violation(
        bad('SurgeryId', 'ReportNotFinalised', 'Finalise the report before adding an addendum.'),
      );
    }
    const body = (await request.json()) as { text?: string };
    if (!body.text?.trim()) {
      return violation(bad('Text', 'NotEmptyValidator', 'Write the addendum.'));
    }
    const actor = actorFor(callerRoles(request));
    const addendumId = newId();
    report.addenda.push({
      id: addendumId,
      text: body.text.trim(),
      addedBy: actor.id,
      addedByName: actor.name,
      addedOn: new Date().toISOString(),
    });
    persist();
    return ok({ addendumId }, 201);
  }),
];
