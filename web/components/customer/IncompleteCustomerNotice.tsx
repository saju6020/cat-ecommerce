/**
 * `components/customer/IncompleteCustomerNotice.tsx` — US-ACC-10. OQ-5's default wording (no
 * owner answer recorded yet — flagged in the gate report per §20.8) plus HO-ACC-001 §4.3's
 * Development-only reset-delivery note (§20.4: never fake delivery or a "show me the link" button).
 */
export function IncompleteCustomerNotice() {
  return (
    <div className="rounded-[var(--radius-md)] border border-warning-ink/30 bg-warning-bg p-4 text-sm text-warning-ink">
      <p>
        Ask the customer to choose &ldquo;Forgot password&rdquo; online with this phone number to
        set their password.
      </p>
      <p className="mt-1">
        Online password setup isn&rsquo;t available yet; the customer can be served in the clinic.
      </p>
    </div>
  );
}
