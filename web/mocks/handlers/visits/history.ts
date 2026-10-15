import { http, type HttpHandler } from 'msw';
import { notFound, ok, requireAction, emrFixtureIds as ids } from './fixtures';
import { CAT_BASICS, filePayload, getDb, type StoredOrder, type StoredVisit } from './store';
import { bulkOrdersFor, bulkVisitsFor, bulkWeightsFor } from './bulk';
import { vitalsViewFor } from './staff';

/**
 * `mocks/handlers/visits/history.ts` — HO-EMR-002 §12, `StaffClinicalHistoryController` +
 * `EmrSectionProviders.cs` + `GetClinicalHistorySummaryQueryHandler`. Whiskers carries the 10-year
 * fixture (60 visits, 250 weights, 80 lab orders — `bulk.ts`); `prescriptions`, `boarding-stays` and
 * `food-purchases` are `available: false` with their owning area, exactly as the server reports
 * a declared section nobody has registered a provider for.
 */

const SECTIONS: Array<{ key: string; owner: string }> = [
  { key: 'timeline', owner: 'HO-EMR-001' },
  { key: 'visits', owner: 'HO-EMR-001' },
  { key: 'diagnoses', owner: 'HO-EMR-001' },
  { key: 'lab-results', owner: 'HO-EMR-001' },
  { key: 'photos', owner: 'HO-EMR-001' },
  { key: 'vaccinations', owner: 'HO-CAT-001' },
  { key: 'parasite-treatments', owner: 'HO-CAT-001' },
  { key: 'weights', owner: 'HO-CAT-001' },
  { key: 'prescriptions', owner: 'HO-PHM-001' },
  { key: 'boarding-stays', owner: 'HO-BRD-001' },
  { key: 'food-purchases', owner: 'HO-ORD-001' },
];

interface Item {
  id: string;
  kind: string;
  occurredOn: string;
  title: string;
  detail: string | null;
  fields: Record<string, string | null>;
  file: ReturnType<typeof filePayload> | null;
}

const midnight = (date: string) => `${date}T00:00:00.000Z`;

function visitsOf(catId: string): StoredVisit[] {
  return [
    ...getDb().visits.filter((v) => v.catId === catId && v.branchId === ids.mainBranchId),
    ...bulkVisitsFor(catId),
  ];
}

function ordersOf(catId: string): StoredOrder[] {
  return [...getDb().orders.filter((o) => o.catId === catId), ...bulkOrdersFor(catId)];
}

function problemNames(visit: StoredVisit): string {
  return visit.problems
    .map((p) => getDb().codes.find((c) => c.id === p.diagnosisCodeId)?.name)
    .filter(Boolean)
    .join(', ');
}

function visitItems(catId: string): Item[] {
  return visitsOf(catId)
    .sort((a, b) => b.openedOn.localeCompare(a.openedOn))
    .map((v) => ({
      id: v.id,
      kind: 'visit',
      occurredOn: v.openedOn,
      title: v.serviceName,
      detail: problemNames(v),
      fields: {
        status: v.status,
        vet: v.finalisedBy ?? v.openedBy,
        addendumCount: String(v.addenda.length),
      },
      file: null,
    }));
}

function diagnosisItems(catId: string): Item[] {
  const rows = new Map<
    string,
    { code: string; name: string; certainty: string; first: string; last: string; count: number }
  >();
  for (const visit of visitsOf(catId)
    .filter((v) => v.status === 'Finalised')
    .sort((a, b) => a.openedOn.localeCompare(b.openedOn))) {
    for (const problem of visit.problems) {
      const code = getDb().codes.find((c) => c.id === problem.diagnosisCodeId);
      if (!code) continue;
      const existing = rows.get(code.id);
      rows.set(code.id, {
        code: code.code,
        name: code.name,
        certainty: problem.certainty,
        first: existing?.first ?? visit.openedOn,
        last: visit.openedOn,
        count: (existing?.count ?? 0) + 1,
      });
    }
  }
  return [...rows.entries()]
    .map(([id, r]) => ({
      id,
      kind: 'diagnosis',
      occurredOn: r.last,
      title: r.name,
      detail: null,
      fields: {
        code: r.code,
        certainty: r.certainty,
        firstSeen: r.first,
        lastSeen: r.last,
        occurrences: String(r.count),
      },
      file: null,
    }))
    .sort((a, b) => b.occurredOn.localeCompare(a.occurredOn));
}

