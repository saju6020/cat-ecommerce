import { redirect } from 'next/navigation';

/** `/staff/procurement` — the section has no landing page of its own; Suppliers is its first tab. */
export default function ProcurementIndexPage(): never {
  redirect('/staff/procurement/suppliers');
}
