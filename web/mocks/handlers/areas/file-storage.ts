import type { HttpHandler } from 'msw';
import { fileStorageHandlers as handlers } from '../files/handlers';

/**
 * File Storage MSW request handlers.
 *
 * `mocks/handlers/index.ts` already imports this file and expects it to export
 * `fileStorageHandlers`, per the scaffold's one-wired-file-per-area convention
 * (`web/README.md` "Adding a functional area"). The actual handler/fixture code lives under
 * `web/mocks/handlers/files/` instead of directly in this file — HO-FIL-002 §12/§13 name that path
 * — so this file is just the wiring point the scaffold requires, re-exporting it. Recorded in
 * HO-FIL-002 §1 Amended (this file sits outside its literal §13 path list, but is the scaffold's
 * own designated integration point for this exact area, already present and empty before this
 * slice).
 */
export const fileStorageHandlers: HttpHandler[] = handlers;