function labItems(catId: string): Item[] {
  return ordersOf(catId)
    .sort((a, b) => b.orderedOn.localeCompare(a.orderedOn))
    .map((o) => {
      const latest = [...o.resultSets].sort((a, b) => b.recordedOn.localeCompare(a.recordedOn))[0];
      const counts = new Map<string, number>();
      latest?.values.forEach((v) => counts.set(v.flag, (counts.get(v.flag) ?? 0) + 1));
      const flags = [...counts.entries()]
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([k, n]) => `${k}:${n}`)
        .join(',');
      return {
        id: o.id,
        kind: 'lab-result',
        occurredOn: o.orderedOn,
        title: o.kind,
        detail: o.status,
        fields: {
          visitId: o.visitId,
          reportedOn: latest?.reportedOn ?? null,
          flags,
          hasAbnormal: latest?.values.some((v) => ['Low', 'High', 'Abnormal'].includes(v.flag))
            ? 'true'
            : 'false',
          resultSetCount: String(o.resultSets.length),
          attachmentCount: String(o.attachments.length),
        },
        file: null,
      };
    });
}

function photoItems(catId: string): Item[] {
  return visitsOf(catId)
    .flatMap((v) => v.photos.map((p) => ({ visit: v, photo: p })))
    .sort((a, b) => b.photo.takenOn.localeCompare(a.photo.takenOn))
    .map(({ visit, photo }) => ({
      id: photo.id,
      kind: 'photo',
      occurredOn: midnight(photo.takenOn),
      title: 'Photograph',
      detail: photo.note,
      fields: { visitId: visit.id, takenOn: photo.takenOn },
      file: filePayload(photo.fileId, photo.fileName, photo.contentType),
    }));
}

function weightItems(catId: string): Item[] {
  const points = [
    ...bulkWeightsFor(catId),
    ...visitsOf(catId).flatMap((v) =>
      v.vitals
        .filter((e) => e.weightKg !== null)
        .map((e) => ({ id: e.id, on: e.measuredOn.slice(0, 10), kg: e.weightKg! })),
    ),
  ];
  return points
    .sort((a, b) => b.on.localeCompare(a.on))
    .map((p) => ({
      id: p.id,
      kind: 'weight',
      occurredOn: midnight(p.on),
      title: `${p.kg.toFixed(2)} kg`,
      detail: null,
      fields: { kilograms: p.kg.toFixed(2), unit: 'kg' },
      file: null,
    }));
}

function vaccinationItems(catId: string): Item[] {
  const basics = CAT_BASICS[catId];
  if (!basics) return [];
  const today = Date.now();
  const row = (n: number, name: string, code: string, daysAgo: number): Item => ({
    id: `b3b00000-0000-0000-0000-00000000000${n}`,
    kind: 'vaccination',
    occurredOn: midnight(new Date(today - daysAgo * 86_400_000).toISOString().slice(0, 10)),
    title: name,
    detail: null,
    fields: {
      code,
      nextDueOn: new Date(today + (365 - daysAgo) * 86_400_000).toISOString().slice(0, 10),
      batch: `B-${100 + n}`,
    },
    file: null,
  });
  return [row(1, 'FVRCP', 'FVRCP', 200), row(2, 'Rabies', 'RAB', 400)];
}

function parasiteItems(catId: string): Item[] {
  if (!CAT_BASICS[catId]) return [];
  const on = new Date(Date.now() - 40 * 86_400_000).toISOString().slice(0, 10);
  return [
    {
      id: 'b3c00000-0000-0000-0000-000000000001',
      kind: 'parasite-treatment',
      occurredOn: midnight(on),
      title: 'Spot-on flea treatment',
      detail: 'Flea',
      fields: { nextDueOn: new Date(Date.now() + 50 * 86_400_000).toISOString().slice(0, 10) },
      file: null,
    },
  ];
}

function itemsFor(section: string, catId: string): Item[] | null {
  switch (section) {
    case 'visits':
      return visitItems(catId);
    case 'diagnoses':
      return diagnosisItems(catId);
    case 'lab-results':
      return labItems(catId);
    case 'photos':
      return photoItems(catId);
    case 'vaccinations':
      return vaccinationItems(catId);
    case 'parasite-treatments':
      return parasiteItems(catId);
    case 'weights':
      return weightItems(catId);
    case 'timeline':
      return [
        ...visitItems(catId),
        ...photoItems(catId),
        ...labItems(catId),
        ...vaccinationItems(catId),
        ...parasiteItems(catId),
        ...weightItems(catId),
      ].sort(
        (a, b) =>
          b.occurredOn.localeCompare(a.occurredOn) ||
          a.kind.localeCompare(b.kind) ||
          a.id.localeCompare(b.id),
      );
    default:
      return null;
  }
}

