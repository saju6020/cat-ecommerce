'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';

/**
 * `components/account/UnsavedChangesGuard.tsx` — HO-ACC-002 §11.7, NFR-USA-5. `beforeunload` for
 * a reload/close — kept native; the browser's own dialog is the only thing that can pause an
 * actual document unload, and it cannot be replaced with a custom one (no navigation-cancelling
 * API exists for that case, unlike the in-app click case below).
 *
 * ARCH gate MEDIUM (first pass) — in-app navigation used `window.confirm()` instead of the shared
 * `ConfirmDialog` every other overlay in the app uses. `window.confirm` is synchronous and its
 * return value has to be known before the click event finishes propagating, which a React dialog
 * (necessarily asynchronous — it renders, waits for a later click, then calls back) cannot supply
 * directly. Replaced with the same "always prevent the click, ask, then replay the navigation on
 * confirm" pattern this codebase already uses nowhere else but is the standard way to reconcile a
 * synchronous DOM event with an asynchronous confirmation: the original click is prevented
 * unconditionally, the destination `href` is captured, and `router.push(href)` runs it for real
 * only if the user confirms — a cancelled confirmation simply never calls `router.push`, leaving
 * the user exactly where they were, indistinguishable from `window.confirm` returning `false`.
 *
 * For in-app navigation the App Router still has no `router.events`-style navigation-blocking API
 * (unlike the Pages Router), so this intercepts a same-document `<a>` click in the capture phase,
 * same as before — `router.push`/`router.replace` calls made from outside a real click (rare on
 * these forms) are not covered by this, a documented limitation of the platform, not a silent gap
 * invented here.
 *
 * CODE_REVIEW suggestion (second pass) — only a same-origin, relative `href` is replayed through
 * `router.push` (Next.js's client-side router, which only ever understands an in-app route). An
 * absolute or cross-origin `href` (a different site, `mailto:`, `tel:`, a download link, …) is left
 * to navigate natively instead — that IS a real document unload, which `beforeunload` above already
 * covers, and handing an external URL to `router.push` would either throw or silently do nothing
 * useful. `target="_blank"`/`_self`-other-than-default and any modified click are excluded the same
 * way they already were.
 */
