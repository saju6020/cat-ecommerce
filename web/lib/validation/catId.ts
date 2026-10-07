/**
 * `lib/validation/catId.ts` — HO-CAT-002 §1 Amended (2026-09-27), the `/cats/{id}` smart-redirect
 * page (HO-CAT-001 §1 Amended, same date, item (a)). HO-CAT-001's `CatsController` constrains its
 * own `catId` route parameter to `{catId:guid}` (ASP.NET Core's built-in GUID route constraint) —
 * an `id` that cannot even be a GUID can never resolve to a real cat, on `GET /api/cats/{catId}`
 * or any other endpoint this contract specifies.
 *
 * Checking the shape here, before firing a single session query or building a single redirect,
 * lets `/cats/{id}` reject a mistyped or corrupted link immediately and honestly, rather than
 * sending the visitor through a session round trip (and, for a signed-out visitor, a sign-in) that
 * could only ever end in a 404. This is a pure format check — it never asks the API whether a cat
 * with this id actually exists, which would need a network call `/cats/{id}` is not allowed to
 * make at all (see that page's own doc comment).
 */
const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export function isValidCatId(id: string): boolean {
  return GUID_PATTERN.test(id);
}
