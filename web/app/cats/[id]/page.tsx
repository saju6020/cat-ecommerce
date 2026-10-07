'use client';

import { use, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useCustomerSession, useStaffSession } from '@/lib/hooks/useSession';
import { useCat } from '@/lib/hooks/useCat';
import { StaffSessionIdleTimeoutError } from '@/lib/api/staff-auth';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { sanitizeNextPath } from '@/lib/validation/nextPath';
import { isValidCatId } from '@/lib/validation/catId';

/**
 * `app/cats/[id]/page.tsx` — the cage-card QR's "smart redirect" page (HO-CAT-001 §1 Amended,
 * 2026-09-27, item (a): the QR now opens `{PublicSite:WebBaseUrl}/cats/{id}`; HO-CAT-002 §1
 * Amended, same date, records this as the pending item this file closes, deferred until
 * HO-ACC-002's session hooks existed to build it against). It asks "who is signed in" — the same
 * two session queries `app/page.tsx` already asks — and, for a signed-in customer only, "do they
 * own this cat", and sends the browser on:
 *
 *  - a signed-in **staff** member → `/staff/cats/{id}`, no cat call at all;
 *  - a signed-in **customer who owns this cat** → `/portal/cats/{id}`;
 *  - a signed-in **customer who does not own this cat** → `/staff/sign-in?next=/staff/cats/{id}`
 *    (owner decision, 2026-09-27 — see the HO-CAT-002 §1 Amended row dated the same day; this
 *    supersedes this file's own first version, which sent every signed-in customer straight to
 *    `/portal/cats/{id}` regardless of ownership);
 *  - **anyone else (not signed in at all)** → `/staff/sign-in?next=/staff/cats/{id}` too (the same
 *    owner decision — staff scan cage cards far more often than owners do, so staff sign-in is now
 *    the default destination for "not a recognised owner", not customer sign-in), so a real sign-in
 *    lands them back on `/staff/cats/{id}`, via `(auth)/staff/sign-in`'s own `sanitizeNextPath`
 *    re-check of the same value on the way back in (`/staff/` is one of its accepted prefixes).
 *
 * **If one browser carries both a staff session and a customer session** (a shared clinic device
 * is the realistic case), **staff wins** — checked first, exactly like `app/page.tsx`'s own
 * session-priority order, which this page deliberately mirrors rather than inventing a second
 * convention for "who is this browser" that could drift from the first. Staff never triggers the
 * ownership check below, or any cat call — staff wins outright, before either is even considered.
 *
 * **The ownership check reuses `useCat(catId)` — the exact same hook, and the exact same
 * `GET /api/cats/{catId}` call, the portal profile itself already makes** (owner decision,
 * 2026-09-27) — no new endpoint. `GET /api/cats/{catId}` applies the caller's own session-scoped
 * query filter server-side (HO-CAT-001 §20.3 — "a query filter, not authorization"), so a signed-in
 * customer who isn't this cat's owner gets the same `404` a nonexistent cat would. A successful
 * response is the only outcome treated as "owns this cat"; a `404`, a `403`, or any other response
 * that cannot tell "doesn't exist" apart from "not yours" is treated as "does not own this cat" —
 * never assumed to be ownership. `useCat`'s new `enabled` option (`lib/hooks/useCat.ts`) gates this
 * so the request is **only ever issued for a signed-in customer** — never for staff (who don't need
 * it) and never for an anonymous visitor, which is what keeps this page from leaking whether `{id}`
 * belongs to a real cat to someone with no session at all: an anonymous visitor triggers no cat
 * call, full stop, regardless of `{id}`.
 *
 * **A `5xx` or a network error from the ownership check fails closed** to the exact same
 * `/staff/sign-in?next=/staff/cats/{id}` destination a confirmed non-owner gets — trusting
 * ownership on the strength of a check that just failed is the wrong direction to fail in, the same
 * logic `CustomerSessionGuard`/`StaffSessionGuard`/`app/page.tsx` already apply to session
 * resolution itself (see below).
 *
 * **Session-resolution failure** (a network error, or a real 5xx from a session endpoint) is
 * treated exactly the same as "not signed in" — the same fail-closed convention already
 * established by `CustomerSessionGuard`, `StaffSessionGuard` and `app/page.tsx` (see their own
 * comments): trusting "signed in" on the strength of a check that just failed is the wrong
 * direction to fail in. There is deliberately no separate "couldn't check your session" screen —
 * landing on sign-in already tells the visitor exactly what to do next (NFR-USA-4), without a
 * second error surface to build and keep accessible for a case indistinguishable, from the
 * visitor's side, from simply not being signed in.
 *
 * **An `id` that cannot possibly be a cat id** (not a GUID — `lib/validation/catId.ts`,
 * HO-CAT-001's own `{catId:guid}` route constraint) short-circuits the redirect effect and the
 * render below before any session or ownership result is USED. It does not, and cannot, stop the
 * two session queries from being ISSUED — `useStaffSession()`/`useCustomerSession()` are called
 * unconditionally, as every React hook must be — but the ownership check specifically is never
 * reached for an invalid id, since `shouldCheckOwnership` below is gated on `validId` too.
 */
