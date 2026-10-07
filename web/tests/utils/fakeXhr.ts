import { vi } from 'vitest';
import { resolveMockUploadOutcome } from '@/mocks/handlers/files/uploadBehavior';

/**
 * A controllable fake `XMLHttpRequest`, standing in for the real one `lib/api/files.ts`'s
 * `uploadFile` constructs (ADR-FE-150's XHR exception).
 *
 * **Why not the real MSW handler.** `@mswjs/interceptors`' jsdom `XMLHttpRequest` interception,
 * bridging a `FormData`/`File` upload body into a `Request` for `msw/node`'s handlers, throws
 * (`TypeError: Cannot read properties of undefined (reading '_buffer')`) with this project's
 * installed `msw@2.15.0` / `jsdom@30.1.1` — reproduced with no application code involved at all
 * (a bare `new XMLHttpRequest()` + `FormData` + `File`, `msw/node`'s `setupServer` listening).
 * Real-browser Playwright runs never go through that bridging code, so the real MSW handlers
 * (`mocks/handlers/files/handlers.ts`) are exercised for real there instead. This harness shares
 * the SAME per-file-name outcome table (`uploadBehavior.ts`) as those handlers, so "what a given
 * file name does" is defined once, not duplicated and liable to drift.
 */
export interface FakeXhrRequest {
  method: string;
  url: string;
  headers: Record<string, string>;
  formData: FormData;
}

let capturedRequests: FakeXhrRequest[] = [];

export function fakeXhrRequests(): readonly FakeXhrRequest[] {
  return capturedRequests;
}

export interface FakeXhrResponse {
  status: number;
  body: unknown;
  headers?: Record<string, string>;
}

export interface InstallFakeXhrOptions {
  /**
   * Overrides `uploadBehavior.ts`'s shared per-file-name table for a test that needs a response
   * shape the real backend can send but that table doesn't model (a `401`/`403 csrf_token_invalid`
   * from `SessionAuthenticationMiddleware`, a `413`/`502` from `FilesController`, or an
   * unrecognised `errorCode`) — gate-fix 2026-09-28 (ARCHITECTURE MEDIUM / SECURITY LOW). Any test
   * exercising ordinary success/violation-code behaviour should keep using the shared table
   * instead of duplicating it here.
   */
  respond?: (file: File) => FakeXhrResponse;
}

/** Installs the fake XHR globally, driven by `uploadBehavior.ts`'s shared per-file-name table by
 *  default (`options.respond` overrides it) — the same behaviour the real mocks give a Playwright
 *  run, without going through the network/MSW bridging bug above. Call `restoreRealXhr()` (or let
 *  a per-test `vi.stubGlobal` auto-restore via `restoreMocks`) when done. */
export function installFakeXhr(options: InstallFakeXhrOptions = {}): void {
  capturedRequests = [];
  const { respond } = options;

  class FakeXMLHttpRequest {
    upload: { onprogress: ((event: ProgressEvent) => void) | null } = { onprogress: null };
    onload: (() => void) | null = null;
    onerror: (() => void) | null = null;
    onabort: (() => void) | null = null;
    ontimeout: (() => void) | null = null;
    status = 0;
    responseText = '';
    withCredentials = false;

    private method = '';
    private url = '';
    private headers: Record<string, string> = {};
    private aborted = false;

    open(method: string, url: string): void {
      this.method = method;
      this.url = url;
    }

    setRequestHeader(name: string, value: string): void {
      this.headers[name] = value;
    }

    private responseHeaders: Record<string, string> = {};

    getResponseHeader(name: string): string | null {
      return this.responseHeaders[name.toLowerCase()] ?? null;
    }

    abort(): void {
      if (this.aborted) return;
      this.aborted = true;
      this.onabort?.();
    }

    send(body: FormData): void {
      capturedRequests.push({
        method: this.method,
        url: this.url,
        headers: { ...this.headers },
        formData: body,
      });

      const file = body.get('file');
      if (!(file instanceof File)) {
        // Mirrors the real controller's own "no file part" refusal — not expected in a real call
        // site, since `uploadFile` always appends one.
        this.status = 400;
        this.responseText = JSON.stringify({ errors: [], isValid: false });
        this.onload?.();
        return;
      }

      // A real transfer reports at least a "fully sent" progress tick before the response
      // arrives — `onProgress` in `lib/api/files.ts` only ever reads `event.loaded/event.total`.
      this.upload.onprogress?.({
        lengthComputable: true,
        loaded: file.size,
        total: file.size,
      } as ProgressEvent);

      if (respond) {
        const outcome = respond(file);
        if (this.aborted) return;
        this.status = outcome.status;
        this.responseText = JSON.stringify(outcome.body);
        this.responseHeaders = Object.fromEntries(
          Object.entries(outcome.headers ?? {}).map(([key, value]) => [key.toLowerCase(), value]),
        );
        this.onload?.();
        return;
      }

      void resolveMockUploadOutcome(
        file,
        (fileId, exp) => `https://mock.test/api/files/${fileId}/content?exp=${exp}`,
      ).then((outcome) => {
        if (this.aborted) return;
        this.status = outcome.status;
        this.responseText = JSON.stringify(outcome.body);
        this.onload?.();
      });
    }
  }

  vi.stubGlobal('XMLHttpRequest', FakeXMLHttpRequest);
}

export function restoreRealXhr(): void {
  vi.unstubAllGlobals();
  capturedRequests = [];
}
