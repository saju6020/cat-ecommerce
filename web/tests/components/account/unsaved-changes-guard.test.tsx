import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';

const push = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push }),
}));

function TestPage({ when }: { when: boolean }) {
  return (
    <div>
      <UnsavedChangesGuard when={when} />
      <a href="/portal/account/profile">Profile</a>
    </div>
  );
}

describe('UnsavedChangesGuard — ARCH gate MEDIUM (first pass)', () => {
  it('a same-document link click opens the shared ConfirmDialog, not window.confirm, and does not navigate until confirmed', async () => {
    const user = userEvent.setup();
    render(<TestPage when />);

    await user.click(screen.getByRole('link', { name: 'Profile' }));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/leave this page/i);
    expect(push).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: /^leave$/i }));
    await waitFor(() => expect(push).toHaveBeenCalledWith('/portal/account/profile'));
  });

  it('Cancel closes the dialog and never navigates', async () => {
    const user = userEvent.setup();
    render(<TestPage when />);

    await user.click(screen.getByRole('link', { name: 'Profile' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /cancel/i }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(push).not.toHaveBeenCalled();
  });

  it('when=false navigates through without any dialog', async () => {
    const user = userEvent.setup();
    render(<TestPage when={false} />);

    await user.click(screen.getByRole('link', { name: 'Profile' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  // CODE_REVIEW suggestion (second pass) — an absolute, cross-origin href (a different site,
  // effectively) is never a Next.js route `router.push` could resolve; left to navigate natively
  // instead (a real document unload, already covered by `beforeunload` above).
  it('an absolute, cross-origin link is never replayed through router.push', async () => {
    const user = userEvent.setup();
    render(
      <div>
        <UnsavedChangesGuard when />
        <a href="https://example.test/">External</a>
      </div>,
    );

    await user.click(screen.getByRole('link', { name: 'External' })).catch(() => {
      // jsdom logs, but does not throw for, an unimplemented cross-document navigation.
    });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });
});

describe('UnsavedChangesGuard — ARCH gate MEDIUM (second pass): Back/Forward (popstate)', () => {
  // CODE_REVIEW gate fourth pass, MEDIUM — the confirmed-Leave replay no longer calls
  // `history.go(-2)` in one shot; see the component's own doc comment above `isProgrammaticNavRef`.
  // It now calls `history.back()`, waits for that step's own `popstate`, then calls it again —
  // simulated here the same way every other test in this file simulates a real Back press, by
  // dispatching a synthetic `PopStateEvent` by hand.
  it('a Back press (popstate) opens the shared ConfirmDialog and replays it via two sequential history.back() calls on confirm', async () => {
    // CODE_REVIEW gate fifth pass, MEDIUM — `onPopState` now only reacts once a sentinel has
    // actually been pushed (`sentinelActiveRef`, the component's own doc comment); pinning
    // `history.length` here makes that precondition explicit, rather than relying on whatever real
    // value earlier tests in this file happened to leave behind.
    Object.defineProperty(window.history, 'length', { value: 2, configurable: true });
    const user = userEvent.setup();
    const backSpy = vi.spyOn(window.history, 'back').mockImplementation(() => {});
    render(<TestPage when />);

    // The real trigger is the browser's own Back button; a real click never fires this — the
    // component's sentinel-history-entry technique (its own comment) is what turns that
    // uncancellable navigation into this same, cancellable `popstate` event instead.
    window.dispatchEvent(new PopStateEvent('popstate'));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/leave this page/i);
    expect(backSpy).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole('button', { name: /^leave$/i }));
    // First leg: consumes the sentinel, lands on the guarded page itself.
    expect(backSpy).toHaveBeenCalledTimes(1);

    // That step's own popstate arriving for real triggers the second leg, attempting to move past
    // the guarded page too.
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(backSpy).toHaveBeenCalledTimes(2);

    backSpy.mockRestore();
    Reflect.deleteProperty(window.history, 'length');
  });

  it('Cancel after a Back press closes the dialog and never calls history.back', async () => {
    Object.defineProperty(window.history, 'length', { value: 2, configurable: true });
    const user = userEvent.setup();
    const backSpy = vi.spyOn(window.history, 'back').mockImplementation(() => {});
    render(<TestPage when />);

    window.dispatchEvent(new PopStateEvent('popstate'));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /cancel/i }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(backSpy).not.toHaveBeenCalled();

    backSpy.mockRestore();
    Reflect.deleteProperty(window.history, 'length');
  });

  it('when=false never opens the dialog on a popstate event', () => {
    render(<TestPage when={false} />);

    window.dispatchEvent(new PopStateEvent('popstate'));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

// CODE_REVIEW gate fourth pass, MEDIUM — `window.history.length === 1` (below) remains a fully
// reliable "nothing behind, nothing ahead" signal (it counts both directions, so a lone value of 1
// leaves no room for either) and is unchanged from ARCHITECTURE gate third pass, LOW (a). What
// changes is `length > 1`: it is NOT the reliable mirror of that check the third pass treated it
// as (it also counts forward entries), so it is now only ever a best-effort guess for "push a
// sentinel" — the two describe blocks below cover what actually makes a wrong guess safe.
describe('UnsavedChangesGuard — ARCHITECTURE gate third pass, LOW (a): the fully-reliable half of the first-history-entry case', () => {
  it('does not push a sentinel when history.length is exactly 1', () => {
    Object.defineProperty(window.history, 'length', { value: 1, configurable: true });
    const pushStateSpy = vi.spyOn(window.history, 'pushState');

    render(<TestPage when />);

    expect(pushStateSpy).not.toHaveBeenCalled();

    pushStateSpy.mockRestore();
    Reflect.deleteProperty(window.history, 'length');
  });

  it('does push a sentinel when history.length is greater than 1', () => {
    Object.defineProperty(window.history, 'length', { value: 2, configurable: true });
    const pushStateSpy = vi.spyOn(window.history, 'pushState');

    render(<TestPage when />);

    expect(pushStateSpy).toHaveBeenCalled();

    pushStateSpy.mockRestore();
    Reflect.deleteProperty(window.history, 'length');
  });
});

describe("UnsavedChangesGuard — CODE_REVIEW gate fifth pass, MEDIUM: survives Next.js's own history.state-stripping replaceState", () => {
  // The App Router patches `history.pushState`/`replaceState` globally and calls
  // `history.replaceState(historyState, '', canonicalUrl)` for the CURRENT entry whenever the
  // canonical URL is unchanged (a `router.refresh()`, a Server Action, a same-URL `<Link>`/
  // `router.push()`, or any ancestor layout doing the same) — `historyState`'s own base object is
  // `{}` unless `preserveCustomHistoryState` is `true` (only for the very first render and a
  // Back/Forward-triggered restore), discarding ANY existing custom key without firing a `popstate`
  // or otherwise notifying this component. The FOURTH-pass `history.state`-marker design
  // (`__unsavedGuardBase`) would have silently stopped reacting to every subsequent Back press once
  // that happened; this test proves the fifth-pass `sentinelActiveRef` redesign does not, by
  // reproducing that exact `replaceState({}, ...)` call by hand before simulating the user's Back
  // press.
  it('a router.refresh()-shaped replaceState({}, ...) while dirty does not disable the guard — the next Back press still opens the dialog', async () => {
    Object.defineProperty(window.history, 'length', { value: 2, configurable: true });
    const pushStateSpy = vi.spyOn(window.history, 'pushState');
    render(<TestPage when />);
    expect(pushStateSpy).toHaveBeenCalled(); // the sentinel was genuinely pushed
    pushStateSpy.mockClear();

    // Reproduces Next's own app-router.js replaceState call exactly: an empty base object,
    // discarding whatever custom state (Next's own `__NA`, or this component's marker in the
    // fourth-pass design) the CURRENT entry — the sentinel — carried.
    window.history.replaceState({}, '', window.location.href);

    window.dispatchEvent(new PopStateEvent('popstate'));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/leave this page/i);
    // The dialog re-armed the guard for a further Back press too, exactly as an unaffected sentinel
    // would have — proof this is not merely a one-off fallback but the same steady-state behaviour.
    expect(pushStateSpy).toHaveBeenCalledTimes(1);

    pushStateSpy.mockRestore();
    Reflect.deleteProperty(window.history, 'length');
  });
});

describe('UnsavedChangesGuard — CODE_REVIEW gate fourth pass, MEDIUM: back → forward → dirty → Back no longer strands the user on the sentinel', () => {
  it('a confirmed Leave still lands the user on the guarded page via the first history.back() leg, even though nothing is genuinely behind it', async () => {
    // The exact scenario `history.length` used to get wrong: `length` reports 2 (as it would after
    // a "back, then forward" sequence back onto this page), even though this page is genuinely the
    // tab's first entry — nothing really sits behind it. The old `window.history.length > 1` check
    // would have trusted that number and pushed a sentinel a later `history.go(-2)` could never
    // actually reach past, silently no-op-ing the whole confirmed Leave.
    Object.defineProperty(window.history, 'length', { value: 2, configurable: true });
    const user = userEvent.setup();
    const backSpy = vi.spyOn(window.history, 'back').mockImplementation(() => {});

    render(<TestPage when />);

    window.dispatchEvent(new PopStateEvent('popstate'));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^leave$/i }));

    // First leg: consumes the sentinel, lands on the guarded page itself — this always succeeds,
    // since the sentinel is guaranteed to be immediately behind a page that genuinely exists (this
    // one), regardless of whether anything real is further behind it.
    expect(backSpy).toHaveBeenCalledTimes(1);

    // Its own popstate arrives for real; the second leg fires, attempting to move past the guarded
    // page too.
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(backSpy).toHaveBeenCalledTimes(2);

    // Nothing is actually behind it (this IS the tab's first entry), so no THIRD call and no further
    // popstate ever arrives for the second leg — unlike a single `history.go(-2)`, which would have
    // made zero progress at all from the very first step, this technique still visibly moved the
    // user back to the guarded page rather than leaving them stuck exactly on the sentinel.
    expect(backSpy).toHaveBeenCalledTimes(2);

    backSpy.mockRestore();
    Reflect.deleteProperty(window.history, 'length');
  });
});

describe('UnsavedChangesGuard — ARCHITECTURE gate third pass, LOW (b): beforeunload is disarmed after a confirmed Leave', () => {
  it('a beforeunload event right after confirming Leave via history.back() is not intercepted', async () => {
    const user = userEvent.setup();
    const backSpy = vi.spyOn(window.history, 'back').mockImplementation(() => {});
    render(<TestPage when />);

    window.dispatchEvent(new PopStateEvent('popstate'));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^leave$/i }));
    expect(backSpy).toHaveBeenCalledTimes(1);

    // The confirmed Leave already resolved the user's decision through THIS dialog — a native
    // beforeunload prompt for the very same navigation would be a redundant second confirmation.
    const event = new Event('beforeunload', { cancelable: true });
    const preventDefaultSpy = vi.spyOn(event, 'preventDefault');
    window.dispatchEvent(event);

    expect(preventDefaultSpy).not.toHaveBeenCalled();

    backSpy.mockRestore();
  });

  it('a beforeunload event right after confirming Leave via a link click is not intercepted', async () => {
    const user = userEvent.setup();
    render(<TestPage when />);

    await user.click(screen.getByRole('link', { name: 'Profile' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^leave$/i }));
    await waitFor(() => expect(push).toHaveBeenCalledWith('/portal/account/profile'));

    const event = new Event('beforeunload', { cancelable: true });
    const preventDefaultSpy = vi.spyOn(event, 'preventDefault');
    window.dispatchEvent(event);

    expect(preventDefaultSpy).not.toHaveBeenCalled();
  });
});