export default function CatSmartRedirectPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const staffSession = useStaffSession();
  const customerSession = useCustomerSession();

  const validId = isValidCatId(id);

  const isStaffIdleTimeout = staffSession.error instanceof StaffSessionIdleTimeoutError;
  const isStaffSignedIn = Boolean(staffSession.data) || isStaffIdleTimeout;
  const isCustomerSignedIn = Boolean(customerSession.data);
  const isSessionResolved =
    (!staffSession.isPending || isStaffIdleTimeout) && !customerSession.isPending;

  // Only a signed-in, non-staff customer ever triggers this — never staff (who win outright,
  // above), never an anonymous visitor (who must trigger no cat call at all).
  const shouldCheckOwnership =
    validId && isSessionResolved && !isStaffSignedIn && isCustomerSignedIn;
  // CAT row 1: ownership is proved only by a request made for THIS visit. `staleTime: 0` makes a
  // cached `['cat', id]` (the app's default 30 s staleTime would otherwise count it as fresh)
  // refetch, and nothing counts until that fetch has finished (`isFetchedAfterMount`, not fetching).
  const ownershipQuery = useCat(id, { enabled: shouldCheckOwnership, staleTime: 0 });
  const isOwnershipFetched = ownershipQuery.isFetchedAfterMount && !ownershipQuery.isFetching;
  const isOwner = isOwnershipFetched && ownershipQuery.isSuccess && Boolean(ownershipQuery.data);
  // A 404/403/5xx/network failure all finish the fetch with no `data` — every one of them falls
  // through to "not confirmed as owner" below, exactly as intended (see the doc comment above:
  // only a successful response counts as ownership).
  const isOwnershipResolved = !shouldCheckOwnership || isOwnershipFetched;

  const staffSignInWithNext = (() => {
    const next = sanitizeNextPath(
      `/staff/cats/${encodeURIComponent(id)}`,
      '/staff/cats',
      '/staff/cats',
    );
    return `/staff/sign-in?next=${encodeURIComponent(next)}`;
  })();

  useEffect(() => {
    if (!validId || !isSessionResolved) return;

    if (isStaffSignedIn) {
      router.replace(`/staff/cats/${encodeURIComponent(id)}`);
      return;
    }

    if (isCustomerSignedIn) {
      if (!isOwnershipResolved) return; // wait for the ownership check before deciding
      router.replace(isOwner ? `/portal/cats/${encodeURIComponent(id)}` : staffSignInWithNext);
      return;
    }

    router.replace(staffSignInWithNext);
  }, [
    validId,
    isSessionResolved,
    isStaffSignedIn,
    isCustomerSignedIn,
    isOwnershipResolved,
    isOwner,
    staffSignInWithNext,
    id,
    router,
  ]);

  if (!validId) {
    return (
      <div className="flex min-h-screen items-center justify-center p-6">
        <div className="w-full max-w-sm">
          <ErrorState
            title="This link isn't valid."
            description="Check the QR code or link on the cage card and try again."
          />
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-6">
      <div className="w-full max-w-sm">
        <LoadingState variant="card" rows={2} label="Loading…" />
      </div>
    </div>
  );
}
