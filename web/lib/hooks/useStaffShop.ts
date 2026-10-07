import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addSku,
  changeProductSlug,
  createOrigin,
  createProduct,
  discontinueSku,
  getModerationQueue,
  getSkuPrices,
  getStaffCountries,
  getStaffOrigins,
  getStaffProduct,
  getStaffProducts,
  hideReview,
  publishReview,
  reactivateSku,
  rejectReview,
  removeProductTranslation,
  scheduleSkuPrice,
  setBundleComponents,
  setFoodDetails,
  setPrescriptionRequired,
  setProductImages,
  setProductTranslation,
  setRelatedProducts,
  transitionProduct,
  updateOrigin,
  updateProductDetails,
  updateSku,
  type AddSkuRequest,
  type CreateProductRequest,
  type OriginInput,
  type ProductImageInput,
  type ProductTransition,
  type SetFoodDetailsRequest,
  type SkuAttributesInput,
  type StaffProductListParams,
  type TranslationInput,
  type UpdateProductDetailsRequest,
} from '@/lib/api/staff-shop';
import { describeApiError, SHOP_CODE_MESSAGES } from '@/lib/api/errors';
import { PartialSaveError } from '@/lib/shop/submitSection';
import { getShopLocales } from '@/lib/api/shop-catalogue';
import { ApiConflictError, ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';

/**
 * HO-SHP-002 §8.4 — back-office state. Staff product: `['staff','product', productId]` (it carries
 * `version`); each section mutation sends `expectedVersion` and invalidates that key plus the list.
 */
function noRetryOnRefusal(failureCount: number, error: unknown) {
  return (
    !(error instanceof ApiForbiddenError || error instanceof ApiNotFoundError) && failureCount < 1
  );
}

export function useStaffProducts(params: StaffProductListParams) {
  return useQuery({
    queryKey: ['staff', 'products', params],
    queryFn: () => getStaffProducts(params),
    retry: noRetryOnRefusal,
  });
}

export function useStaffProduct(productId: string) {
  return useQuery({
    queryKey: ['staff', 'product', productId],
    queryFn: () => getStaffProduct(productId),
    enabled: !!productId,
    retry: noRetryOnRefusal,
  });
}

function useInvalidateProduct(productId: string) {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: ['staff', 'product', productId] });
    void queryClient.invalidateQueries({ queryKey: ['staff', 'products'] });
  };
}

/**
 * Two-command saves (details then slug; food then prescription flag). The product is invalidated ONCE,
 * after both commands settle — invalidating between them refetched the product mid-save while the
 * second command was still in flight. A refusal of the SECOND command becomes a `PartialSaveError`
 * (the first is saved; the form stays mounted with what was typed and says so). The second command's
 * `expectedVersion` is the first's + 1: each command bumps the aggregate version exactly once and
 * answers 204 with no body to read it from; a mismatch is a 409 and shows the reload message.
 */
export function useTwoStepSave(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return async (
    first: () => Promise<unknown>,
    second: (() => Promise<unknown>) | null,
    partialPrefix: string,
  ) => {
    await first(); // a refusal here saved nothing: the caller shows it on the form
    try {
      if (second) await second();
    } catch (error) {
      // A stale version is a conflict like any other: the section offers Reload (S13).
      if (error instanceof ApiConflictError) throw error;
      throw new PartialSaveError(
        `${partialPrefix} — ${describeApiError(error, SHOP_CODE_MESSAGES)}`,
      );
    } finally {
      invalidate();
    }
  };
}

export function useCreateProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateProductRequest) => createProduct(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['staff', 'products'] }),
  });
}

export function useUpdateProductDetails(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: UpdateProductDetailsRequest) => updateProductDetails(productId, request),
    onSuccess: invalidate,
  });
}

export function useChangeProductSlug(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: { expectedVersion: number; slug: string }) =>
      changeProductSlug(productId, request),
    onSuccess: invalidate,
  });
}

export function useSetFoodDetails(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: SetFoodDetailsRequest) => setFoodDetails(productId, request),
    onSuccess: invalidate,
  });
}

export function useSetPrescriptionRequired(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: { expectedVersion: number; prescriptionRequired: boolean }) =>
      setPrescriptionRequired(productId, request),
    onSuccess: invalidate,
  });
}

