/**
 * `lib/validation/guid.ts` — HO-SHP-002 S10. The shop API's id route parameters are all
 * `{id:guid}`; a route id that is not a GUID can never resolve, so the page answers 404 before any
 * request is built from it.
 */
const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export function isGuid(id: string): boolean {
  return GUID_PATTERN.test(id);
}