describe('UnsavedChangesGuard — ARCHITECTURE/CODE_REVIEW gate third pass: exactly one live sentinel at a time', () => {
  it('removes the sentinel when the form goes clean, then pushes a fresh one the next time it goes dirty again — never two stacked at once', () => {
    Object.defineProperty(window.history, 'length', { value: 2, configurable: true });
    const pushStateSpy = vi.spyOn(window.history, 'pushState');
    const backSpy = vi.spyOn(window.history, 'back').mockImplementation(() => {});

    const { rerender } = render(<TestPage when />);
    expect(pushStateSpy).toHaveBeenCalledTimes(1);
    expect(backSpy).not.toHaveBeenCalled();

    // CODE_REVIEW gate third pass, MEDIUM — an ordinary successful save (the form goes clean
    // without navigating away) must pop the now-stray sentinel, not leave it sitting there to
    // swallow the user's next Back press.
    rerender(<TestPage when={false} />);
    expect(backSpy).toHaveBeenCalledTimes(1);

    // Going dirty again afterward pushes a genuinely NEW sentinel — the previous one was already
    // removed above, so this is not the old "stacking duplicates" bug the ARCHITECTURE gate's
    // second pass found; it is the correct one-live-sentinel-at-a-time invariant.
    rerender(<TestPage when />);
    expect(pushStateSpy).toHaveBeenCalledTimes(2);
    expect(backSpy).toHaveBeenCalledTimes(1);

    pushStateSpy.mockRestore();
    backSpy.mockRestore();
    Reflect.deleteProperty(window.history, 'length');
  });
});

