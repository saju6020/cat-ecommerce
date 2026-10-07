/**
 * Shop MSW request handlers.
 *
 * Owned entirely by the Shop area. HO-SHP-002 §13 puts its handlers under
 * `mocks/handlers/shop/**`; this pre-wired file (already imported by `mocks/handlers/index.ts`)
 * only re-exports them, so no other file needs to change (HO-SHP-002 §1 Amended 2026-10-04).
 */
export { shopHandlers } from '../shop';
