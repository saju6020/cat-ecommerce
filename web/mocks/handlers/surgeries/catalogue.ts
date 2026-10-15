import { http, type HttpHandler } from 'msw';
import type { CampaignInput } from '@/lib/api/surgery-campaigns';
import type { UpsertProtocolRequest } from '@/lib/api/surgery-protocols';
import {
  bad,
  branchOf,
  conflict,
  money,
  noContent,
  notFound,
  ok,
  requireAction,
  violation,
} from './fixtures';
import {
  SERVICES,
  findCat,
  findService,
  getDb,
  localDateOf,
  newId,
  persist,
  type MCampaign,
} from './store';
import { applicableCampaigns } from './booking';
import { zoneOfBranch } from './fixtures';

/**
 * `mocks/handlers/surgeries/catalogue.ts` — protocols (`StaffSurgeryProtocolsController`) and campaigns
 * (`StaffSurgeryCampaignsController`). A protocol follow-up must sit inside its own window; a campaign's
 * prices are locked once any surgery has been booked under it (`CampaignPriceLocked`); a duplicate code is
 * refused; a campaign is deactivated, never deleted.
 */
function campaignView(c: MCampaign) {
  const booked = getDb().surgeries.filter(
    (s) => s.campaignId === c.id && s.status !== 'Cancelled',
  ).length;
  return {
    id: c.id,
    version: c.version,
    code: c.code,
    name: c.name,
    description: c.description,
    validFrom: c.validFrom,
    validTo: c.validTo,
    branchIds: c.branchIds,
    prices: c.prices.map((p) => ({
      serviceId: p.serviceId,
      serviceName: findService(p.serviceId)?.name ?? 'Procedure',
      price: money(p.amount),
    })),
    maxCases: c.maxCases,
    casesBooked: booked,
    casesRemaining: c.maxCases === null ? null : Math.max(0, c.maxCases - booked),
    isActive: c.isActive,
    pricesLocked: booked > 0,
  };
}

function paged<T>(rows: T[], url: URL) {
  const pageNumber = Math.max(1, Number(url.searchParams.get('pageNumber') ?? '1'));
  const pageSize = Math.min(100, Math.max(1, Number(url.searchParams.get('pageSize') ?? '20')));
  return {
    items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
    pageNumber,
    pageSize,
    totalCount: rows.length,
  };
}

function validateInput(body: CampaignInput) {
  const errors = [];
  if (!body.name?.trim())
    errors.push(bad('Name', 'NotEmptyValidator', 'Give the campaign a name.'));
  if (!body.validFrom || !body.validTo || body.validTo < body.validFrom) {
    errors.push(
      bad(
        'ValidTo',
        'SurgeryCampaignInvalid',
        'The campaign must end on or after the day it starts.',
      ),
    );
  }
  for (const price of body.prices) {
    if (!findService(price.serviceId)?.isSterilisation) {
      errors.push(
        bad(
          'Prices',
          'CampaignServiceNotSterilisation',
          'A campaign can only price spay and neuter procedures.',
        ),
      );
    }
  }
  return errors;
}

