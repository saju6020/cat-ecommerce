import { apiFetch } from './client';

/**
 * Signed-in customer's own account endpoints (`/api/customers/me/**`) — HO-ACC-002 §8.2's
 * `lib/api/account.ts`. Shapes verified against `CustomerProfileController.cs` and the read-side
 * view models in `src/Read/.../ViewModels/{CustomerProfile,DeliveryAddress,
 * NotificationPreferences,PersonalDataExport}ViewModel.cs`.
 */

// -------------------------------------------------------------------------------------------
// Profile — Q-C2/C6, US-ACC-06
// -------------------------------------------------------------------------------------------

export interface CustomerProfile {
  fullName: string;
  mobileNumber: string | null;
  emailAddress: string | null;
  hasMobileNumber: boolean;
  hasEmailAddress: boolean;
  canEditEmail: boolean;
  additionalPhoneNumbers: string[];
  registeredOn: string;
}

export async function getCustomerProfile(): Promise<CustomerProfile> {
  return apiFetch<CustomerProfile>('/api/customers/me');
}

export interface UpdateCustomerProfileRequest {
  fullName: string;
  emailAddress: string | null;
  additionalPhoneNumbers: string[];
  /** Required only when `emailAddress` is actually changing from the loaded value (HO-ACC-001 §1
   *  Amended (17), HO-ACC-002 §1 Amended 2026-09-25) — the same step-up account deletion uses. */
  currentPassword?: string | null;
}

export async function updateCustomerProfile(request: UpdateCustomerProfileRequest): Promise<void> {
  await apiFetch<void>('/api/customers/me', { method: 'PUT', body: request });
}

// -------------------------------------------------------------------------------------------
// Addresses — Q-E1/C7, US-ACC-06
// -------------------------------------------------------------------------------------------

export interface DeliveryAddress {
  id: string;
  label: string;
  line1: string;
  line2: string | null;
  city: string;
  district: string;
  postalCode: string | null;
  deliveryInstructions: string | null;
  isDefault: boolean;
}

export interface DeliveryAddressBook {
  addresses: DeliveryAddress[];
  maxAddresses: number;
}

export async function getDeliveryAddresses(): Promise<DeliveryAddressBook> {
  return apiFetch<DeliveryAddressBook>('/api/customers/me/addresses');
}

export interface DeliveryAddressRequest {
  label: string;
  line1: string;
  line2?: string | null;
  city: string;
  district: string;
  postalCode?: string | null;
  deliveryInstructions?: string | null;
}

export async function addDeliveryAddress(
  request: DeliveryAddressRequest,
): Promise<{ addressId: string }> {
  return apiFetch<{ addressId: string }>('/api/customers/me/addresses', {
    method: 'POST',
    body: request,
  });
}

export async function updateDeliveryAddress(
  addressId: string,
  request: DeliveryAddressRequest,
): Promise<void> {
  await apiFetch<void>(`/api/customers/me/addresses/${encodeURIComponent(addressId)}`, {
    method: 'PUT',
    body: request,
  });
}

export async function removeDeliveryAddress(addressId: string): Promise<void> {
  await apiFetch<void>(`/api/customers/me/addresses/${encodeURIComponent(addressId)}`, {
    method: 'DELETE',
  });
}

export async function setDefaultDeliveryAddress(addressId: string): Promise<void> {
  await apiFetch<void>(`/api/customers/me/addresses/${encodeURIComponent(addressId)}/default`, {
    method: 'PUT',
  });
}

// -------------------------------------------------------------------------------------------
// Notification preferences — Q-E2/C8, US-ACC-06
// -------------------------------------------------------------------------------------------

export interface NotificationPreferenceCell {
  channel: string;
  category: string;
  isGranted: boolean;
  isEditable: boolean;
  isAlwaysOn: boolean;
}

export interface NotificationPreferences {
  cells: NotificationPreferenceCell[];
  /** HO-NTF-002 §11.2 / HO-NTF-001 §12.4 amendment. The channel the customer chose for important
   *  messages; `null` until they choose one. Values: `'Sms'` | `'Email'`. */
  preferredChannel?: string | null;
  /** What the server will actually use today — selected in the UI even when `preferredChannel` is null. */
  effectivePreferredChannel?: string | null;
  /** The channels the account has an identifier for (a mobile number for SMS, an email address). */
  availablePreferredChannels?: string[];
}

export async function getNotificationPreferences(): Promise<NotificationPreferences> {
  return apiFetch<NotificationPreferences>('/api/customers/me/notification-preferences');
}

export interface NotificationPreferenceChange {
  channel: string;
  category: string;
  isGranted: boolean;
}

export async function changeNotificationPreferences(
  changes: NotificationPreferenceChange[],
  preferredChannel?: string,
): Promise<void> {
  await apiFetch<void>('/api/customers/me/notification-preferences', {
    method: 'PUT',
    body: preferredChannel ? { changes, preferredChannel } : { changes },
  });
}

// -------------------------------------------------------------------------------------------
// Personal data export / deletion — C17/Q-J1/C18, US-ACC-13
// -------------------------------------------------------------------------------------------

export interface PersonalDataExportCat {
  id: string;
  name: string;
  species: string;
  breed: string | null;
  sex: string;
}

export interface PersonalDataExport {
  schemaVersion: 1;
  exportedOn: string;
  profile: CustomerProfile;
  addresses: DeliveryAddress[];
  notificationPreferences: NotificationPreferenceCell[];
  cats: PersonalDataExportCat[];
}

export async function exportPersonalData(): Promise<PersonalDataExport> {
  return apiFetch<PersonalDataExport>('/api/customers/me/personal-data-export', {
    method: 'POST',
  });
}

export interface DeleteAccountRequest {
  currentPassword: string;
}

export async function deleteAccount(request: DeleteAccountRequest): Promise<void> {
  await apiFetch<void>('/api/customers/me/deletion', { method: 'POST', body: request });
}
