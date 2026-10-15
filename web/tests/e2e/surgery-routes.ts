import type { Locator, Page } from '@playwright/test';
import type { SurgeryRole } from './surgery-helpers';
import { ids } from './surgery-helpers';

/**
 * Every route in HO-SRG-002 §8.1 with a role that can open it and a locator that is visible once the page
 * is ready. Dynamic segments are resolved with the mock's seeded ids.
 */
export interface SurgeryRoute {
  path: string;
  role: SurgeryRole;
  ready: (page: Page) => Locator;
}

const heading = (page: Page, name: RegExp | string) => page.getByRole('heading', { name }).first();

export const SURGERY_ROUTES: SurgeryRoute[] = [
  { path: '/staff/surgeries', role: 'Reception', ready: (p) => heading(p, 'Theatre day list') },
  { path: '/staff/surgeries/new', role: 'Reception', ready: (p) => heading(p, 'Book a surgery') },
  {
    path: `/staff/surgeries/${ids.surgeryScheduledTodayId}`,
    role: 'Nurse',
    ready: (p) => p.getByRole('heading', { name: 'Pre-op checklist' }),
  },
  {
    path: `/staff/surgeries/${ids.surgeryPreOpConsentOnlyId}#pre-op`,
    role: 'Vet',
    ready: (p) => p.getByRole('button', { name: 'Start with emergency override' }),
  },
  {
    path: `/staff/surgeries/${ids.surgeryInTheatreId}#anaesthesia`,
    role: 'Vet',
    ready: (p) => p.getByRole('heading', { name: 'Anaesthesia log' }),
  },
  {
    path: `/staff/surgeries/${ids.surgeryInTheatreId}#report`,
    role: 'Vet',
    ready: (p) => p.getByRole('heading', { name: 'End the procedure' }),
  },
  {
    path: `/staff/surgeries/${ids.surgeryRecoveryId}#report`,
    role: 'Vet',
    ready: (p) => p.getByRole('form', { name: 'Surgical report' }),
  },
  {
    path: `/staff/surgeries/${ids.surgeryOwnerAfterId}#report`,
    role: 'Vet',
    ready: (p) => p.getByText(/Finalised by/),
  },
  {
    path: `/staff/surgeries/${ids.surgeryRecoveryId}#recovery`,
    role: 'Nurse',
    ready: (p) => p.getByRole('heading', { name: 'Recovery status' }),
  },
  {
    path: '/staff/surgeries/e5000000-0000-4000-8000-0000000000a2#discharge',
    role: 'Nurse',
    ready: (p) => p.getByRole('form', { name: 'Discharge' }),
  },
  {
    path: `/staff/surgeries/${ids.surgeryOwnerAfterId}#discharge`,
    role: 'Nurse',
    ready: (p) => p.getByRole('link', { name: 'Print post-op sheet' }),
  },
  {
    path: `/staff/surgeries/${ids.surgeryOwnerAfterId}/post-op-sheet`,
    role: 'Nurse',
    ready: (p) => p.getByRole('article', { name: /Post-op sheet/ }),
  },
  {
    path: '/staff/surgeries/protocols',
    role: 'Vet',
    ready: (p) => heading(p, 'Surgery protocols'),
  },
  {
    path: '/staff/surgeries/campaigns',
    role: 'Administrator',
    ready: (p) => heading(p, 'Surgery campaigns'),
  },
  {
    path: `/staff/surgeries/campaigns/${ids.campaignOutreachId}`,
    role: 'Administrator',
    ready: (p) => p.getByLabel('Totals'),
  },
  {
    path: '/staff/front-desk?tab=recovery',
    role: 'Reception',
    ready: (p) => heading(p, 'Recovery'),
  },
  { path: '/portal/surgeries', role: 'Customer', ready: (p) => heading(p, 'Surgeries') },
  {
    path: `/portal/surgeries/${ids.surgeryOwnerBeforeId}`,
    role: 'Customer',
    ready: (p) => p.getByRole('heading', { name: 'Fasting instructions' }),
  },
  {
    path: `/portal/surgeries/${ids.surgeryRecoveryId}`,
    role: 'Customer',
    ready: (p) => p.getByText(/is in our care/),
  },
  {
    path: `/portal/surgeries/${ids.surgeryOwnerAfterId}`,
    role: 'Customer',
    ready: (p) => p.getByRole('link', { name: 'Book follow-up' }),
  },
];
