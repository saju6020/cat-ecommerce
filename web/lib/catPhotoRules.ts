/**
 * Mirrors `CatPhotoRules.MaxPhotosPerCat` (HO-CAT-001 §10). Type and size limits for a cat photo
 * now live in ONE place client-side: the `CatProfilePhoto` row of `lib/files/fileRules.ts`, which
 * `<FileUploader>` checks before sending anything (HO-FIL-002; US-CAT-01 AC3).
 */
export const CAT_PHOTO_RULES = {
  maxPhotosPerCat: 10,
};
