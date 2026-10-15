import fs from 'node:fs';
import path from 'node:path';

/**
 * The files the HO-ORD-002 guard scans cover (§12.3). `eslint.config.mjs` is forbidden scope for this
 * handover, so both compliance rules are Vitest source scans (`tests/guards/*.test.ts`) that fail the
 * suite. Mocks are excluded from the money rule on purpose: the mock server IS the server, and its
 * arithmetic is the server's.
 */
const ROOT = path.resolve(import.meta.dirname, '../..');

const ORDERING_COMPONENT_FOLDERS = [
  'cart',
  'checkout',
  'orders',
  'fulfilment',
  'returns',
  'refunds',
  'pos',
  'ordering-admin',
  'print',
];

const ORDERING_FILES = [
  'lib/api/cart.ts',
  'lib/api/checkout.ts',
  'lib/api/my-orders.ts',
  'lib/api/staff-orders.ts',
  'lib/api/staff-returns.ts',
  'lib/api/staff-refunds.ts',
  'lib/api/pos.ts',
  'lib/api/ordering-admin.ts',
  'lib/format/orders.ts',
  'lib/hooks/useCart.ts',
  'lib/hooks/useCheckout.ts',
  'lib/hooks/useMyOrders.ts',
  'lib/hooks/useStaffOrders.ts',
  'lib/hooks/useStaffReturns.ts',
  'lib/hooks/usePos.ts',
  'lib/hooks/useOrderingAdmin.ts',
  'lib/hooks/useScanner.ts',
  'lib/hooks/useIdempotencyKey.ts',
];

const ORDERING_APP_FOLDERS = [
  'app/(shop)/cart',
  'app/(portal)/checkout',
  'app/(portal)/portal/orders',
  'app/(staff)/staff/orders',
  'app/(staff)/staff/fulfilment',
  'app/(staff)/staff/returns',
  'app/(staff)/staff/refunds',
  'app/(staff)/staff/pos',
  'app/(staff)/staff/admin/delivery-zones',
  'app/(staff)/staff/admin/discount-codes',
  'app/(staff)/staff/admin/couriers',
  'app/(staff)/staff/admin/payment-settings',
];

function walk(directory: string): string[] {
  if (!fs.existsSync(directory)) return [];
  const found: string[] = [];
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const full = path.join(directory, entry.name);
    if (entry.isDirectory()) found.push(...walk(full));
    else if (/\.(ts|tsx)$/.test(entry.name)) found.push(full);
  }
  return found;
}

/** Every Ordering source file (absolute paths) the guards inspect. */
export function orderingSourceFiles(): string[] {
  const files = new Set<string>();
  for (const folder of ORDERING_COMPONENT_FOLDERS) {
    for (const file of walk(path.join(ROOT, 'components', folder))) files.add(file);
  }
  for (const folder of ORDERING_APP_FOLDERS) {
    for (const file of walk(path.join(ROOT, folder))) files.add(file);
  }
  for (const relative of ORDERING_FILES) {
    const full = path.join(ROOT, relative);
    if (fs.existsSync(full)) files.add(full);
  }
  return [...files].sort();
}

/** Every `.ts` / `.tsx` under `app/` and `components/` (the whole UI), for the card-input scan. */
export function allUiSourceFiles(): string[] {
  return [...walk(path.join(ROOT, 'app')), ...walk(path.join(ROOT, 'components'))].sort();
}

export function relativeToRoot(file: string): string {
  return path.relative(ROOT, file).split(path.sep).join('/');
}
