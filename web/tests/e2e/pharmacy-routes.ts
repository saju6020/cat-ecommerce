import type { Locator, Page } from '@playwright/test';
import { ids } from './pharmacy-helpers';

/**
 * HO-PHM-002 §8.1 — every route this handover adds, dynamic segments resolved with seeded MSW ids so
 * each renders real data. `role` is who signs in (the mock turns its nurse account into a Vet or a
 * Receptionist); `ready` is something visible once the route has actually rendered.
 */
export interface PharmacyRoute {
  path: string;
  role: 'Administrator' | 'Vet' | 'Reception' | 'Customer';
  ready: (page: Page) => Locator;
}

const heading = (name: RegExp | string) => (page: Page) =>
  page.getByRole('heading', { name }).first();
const label = (page: Page) => page.getByRole('article', { name: /medicine label/i });

export const PHARMACY_ROUTES: PharmacyRoute[] = [
  // Prescribing — under the cat's clinical header.
  {
    path: `/staff/cats/${ids.catWhiskersId}/prescriptions`,
    role: 'Vet',
    ready: heading('Prescriptions'),
  },
  {
    path: `/staff/cats/${ids.catWhiskersId}/prescriptions/new?visitId=${ids.visitOpenId}`,
    role: 'Vet',
    ready: heading('New prescription'),
  },
  {
    path: `/staff/cats/${ids.catMittensId}/prescriptions/${ids.rxMittensActiveId}`,
    role: 'Vet',
    ready: heading('RX-2026-000101'),
  },
  {
    path: `/staff/cats/${ids.catMittensId}/prescriptions/${ids.rxMittensActiveId}/renew`,
    role: 'Vet',
    ready: heading('Renew prescription'),
  },
  {
    path: `/staff/cats/${ids.catMittensId}/diet-authorisations`,
    role: 'Vet',
    ready: heading('Diet authorisations'),
  },
  {
    path: `/staff/cats/${ids.catMittensId}/diet-authorisations/new?visitId=${ids.visitTemplateId}`,
    role: 'Vet',
    ready: heading('Authorise a diet'),
  },
  // Formulary.
  { path: '/staff/admin/formulary', role: 'Administrator', ready: heading('Formulary') },
  { path: '/staff/admin/formulary/new', role: 'Administrator', ready: heading('New medicine') },
  {
    path: `/staff/admin/formulary/${ids.medMeloxicamId}`,
    role: 'Administrator',
    ready: heading('Meloxicam oral suspension'),
  },
  // Dispensary.
  { path: '/staff/pharmacy', role: 'Reception', ready: heading('Pharmacy') },
  {
    path: `/staff/pharmacy/dispense/${ids.rxMittensActiveId}`,
    role: 'Reception',
    ready: heading('Dispense'),
  },
  {
    path: `/staff/pharmacy/dispense/${ids.rxTigerControlledQueueId}`,
    role: 'Reception',
    ready: heading('Dispense'),
  },
  {
    path: `/staff/pharmacy/dispense/${ids.rxExpiredOnlyQueueId}`,
    role: 'Reception',
    ready: heading('Dispense'),
  },
  {
    path: `/staff/pharmacy/dispensings/${ids.dispensingControlledId}`,
    role: 'Vet',
    ready: heading(/Supply 1 of RX-2026-000088/),
  },
  {
    path: `/staff/pharmacy/dispensings/${ids.dispensingControlledId}/label`,
    role: 'Reception',
    ready: label,
  },
  // Controlled-drug register.
  {
    path: '/staff/pharmacy/controlled-drugs',
    role: 'Vet',
    ready: heading('Controlled-drug register'),
  },
  {
    path: '/staff/pharmacy/controlled-drugs?tab=balances',
    role: 'Vet',
    ready: heading('Controlled-drug register'),
  },
  {
    path: '/staff/pharmacy/controlled-drugs?tab=integrity',
    role: 'Vet',
    ready: heading('Controlled-drug register'),
  },
  {
    path: '/staff/pharmacy/controlled-drugs/eb000000-0000-4000-8000-000000000003',
    role: 'Vet',
    ready: heading('Register entry #1200'),
  },
  {
    path: '/staff/pharmacy/controlled-drugs/eb000000-0000-4000-8000-000000000003/correct',
    role: 'Vet',
    ready: heading('Add a correction'),
  },
  // The owner's portal.
  { path: '/portal/prescriptions', role: 'Customer', ready: heading('Prescriptions') },
  { path: '/portal/prescriptions?tab=past', role: 'Customer', ready: heading('Prescriptions') },
  { path: '/portal/prescriptions?tab=diets', role: 'Customer', ready: heading('Prescriptions') },
  {
    path: `/portal/prescriptions/${ids.rxMittensActiveId}`,
    role: 'Customer',
    ready: heading('Meloxicam oral suspension'),
  },
  {
    path: `/portal/prescriptions/${ids.rxMittensEndedId}`,
    role: 'Customer',
    ready: heading('Fusidic acid gel'),
  },
];
