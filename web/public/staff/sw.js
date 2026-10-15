/* PurrCare staff offline service worker — HO-XCT-002 §12.1, ADR-FE-139 / ADR-FE-140.
 *
 * Served from /staff/sw.js, so its scope is /staff/ (no scope-widening response header, no attempt
 * to widen it). It caches ONLY hashed static assets (/_next/static/**, /staff/icons/**) and the
 * static offline shell (/staff/offline). It NEVER touches anything under /api/, never caches any
 * other HTML, and stores no personal data: the snapshot lives in IndexedDB, written by the app.
 *
 * Navigations are network-first. The fallback happens only when the fetch REJECTS (a slow answer is
 * still an answer):
 *   - /staff/offline[...]   -> the cached offline shell, so a reload while offline lands on the board;
 *   - any other /staff/ page -> a redirect to /staff/offline?from=<path>, so the person reaches the
 *     board with an explanation instead of a dead session guard.
 */
const VERSION = 1;
const SHELL_CACHE = 'purrcare-shell-v' + VERSION;
const ASSET_CACHE = 'purrcare-assets-v' + VERSION;
const SHELL_URL = '/staff/offline';

function isStaticAsset(url) {
  return url.pathname.startsWith('/_next/static/') || url.pathname.startsWith('/staff/icons/');
}

function isOfflineShellPath(url) {
  return url.pathname === SHELL_URL || url.pathname === SHELL_URL + '/';
}

/** Fetches the offline shell and every /_next/static/** asset it references. */
async function cacheShell() {
  const shellCache = await caches.open(SHELL_CACHE);
  const assetCache = await caches.open(ASSET_CACHE);
  const response = await fetch(SHELL_URL, { cache: 'reload', credentials: 'omit' });
  if (!response.ok) throw new Error('Offline shell unavailable');
  const html = await response.clone().text();
  await shellCache.put(SHELL_URL, response);
  const assets = new Set();
  // Two shapes: <script src="/_next/static/..."> in the markup, and "static/chunks/..." strings in
  // the flight payload (no /_next/ prefix; the runtime loads those on demand, so they must be
  // cached too). Route-group folders such as "(offline)" put parentheses in a file name, so
  // balanced parentheses are part of a name.
  const name = '(?:[^"\'\\\\\\s<>()]|\\([^"\'\\\\\\s<>()]*\\))+';
  const patterns = [
    new RegExp('/_next/static/' + name, 'g'),
    new RegExp('static/(?:chunks|css|media)/' + name, 'g'),
  ];
  for (const pattern of patterns) {
    let match;
    while ((match = pattern.exec(html)) !== null) {
      const found = match[0];
      assets.add(found.startsWith('/_next/') ? found : '/_next/' + found);
    }
  }
  // Drop hashed chunks of earlier builds: anything cached that this shell no longer references.
  for (const request of await assetCache.keys()) {
    const path = new URL(request.url).pathname;
    if (!assets.has(path) && !assets.has(decodeURI(path))) await assetCache.delete(request);
  }
  await Promise.all(
    Array.from(assets).map(async (path) => {
      try {
        const asset = await fetch(path, { credentials: 'omit' });
        if (asset.ok) await assetCache.put(path, asset);
      } catch {
        /* A missing asset only makes the shell less complete; never fail the install for it. */
      }
    }),
  );
}

self.addEventListener('install', (event) => {
  event.waitUntil(
    cacheShell().then(
      () => self.skipWaiting(),
      () => self.skipWaiting(),
    ),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((names) =>
        Promise.all(
          names
            .filter((name) => name.startsWith('purrcare-') && name !== SHELL_CACHE && name !== ASSET_CACHE)
            .map((name) => caches.delete(name)),
        ),
      )
      .then(() => self.clients.claim()),
  );
});

async function handleNavigation(request, url) {
  try {
    // Only a REJECTED fetch (a real network failure) falls back. A slow answer is still an answer:
    // a cold start or a slow connection must not send an online person to the offline board.
    return await fetch(request);
  } catch {
    if (isOfflineShellPath(url)) {
      const cached = await caches.match(SHELL_URL, { ignoreSearch: true });
      if (cached) return cached;
      return Response.error();
    }
    const target = SHELL_URL + '?from=' + encodeURIComponent(url.pathname);
    return Response.redirect(target, 302);
  }
}

async function handleAsset(request) {
  const cached = await caches.match(request);
  if (cached) return cached;
  const response = await fetch(request);
  if (response.ok) {
    const cache = await caches.open(ASSET_CACHE);
    await cache.put(request, response.clone());
  }
  return response;
}

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET') return;
  const url = new URL(request.url);
  // Any other origin, and anything under /api/: never touched (no respondWith).
  if (url.origin !== self.location.origin) return;
  if (url.pathname.startsWith('/api/')) return;
  if (request.mode === 'navigate' && url.pathname.startsWith('/staff/')) {
    // The sign-in page must stay reachable by the normal network path only.
    if (url.pathname.startsWith('/staff/sign-in')) return;
    event.respondWith(handleNavigation(request, url));
    return;
  }
  if (isStaticAsset(url)) {
    event.respondWith(handleAsset(request));
  }
});

self.addEventListener('message', (event) => {
  const data = event.data || {};
  if (data.type === 'warm-shell') {
    event.waitUntil(cacheShell().catch(() => undefined));
  } else if (data.type === 'purge') {
    event.waitUntil(
      caches.keys().then((names) => Promise.all(names.filter((n) => n.startsWith('purrcare-')).map((n) => caches.delete(n)))),
    );
  } else if (data.type === 'version' && event.source) {
    event.source.postMessage({ type: 'version', version: VERSION });
  }
});
