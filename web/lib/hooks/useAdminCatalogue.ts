import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getAdminServices,
  getAdminService,
  createClinicService,
  updateClinicService,
  setServiceActivation,
  getServicePrices,
  setServicePrice,
  getTaxClasses,
  getTaxClass,
  createTaxClass,
  updateTaxClass,
  getTaxRates,
  setTaxRate,
  type CreateClinicServiceRequest,
  type UpdateClinicServiceRequest,
  type SetServicePriceRequest,
  type CreateTaxClassRequest,
  type UpdateTaxClassRequest,
  type SetTaxRateRequest,
} from '@/lib/api/admin-catalogue';
import { ApiForbiddenError } from '@/lib/api/client';

function noRetryOn403(failureCount: number, error: unknown) {
  return !(error instanceof ApiForbiddenError) && failureCount < 1;
}

export function useAdminServices() {
  return useQuery({
    queryKey: ['admin', 'services'],
    queryFn: getAdminServices,
    retry: noRetryOn403,
  });
}

export function useAdminService(serviceId: string) {
  return useQuery({
    queryKey: ['admin', 'services', serviceId],
    queryFn: () => getAdminService(serviceId),
    enabled: !!serviceId,
    retry: noRetryOn403,
  });
}

export function useCreateClinicService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateClinicServiceRequest) => createClinicService(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'services'] }),
  });
}

export function useUpdateClinicService(serviceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateClinicServiceRequest) => updateClinicService(serviceId, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'services'] }),
  });
}

export function useSetServiceActivation(serviceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (isActive: boolean) => setServiceActivation(serviceId, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'services'] }),
  });
}

export function useServicePrices(serviceId: string) {
  return useQuery({
    queryKey: ['admin', 'services', serviceId, 'prices'],
    queryFn: () => getServicePrices(serviceId),
    enabled: !!serviceId,
    retry: noRetryOn403,
  });
}

export function useSetServicePrice(serviceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SetServicePriceRequest) => setServicePrice(serviceId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'services', serviceId, 'prices'] });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'services'] });
    },
  });
}

export function useTaxClasses() {
  return useQuery({
    queryKey: ['admin', 'tax-classes'],
    queryFn: getTaxClasses,
    retry: noRetryOn403,
  });
}

export function useTaxClass(taxClassId: string) {
  return useQuery({
    queryKey: ['admin', 'tax-classes', taxClassId],
    queryFn: () => getTaxClass(taxClassId),
    enabled: !!taxClassId,
    retry: noRetryOn403,
  });
}

export function useCreateTaxClass() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTaxClassRequest) => createTaxClass(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'tax-classes'] }),
  });
}

export function useUpdateTaxClass(taxClassId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateTaxClassRequest) => updateTaxClass(taxClassId, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'tax-classes'] }),
  });
}

export function useTaxRates(taxClassId: string) {
  return useQuery({
    queryKey: ['admin', 'tax-classes', taxClassId, 'rates'],
    queryFn: () => getTaxRates(taxClassId),
    enabled: !!taxClassId,
    retry: noRetryOn403,
  });
}

export function useSetTaxRate(taxClassId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SetTaxRateRequest) => setTaxRate(taxClassId, request),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['admin', 'tax-classes', taxClassId, 'rates'] }),
  });
}
