/**
 * Mirrors `CatPhotoRules` (HO-CAT-001 §10, `Common/Constants/CatPhotoRules.cs`) exactly —
 * `MaxSizeInBytes = 5 * 1024 * 1024`, `AllowedContentTypes = ["image/jpeg", "image/png",
 * "image/webp"]`, `MaxPhotosPerCat = 10`. Shared by `components/cat/PhotoField.tsx` (client-side
 * validation, US-CAT-01 AC3) and `mocks/handlers/areas/cat-profiles.ts` (the same check the real
 * `AddCatPhotoCommandValidator` performs server-side), so the two limits can never drift apart.
 */
export const CAT_PHOTO_RULES = {
  maxSizeInBytes: 5 * 1024 * 1024,
  allowedContentTypes: ['image/jpeg', 'image/png', 'image/webp'] as const,
  maxPhotosPerCat: 10,
};

export type CatPhotoContentType = (typeof CAT_PHOTO_RULES.allowedContentTypes)[number];