export function useSetProductImages(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: { expectedVersion: number; images: ProductImageInput[] }) =>
      setProductImages(productId, request),
    onSuccess: invalidate,
  });
}

export function useSetBundleComponents(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: {
      expectedVersion: number;
      components: Array<{ componentSkuId: string; quantity: number }>;
    }) => setBundleComponents(productId, request),
    onSuccess: invalidate,
  });
}

export function useSetRelatedProducts(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: {
      expectedVersion: number;
      links: Array<{ relatedProductId: string; kind: string; sortOrder: number }>;
    }) => setRelatedProducts(productId, request),
    onSuccess: invalidate,
  });
}

export function useSetProductTranslation(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: ({ locale, ...request }: TranslationInput & { locale: string }) =>
      setProductTranslation(productId, locale, request),
    onSuccess: invalidate,
  });
}

export function useRemoveProductTranslation(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: ({ locale, expectedVersion }: { locale: string; expectedVersion: number }) =>
      removeProductTranslation(productId, locale, expectedVersion),
    onSuccess: invalidate,
  });
}

export function useAddSku(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: AddSkuRequest) => addSku(productId, request),
    onSuccess: invalidate,
  });
}

export function useUpdateSku(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: ({ skuId, ...request }: SkuAttributesInput & { skuId: string }) =>
      updateSku(productId, skuId, request),
    onSuccess: invalidate,
  });
}

export function useSetSkuStatus(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: ({
      skuId,
      active,
      expectedVersion,
    }: {
      skuId: string;
      active: boolean;
      expectedVersion: number;
    }) =>
      active
        ? reactivateSku(productId, skuId, expectedVersion)
        : discontinueSku(productId, skuId, expectedVersion),
    onSuccess: invalidate,
  });
}

export function useProductTransition(productId: string) {
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: ({
      transition,
      expectedVersion,
    }: {
      transition: ProductTransition;
      expectedVersion: number;
    }) => transitionProduct(productId, transition, expectedVersion),
    onSuccess: invalidate,
  });
}

export function useSkuPrices(skuId: string | null) {
  return useQuery({
    queryKey: ['staff', 'sku-prices', skuId],
    queryFn: () => getSkuPrices(skuId!),
    enabled: !!skuId,
    retry: noRetryOnRefusal,
  });
}

export function useScheduleSkuPrice(productId: string, skuId: string) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateProduct(productId);
  return useMutation({
    mutationFn: (request: { amount: number; currencyCode: string; effectiveFrom: string }) =>
      scheduleSkuPrice(skuId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['staff', 'sku-prices', skuId] });
      invalidate();
    },
  });
}

export function useStaffCountries() {
  return useQuery({
    queryKey: ['staff', 'countries'],
    queryFn: getStaffCountries,
    staleTime: 300_000,
  });
}

export function useShopLocales() {
  return useQuery({ queryKey: ['shop', 'locales'], queryFn: getShopLocales, staleTime: 300_000 });
}

export function useStaffOrigins() {
  return useQuery({
    queryKey: ['staff', 'origins'],
    queryFn: getStaffOrigins,
    retry: noRetryOnRefusal,
  });
}

export function useSaveOrigin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      countryCode,
      isNew,
      ...input
    }: OriginInput & { countryCode: string; isNew: boolean }) => {
      if (isNew) await createOrigin({ countryCode, ...input });
      else await updateOrigin(countryCode, input);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['staff', 'origins'] }),
  });
}

export function useModerationQueue(params: { status?: string; pageNumber?: number }) {
  return useQuery({
    queryKey: ['staff', 'reviews', params],
    queryFn: () => getModerationQueue(params),
    retry: noRetryOnRefusal,
  });
}

export function useModerateReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (
      action:
        | { kind: 'publish'; reviewId: string }
        | { kind: 'hide'; reviewId: string }
        | { kind: 'reject'; reviewId: string; reason: string; note?: string | null },
    ) =>
      action.kind === 'publish'
        ? publishReview(action.reviewId)
        : action.kind === 'hide'
          ? hideReview(action.reviewId)
          : rejectReview(action.reviewId, { reason: action.reason, note: action.note }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['staff', 'reviews'] }),
  });
}