function summaryFor(catId: string) {
  const basics = CAT_BASICS[catId]!;
  const visits = visitsOf(catId);
  const finalisedWithin12Months = visits.filter(
    (v) => v.status === 'Finalised' && Date.parse(v.openedOn) > Date.now() - 365 * 86_400_000,
  );
  const activeProblems = new Map<
    string,
    { diagnosisCodeId: string; code: string; name: string; certainty: string; lastSeen: string }
  >();
  for (const visit of finalisedWithin12Months.sort((a, b) =>
    a.openedOn.localeCompare(b.openedOn),
  )) {
    for (const p of visit.problems) {
      const code = getDb().codes.find((c) => c.id === p.diagnosisCodeId);
      if (code)
        activeProblems.set(code.id, {
          diagnosisCodeId: code.id,
          code: code.code,
          name: code.name,
          certainty: p.certainty,
          lastSeen: visit.openedOn,
        });
    }
  }
  const lastVitals = visits
    .flatMap((v) => v.vitals)
    .sort((a, b) => b.measuredOn.localeCompare(a.measuredOn))[0];
  const weights = weightItems(catId)
    .map((i) => ({ recordedOn: i.occurredOn.slice(0, 10), kilograms: Number(i.fields.kilograms) }))
    .sort((a, b) => a.recordedOn.localeCompare(b.recordedOn));

  return {
    cat: {
      id: catId,
      name: basics.name,
      sex: basics.sex,
      dateOfBirth: basics.dob,
      breed: basics.breed,
      isHighRisk: basics.isHighRisk,
      lifeStatus: 'Alive',
    },
    activeProblems: [...activeProblems.values()].sort((a, b) =>
      b.lastSeen.localeCompare(a.lastSeen),
    ),
    recentVisits: visitItems(catId).slice(0, 5),
    lastVitals: lastVitals ? vitalsViewFor(lastVitals) : null,
    weightSeries: weights,
    vaccinationStatus: {
      catId,
      overallStatus: 'Current',
      earliestNextDueOn: new Date(Date.now() + 165 * 86_400_000).toISOString().slice(0, 10),
      bookingEligible: true,
      vaccines: [
        {
          vaccineId: '30000000-0000-0000-0000-000000000001',
          vaccineCode: 'RAB',
          vaccineName: 'Rabies',
          status: 'Current',
          nextDueOn: null,
          reason: null,
        },
      ],
    },
    pendingDiagnostics: ordersOf(catId)
      .filter((o) => o.status === 'Pending')
      .sort((a, b) => a.orderedOn.localeCompare(b.orderedOn))
      .slice(0, 50)
      .map((o) => ({
        id: o.id,
        visitId: o.visitId,
        catId,
        catName: basics.name,
        kind: o.kind,
        status: o.status,
        orderedOn: o.orderedOn,
        orderedBy: o.orderedBy,
      })),
    sections: SECTIONS.map((s) => {
      const items = itemsFor(s.key, catId);
      return items === null
        ? { key: s.key, available: false, owner: s.owner, count: 0 }
        : { key: s.key, available: true, owner: s.owner, count: items.length };
    }),
  };
}

export const historyHandlers: HttpHandler[] = [
  http.get('*/api/staff/cats/:catId/clinical-history', ({ request, params }) => {
    const refused = requireAction(request, 'Visits.View');
    if (refused) return refused;
    return CAT_BASICS[params.catId as string] ? ok(summaryFor(params.catId as string)) : notFound();
  }),

  http.get('*/api/staff/cats/:catId/clinical-history/:section', ({ request, params }) => {
    const refused = requireAction(request, 'Visits.View');
    if (refused) return refused;
    const catId = params.catId as string;
    const section = SECTIONS.find((s) => s.key === params.section);
    if (!section || !CAT_BASICS[catId]) return notFound();
    const url = new URL(request.url);
    const pageNumber = Number(url.searchParams.get('pageNumber') ?? 1);
    const pageSize = Number(url.searchParams.get('pageSize') ?? 20);
    const items = itemsFor(section.key, catId);
    if (items === null) {
      return ok({
        key: section.key,
        available: false,
        owner: section.owner,
        items: [],
        pageNumber,
        pageSize,
        totalCount: 0,
      });
    }
    return ok({
      key: section.key,
      available: true,
      owner: section.owner,
      items: items.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
      pageNumber,
      pageSize,
      totalCount: items.length,
    });
  }),
];