describe('CODE_REVIEW gate third pass: isProgrammaticNavRef can never stick', () => {
  it('resets after a timeout if the expected popstate never arrives, so a later real Back press is not silently swallowed', async () => {
    vi.useFakeTimers();
    Object.defineProperty(window.history, 'length', { value: 2, configurable: true });
    // Simulates a genuine no-op history.back() — called, but nothing was actually there to move
    // to, so (unlike a real browser moving between two real entries) no popstate ever fires as a
    // result. Without the timeout safety net this would leave the guard flag stuck forever.
    const backSpy = vi.spyOn(window.history, 'back').mockImplementation(() => {});

    const { rerender } = render(<TestPage when />);
    rerender(<TestPage when={false} />); // the clean-save cleanup calls the mocked history.back()
    expect(backSpy).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(500);
    vi.useRealTimers(); // testing-library's own findBy* polling needs real timers from here on

    // Go dirty again (pushes a fresh sentinel) and fire a REAL popstate, simulating a genuine user
    // Back press this time. If the flag from the step above were still stuck, this would be
    // swallowed as "our own" programmatic navigation and the dialog would never open.
    rerender(<TestPage when />);
    window.dispatchEvent(new PopStateEvent('popstate'));

    expect(await screen.findByRole('dialog')).toBeInTheDocument();

    backSpy.mockRestore();
    Reflect.deleteProperty(window.history, 'length');
  });
});
