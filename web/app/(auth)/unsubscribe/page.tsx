'use client';

import { useEffect, useRef, useState } from 'react';
import {
  UnsubscribePanel,
  type UnsubscribeView,
} from '@/components/notifications/UnsubscribePanel';
import { useUnsubscribe } from '@/lib/hooks/useUnsubscribe';
import { ApiRateLimitedError } from '@/lib/api/client';
import { isInvalidUnsubscribeLink, UNSUBSCRIBE_RATE_LIMITED_MESSAGE } from '@/lib/api/errors';

/** Reads `token` from `#token=…` (ADR-FE-161). A fragment never reaches a server or a log. */
function readTokenFromHash(): string | null {
  const raw = window.location.hash;
  const params = new URLSearchParams(raw.startsWith('#') ? raw.slice(1) : raw);
  const token = params.get('token');
  return token && token.length > 0 ? token : null;
}

/**
 * `/unsubscribe` — US-NTF-04, HO-NTF-002 §11.4, ADR-FE-161. Public (`(auth)` shell), no session.
 *
 *  1. On mount: read the token from `location.hash`, then `history.replaceState` to `/unsubscribe`
 *     so it is not left in the address bar, history entry or a bookmark.
 *  2. **Nothing is sent on load.** Mail scanners and link previewers prefetch links; only an
 *     explicit click on **Unsubscribe** sends the request.
 *  3. The token lives in a ref and state of this page only — never in a URL, storage or the query
 *     cache.
 */
export default function UnsubscribePage() {
  // `undefined` = not read yet (first paint, before the effect); `null` = no token in the link.
  const tokenRef = useRef<string | null | undefined>(undefined);
  const [token, setToken] = useState<string | null | undefined>(undefined);
  const mutation = useUnsubscribe();
  const [error, setError] = useState<string | null>(null);
  const [linkRefused, setLinkRefused] = useState(false);

  useEffect(() => {
    // React Strict Mode runs this effect twice in development; the second run would find the hash
    // already removed and wrongly conclude the link has no token, so the first read is kept.
    if (tokenRef.current === undefined) {
      tokenRef.current = readTokenFromHash();
      window.history.replaceState(window.history.state, '', '/unsubscribe');
    }
    setToken(tokenRef.current);
  }, []);

  if (token === undefined) return null;

  let view: UnsubscribeView;
  if (token === null || linkRefused) {
    view = { phase: 'invalid' };
  } else if (mutation.data) {
    view = {
      phase: 'done',
      channel: mutation.data.channel,
      wasSubscribed: mutation.data.wasSubscribed,
    };
  } else {
    view = { phase: 'confirm', error };
  }

  return (
    <div data-theme="customer" className="w-full">
      <UnsubscribePanel
        view={view}
        isPending={mutation.isPending}
        onConfirm={() => {
          if (!token) return;
          setError(null);
          mutation.mutate(token, {
            onError: (caught) => {
              if (isInvalidUnsubscribeLink(caught)) setLinkRefused(true);
              else if (caught instanceof ApiRateLimitedError) {
                setError(UNSUBSCRIBE_RATE_LIMITED_MESSAGE);
              } else setError('Something went wrong. Please try again.');
            },
          });
        }}
      />
    </div>
  );
}
