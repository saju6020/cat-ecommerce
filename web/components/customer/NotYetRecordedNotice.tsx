/** `components/customer/NotYetRecordedNotice.tsx` — ⭐ US-ACC-11, ADR-FE-012. Data the system does
 *  not record is stated as not recorded, never shown as zero. */
export function NotYetRecordedNotice() {
  return (
    <p className="text-xs text-ink-muted">
      Orders, invoices and balances aren&rsquo;t recorded in the system yet, so they can&rsquo;t be
      shown or moved.
    </p>
  );
}
