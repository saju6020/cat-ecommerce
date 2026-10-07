import type { HttpHandler } from 'msw';

/**
 * Notifications MSW request handlers.
 *
 * Owned entirely by the Notifications area — add handlers here as that area's slice is built,
 * derived from its own handover's API contract (never invented — HO-CAT-002 §5.1, §20.2).
 * mocks/handlers/index.ts already imports this file; no other file needs to change.
 */
export const notificationsHandlers: HttpHandler[] = [];