export const catalogueHandlers: HttpHandler[] = [
  http.get('*/api/staff/surgery-protocols', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    return ok(
      getDb().protocols.map((p) => ({
        ...p,
        serviceName: findService(p.serviceId)?.name ?? 'Procedure',
        category: 'Surgery',
      })),
    );
  }),

  http.put('*/api/staff/surgery-protocols/:serviceId', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.ManageProtocols');
    if (refused) return refused;
    const serviceId = params.serviceId as string;
    const protocol = getDb().protocols.find((p) => p.serviceId === serviceId);
    if (!protocol || !SERVICES.some((s) => s.id === serviceId)) return notFound();
    const body = (await request.json()) as UpsertProtocolRequest;
    const errors = [];
    if (!body.fastingInstructions?.trim())
      errors.push(
        bad('FastingInstructions', 'NotEmptyValidator', 'Write the fasting instructions.'),
      );
    if (!body.postOpInstructions?.trim())
      errors.push(
        bad('PostOpInstructions', 'NotEmptyValidator', 'Write the post-op instructions.'),
      );
    body.followUps.forEach((f, i) => {
      if (f.windowFromDay > f.dayOffset || f.dayOffset > f.windowToDay) {
        errors.push(
          bad(
            `FollowUps[${i}].DayOffset`,
            'FollowUpOutsideWindow',
            'A follow-up day must sit inside its own window.',
          ),
        );
      }
    });
    if (errors.length > 0) return violation(...errors);
    Object.assign(protocol, {
      isDefault: false,
      fastingInstructions: body.fastingInstructions.trim(),
      fastingHours: body.fastingHours,
      postOpInstructions: body.postOpInstructions.trim(),
      followUps: body.followUps,
      updatedOn: new Date().toISOString(),
      version: protocol.version + 1,
    });
    persist();
    return ok({ serviceId });
  }),

  http.get('*/api/staff/surgery-campaigns/applicable', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const url = new URL(request.url);
    return ok(
      applicableCampaigns(
        branchOf(request),
        url.searchParams.get('serviceId') ?? '',
        url.searchParams.get('date') ?? '',
      ),
    );
  }),

  http.get('*/api/staff/surgery-campaigns/:id/cases', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const campaign = getDb().campaigns.find((c) => c.id === params.id);
    if (!campaign) return notFound();
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const zone = zoneOfBranch(branchOf(request));
    const rows = getDb()
      .surgeries.filter((s) => s.campaignId === campaign.id && s.branchId === branchOf(request))
      .filter((s) => {
        const day = localDateOf(s.startsOn, zone);
        return (!from || day >= from) && (!to || day <= to);
      })
      .map((s) => ({
        surgeryId: s.id,
        catId: s.catId,
        catName: findCat(s.catId)?.name ?? 'Cat',
        procedure: findService(s.serviceId)!.name,
        startsOn: s.startsOn,
        status: s.status,
        outcome: s.outcome,
        campaignPrice: money(s.priceAmount),
        standardPrice: money(findService(s.serviceId)!.price),
        eligibilityNote: s.eligibilityNote,
      }));
    const counted = rows.filter((r) => r.status !== 'Cancelled');
    const page = paged(rows, url);
    return ok({
      campaignId: campaign.id,
      code: campaign.code,
      name: campaign.name,
      cases: page.items,
      totals: {
        count: counted.length,
        campaignTotal: money(counted.reduce((sum, r) => sum + Number(r.campaignPrice.amount), 0)),
        standardTotal: money(counted.reduce((sum, r) => sum + Number(r.standardPrice.amount), 0)),
      },
      pageNumber: page.pageNumber,
      pageSize: page.pageSize,
      totalCases: page.totalCount,
    });
  }),

  http.get('*/api/staff/surgery-campaigns', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const url = new URL(request.url);
    const activeOnly = url.searchParams.get('activeOnly') === 'true';
    return ok(
      paged(
        getDb()
          .campaigns.filter((c) => !activeOnly || c.isActive)
          .map(campaignView),
        url,
      ),
    );
  }),

  http.post('*/api/staff/surgery-campaigns', async ({ request }) => {
    const refused = requireAction(request, 'Surgery.ManageCampaigns');
    if (refused) return refused;
    const body = (await request.json()) as CampaignInput & { code: string };
    const errors = validateInput(body);
    if (!body.code?.trim())
      errors.push(bad('Code', 'NotEmptyValidator', 'Give the campaign a code.'));
    if (getDb().campaigns.some((c) => c.code.toLowerCase() === body.code?.trim().toLowerCase())) {
      errors.push(
        bad('Code', 'CampaignCodeAlreadyExists', 'A campaign with this code already exists.'),
      );
    }
    if (errors.length > 0) return violation(...errors);
    const created: MCampaign = {
      id: newId(),
      version: 1,
      code: body.code.trim(),
      name: body.name.trim(),
      description: body.description?.trim() ?? '',
      validFrom: body.validFrom,
      validTo: body.validTo,
      branchIds: body.branchIds,
      prices: body.prices,
      maxCases: body.maxCases ?? null,
      isActive: true,
    };
    getDb().campaigns.push(created);
    persist();
    return ok({ campaignId: created.id }, 201);
  }),

  http.put('*/api/staff/surgery-campaigns/:id', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.ManageCampaigns');
    if (refused) return refused;
    const campaign = getDb().campaigns.find((c) => c.id === params.id);
    if (!campaign) return notFound();
    const body = (await request.json()) as CampaignInput & { expectedVersion: number };
    if (body.expectedVersion !== campaign.version) {
      return conflict(
        bad(
          'ExpectedVersion',
          'StaleVersion',
          'Someone else changed this campaign. Reload to see their changes.',
        ),
      );
    }
    const errors = validateInput(body);
    const locked = campaignView(campaign).pricesLocked;
    const changed = JSON.stringify(body.prices) !== JSON.stringify(campaign.prices);
    if (locked && changed) {
      errors.push(
        bad(
          'Prices',
          'CampaignPriceLocked',
          'Prices can’t change once a surgery has been booked under this campaign.',
        ),
      );
    }
    if (errors.length > 0) return violation(...errors);
    Object.assign(campaign, {
      name: body.name.trim(),
      description: body.description?.trim() ?? '',
      validFrom: body.validFrom,
      validTo: body.validTo,
      branchIds: body.branchIds,
      prices: body.prices,
      maxCases: body.maxCases ?? null,
      version: campaign.version + 1,
    });
    persist();
    return noContent();
  }),

  http.post('*/api/staff/surgery-campaigns/:id/active', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.ManageCampaigns');
    if (refused) return refused;
    const campaign = getDb().campaigns.find((c) => c.id === params.id);
    if (!campaign) return notFound();
    campaign.isActive = ((await request.json()) as { isActive: boolean }).isActive;
    campaign.version += 1;
    persist();
    return noContent();
  }),
];
