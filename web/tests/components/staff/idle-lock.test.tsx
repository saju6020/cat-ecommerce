import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider, onlineManager } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { IdleLock } from '@/components/staff/IdleLock';
import { StaffSessionGuard } from '@/components/staff/StaffSessionGuard';
import { StaffLockProvider } from '@/components/staff/IdleLockContext';
import { STAFF_SESSION_QUERY_KEY } from '@/lib/hooks/useSession';

const replace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace }),
  usePathname: () => '/staff/cats',
}));

// ARCH gate HIGH (second pass) — `locked` now lives in `StaffLockProvider`, one level above
// `StaffSessionGuard` (`(staff)/layout.tsx`'s own composition); every render in this file needs it,
// the same way every render already needs `QueryClientProvider`.
function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    queryClient,
    ...render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>{children}</StaffLockProvider>
      </QueryClientProvider>,
    ),
  };
}

function statusBody(overrides: Partial<Record<string, unknown>> = {}) {
  const now = new Date();
  return {
    staffAccountId: 'staff-1',
    fullName: 'Ada Administrator',
    roleIds: ['Administrator'],
    issuedOn: now.toISOString(),
    lastActivityOn: now.toISOString(),
    idleExpiresOn: new Date(now.getTime() + 30 * 60_000).toISOString(),
    absoluteExpiresOn: new Date(now.getTime() + 12 * 60 * 60_000).toISOString(),
    idleTimeoutMinutes: 30,
    serverTime: now.toISOString(),
    ...overrides,
  };
}

// U-G2 — a single `advanceTimersByTimeAsync(0)` only flushes one microtask/macrotask hop; the
// fetch → `setStatus` → countdown-effect → `setTimeout` chain (and later, timer fires →
// `setWarning`/`lockNow` → commit) needs several. 0ms advances never move the fake clock itself.
async function flushFakeTimerEffects() {
  for (let i = 0; i < 10; i += 1) {
    await vi.advanceTimersByTimeAsync(0);
  }
}

function TestForm() {
  return (
    <div>
      <label htmlFor="draft">Draft field</label>
      <input id="draft" />
    </div>
  );
}