export function UnsavedChangesGuard({ when }: { when: boolean }) {
  const router = useRouter();
  const whenRef = useRef(when);
  const [pendingAction, setPendingAction] = useState<
    { type: 'link'; href: string } | { type: 'history' } | null
  >(null);

  // ARCH gate MEDIUM (second pass) — the Back/Forward buttons fire `popstate`, not a click this
  // file's own capture-phase listener ever sees, so a dirty form used to be lost silently on Back
  // exactly the same way it used to be lost on an in-app link click before this file's first-pass
  // fix. There is still no cancellable browser API for an in-flight Back/Forward navigation (the
  // platform limitation this file's own doc comment already names for links applies here too, more
  // so — `popstate` fires only AFTER the navigation already happened), so this uses the same
  // "sentinel history entry" technique most SPA frameworks use for exactly this gap: while dirty, a
  // duplicate history entry for the CURRENT page is pushed once, so the first Back press lands on
  // it (a same-URL, invisible "no-op" navigation) and fires `popstate` before the browser would
  // otherwise leave. That handler re-pushes the same sentinel again (undoing the pop, so the user
  // is still on the same page — `pushState` never itself fires `popstate`) and opens the same
  // `ConfirmDialog`; confirming replays the ORIGINAL Back intent via `history.go(-2)` (one step past
  // the sentinel, to whatever page came before it), with a ref-guarded flag so that programmatic
  // navigation's own resulting `popstate` is not re-intercepted as if the user had pressed Back
  // again.
  //
  // Accepted, documented residual gap (recorded in `web/docs/decisions.md` ADR-FE-013): this
  // depends on exactly one extra history entry sitting between the user and "away" at any moment,
  // which holds for a single Back press but can be defeated by pressing Back twice in rapid
  // succession before React re-renders (the second `popstate` can land past the point this
  // component's own re-push effect had a chance to run) — a low-probability race on real hardware,
  // and Forward after a genuine, confirmed Leave is not specially handled either (it simply
  // navigates forward like any other page, since `when` is false again once this component is
  // unmounted with the page it guarded). The other gaps the ARCHITECTURE/CODE_REVIEW gates' third
  // pass found in this same technique — the first-history-entry case, a duplicate `beforeunload`
  // prompt after a confirmed Leave, a second sentinel pushed on a dirty→clean→dirty cycle, a stray
  // sentinel left behind by an ordinary successful save, and `isProgrammaticNavRef` sticking forever
  // if an expected `popstate` never arrives — are fixed below, not accepted.
  //
  // CODE_REVIEW gate fourth pass, MEDIUM — the third pass's own fix for the first-history-entry
  // case (only push a sentinel when `window.history.length > 1`, below) rested on an inference that
  // does not hold in both directions. `length === 1` reliably means nothing is behind (it counts
  // BOTH directions, so a lone value of 1 leaves no room for anything either way) — that half is
  // kept. `length > 1`, though, does NOT reliably mean something is behind: it also counts forward
  // entries, so a page that is genuinely the tab's first entry can still report `length > 1` if the
  // user later navigated forward from it and pressed Back again (back → forward → dirty → Back) —
  // the guard would then push a sentinel believing something real sits behind it, and a confirmed
  // Leave's single `history.go(-2)` would silently no-op (nothing really is two entries back),
  // stranding the user on the sentinel with no visible sign anything went wrong — a swallowed Back.
  // Rather than try to make the `length > 1` guess itself reliable (structurally impossible with
  // this one number alone), the CONSEQUENCE of it being wrong is made safe instead: a confirmed
  // Leave replays via two SEQUENTIAL `history.back()` calls, not one `go(-2)` — the first is
  // guaranteed to land somewhere real (the guarded page itself, always immediately behind the
  // sentinel), and only once that step's own `popstate` actually arrives does the second one run, to
  // attempt moving past the guarded page too. If nothing is behind it, that second call is a no-op
  // with no further `popstate`, but the first step already, correctly, left the user on the guarded
  // page — never stuck on the sentinel the way a single out-of-range `go(-2)` used to leave them.
  // (How `onPopState` decides a given `popstate` is even "ours" to react to at all is `sentinelActiveRef`,
  // below — see the CODE_REVIEW gate fifth-pass comment there for why, after this same fourth pass
  // originally answered that question a different way.)
  const isProgrammaticNavRef = useRef(false);
  const programmaticNavTimeoutRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  // `true` exactly while a sentinel entry this component itself pushed is currently sitting in
  // history, unconsumed — gates both pushing a new one (don't push a second one on top of a live
  // one) and popping the current one back off (below) once it is no longer needed.
  //
  // CODE_REVIEW gate fifth pass, MEDIUM — this is now ALSO `onPopState`'s only signal for "is this
  // popstate even ours to react to", replacing the fourth pass's `history.state`-marker approach
  // (`useId()` plus a `__unsavedGuardBase`/`__unsavedGuard` pair, stamped via `replaceState`/
  // `pushState`). That approach broke silently under a real, source-verified mechanism: Next.js's
  // own App Router patches `history.pushState`/`replaceState` globally, and its OWN reactive effect
  // calls `history.replaceState(historyState, '', canonicalUrl)` for the CURRENT entry whenever the
  // canonical URL is unchanged (no navigation away — a `router.refresh()`, a Server Action, a
  // `<Link>`/`router.push()` that resolves to the same URL, or any ancestor layout doing the same) —
  // `historyState`'s own base object is `{}` unless `preserveCustomHistoryState` is `true` (only
  // true for the very first render and a Back/Forward-triggered restore), discarding ANY existing
  // custom key, including this component's own marker, WITHOUT firing a `popstate` or otherwise
  // notifying this component at all. Once stripped, `onPopState`'s old check
  // (`landedState?.__unsavedGuardBase !== guardId`) would fail for EVERY subsequent Back press for
  // the rest of that mounted instance's life — silently, with no dialog, no re-push, and no way for
  // this component to ever detect or recover, exactly the class of failure NFR-USA-5 exists to
  // prevent. Not currently triggered by `ProfileForm.tsx`/`NotificationPreferenceMatrix.tsx` (neither
  // calls `router.refresh()` or a Server Action), but latent for any future page that does.
  //
  // `sentinelActiveRef` avoids the whole failure class rather than working around it: it lives in
  // this component's own React state (a plain `useRef`), never inside `history.state`, so nothing
  // Next's router (or anything else) does to `history.state` can ever touch it. It is exactly as
  // reliable a signal for "the entry currently on top IS my sentinel" as the marker was ever meant to
  // be — this component is the ONLY thing that ever pushes that sentinel or pops it back off, and a
  // `replaceState` call (Next's own, or anyone else's) never changes WHICH entry is current, only
  // that entry's `state` content, so the ref is never invalidated by it. A `pushState` call from
  // somewhere else WHILE the sentinel is current (inserting a genuinely new entry on top of it) is
  // the one scenario this cannot detect either way — an existing, documented, accepted limitation
  // (the platform gives no signal for a `router.push()`/`pushState()` call made from outside a click
  // this file's own capture-phase listener sees), not a new gap this round introduces.
  const sentinelActiveRef = useRef(false);
  // Set to `'first'` right before the confirmed-Leave replay's first `history.back()`, then to
  // `'second'` once that step's own `popstate` arrives (triggering the second `history.back()`);
  // `null` the rest of the time. See the doc comment above `isProgrammaticNavRef`.
  const leaveReplayStepRef = useRef<'first' | 'second' | null>(null);

  useEffect(() => {
    whenRef.current = when;
  }, [when]);

  // CODE_REVIEW gate third pass — `history.back()`/`history.go(-2)` are the only two programmatic
  // history moves this component ever makes, and both need the SAME guard against the `popstate`
  // they cause being re-intercepted as a real user Back press. A bare `isProgrammaticNavRef.current
  // = true` before the call is not quite enough on its own: if the call turns out to be a genuine
  // no-op (nothing left in that direction to actually move to — the exact first-history-entry case
  // ARCHITECTURE gate third pass LOW (a) already guards the PUSH side of, but a defensive second
  // layer here costs nothing and covers any other reason the expected event might not arrive), no
  // `popstate` ever fires to reset the flag, and it would stay stuck — silently swallowing every
  // Back/Forward press for the rest of this page's life, unguarded. The timeout is a pure safety
  // net: `onPopState` below clears it on the expected event, so it only ever actually fires when
  // that event didn't.
  const beginProgrammaticHistoryNav = useCallback(() => {
    isProgrammaticNavRef.current = true;
    clearTimeout(programmaticNavTimeoutRef.current);
    programmaticNavTimeoutRef.current = setTimeout(() => {
      isProgrammaticNavRef.current = false;
    }, 500);
  }, []);

  // Shared by the initial push (below) and the re-push `onPopState` does once it recognises a
  // landing as its own sentinel being consumed (further down). Spreads `window.history.state`
  // forward rather than replacing it — Next's own internal keys (e.g. `__NA`) already live there and
  // are never this component's to discard — but no longer writes any marker of its own into it
  // (CODE_REVIEW gate fifth pass, MEDIUM; see the doc comment above `sentinelActiveRef`): the ref set
  // on the next line is this component's only record that this push happened at all.
  const pushSentinel = useCallback(() => {
    window.history.pushState(
      { ...(window.history.state as object | null) },
      '',
      window.location.href,
    );
    sentinelActiveRef.current = true;
  }, []);

  useEffect(() => {
    if (when) {
      // ARCHITECTURE gate third pass, LOW (c) — a sentinel is only ever (re-)pushed while none is
      // currently active, not once per dirty→clean→dirty cycle: unconditionally pushing again on
      // every transition to dirty used to stack duplicates once the cleanup below started actually
      // removing a consumed one, which would otherwise make an extra Back press do nothing.
      //
      // ARCHITECTURE gate third pass, LOW (a) — `window.history.length === 1` is a fully reliable
      // signal that the guarded page is genuinely alone in the tab's joint session history (nothing
      // behind AND nothing ahead — length counts both), so no sentinel is pushed at all in that one
      // case: the browser's own Back handling is left to run natively, exactly as it would with no
      // guard mounted at all.
      //
      // CODE_REVIEW gate fourth pass, MEDIUM — `length > 1` is NOT the reliable mirror of that
      // check the third pass treated it as, though: it counts forward entries too, so a page that
      // IS genuinely the tab's first entry can still report `length > 1` after the user navigates
      // forward from it and presses Back again (back → forward → dirty → Back) — the guard would
      // then push a sentinel believing something real sits behind it, and a confirmed Leave's old,
      // single `history.go(-2)` would silently no-op, stranding the user on the sentinel. The
      // `length > 1` push below is therefore only ever a best-effort guess, never a certainty — the
      // state-marking and two-step replay described in the doc comment above `isProgrammaticNavRef`
      // are what make the CONSEQUENCE of that guess being wrong (nothing actually behind the
      // guarded page after all) safe, rather than trying to make the guess itself perfectly
      // reliable, which `history.length` structurally cannot support.
      if (!sentinelActiveRef.current && window.history.length > 1) {
        pushSentinel();
      }
    } else if (sentinelActiveRef.current) {
      // CODE_REVIEW gate third pass, MEDIUM — the form went clean (an ordinary successful save,
      // most commonly) WITHOUT ever actually navigating away, so the sentinel pushed while it was
      // dirty is now a stray, useless duplicate entry still sitting on top of history — left alone,
      // it would swallow the user's next Back press (landing back on this same, now-saved page
      // instead of wherever they actually meant to go). `history.back()` removes it; the resulting
      // `popstate` is not a real Back press to react to, so it is guarded the same way the confirmed
      // Leave replay below already is.
      beginProgrammaticHistoryNav();
      window.history.back();
      sentinelActiveRef.current = false;
    }
  }, [when, beginProgrammaticHistoryNav, pushSentinel]);

  useEffect(() => {
    function onBeforeUnload(event: BeforeUnloadEvent) {
      if (!whenRef.current) return;
      event.preventDefault();
      event.returnValue = '';
    }

    function onClickCapture(event: MouseEvent) {
      if (!whenRef.current) return;
      // A modified click (new tab, download, …) or one already handled elsewhere is never ours to
      // intercept — only a plain, same-document left click.
      if (event.defaultPrevented || event.button !== 0) return;
      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const anchor = (event.target as HTMLElement)?.closest?.(
        'a[href]',
      ) as HTMLAnchorElement | null;
      if (!anchor) return;
      if (anchor.target && anchor.target !== '_self') return;
      const href = anchor.getAttribute('href');
      if (!href) return;
      // `anchor.href` (the resolved property, not `getAttribute`) is always absolute — comparing
      // its origin against the page's own origin is the reliable way to tell "internal route" from
      // "everything else" (a different site, `mailto:`, `tel:`, …), regardless of whether `href`
      // itself was written as relative or absolute in the markup.
      if (anchor.origin !== window.location.origin) return;

      event.preventDefault();
      event.stopImmediatePropagation();
      setPendingAction({ type: 'link', href });
    }

    function onPopState() {
      if (isProgrammaticNavRef.current) {
        isProgrammaticNavRef.current = false;
        clearTimeout(programmaticNavTimeoutRef.current);
        // CODE_REVIEW gate fourth pass, MEDIUM — the second leg of the confirmed-Leave replay (see
        // the doc comment above `isProgrammaticNavRef`): the first `history.back()` just landed,
        // for real, on the guarded page itself — now attempt to move past it too. If nothing is
        // actually behind it this is a harmless no-op (no further `popstate`, the safety-net
        // timeout above already cleared), but the user is at least never left stuck on the
        // sentinel the way a single out-of-range `go(-2)` used to leave them.
        if (leaveReplayStepRef.current === 'first') {
          leaveReplayStepRef.current = 'second';
          beginProgrammaticHistoryNav();
          window.history.back();
        } else {
          leaveReplayStepRef.current = null;
        }
        return;
      }
      if (!whenRef.current) return;
      // CODE_REVIEW gate fifth pass, MEDIUM — only react as "our sentinel was just consumed" when
      // this component's own ref says a sentinel is actually current (see the doc comment above
      // `sentinelActiveRef` for why this replaced a `history.state`-marker check here). Any other
      // `popstate` — landing somewhere this sentinel technique never actually explains — is left
      // alone: no re-push, no dialog, `beforeunload` remains the only guard for it.
      if (!sentinelActiveRef.current) return;
      pushSentinel();
      setPendingAction({ type: 'history' });
    }

    window.addEventListener('beforeunload', onBeforeUnload);
    document.addEventListener('click', onClickCapture, true);
    window.addEventListener('popstate', onPopState);
    return () => {
      window.removeEventListener('beforeunload', onBeforeUnload);
      document.removeEventListener('click', onClickCapture, true);
      window.removeEventListener('popstate', onPopState);
      clearTimeout(programmaticNavTimeoutRef.current);
    };
  }, [pushSentinel, beginProgrammaticHistoryNav]);

  return (
    <ConfirmDialog
      open={pendingAction !== null}
      onOpenChange={(open) => {
        if (!open) setPendingAction(null);
      }}
      title="Leave this page?"
      consequence="You have unsaved changes. Leave this page without saving?"
      confirmLabel="Leave"
      variant="destructive"
      onConfirm={() => {
        const action = pendingAction;
        setPendingAction(null);
        if (!action) return;
        // ARCHITECTURE gate third pass, LOW (b) — the user already confirmed Leave through THIS
        // dialog; `whenRef` would otherwise stay `true` until the prop itself catches up (a render
        // this component may not even survive to see, if the destination is another document), so
        // the replayed navigation below could still trip the native `beforeunload` prompt a second
        // time — a real risk specifically for the `history` case, since the entry two steps back can
        // be an actual different document, not this SPA. Disarming it here, synchronously, before
        // either kind of replay, means a confirmed Leave is never followed by a second, native
        // confirmation for the same decision.
        whenRef.current = false;
        if (action.type === 'link') {
          router.push(action.href);
        } else {
          // CODE_REVIEW gate fourth pass, MEDIUM — two sequential `history.back()` calls, not one
          // `go(-2)` (see the doc comment above `isProgrammaticNavRef`); `onPopState` above runs the
          // second step once the first one's own `popstate` actually arrives.
          leaveReplayStepRef.current = 'first';
          beginProgrammaticHistoryNav();
          window.history.back();
        }
      }}
    />
  );
}