describe('IdleLock — US-ACC-08', () => {
  beforeEach(() => {
    server.use(http.get('*/api/staff/session', () => HttpResponse.json(statusBody())));
    replace.mockClear();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('renders children immediately, with no lock dialog open', async () => {
    renderWithClient(
      <IdleLock>
        <TestForm />
      </IdleLock>,
    );
    expect(screen.getByLabelText(/draft field/i)).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('U-G2: the idle warning appears at idleExpiresOn − 2min, computed from serverTime, correcting for a skewed local clock', async () => {
    // The local clock is wrong: it thinks the current moment is `wrongNow`, five minutes behind
    // the server's own `serverTime`. If `IdleLock` used raw `Date.now()` instead of correcting via
    // `clockOffsetMs` (server time minus local time at fetch), the warning would not appear until
    // 5 real minutes after the assertion below — this test would then time out waiting for it.
    const wrongNow = new Date('2025-01-01T00:00:00.000Z');
    vi.useFakeTimers();
    vi.setSystemTime(wrongNow);
    const realServerNow = new Date(wrongNow.getTime() + 5 * 60_000); // server is 5 min "ahead"
    const idleExpiresOn = new Date(realServerNow.getTime() + 125_000); // 2min5s of real time left
    server.use(
      http.get('*/api/staff/session', () =>
        HttpResponse.json(
          statusBody({
            serverTime: realServerNow.toISOString(),
            lastActivityOn: realServerNow.toISOString(),
            idleExpiresOn: idleExpiresOn.toISOString(),
          }),
        ),
      ),
    );

    renderWithClient(
      <IdleLock>
        <TestForm />
      </IdleLock>,
    );
    // Flushes the initial `GET /api/staff/session` promise chain, and lets React commit the state
    // updates and effects it triggers (`setStatus`, then the countdown effect that actually
    // schedules the warning timer — more than one microtask/render hop), all on the SAME fake
    // clock this test controls throughout. Real `findBy*`/`waitFor` polling needs real timers (see
    // the sibling describe block below), which are not an option here.
    await flushFakeTimerEffects();
    expect(screen.getByLabelText(/draft field/i)).toBeInTheDocument();
    expect(screen.queryByText(/signed out for inactivity in 2 minutes/i)).not.toBeInTheDocument();

    // 4999ms of virtual time later — 1ms before the correctly-computed warning lead — still
    // nothing. Proves the assertion below is about the warning firing at the RIGHT time, not just
    // eventually.
    await vi.advanceTimersByTimeAsync(4_999);
    expect(screen.queryByText(/signed out for inactivity in 2 minutes/i)).not.toBeInTheDocument();

    await vi.advanceTimersByTimeAsync(2);
    await flushFakeTimerEffects(); // lets the `setWarning(true)` from the fired timer commit
    expect(screen.getByText(/signed out for inactivity in 2 minutes/i)).toBeInTheDocument();
  });

  it('U-G3: posts at most one activity heartbeat per 60s of keydown events', async () => {
    let activityCalls = 0;
    server.use(
      http.post('*/api/staff/session/activity', () => {
        activityCalls += 1;
        return HttpResponse.json(statusBody());
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithClient(
      <IdleLock>
        <TestForm />
      </IdleLock>,
    );
    await screen.findByLabelText(/draft field/i);

    await user.type(screen.getByLabelText(/draft field/i), 'ab');
    await waitFor(() => expect(activityCalls).toBe(1));
  });

  it('U-G4: a session_idle_timeout from any call shows the lock overlay above the still-mounted form, with typed values intact', async () => {
    const user = userEvent.setup();

    renderWithClient(
      <IdleLock>
        <TestForm />
      </IdleLock>,
    );
    await screen.findByLabelText(/draft field/i);
    // Typed BEFORE anything reports idle-timeout, so nothing here is racing the lock's own
    // "hide the background" behaviour — the assertion below is about what survives the lock, not
    // about typing through it.
    await user.type(screen.getByLabelText(/draft field/i), 'unsaved work');

    // ARCH gate HIGH 2 (first pass) — "any call" is exercised for real here, not simulated via
    // `lib/staffIdleLockBridge.ts` (removed): a plain, unrelated `apiFetch` call (standing in for
    // a real staff-audience endpoint like `/api/cats` or `/api/vaccines`) reports the
    // idle-timeout, reaching `IdleLock` through the SAME `client.ts` session-error handler this
    // component registers on itself (its own comment) — never the activity heartbeat specifically,
    // which this test deliberately does NOT rely on triggering the lock.
    server.use(
      http.get('*/api/some-unrelated-staff-endpoint', () =>
        HttpResponse.json(
          { error: 'session_idle_timeout', idleTimeoutMinutes: 30 },
          { status: 401 },
        ),
      ),
      http.post('*/api/staff/auth/sign-in', () =>
        HttpResponse.json({ staffAccountId: 'staff-1', expiresOn: new Date().toISOString() }),
      ),
      http.get('*/api/staff/session', () => HttpResponse.json(statusBody())),
    );
    const { apiFetch } = await import('@/lib/api/client');
    await expect(apiFetch('/api/some-unrelated-staff-endpoint')).rejects.toThrow();

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading')).toHaveTextContent(/signed out after/i);
    // The page stayed mounted — the typed value survived the lock.
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe('unsaved work');
    // SEC gate LOW (first pass) — the lock's own overlay is opaque (bg-surface-page, no alpha
    // channel in either theme), not the shared dialog default (bg-ink/40, translucent) — the
    // whole point being that nothing behind it stays readable.
    expect(document.querySelector('.bg-surface-page')).not.toBeNull();
    expect(document.querySelector('.bg-ink\\/40')).toBeNull();

    await user.type(
      within(dialog).getByLabelText(/mobile number or email/i),
      'admin@purrcare.test',
    );
    await user.type(within(dialog).getByLabelText(/^password/i), 'AdminPass123!');
    await user.click(within(dialog).getByRole('button', { name: /^sign in$/i }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe('unsaved work');
  });

  it('ARCH gate HIGH 1 (first pass): a window-refocus-triggered session_idle_timeout locks in place — it never redirects or unmounts the form', async () => {
    // The real bug: `refetchOnWindowFocus` (staleTime 10s on `['session','staff']`) refetches
    // `GET /api/staff/session` whenever a member of staff returns to the tab. If that refetch
    // lands after the idle window has already passed server-side, the response is a
    // `401 session_idle_timeout`, not `session_invalid` — and `StaffSessionGuard`, nested directly
    // above `IdleLock` exactly as `app/(staff)/layout.tsx` composes them, must not treat that as
    // "no session" (it used to, collapsing both into the same `null` and redirecting, which
    // unmounted `IdleLock` and every open form along with it).
    // The endpoint only starts reporting idle-timeout once the "focus" refetch below actually
    // runs — seeding fresh (non-stale) data into the cache BEFORE mounting means the initial
    // render never fetches at all (staleTime 10s), so it can't race this override.
    server.use(
      http.get('*/api/staff/session', () =>
        HttpResponse.json(
          { error: 'session_idle_timeout', idleTimeoutMinutes: 20 },
          { status: 401 },
        ),
      ),
      // Typing below fires a real activity heartbeat too — this test seeds the session query's
      // cache directly rather than a real sign-in, so there is no real `pc_staff_session` cookie
      // for that separate request to authenticate with; without this override it would 401 with
      // `session_invalid` on its own and redirect, which is a correct but different code path
      // (ARCH gate HIGH 2's global handler) than the one this test isolates.
      http.post('*/api/staff/session/activity', () => HttpResponse.json(statusBody())),
    );
    const user = userEvent.setup({ delay: null });
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(STAFF_SESSION_QUERY_KEY, statusBody());
    render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>
          <StaffSessionGuard>
            <IdleLock>
              <TestForm />
            </IdleLock>
          </StaffSessionGuard>
        </StaffLockProvider>
      </QueryClientProvider>,
    );

    await screen.findByLabelText(/draft field/i);
    await user.type(screen.getByLabelText(/draft field/i), 'unsaved on refocus');

    // The deterministic equivalent of a real browser's `focusManager` firing on tab-refocus —
    // the e2e counterpart of this test exercises the real trigger end to end.
    await queryClient.refetchQueries({ queryKey: STAFF_SESSION_QUERY_KEY });

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading')).toHaveTextContent(/signed out after 20 minutes/i);
    // Never redirected — the guard kept `children` (this `IdleLock`) mounted throughout.
    expect(replace).not.toHaveBeenCalled();
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe(
      'unsaved on refocus',
    );
  });

  it('ARCH gate HIGH (second pass): while locked, a later session_invalid (real API behaviour after it ends the idle session) never redirects or unmounts the form — only re-authenticating as the same person unlocks it', async () => {
    // The real API ends the staff session on the FIRST idle-timeout detection and expires
    // pc_staff_session (SessionAuthenticationMiddleware's EndForIdleTimeout + ExpireStaffSession).
    // Every LATER call then finds no session at all and reports session_invalid, never
    // session_idle_timeout again — indistinguishable from a genuine sign-out unless the guard and
    // IdleLock's own client.ts handler both know the terminal is already locked.
    server.use(
      http.get('*/api/staff/session', () =>
        HttpResponse.json(
          { error: 'session_idle_timeout', idleTimeoutMinutes: 20 },
          { status: 401 },
        ),
      ),
      http.post('*/api/staff/session/activity', () => HttpResponse.json(statusBody())),
    );
    const user = userEvent.setup({ delay: null });
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(STAFF_SESSION_QUERY_KEY, statusBody());
    render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>
          <StaffSessionGuard>
            <IdleLock>
              <TestForm />
            </IdleLock>
          </StaffSessionGuard>
        </StaffLockProvider>
      </QueryClientProvider>,
    );

    await screen.findByLabelText(/draft field/i);
    await user.type(screen.getByLabelText(/draft field/i), 'unsaved across the whole cycle');

    // Step 1: lock, the same way as the ARCH gate HIGH 1 (first pass) test — a focus-triggered
    // session_idle_timeout on the guard/IdleLock's own ['session','staff'] query.
    await queryClient.refetchQueries({ queryKey: STAFF_SESSION_QUERY_KEY });
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading')).toHaveTextContent(/signed out after 20 minutes/i);

    // Step 2: "tab away, then back" — another focus refetch of the SAME query, now resolving to
    // session_invalid/null (the real API's actual behaviour on the second and every later read,
    // since it already ended the session on step 1's detection). Must NOT redirect or unmount.
    server.use(
      http.get('*/api/staff/session', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    await queryClient.refetchQueries({ queryKey: STAFF_SESSION_QUERY_KEY });
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe(
      'unsaved across the whole cycle',
    );

    // Step 3: a PAGE query (never the session-status endpoint itself, which always sets
    // skipSessionErrorHandling) also reports session_invalid — the other path into IdleLock's own
    // client.ts handler (ARCH gate HIGH 2, first pass). Must also stay locked: no cache clear, no
    // redirect.
    server.use(
      http.get('*/api/some-unrelated-staff-endpoint', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    const { apiFetch } = await import('@/lib/api/client');
    await expect(apiFetch('/api/some-unrelated-staff-endpoint')).rejects.toThrow();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe(
      'unsaved across the whole cycle',
    );

    // Step 4: only re-authenticating as the SAME staff member unlocks it — the draft is still there.
    server.use(
      http.post('*/api/staff/auth/sign-in', () =>
        HttpResponse.json({ staffAccountId: 'staff-1', expiresOn: new Date().toISOString() }),
      ),
      http.get('*/api/staff/session', () => HttpResponse.json(statusBody())),
    );
    await user.type(
      within(dialog).getByLabelText(/mobile number or email/i),
      'admin@purrcare.test',
    );
    await user.type(within(dialog).getByLabelText(/^password/i), 'AdminPass123!');
    await user.click(within(dialog).getByRole('button', { name: /^sign in$/i }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe(
      'unsaved across the whole cycle',
    );
  });

  it('ARCHITECTURE gate third pass, HIGH: on a page WITH its own page query (staff account detail), lock -> refocus -> that page query 401s too -> the changed-role draft survives, never blanked by ErrorState -> unlock -> draft intact', async () => {
    // This is `app/(staff)/staff/admin/staff-accounts/[staffAccountId]/page.tsx`'s own control
    // flow, reproduced with `staffAccountId` as a plain prop instead of `use(params)` — a
    // `Promise`-wrapped `params` needs an extra Suspense-resolution `act()` tick entirely unrelated
    // to the race this test exists to prove, and introduces its own test-environment timing
    // friction to fight instead. `if (isError && !account) { ... }` below is copied verbatim from
    // the real page; the two ARE the same guard, exercised through the same real
    // `useStaffAccount`/`useStaffRoleCatalogue`/`useChangeStaffRoles` hooks and the same real
    // `StaffAccountDetail` component.
    const { useStaffAccount, useStaffRoleCatalogue } = await import('@/lib/hooks/useStaffAdmin');
    const { useChangeStaffRoles } = await import('@/lib/hooks/useStaffAdminMutations');
    const { StaffAccountDetail } = await import('@/components/admin/StaffAccountDetail');
    const { LoadingState } = await import('@/components/common/LoadingState');
    const { ErrorState } = await import('@/components/common/ErrorState');

    function StaffAccountDetailPageEquivalent({ staffAccountId }: { staffAccountId: string }) {
      const { data: account, isLoading, isError, refetch } = useStaffAccount(staffAccountId);
      const catalogue = useStaffRoleCatalogue();
      const changeRolesMutation = useChangeStaffRoles(staffAccountId);

      if (isLoading) return <LoadingState variant="card" rows={5} label="Loading staff account…" />;
      if (isError && !account) {
        return (
          <ErrorState
            description="We couldn't load this account just now."
            onRetry={() => refetch()}
          />
        );
      }
      if (!account) return null;

      return (
        <StaffAccountDetail
          account={account}
          catalogue={catalogue}
          onChangeRoles={(roleIds) => changeRolesMutation.mutateAsync(roleIds)}
          isPending={changeRolesMutation.isPending}
        />
      );
    }

    const account = {
      id: 'staff-1',
      fullName: 'Ada Administrator',
      mobileNumber: '01700000001',
      emailAddress: null,
      roleIds: ['Nurse'],
      hasPassword: true,
    };

    server.use(
      http.get('*/api/staff-roles', () =>
        HttpResponse.json({ roleIds: ['Nurse', 'Administrator'], capabilities: [], cells: [] }),
      ),
      http.post('*/api/staff/session/activity', () => HttpResponse.json(statusBody())),
      http.get('*/api/staff-accounts/staff-1', () => HttpResponse.json(account)),
    );

    const user = userEvent.setup({ delay: null });
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    // Both queries seeded fresh, exactly like the ARCH gate HIGH 1 (first pass) test above, so the
    // initial render fetches neither — only the deliberate `refetchQueries()` below does, mirroring
    // a real window-focus event refetching every stale active query at once.
    queryClient.setQueryData(STAFF_SESSION_QUERY_KEY, statusBody());
    queryClient.setQueryData(['staff-admin', 'staff-account', 'staff-1'], account);

    render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>
          <StaffSessionGuard>
            <IdleLock>
              <StaffAccountDetailPageEquivalent staffAccountId="staff-1" />
            </IdleLock>
          </StaffSessionGuard>
        </StaffLockProvider>
      </QueryClientProvider>,
    );

    // A changed role selection — the unsaved draft this whole mechanism exists to protect.
    const administratorCheckbox = await screen.findByRole('checkbox', { name: 'Administrator' });
    await user.click(administratorCheckbox);
    expect(administratorCheckbox).toBeChecked();

    // Step 1: the session query resolves session_idle_timeout — the same focus-triggered lock the
    // ARCH gate HIGH 1 (first pass) test above exercises.
    server.use(
      http.get('*/api/staff/session', () =>
        HttpResponse.json(
          { error: 'session_idle_timeout', idleTimeoutMinutes: 20 },
          { status: 401 },
        ),
      ),
    );
    await queryClient.refetchQueries({ queryKey: STAFF_SESSION_QUERY_KEY });

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading')).toHaveTextContent(/signed out after 20 minutes/i);
    // `getByRole` (not used here) respects `aria-hidden`, which Radix's own modal Dialog correctly
    // applies to the rest of the page while open (the same reason U-G4 above already uses
    // `getByLabelText`, not `getByRole`, to check its own draft field survives under the lock) —
    // `getByLabelText` finds it regardless, exactly as a non-role-based DOM inspection would.
    expect(screen.getByLabelText('Administrator')).toBeChecked();

    // Step 2: the page's OWN useStaffAccount() query independently 401s too — the exact race the
    // gate found (a real window-focus event refetches BOTH concurrently; triggered sequentially
    // here for a deterministic test, the same trade-off the ARCH gate HIGH 1 test above already
    // makes for the session query alone). `client.ts`'s session-error handler for this 401 is a
    // no-op once locked (IdleLock's own handler), but the QUERY ITSELF still rejects and sets
    // `isError: true`, keeping its last-known-good `data` (TanStack's own documented
    // refetch-failure behaviour) — this is the part `isError && !account` (not `isError` alone)
    // exists to survive.
    server.use(
      http.get('*/api/staff-accounts/staff-1', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    await queryClient.refetchQueries({ queryKey: ['staff-admin', 'staff-account', 'staff-1'] });

    // The page never swapped to ErrorState — the draft is still visible and still checked, exactly
    // the loss US-ACC-08 AC2/ADR-FE-010 exist to prevent. Still locked throughout.
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByLabelText('Administrator')).toBeChecked();
    expect(screen.queryByText(/couldn't load this account/i)).not.toBeInTheDocument();

    // Unlock via re-authentication as the same person; the account endpoint recovers, matching what
    // the real API does once a fresh session exists again.
    server.use(
      http.get('*/api/staff-accounts/staff-1', () => HttpResponse.json(account)),
      http.post('*/api/staff/auth/sign-in', () =>
        HttpResponse.json({ staffAccountId: 'staff-1', expiresOn: new Date().toISOString() }),
      ),
      http.get('*/api/staff/session', () => HttpResponse.json(statusBody())),
    );
    await user.type(
      within(dialog).getByLabelText(/mobile number or email/i),
      'admin@purrcare.test',
    );
    await user.type(within(dialog).getByLabelText(/^password/i), 'AdminPass123!');
    await user.click(within(dialog).getByRole('button', { name: /^sign in$/i }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('checkbox', { name: 'Administrator' })).toBeChecked();
  });

  it('U-G5: re-signing in as a different person shows a discard notice and reloads', async () => {
    const reloadSpy = vi.fn();
    const originalLocation = window.location;
    // @ts-expect-error -- jsdom location override for this test only
    delete window.location;
    // @ts-expect-error -- test stub
    window.location = { ...originalLocation, reload: reloadSpy };

    server.use(
      http.post('*/api/staff/auth/sign-in', () =>
        HttpResponse.json({
          staffAccountId: 'different-staff',
          expiresOn: new Date().toISOString(),
        }),
      ),
      http.post('*/api/staff/session/activity', () =>
        HttpResponse.json(
          { error: 'session_idle_timeout', idleTimeoutMinutes: 30 },
          { status: 401 },
        ),
      ),
    );

    const user = userEvent.setup();
    renderWithClient(
      <IdleLock>
        <TestForm />
      </IdleLock>,
    );
    await screen.findByLabelText(/draft field/i);
    // Locks for real via the activity heartbeat's own 401, same as U-G4 — see that test's comment.
    await user.type(screen.getByLabelText(/draft field/i), 'x');
    const dialog = await screen.findByRole('dialog');

    await user.type(
      within(dialog).getByLabelText(/mobile number or email/i),
      'nurse@purrcare.test',
    );
    await user.type(within(dialog).getByLabelText(/^password/i), 'NursePass123!');
    await user.click(within(dialog).getByRole('button', { name: /^sign in$/i }));

    expect(await screen.findByText(/signed in as a different person/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /continue/i }));
    expect(reloadSpy).toHaveBeenCalled();

    // @ts-expect-error -- restoring the jsdom location stub from above
    window.location = originalLocation;
  });

  it("TEST gate F1: a bystander session_invalid that resolves before the session query's own session_idle_timeout must not win the race and sign the user out", async () => {
    const user = userEvent.setup({ delay: null });

    // The session query's own request is the one that actually detects the idle timeout
    // server-side, but its response is deliberately slow to reach the browser — reproducing the
    // documented race (TEST gate F1 / ARCH gate third pass LOW): the bystander's own 401
    // (`session_invalid` — the real API's behaviour for every OTHER concurrent request once the
    // idle session has already ended, HO-ACC-002 §11.9) resolves FIRST.
    let releaseSessionResponse: () => void = () => {};
    const sessionResponseGate = new Promise<void>((resolve) => {
      releaseSessionResponse = resolve;
    });
    server.use(
      http.get('*/api/staff/session', async () => {
        await sessionResponseGate;
        return HttpResponse.json(
          { error: 'session_idle_timeout', idleTimeoutMinutes: 20 },
          { status: 401 },
        );
      }),
      http.get('*/api/some-unrelated-staff-endpoint', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(STAFF_SESSION_QUERY_KEY, statusBody());
    render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>
          <StaffSessionGuard>
            <IdleLock>
              <TestForm />
            </IdleLock>
          </StaffSessionGuard>
        </StaffLockProvider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText(/draft field/i);
    await user.type(screen.getByLabelText(/draft field/i), 'unsaved during the race');

    // The same window-focus batch: the session query's own (slow) refetch starts first...
    const sessionRefetch = queryClient.refetchQueries({ queryKey: STAFF_SESSION_QUERY_KEY });
    // ...and a bystander page query's call, whose response reaches the browser first.
    const { apiFetch } = await import('@/lib/api/client');
    await expect(apiFetch('/api/some-unrelated-staff-endpoint')).rejects.toThrow();

    // If the bug were present, the bystander's own `session_invalid` would already have cleared
    // the cache and redirected here, before the session query's slow response is ever released.
    // With the fix, the bystander's own handling is routed through an authoritative check that
    // dedupes onto the session query's still-pending fetch, so nothing has been decided yet.
    expect(replace).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe(
      'unsaved during the race',
    );

    // Now let the session query's own slow response through — this is the genuine idle detection.
    releaseSessionResponse();
    await sessionRefetch.catch(() => {});

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading')).toHaveTextContent(/signed out after 20 minutes/i);
    expect(replace).not.toHaveBeenCalled();
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe(
      'unsaved during the race',
    );
  });

  it('CODE_REVIEW gate seventh pass, HIGH: the authoritative session check must not be satisfied by a recently-fresh cached session read (staleTime 30s, matching app/providers.tsx)', async () => {
    const user = userEvent.setup({ delay: null });

    server.use(
      http.get('*/api/staff/session', () =>
        HttpResponse.json(
          { error: 'session_idle_timeout', idleTimeoutMinutes: 20 },
          { status: 401 },
        ),
      ),
      http.get('*/api/some-unrelated-staff-endpoint', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );

    // Matches `app/providers.tsx`'s own `QueryClient` default (`staleTime: 30_000`) — every OTHER
    // test in this file uses `retry: false` only (an implicit `staleTime: 0`), which never exercises
    // `fetchQuery`'s own "the cache is still fresh, skip the fetch entirely" branch. That branch is
    // exactly what let a stale-but-not-yet-expired cached session silently win over a genuine sign-
    // out (CODE_REVIEW gate seventh pass, HIGH).
    const queryClient = new QueryClient({
      defaultOptions: { queries: { staleTime: 30_000, retry: false } },
    });
    // Pre-seeded as though a successful `GET /api/staff/session` had resolved moments ago — well
    // inside the 30s staleTime, and deliberately NOT refetched here, so the query is not in flight
    // either: the only way to learn the session has actually ended since is a genuinely fresh fetch,
    // not a reuse of this cached "still signed in" answer.
    queryClient.setQueryData(STAFF_SESSION_QUERY_KEY, statusBody());

    render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>
          <StaffSessionGuard>
            <IdleLock>
              <TestForm />
            </IdleLock>
          </StaffSessionGuard>
        </StaffLockProvider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText(/draft field/i);
    await user.type(screen.getByLabelText(/draft field/i), 'unsaved despite fresh cache');

    const { apiFetch } = await import('@/lib/api/client');
    await expect(apiFetch('/api/some-unrelated-staff-endpoint')).rejects.toThrow();

    // Without `staleTime: 0` on the authoritative check, `fetchQuery` sees the 30s-fresh cache and
    // resolves instantly to the pre-seeded "still signed in" data, never touching the network —
    // silently ignoring the idle timeout the `/api/staff/session` handler above actually reports.
    // With the fix, the check always fetches fresh (deduping onto any in-flight fetch instead, when
    // there is one) and locks.
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading')).toHaveTextContent(/signed out after 20 minutes/i);
    expect(replace).not.toHaveBeenCalled();
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe(
      'unsaved despite fresh cache',
    );
  });

  it('TEST gate F1 (genuine sign-out preserved): a bystander session_invalid for a genuinely revoked/unknown session still signs the user out', async () => {
    server.use(
      http.get('*/api/staff/session', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
      http.get('*/api/some-unrelated-staff-endpoint', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(STAFF_SESSION_QUERY_KEY, statusBody());
    queryClient.setQueryData(['some', 'unrelated', 'staff', 'data'], { secret: true });

    render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>
          <StaffSessionGuard>
            <IdleLock>
              <TestForm />
            </IdleLock>
          </StaffSessionGuard>
        </StaffLockProvider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText(/draft field/i);

    const { apiFetch } = await import('@/lib/api/client');
    await expect(apiFetch('/api/some-unrelated-staff-endpoint')).rejects.toThrow();

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith(expect.stringContaining('/staff/sign-in')),
    );
    expect(queryClient.getQueryData(['some', 'unrelated', 'staff', 'data'])).toBeUndefined();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('ARCHITECTURE gate fifth pass, MEDIUM: locking while already offline does not deadlock the re-sign-in mutation, and unlock resyncs onlineManager with reality', async () => {
    const user = userEvent.setup();

    renderWithClient(
      <IdleLock>
        <TestForm />
      </IdleLock>,
    );
    await screen.findByLabelText(/draft field/i);
    await user.type(screen.getByLabelText(/draft field/i), 'unsaved while offline');

    // Simulates the terminal already being offline the instant the lock takes hold (Wi-Fi dropped,
    // lid closed, ...) — onlineManager's REAL listener is still attached at this point (nothing is
    // locked yet), so this reflects reality exactly the way a real `offline` event would.
    onlineManager.setOnline(false);

    server.use(
      http.get('*/api/some-unrelated-staff-endpoint', () =>
        HttpResponse.json(
          { error: 'session_idle_timeout', idleTimeoutMinutes: 30 },
          { status: 401 },
        ),
      ),
      http.post('*/api/staff/auth/sign-in', () =>
        HttpResponse.json({ staffAccountId: 'staff-1', expiresOn: new Date().toISOString() }),
      ),
      http.get('*/api/staff/session', () => HttpResponse.json(statusBody())),
    );
    const { apiFetch } = await import('@/lib/api/client');
    await expect(apiFetch('/api/some-unrelated-staff-endpoint')).rejects.toThrow();

    const dialog = await screen.findByRole('dialog');
    // Still frozen at `false` — the lock's own listener swap means a real reconnect could never
    // have reached `onlineManager` from here on, exactly the scenario that used to deadlock.
    expect(onlineManager.isOnline()).toBe(false);

    // Without ARCHITECTURE gate fifth pass's `networkMode: 'always'` fix on `useSignInStaff()`, this
    // would never even invoke `signInStaff` — TanStack's default `networkMode: 'online'` pauses a
    // mutation indefinitely while `onlineManager.isOnline()` reports `false`, and nothing left in
    // this test could ever flip it back (the lock's own listener stays detached until `unlock()`,
    // which only runs once this very mutation succeeds). The dialog actually closing below is the
    // proof the mutation ran despite the frozen `false`.
    await user.type(
      within(dialog).getByLabelText(/mobile number or email/i),
      'admin@purrcare.test',
    );
    await user.type(within(dialog).getByLabelText(/^password/i), 'AdminPass123!');
    await user.click(within(dialog).getByRole('button', { name: /^sign in$/i }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect((screen.getByLabelText(/draft field/i) as HTMLInputElement).value).toBe(
      'unsaved while offline',
    );

    // unlock() resynced onlineManager with reality (navigator.onLine — jsdom's own default `true`,
    // unchanged by this test) instead of leaving it stuck at the `false` forced above, which no real
    // event ever had the chance to correct on its own.
    expect(onlineManager.isOnline()).toBe(true);
  });

  it('ACC 8: a bystander session_invalid that resolves after IdleLock unmounted never calls router.replace', async () => {
    let releaseSessionResponse: () => void = () => {};
    const gate = new Promise<void>((resolve) => {
      releaseSessionResponse = resolve;
    });
    server.use(
      http.get('*/api/staff/session', async () => {
        await gate;
        return HttpResponse.json({ error: 'session_invalid' }, { status: 401 });
      }),
      http.get('*/api/some-unrelated-staff-endpoint', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(STAFF_SESSION_QUERY_KEY, statusBody());
    const view = render(
      <QueryClientProvider client={queryClient}>
        <StaffLockProvider>
          <IdleLock>
            <TestForm />
          </IdleLock>
        </StaffLockProvider>
      </QueryClientProvider>,
    );
    await screen.findByLabelText(/draft field/i);

    const sessionRefetch = queryClient.refetchQueries({ queryKey: STAFF_SESSION_QUERY_KEY });
    const { apiFetch } = await import('@/lib/api/client');
    await expect(apiFetch('/api/some-unrelated-staff-endpoint')).rejects.toThrow();

    view.unmount(); // the user navigated away while the authoritative check was in flight
    releaseSessionResponse();
    await sessionRefetch.catch(() => {});
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(replace).not.toHaveBeenCalled();
  });
});
