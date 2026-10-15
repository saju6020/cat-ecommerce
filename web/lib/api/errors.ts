import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import {
  ApiConflictError,
  ApiError,
  ApiForbiddenError,
  ApiNotFoundError,
  ApiRateLimitedError,
  ApiValidationError,
  OfflineError,
} from './client';

const GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.';

/** HO-XCT-002 §11.3/§11.7 (ADR-FE-143) — what a refused offline write says. Nothing was lost, and
 *  nothing was queued. */
export const OFFLINE_WRITE_MESSAGE =
  "You're offline, so this couldn't be saved. Your entries are still here — try again when the connection returns.";

/**
 * Bridges the API's ValidationResponse to react-hook-form (HO-CAT-002 §11.8). This is where
 * NFR-USA-4 is met or failed:
 *  - `errorMessage` is written for a human by the API and is shown as-is.
 *  - `errorCode` (e.g. "MicrochipAlreadyRegistered") is only ever read to pick an area's OWN
 *    fallback sentence from a `codeMessages` map the caller passes in (used when `errorMessage`
 *    is empty); it is never rendered. With no map the fallback is the generic sentence — no
 *    area's wording is built in here.
 *  - An error whose field isn't one of `knownFields` becomes a form-level message under `root`
 *    instead of being silently dropped — a validation failure with no visible cause is worse
 *    than a crash.
 *
 * `fieldMap` translates the API command's `propertyName` to this form's field name where they
 * differ. Keep that mapping in one table per form, not scattered across the codebase.
 */
export function applyValidationErrors<TFieldValues extends FieldValues>(
  error: ApiValidationError,
  setError: UseFormSetError<TFieldValues>,
  options: {
    fieldMap?: Partial<Record<string, Path<TFieldValues>>>;
    knownFields?: ReadonlyArray<Path<TFieldValues>>;
    codeMessages?: Readonly<Record<string, string>>;
  } = {},
): void {
  const { fieldMap = {}, knownFields, codeMessages } = options;
  const rootField = 'root' as Path<TFieldValues>;

  if (error.response.errors.length === 0) {
    setError(rootField, { type: 'server', message: 'Something went wrong. Please try again.' });
    return;
  }

  for (const validationError of error.response.errors) {
    const mapped = fieldMap[validationError.propertyName];
    const target = mapped ?? (validationError.propertyName as Path<TFieldValues>);
    const isKnown = !knownFields || knownFields.includes(target);

    setError(isKnown ? target : rootField, {
      type: 'server',
      // The server's message is preferred; the caller's mapped plain-language text is the fallback
      // when it is empty. `errorCode` is read only to pick that fallback, never shown.
      message:
        validationError.errorMessage ||
        codeMessages?.[validationError.errorCode] ||
        GENERIC_ERROR_MESSAGE,
    });
  }
}

/**
 * A form's submit handler, wrapped so every Cat Profiles form applies §11.8 the same way instead
 * of each one re-writing its own try/catch (added for HO-CAT-002; the function above is
 * HO-CAT-002 §11.8's own spec, unchanged):
 *  - `ApiValidationError` → `applyValidationErrors` (field-level, or `root` when unmapped).
 *  - `ApiConflictError` (HO-CAT-001's `409`, a concurrent edit) → a `root` message asking the
 *    user to get the latest version and try again — component test E20's "user is told the
 *    record changed", **never a silent overwrite**. The caller's field values are untouched
 *    either way, since nothing here resets the form (NFR-USA-5).
 *  - anything else (network failure, `5xx`) → a plain-language `root` message, never the raw
 *    error (NFR-USA-4, E21).
 *
 * Returns the action's result on success, or `undefined` after recording the error — callers
 * that need to react to success (closing a dialog, navigating) check for that.
 */
export async function submitWithServerErrors<TFieldValues extends FieldValues, TResult>(
  action: () => Promise<TResult>,
  setError: UseFormSetError<TFieldValues>,
  options: {
    fieldMap?: Partial<Record<string, Path<TFieldValues>>>;
    knownFields?: ReadonlyArray<Path<TFieldValues>>;
    codeMessages?: Readonly<Record<string, string>>;
  } = {},
): Promise<TResult | undefined> {
  try {
    return await action();
  } catch (error) {
    const rootField = 'root' as Path<TFieldValues>;
    if (error instanceof OfflineError) {
      setError(rootField, { type: 'server', message: OFFLINE_WRITE_MESSAGE });
    } else if (error instanceof ApiValidationError) {
      applyValidationErrors(error, setError, options);
    } else if (error instanceof ApiConflictError) {
      setError(rootField, {
        type: 'server',
        message:
          'Someone else changed this record while you were editing it. Get the latest version, then try saving again — your entered changes are still here.',
      });
    } else {
      setError(rootField, { type: 'server', message: describeUnexpectedError(error) });
    }
    return undefined;
  }
}

/**
 * `applyValidationErrors`/`submitWithServerErrors` both need a react-hook-form `setError` —
 * fine for every Cat Profiles form, but the cat photos page (§11.6) isn't a react-hook-form form at all
 * (it's a plain controlled input plus a button click, not a `<form onSubmit>`). This is the same
 * §11.8 mapping for that case: every error shown (never just `errors[0]` — "must never be
 * swallowed" applies here too), a `409` gets its own message, and `errorCode` is only read to
 * pick a fallback from the caller's optional `codeMessages` (never rendered). Returns one display string rather than setting per-field state, since the caller
 * (the photos page's inline alert) only has one error slot to show.
 */
export function describeApiError(
  error: unknown,
  codeMessages?: Readonly<Record<string, string>>,
): string {
  if (error instanceof OfflineError) return OFFLINE_WRITE_MESSAGE;
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  if (error instanceof ApiValidationError) {
    if (error.response.errors.length === 0) {
      return 'Something went wrong. Please try again.';
    }
    return error.response.errors
      .map(
        (validationError) =>
          validationError.errorMessage ||
          codeMessages?.[validationError.errorCode] ||
          GENERIC_ERROR_MESSAGE,
      )
      .join(' ');
  }
  if (error instanceof ApiConflictError) {
    return 'Someone else changed this record while you were working. Reload and try again.';
  }
  return 'Something went wrong. Please try again.';
}

/**
 * HO-ADM-002 §18 A3 — a server `403` on a save/action is shown as a clear permission message, never
 * the generic "Something went wrong. Please try again." (retrying a refusal cannot help). Names no
 * role and no action, like `PermissionDeniedState` (ADR-FE-009); the two branch-assignment codes
 * (§11.9) get their own wording because they mean "pick or get assigned a branch", not "never".
 */
export const PERMISSION_DENIED_MESSAGE =
  "You don't have permission to do this. Ask an administrator if you need access.";

function describeForbidden(error: ApiForbiddenError): string {
  const code = error.body.errors?.[0]?.errorCode;
  if (code === 'BranchNotAssigned') {
    return "You aren't assigned to the active branch. Choose one of your branches from the branch picker.";
  }
  if (code === 'BranchAssignmentRequired') {
    return 'Ask an administrator to assign you to a branch.';
  }
  return PERMISSION_DENIED_MESSAGE;
}

/** The root message for a submit that failed with anything other than a `400`: a permission
 *  message for a `403`, otherwise the generic plain-language retry text (NFR-USA-4). */
export function describeUnexpectedError(error: unknown): string {
  if (error instanceof OfflineError) return OFFLINE_WRITE_MESSAGE;
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  return 'Something went wrong. Please try again.';
}

/**
 * HO-ADM-002 §11.9 — `BranchNotAssigned` (a stale/refused `X-Branch-Id`) and
 * `BranchAssignmentRequired` (no assignment, and more than one active branch exists) are the two
 * `403` codes every branch-scoped admin page can hit even for a role that otherwise holds the
 * action — they mean "pick or get assigned a branch", not "you may never do this". Callers show a
 * banner with a link to the branch picker (`BranchNotAssigned`) or the plain-language ask-an-
 * administrator text (`BranchAssignmentRequired`) instead of falling through to the generic
 * `PermissionDeniedState`, which would wrongly suggest the action itself is forbidden.
 */
export type BranchAssignmentErrorCode = 'BranchNotAssigned' | 'BranchAssignmentRequired';

export function getBranchAssignmentErrorCode(error: unknown): BranchAssignmentErrorCode | null {
  if (!(error instanceof ApiForbiddenError)) return null;
  const code = error.body.errors?.[0]?.errorCode;
  return code === 'BranchNotAssigned' || code === 'BranchAssignmentRequired' ? code : null;
}

// -------------------------------------------------------------------------------------------
// HO-NTF-002 §11.6 — Notifications. `errorCode` is read here only to CHOOSE a plain-language
// sentence; it is never rendered (NFR-USA-4).
// -------------------------------------------------------------------------------------------

export type TemplateContentField = 'subject' | 'title' | 'body' | 'linkPath';

/**
 * C1 / preview violations name their field as `Contents[i].Subject | Title | Body | LinkPath`
 * (HO-NTF-001 §8.2 rule 3). `i` indexes the `contents` array the client SENT, so the caller maps
 * the index back to a channel tab. `Contents` on its own (no index) has no field and returns null.
 */
export function parseTemplateContentsProperty(
  propertyName: string,
): { index: number; field: TemplateContentField } | null {
  const match = /^Contents\[(\d+)\]\.(Subject|Title|Body|LinkPath)$/i.exec(propertyName);
  if (!match) return null;
  const field = match[2]!;
  return {
    index: Number(match[1]),
    field: (field.charAt(0).toLowerCase() + field.slice(1)) as TemplateContentField,
  };
}

/** C8 (preferences) `PreferredChannel`, C7 (resend) `Channel` -> the form field of the same meaning. */
export const PREFERRED_CHANNEL_FIELD_MAP = { PreferredChannel: 'preferredChannel' } as const;
export const RESEND_FIELD_MAP = { Channel: 'channel' } as const;

export const TEMPLATE_CONFLICT_MESSAGE =
  'Someone else changed this template. Reload to see their version.';

function firstErrorCode(error: ApiValidationError): string | undefined {
  return error.response?.errors?.[0]?.errorCode;
}

/** A resend that the server refused (C7), as one plain sentence. `null` when it is not one of the
 *  three resend-specific refusals, so the caller falls back to `describeApiError`. */
export function describeResendRefusal(error: unknown): string | null {
  if (!(error instanceof ApiValidationError)) return null;
  const code = firstErrorCode(error);
  if (code === 'ResendLimitReached') return 'This message has already been resent 3 times.';
  if (code === 'ResendChannelNotAvailable') {
    return "That channel isn't available for this message. Choose another one.";
  }
  if (code === 'NotificationCannotBeResent') {
    // The API writes this sentence for a person (it names the reason) — shown as-is.
    return error.response.errors[0]?.errorMessage ?? "This message can't be resent right now.";
  }
  return null;
}

export const INVALID_UNSUBSCRIBE_LINK_MESSAGE =
  "This link isn't valid any more. You can manage your preferences after signing in.";
export const UNSUBSCRIBE_RATE_LIMITED_MESSAGE =
  'Too many attempts. Please wait a minute and try again.';

export function isInvalidUnsubscribeLink(error: unknown): boolean {
  return error instanceof ApiValidationError && firstErrorCode(error) === 'InvalidUnsubscribeLink';
}

// ---------------------------------------------------------------------------------------------
// HO-APT-002 §11.8 — Appointments. Command `propertyName`s (PascalCase) -> form field names, and
// the HO-APT-001 §10.4 violation codes -> the plain-language text used in §11. The SERVER'S
// message is preferred when present (it names the conflicting time, e.g. "Dr Rahman already has
// an appointment 11:30–12:00."); the text below is the fallback. `errorCode` itself is read here
// only to pick that fallback and to branch behaviour — it is never rendered (NFR-USA-4).
// ---------------------------------------------------------------------------------------------

export const APPOINTMENT_FIELD_MAP = {
  BranchId: 'branchId',
  ServiceId: 'serviceId',
  CatId: 'catId',
  CustomerId: 'customerId',
  StartsOn: 'startsOn',
  PerformerId: 'performerId',
  RoomId: 'roomId',
  ReasonForVisit: 'reasonForVisit',
  Reason: 'reason',
  TriagePriority: 'triagePriority',
  PresentingComplaint: 'presentingComplaint',
  LocalDate: 'localDate',
  PreferredPerformerId: 'preferredPerformerId',
  ExpectedVersion: 'expectedVersion',
} as const;

export const APPOINTMENT_VIOLATION_MESSAGES: Record<string, string> = {
  SlotNoLongerAvailable: 'Someone has just taken that time. Please choose another.',
  SlotHoldExpired: 'Your hold has ended — please choose a time again.',
  ChangeCutoffPassed:
    "It's too close to the appointment to change it online. Please call the clinic.",
  PerformerBusy: 'This vet already has an appointment at that time.',
  RoomBusy: 'That room is already in use at that time.',
  CatAlreadyBooked: 'This cat already has an appointment at that time.',
  PerformerNotAvailable: 'This vet is not available at that time.',
  PerformerNotQualified: 'This vet is not qualified for this service.',
  RoomKindMismatch: 'That room is not the right kind of room for this service.',
  InvalidAppointmentTransition: 'That is not possible for an appointment in its current state.',
  NoShowTooEarly:
    'A no-show can only be recorded once the grace period after the start has passed.',
  CheckInNotToday: 'This appointment can only be checked in on the day it is booked for.',
  SlotsStillAvailable: 'Times have just become available — pick one below.',
  AlreadyWaitlisted: 'This cat is already on the waitlist for that day.',
  CatNotBookable: 'This cat cannot be booked right now.',
  CancellationReasonRequired: 'Enter a reason for cancelling.',
  ServiceNotBookable: 'This service cannot be booked online.',
  SlotOutsideAvailability: 'That time is outside the vet’s available hours.',
  SlotTooSoon: 'That time is too soon to book online. Please choose a later time.',
  SlotBeyondHorizon: 'That date is too far ahead to book yet.',
  HoldNotFound: 'This hold could not be found. Please choose a time again.',
  AppointmentNotFound: 'This appointment could not be found.',
};

/** Codes whose fixed wording is part of the screen's own flow (US-CLN-03 AC2/AC3), so the mapped
 *  text wins over whatever the server's generic message says. */
const ALWAYS_MAPPED_CODES = new Set(['SlotNoLongerAvailable', 'SlotHoldExpired']);

/** The `errorCode` of a `400`/`409`'s first violation, for BEHAVIOUR only (never rendered). */
export function getViolationCode(error: unknown): string | null {
  if (error instanceof ApiValidationError) return error.response.errors[0]?.errorCode ?? null;
  if (error instanceof ApiConflictError) return error.response?.errors[0]?.errorCode ?? null;
  return null;
}

/** One sentence for an Appointments failure: the mapped text for the always-mapped codes, else the
 *  server's own message, else the mapped fallback, else the generic text; a `403` reads as a
 *  permission message. */
export function describeAppointmentError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  const code = getViolationCode(error);
  if (code && ALWAYS_MAPPED_CODES.has(code)) return APPOINTMENT_VIOLATION_MESSAGES[code] ?? '';
  const response =
    error instanceof ApiValidationError
      ? error.response
      : error instanceof ApiConflictError
        ? error.response
        : undefined;
  const serverMessage = response?.errors
    .map((violation) => violation.errorMessage)
    .filter(Boolean)
    .join(' ');
  if (serverMessage) return serverMessage;
  if (code && APPOINTMENT_VIOLATION_MESSAGES[code]) return APPOINTMENT_VIOLATION_MESSAGES[code]!;
  if (error instanceof ApiConflictError) {
    return 'This appointment changed since you loaded it. The latest version has been loaded.';
  }
  return 'Something went wrong. Please try again.';
}

/**
 * HO-SHP-002 §11.7 — HO-SHP-001 §10.4 codes mapped to the plain-language messages used in the shop
 * screens. The server's own message is always preferred when present; this is the fallback
 * (NFR-USA-4: the code itself is never rendered, only the text it selects).
 */
export const SHOP_CODE_MESSAGES: Readonly<Record<string, string>> = {
  ProductNotPublishable: "This product can't be published yet. Fix the items marked below.",
  SlugAlreadyInUse: 'This web address is already used by another product.',
  SlugLockedAfterPublication: "The web address can't change once a product has been published.",
  SkuCodeAlreadyInUse: 'This SKU code is already in use.',
  BarcodeAlreadyInUse: 'This barcode is already used by another SKU.',
  InvalidBarcode: 'This barcode is not valid.',
  InvalidShippingWeight: 'Enter a shipping weight between 1 and 100000 g.',
  DuplicateVariant: 'A SKU with the same size, flavour and colour already exists.',
  VariantAttributesRequired: 'Add at least a size or weight, a flavour or a colour.',
  ImageAltTextRequired: 'Describe every image in a short alt text.',
  CountryOfOriginRequired: 'Add the country of origin before publishing this food product.',
  InvalidGuaranteedAnalysis: 'Enter all four analysis values, each between 0 and 100 %.',
  PriceCannotBeBackdated: "A new price can't start in the past.",
  PriceMustBePositive: 'The price must be more than zero.',
  InvalidBundleComponent:
    "This item can't be part of a kit. Kits can't contain other kits or prescription diets.",
  ReviewNotPermitted: 'Only customers who bought this product can review it.',
  AlreadyReviewed: 'You have already reviewed this product.',
  SkuAlreadyAvailable: 'This size is in stock again — you can buy it now.',
  StockAlertAlreadyRequested: 'You already have a pending alert for this item.',
  PurchaseNotPermitted: "This item can't be bought, so we can't set an alert for it.",
  LocaleNotEnabled: 'That language is not enabled for the shop.',
  InvalidCatalogueFilter: 'One of the filters is not valid. Clear the filters and try again.',
};

/** Every HO-SHP-001 command `propertyName` (PascalCase on the wire). `SHOP_FIELD_MAP` maps each to
 *  the lower-camel form field of the same name, so a section form passes one shared table. */
export const SHOP_PROPERTY_NAMES = [
  'Name',
  'Brand',
  'Description',
  'Category',
  'Subcategory',
  'Tags',
  'TaxClassId',
  'CountryOfOriginCode',
  'ListedOnStorefront',
  'IsExpiryTracked',
  'HealthFocus',
  'Slug',
  'Kind',
  'BundleSkuCode',
  'Code',
  'Barcode',
  'SizeOrWeight',
  'Flavour',
  'Colour',
  'PackQuantity',
  'Manufacturer',
  'LifeStage',
  'ProteinSource',
  'IngredientList',
  'GuaranteedAnalysis',
  'ProteinPercent',
  'FatPercent',
  'FibrePercent',
  'MoisturePercent',
  'FeedingGuide',
  'StorageInstructions',
  'PrescriptionRequired',
  'Images',
  'FileId',
  'AltText',
  'Components',
  'ComponentSkuId',
  'Quantity',
  'Links',
  'RelatedProductId',
  'Locale',
  'Amount',
  'CurrencyCode',
  'EffectiveFrom',
  'CountryCode',
  'SourcingNotes',
  'IsFeatured',
  'SortOrder',
  'Rating',
  'Title',
  'Body',
  'SkuId',
  'ProductId',
  'Reason',
  'Note',
] as const;

export const SHOP_FIELD_MAP: Readonly<Record<string, string>> = Object.fromEntries(
  SHOP_PROPERTY_NAMES.map((name) => [name, name.charAt(0).toLowerCase() + name.slice(1)]),
);

/**
 * HO-EMR-002 §11.10 — HO-EMR-001 §10.4 codes used by the clinical screens, as the plain-language
 * fallback sentence for each. The server's own message is always preferred when present
 * (NFR-USA-4); `errorCode` itself is only ever read to pick one of these, never rendered (U-X1).
 */
export const CLINICAL_VIOLATION_MESSAGES: Readonly<Record<string, string>> = {
  VisitFinalised:
    'This visit has been finalised and can no longer be changed. Add an addendum instead.',
  VitalsOutsidePlausibleRange:
    'Some values are outside the usual range. Check them, then confirm to save anyway.',
  VitalSignOutOfRange: 'That value is outside what can be recorded. Check it and try again.',
  VitalsRequired: 'Enter at least one measurement.',
  ProblemRequired:
    'Add at least one diagnosis before finalising. Use "Routine visit — no abnormality detected" for a well visit.',
  AssessmentAndPlanRequired: 'Write the assessment and the plan before finalising.',
  DuplicateProblem: 'That diagnosis is already on the problem list.',
  DiagnosisCodeInactive: 'That diagnosis code has been retired. Choose a current one.',
  AddendumRequiresFinalisedVisit: 'This visit is still open — edit the note instead.',
  BatchExpired: 'That batch has expired and cannot be used. Choose another batch.',
  BatchOverrideReasonRequired: 'Say why you are not using the batch that expires first.',
  InsufficientStock: 'There is not enough stock in the dispensary for that quantity.',
  ControlledDrugNotSupportedHere: 'Controlled drugs are recorded in the register, not here.',
  FollowUpDateInPast: 'Choose a follow-up date after today.',
  FollowUpAppointmentInvalid: 'That appointment cannot be used as the follow-up for this visit.',
  ConsentFormVersionNotCurrent:
    'The consent wording has changed since this page opened. Reload to read the current version before signing.',
  InvalidSignature: 'The signature could not be used. Clear it and sign again.',
  FileTooLarge: 'That file is too large.',
  FileTypeNotAllowed: 'That type of file cannot be uploaded here.',
  FileContentMismatch: 'That file does not look like the type it claims to be.',
  FileRejected: 'That file was rejected by the virus scanner and has not been saved.',
  FileScanUnavailable:
    "File uploads aren't available yet on this system. Record the result values instead, or keep the paper copy.",
  VisitNotFinalised: 'Finalise the consultation before completing the appointment.',
  VisitInProgress: 'This appointment has a consultation in progress, so it cannot be cancelled.',
  VisitBelongsToAnotherBranch: 'This visit belongs to another branch.',
  AppointmentNotReadyForVisit: 'The appointment must be checked in before a visit can be opened.',
  ConsentAlreadyWithdrawn: 'This consent has already been withdrawn.',
  DiagnosticOrderHasResults: 'This order already has results or files and cannot be cancelled.',
  InvalidDiagnosticResult:
    'One of the result values could not be used. Check the rows marked below.',
  InvalidReferenceRange: 'The low value must not be higher than the high value.',
  DiagnosisCodeAlreadyExists: 'A diagnosis code with that code already exists.',
  LabAnalyteAlreadyExists: 'A lab analyte with that code already exists.',
  NoteTemplateAlreadyExistsForService: 'That service already has an active note template.',
  SystemDiagnosisCodeReadOnly: 'This is a built-in code and cannot be changed.',
};

/** The first violation's code of a `400`/`409` — for BEHAVIOUR only (re-exported name kept local). */
export function getClinicalViolationCode(error: unknown): string | null {
  return getViolationCode(error);
}

/**
 * One sentence for a clinical failure: a `403` is a permission message; a `400` is the server's own
 * message, else the mapped fallback for its code; a `409` is the stale-record sentence; anything else
 * is the generic text. Never the code itself.
 */
export function describeClinicalError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  if (error instanceof ApiValidationError) {
    const messages = error.response.errors
      .map((e) => e.errorMessage || CLINICAL_VIOLATION_MESSAGES[e.errorCode] || '')
      .filter(Boolean);
    return messages.length > 0 ? messages.join(' ') : GENERIC_ERROR_MESSAGE;
  }
  if (error instanceof ApiConflictError) {
    return 'This record was changed by someone else. Reload and try again.';
  }
  return GENERIC_ERROR_MESSAGE;
}

export interface MappedFormErrors {
  /** Field name -> message, for the fields the form knows. */
  fields: Record<string, string>;
  /** Everything else (or a non-`400`): one sentence shown above the form — never dropped. */
  root: string | null;
}

/**
 * HO-EMR-002 U-X1 — the §11.8 mapping for forms that keep their values in plain state rather than
 * react-hook-form. A `400`'s violations land on the field `fieldMap` names for their `propertyName`
 * (else the root message); the message is the server's own, else the clinical fallback for its code.
 * `errorCode` is read only to pick that fallback and is never returned as text.
 */
export function mapClinicalFormErrors(
  error: unknown,
  fieldMap: Readonly<Record<string, string>> = {},
): MappedFormErrors {
  const fields: Record<string, string> = {};
  const rootMessages: string[] = [];
  if (error instanceof ApiValidationError) {
    for (const violation of error.response.errors) {
      const message =
        violation.errorMessage ||
        CLINICAL_VIOLATION_MESSAGES[violation.errorCode] ||
        GENERIC_ERROR_MESSAGE;
      const field = fieldMap[violation.propertyName];
      if (field && !fields[field]) fields[field] = message;
      else rootMessages.push(message);
    }
    return { fields, root: rootMessages.length > 0 ? rootMessages.join(' ') : null };
  }
  return { fields, root: describeClinicalError(error) };
}

// ---------------------------------------------------------------------------------------------
// HO-BIL-002 §11.12 — Billing. HO-BIL-001 §10.4 codes mapped to the plain-language messages the
// screens use. The server's own message is ALWAYS preferred (it names amounts: "Only BDT 400.00 is
// outstanding on INV-…"); this table is the fallback when it is empty. `errorCode` is read to pick
// a sentence or to drive behaviour (never rendered). A message about a card number never carries
// the value: the server refuses it without echoing it, and nothing here puts it back.
// ---------------------------------------------------------------------------------------------

export const BILLING_CODE_MESSAGES: Readonly<Record<string, string>> = {
  CurrencyMismatch: 'These amounts are not in the same currency.',
  ChargeNotFound: 'That charge could not be found.',
  ChargeAlreadyRecorded: 'This charge has already been recorded.',
  ChargeAlreadyInvoiced: 'This charge is already on an invoice.',
  ChargeNotActive: 'That charge has been voided.',
  ChargesMixedCustomerOrBranch:
    'Charges on one invoice must belong to the same customer and branch.',
  TaxClassNotFound: 'That tax class could not be found.',
  TaxRateNotConfigured: 'A tax class used here has no rate configured. Set a rate before issuing.',
  InvalidQuantity: 'Enter a quantity above zero.',
  DiscountExceedsAmount: 'The discount is more than the amount.',
  InvoiceNotFound: 'That invoice could not be found.',
  InvoiceNotDraft:
    'Only a draft invoice can be changed. Issued invoices are corrected with a credit note.',
  InvoiceTooManyLines: 'This invoice has too many lines. Split it into two invoices.',
  BillingProfileMissing:
    'This branch has no billing profile yet. An administrator must set one before invoices can be issued.',
  FinancialYearConfigurationChanged: 'The financial year setting changed. Reload and try again.',
  AllocationExceedsOutstanding: 'That is more than is outstanding on the invoice.',
  AllocationExceedsUnallocated: 'That is more than is left unallocated on this payment.',
  EarmarkMismatch: 'This deposit is set aside for something else.',
  PaymentNotFound: 'That payment could not be found.',
  PaymentNotAccepted: 'Only an accepted payment can be changed this way.',
  PaymentNotPending: 'This payment is no longer waiting for approval.',
  ApproverMustDiffer:
    'A payment must be approved by a different staff member than the one who recorded it.',
  ReferenceRequired: 'Enter the reference for this payment method.',
  CollectorRequired: 'Choose who collected this payment.',
  ReferenceLooksLikeCardNumber:
    'This looks like a card number. Enter the terminal slip number instead — card numbers must never be recorded.',
  ReceivedDateInFuture: 'The date received cannot be in the future.',
  ReceivedDateTooOld: 'The date received is too far in the past.',
  PaymentHasRefunds: 'A payment that has been refunded cannot be reversed.',
  ReasonRequired: 'Enter a reason.',
  CreditExceedsInvoice: 'The credit is more than what remains on the invoice.',
  CreditExceedsLine: 'The credit is more than what remains on that line.',
  CreditNoteNotFound: 'That credit note could not be found.',
  RefundMethodNotAllowed: 'That refund method is not allowed for this payment.',
  RefundExceedsAvailable: 'That is more than is available to refund on this payment.',
  CashUpClosed: 'This day has been closed and can no longer be changed.',
  CashUpDateInFuture: 'You cannot count the till for a future day.',
  CountRequired: 'Count the cash before closing the day.',
  VarianceExplanationRequired:
    'The counted cash differs from the expected cash. Enter an explanation before closing.',
  PendingApprovalsExist:
    'Payments recorded for this day are still waiting for approval. Approve or reject them before closing.',
  CollectionNotCod: 'That payment is not a cash-on-delivery collection.',
  CollectionCourierMismatch: 'That collection belongs to a different courier.',
  RemittanceExceedsCollection: 'The amount remitted is more than was collected.',
  StatementDuplicate: 'This statement file has already been imported for this account.',
  StatementTooLarge: 'The statement file is larger than 1 MiB.',
  StatementMalformed: 'The statement file could not be read.',
  StatementLineNotFound: 'That statement line could not be found.',
  BankLineAlreadyMatched: 'This statement line is already matched to a payment.',
  PaymentAlreadyMatched: 'This payment is already matched to a statement line.',
  MatchAmountMismatch: 'The statement line and the payment are not the same amount.',
  MatchMethodNotTransfer: 'Only a bank transfer payment can be matched to a statement line.',
  ExportRangeTooLarge: 'An export can cover at most 366 days. Choose a shorter range.',
  LoyaltyNotEnabled: 'Loyalty points are not enabled.',
  LoyaltyInsufficientPoints: 'The customer does not have that many points.',
  LoyaltyBelowMinimum: 'That is below the minimum number of points that can be redeemed.',
  LoyaltyRedemptionExceedsNet: 'The points are worth more than the invoice.',
  IdempotencyKeyConflict:
    'This form was already used for a different request. Start again from the beginning.',
  StaleVersion: 'This record changed while you were working. It has been reloaded.',
};

/** `Allocations[0].Amount` → `amount`; `Collector.CourierCode` → `courierCode`; `ExternalReference`
 *  → `externalReference`; a camelCase parameter name (`countedCash`) is kept — the form field names
 *  Billing forms register. */
export function billingPropertyName(propertyName: string): string {
  const last = propertyName.includes('.')
    ? propertyName.slice(propertyName.lastIndexOf('.') + 1)
    : propertyName;
  const base = last.replace(/\[\d+\]/g, '');
  if (base.length === 0) return propertyName;
  return base.charAt(0).toLowerCase() + base.slice(1);
}

/** Every violation code of a `400`/`409`, for BEHAVIOUR only (never rendered). */
export function billingViolationCodes(error: unknown): string[] {
  const response =
    error instanceof ApiValidationError
      ? error.response
      : error instanceof ApiConflictError
        ? error.response
        : undefined;
  return response?.errors.map((violation) => violation.errorCode) ?? [];
}

/** The server's message(s) for a `400`/`409`, else the mapped fallback, else null. */
function billingServerMessage(error: unknown): string | null {
  const response =
    error instanceof ApiValidationError
      ? error.response
      : error instanceof ApiConflictError
        ? error.response
        : undefined;
  if (!response || response.errors.length === 0) return null;
  const parts = response.errors
    .map((violation) => violation.errorMessage || BILLING_CODE_MESSAGES[violation.errorCode] || '')
    .filter(Boolean);
  return parts.length > 0 ? parts.join(' ') : null;
}

/** One plain-language sentence for a failed Billing request (a stale write reads as a reload). */
export function describeBillingError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  if (error instanceof ApiConflictError && !billingServerMessage(error)) {
    return BILLING_CODE_MESSAGES.StaleVersion ?? GENERIC_ERROR_MESSAGE;
  }
  return billingServerMessage(error) ?? GENERIC_ERROR_MESSAGE;
}

/**
 * `submitWithServerErrors` for Billing forms: a `400`'s `propertyName`s are normalised
 * (`billingPropertyName`) onto the form's own field names, an unmapped one lands on `root`; a `409`
 * reads as the server's message (or the stale-record sentence); everything else is the generic text.
 * Field values are never reset (NFR-USA-5).
 */
export async function submitBillingForm<TFieldValues extends FieldValues, TResult>(
  action: () => Promise<TResult>,
  setError: UseFormSetError<TFieldValues>,
  options: { knownFields?: ReadonlyArray<Path<TFieldValues>> } = {},
): Promise<TResult | undefined> {
  try {
    return await action();
  } catch (error) {
    const rootField = 'root' as Path<TFieldValues>;
    if (error instanceof ApiValidationError) {
      const fieldMap: Partial<Record<string, Path<TFieldValues>>> = {};
      for (const violation of error.response.errors) {
        fieldMap[violation.propertyName] = billingPropertyName(
          violation.propertyName,
        ) as Path<TFieldValues>;
      }
      applyValidationErrors(error, setError, {
        fieldMap,
        knownFields: options.knownFields,
        codeMessages: BILLING_CODE_MESSAGES,
      });
    } else {
      setError(rootField, { type: 'server', message: describeBillingError(error) });
    }
    return undefined;
  }
}

// ---------------------------------------------------------------------------------------------
// HO-IMP-002 §11.9 — Imports, batches and inventory. HO-IMP-001 §10.4 codes -> the plain-language
// wording used on the screens. The SERVER'S message is preferred when present (it names the lot,
// the outstanding quantity, the location); the text below is the fallback. `errorCode` is read
// only to pick that fallback and to branch behaviour — it is never rendered (NFR-USA-4).
// ---------------------------------------------------------------------------------------------

export const INVENTORY_CODE_MESSAGES: Readonly<Record<string, string>> = {
  InsufficientStock:
    "There isn't enough stock left — someone may have just taken it. Refresh and try again.",
  BatchExpired:
    'This batch has expired and cannot be sold, dispensed or fed. There is no override.',
  BatchNotUsable: 'This batch cannot be used in its current state.',
  ExpiryDateRequired: 'Food and medicine must be received with an expiry date.',
  BatchAlreadyExpired: 'This stock has already expired — reject it instead of accepting it.',
  AllocationOverrideReasonRequired: "You're not taking the soonest-expiring batch. Please say why.",
  ReceiptExceedsOutstanding: "That's more than the purchase order still expects.",
  ApproverMustDiffer: 'Someone else has to approve your request.',
  CountLocationBusy: 'A count is already open at this location.',
  RecallConfirmationMismatch: "The lot number you typed doesn't match.",
  RecallStockStillHeld: 'Write off or return the remaining stock first.',
  ConsignmentNotCleared:
    'The consignment must clear customs before goods can be received against it.',
  MovementNotPermittedAtLocation: "That movement isn't allowed at that location.",
  ConsignmentVersionConflict: 'Someone else changed these costs — reload.',
  ClearanceDocumentsMissing:
    'Customs clearance needs the import permit, health certificate and customs declaration numbers.',
  StockLocationHasStock: 'This location still holds stock. Move or write it off first.',
  StockLocationNotFound:
    'That location could not be used. A write-off needs you to be assigned to every branch that holds the batch.',
  PurchaseOrderHasReceipts:
    "This order already has receipts, so it can't be cancelled. Close it short instead.",
  FractionalQuantityNotAllowed: 'This item is counted in whole units — no decimals.',
  InvalidTemperatureRange: 'The minimum temperature must be lower than the maximum.',
  TransferLocationsMustDiffer: 'Choose two different locations.',
  SupplierCodeAlreadyExists: 'This supplier code is already in use.',
  StockLocationCodeAlreadyExists: 'A location with this code already exists at this branch.',
  NoReceivedQuantityToAllocate:
    'Nothing has been received on this consignment yet, so there is nothing to allocate costs across.',
  ExchangeRateMissing: 'Add an exchange rate for every currency used in the costs.',
  RejectionReasonRequired: 'Say why the rejected quantity was rejected.',
  LotNumberRequired: 'Enter the lot number.',
  UnitCostRequired: 'Enter the unit cost.',
  DuplicateReceiptLine: 'The same SKU, lot and expiry cannot appear twice in one receipt.',
  AdjustmentNoteRequired: 'A note is required.',
  AdjustmentReasonRequired: 'Choose a reason.',
  CloseReasonRequired: 'Give a reason for closing the order short.',
  ExpectedDeliveryInPast: 'The expected delivery date cannot be in the past.',
  InvalidHsCode: 'An HS code is four digits followed by up to three groups of two.',
  InvalidCurrencyCode: 'That is not a valid currency code.',
  InvalidCountryCode: 'That is not a valid country.',
};

/** Every violation of a `400`/`409`, as plain text, in order. */
export function inventoryViolations(
  error: unknown,
): Array<{ propertyName: string; errorCode: string; message: string }> {
  const response =
    error instanceof ApiValidationError
      ? error.response
      : error instanceof ApiConflictError
        ? error.response
        : undefined;
  return (response?.errors ?? []).map((violation) => ({
    propertyName: violation.propertyName,
    errorCode: violation.errorCode,
    message:
      violation.errorMessage ||
      INVENTORY_CODE_MESSAGES[violation.errorCode] ||
      GENERIC_ERROR_MESSAGE,
  }));
}

/** One sentence for an Inventory failure. A `409` without a usable body is a stale record. */
export function describeInventoryError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  const violations = inventoryViolations(error);
  if (violations.length > 0) return violations.map((v) => v.message).join(' ');
  if (error instanceof ApiConflictError) {
    return 'Someone else changed this record while you were working. Reload and try again.';
  }
  return GENERIC_ERROR_MESSAGE;
}

/** True when the first violation carries `code` (behaviour only — the code is never shown). */
export function hasViolationCode(error: unknown, code: string): boolean {
  return inventoryViolations(error).some((violation) => violation.errorCode === code);
}

/** `RecallStockStillHeld` etc. may be mapped with a quantity the server named; the server's own
 *  message is used as written. This helper only lower-camels a PascalCase `propertyName`. */
export function toFieldName(propertyName: string): string {
  return propertyName.charAt(0).toLowerCase() + propertyName.slice(1);
}

/**
 * A line violation is named `Lines[i].Field` (receipt, PO, transfer, adjustment) or
 * `CostComponents[i].Amount`. Returns the index and the lower-camel field, or `null` when the name
 * is not an indexed one.
 */
export function parseIndexedProperty(
  propertyName: string,
): { collection: string; index: number; field: string } | null {
  const match = /^(\w+)\[(\d+)\](?:\.(\w+))?$/.exec(propertyName);
  if (!match) return null;
  return {
    collection: toFieldName(match[1]!),
    index: parseInt(match[2]!, 10),
    field: match[3] ? toFieldName(match[3]) : '',
  };
}

// ---------------------------------------------------------------------------------------------
// HO-PHM-002 §11.9 — Pharmacy and Prescriptions (HO-PHM-001 §10.4 codes). The server's own message
// is preferred wherever it sent one (they are written for the user); these are the fallbacks for an
// empty message. `errorCode` is read only to pick a reaction or one of these sentences, never shown
// (NFR-USA-4).
// ---------------------------------------------------------------------------------------------

export const PHARMACY_CODE_MESSAGES: Readonly<Record<string, string>> = {
  RecentWeightRequired: 'Record a recent weight for this cat before prescribing.',
  PrescriptionWarningsNotAcknowledged: 'Review and acknowledge the warnings before issuing.',
  PrescriptionWarningsChanged: 'Something changed — please review the warnings again.',
  AllergyAcknowledgementReasonRequired: 'Say why you are prescribing despite the allergy.',
  VisitNotOpen: 'This visit has been finalised — prescriptions can no longer be added to it.',
  VisitNotFound: 'That visit could not be found.',
  VisitCatMismatch: 'That visit is for a different cat.',
  CatNotTreatable: 'Nothing can be prescribed for this cat.',
  RouteNotAllowedForMedicine: 'This medicine is not given by that route.',
  MedicineInactive: 'This medicine is no longer active in the formulary.',
  MedicineNotStocked: 'This medicine is not stocked, so it cannot be dispensed here.',
  PrescriptionAlreadyRenewed: 'This prescription has already been renewed.',
  CancellationReasonRequired: 'Give a reason for cancelling.',
  DeclineReasonRequired: 'Give a reason for declining.',
  BatchExpired: 'This batch has expired and cannot be dispensed. There is no override.',
  OnlyExpiredStockAvailable:
    'The only stock in the dispensary has expired and cannot be dispensed. Ask Inventory to write it off and restock.',
  FefoOverrideReasonRequired: 'Say why you are not using the batch that expires first.',
  InsufficientDispensaryStock: 'There is not enough in the dispensary for this supply.',
  InsufficientBatchStock: 'That batch no longer has enough stock. The batch list has been refreshed.',
  DispenseQuantityMismatch: 'The batches chosen must add up to the prescribed quantity.',
  TooManyDispensingLines: 'A supply can be taken from at most five batches.',
  PrescriptionValidityEnded: 'This prescription can no longer be dispensed or repeated.',
  NoSuppliesRemaining: 'There are no supplies left on this prescription.',
  NoRepeatsRemaining: 'There are no repeats left on this prescription.',
  RepeatRequestAlreadyOpen: 'A repeat has already been requested.',
  WitnessRequired: 'A controlled drug needs a witness. The witness enters their own sign-in details.',
  WitnessNotVerified: 'The witness could not be verified. Ask them to enter their own sign-in details.',
  WitnessMustDifferFromDispenser: 'The witness must be a different person from the one dispensing.',
  ReversalWindowPassed: 'This dispensing can no longer be reversed.',
  ReversalReasonRequired: 'Give a reason for the reversal.',
  DispensingNotReversible: 'This dispensing cannot be reversed.',
  CorrectionReasonRequired: 'Give the reason for the correction.',
  CorrectionTargetMismatch: 'That entry cannot be corrected this way.',
  InvalidRegisterQuantity: 'Enter a quantity change the register can record.',
  DietAuthorisationAlreadyActive: 'This cat already has an active authorisation for this food — extend it instead.',
  ProductNotPrescriptionDiet: 'This food does not need a vet’s authorisation.',
  InvalidDietValidity: 'Check the dates — the end cannot be before the start.',
  DietAuthorisationNotExtendable: 'This authorisation can no longer be extended.',
  RevocationReasonRequired: 'Give a reason for revoking.',
  SkuAlreadyLinkedToMedicine: 'One of these SKUs is already linked to another medicine.',
  SkuNotFound: 'One of these SKUs could not be found.',
  FormularyCodeAlreadyExists: 'A medicine with this code already exists.',
  ControlledScheduleRequired: 'Enter the controlled schedule.',
  ControlledFlagChangeReasonRequired: 'Say why this medicine’s controlled status is changing.',
  DoseRangeRequired: 'A per-kg medicine needs a minimum and maximum dose per kg.',
  InvalidDoseRange: 'The minimum must be lower than the maximum.',
  InvalidStrength: 'Enter a strength greater than zero.',
  InvalidInteraction: 'Each interaction needs a medicine or a term, and a note.',
  StaleVersion: 'Someone else changed this medicine. Reload to see their changes.',
};

/** Every violation of a pharmacy `400`/`409`, with the message the screen should show. */
export function pharmacyViolations(
  error: unknown,
): Array<{ propertyName: string; errorCode: string; message: string }> {
  const response =
    error instanceof ApiValidationError || error instanceof ApiConflictError
      ? error.response
      : undefined;
  return (response?.errors ?? []).map((violation) => ({
    propertyName: violation.propertyName,
    errorCode: violation.errorCode,
    message:
      violation.errorMessage || PHARMACY_CODE_MESSAGES[violation.errorCode] || GENERIC_ERROR_MESSAGE,
  }));
}

/** One sentence for a pharmacy failure (a `403` is a permission message; a bare `409` a stale record). */
export function describePharmacyError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  const violations = pharmacyViolations(error);
  if (violations.length > 0) return violations.map((v) => v.message).join(' ');
  if (error instanceof ApiConflictError) {
    return 'Someone else changed this record while you were working. Reload and try again.';
  }
  return GENERIC_ERROR_MESSAGE;
}

/** The first violation's `errorCode` if it is one of `codes` — behaviour only, never shown. */
export function pharmacyCode(error: unknown, ...codes: string[]): string | null {
  const found = pharmacyViolations(error).find((v) => codes.includes(v.errorCode));
  return found ? found.errorCode : null;
}

/** `Dose.Amount` / `catId` / `AcknowledgedWarningsFingerprint` → `dose.amount` / `catId` / … */
export function pharmacyFieldName(propertyName: string): string {
  return propertyName
    .split('.')
    .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
    .join('.');
}

// ---------------------------------------------------------------------------------------------
// HO-ORD-002 §11.13 — Cart, checkout and orders. HO-ORD-001 §10.4 codes -> the plain-language
// fallback used when the server sent no message. The SERVER'S message is always preferred (it names
// the product, the amount, the batch); `errorCode` is read here only to pick a fallback and to
// branch behaviour — it is never rendered (NFR-USA-4). Checked against `BusinessViolationCodes` and
// `OrderingResults` in `src/`.
// ---------------------------------------------------------------------------------------------

export const ORDER_CODE_MESSAGES: Readonly<Record<string, string>> = {
  // cart
  CartFull: 'Your cart is full. Remove something before adding more.',
  SkuNotPurchasable: "This item can't be bought right now.",
  SkuUnavailable: "This item isn't available right now.",
  QuantityOutOfRange: 'Choose a smaller quantity.',
  PrescriptionCatRequired: 'Choose which cat this prescription food is for.',
  PrescriptionNotAuthorised: "There's no current vet authorisation for this food for that cat.",
  // checkout
  CheckoutQuoteChanged:
    'Something changed while you were checking out — please review and place the order again.',
  StockNoLongerAvailable:
    'An item has just sold out. It has been removed from your order — please review and place it again.',
  CartEmpty: 'Your cart is empty.',
  NothingToPlace: 'There is nothing left in your cart that can be ordered.',
  PolicyAcceptanceRequired: 'Please read and accept our updated policies before placing your order.',
  DeliveryAddressNotFound: 'Choose a delivery address.',
  AddressOutsideServedZones: "We don't deliver to that address yet. You can collect your order instead.",
  ServiceLevelUnavailable: "That delivery option isn't available. Choose another.",
  DeliverySlotUnavailable: "That delivery time isn't available. Choose another.",
  PickupBranchUnavailable: "That branch isn't available for collection. Choose another.",
  PaymentMethodUnavailable: "That payment method isn't available for this order.",
  RecipientPhoneRequired: 'Enter a phone number for the person receiving the order.',
  DeliveryTaxNotConfigured:
    'Delivery is temporarily unavailable. Please choose collection from a branch, or try again later.',
  DuplicateSubmission: 'This order has already been placed.',
  IdempotencyKeyRequired: 'Something went wrong sending that. Please try again.',
  IdempotencyKeyReused: 'That looked like a different request — please start again.',
  RequestInProgress: 'This is already being processed.',
  // discounts
  DiscountCodeNotFound: "We don't recognise that code.",
  DiscountCodeInactive: 'That code is not active.',
  DiscountCodeNotYetValid: "That code isn't valid yet.",
  DiscountCodeExpired: 'That code has expired.',
  DiscountUsageLimitReached: 'That code has been used up.',
  DiscountCustomerLimitReached: "You've already used that code the maximum number of times.",
  DiscountFirstOrderOnly: 'That code is for first orders only.',
  DiscountNotApplicableToItems: "That code doesn't apply to the items in your order.",
  DiscountMinimumSpendNotMet: "Your order doesn't reach the minimum spend for that code.",
  DiscountCodeAlreadyExists: 'A code with this name already exists.',
  InvalidDiscountDefinition: 'Check the discount settings.',
  // order lifecycle
  OrderNotFound: 'This order could not be found.',
  InvalidOrderTransition: "That isn't possible for an order in its current state.",
  CancellationNotAvailable:
    "This order can't be cancelled online any more. Please contact the shop.",
  CancellationReasonRequired: 'Enter a reason for cancelling.',
  OrderNotAwaitingPayment: 'This order is not waiting for a payment.',
  OrderNotExpirable: 'This order cannot expire.',
  TransferAmountMismatch: "The amount doesn't match the order total.",
  PaymentAlreadyAwaitingApproval: 'A payment for this order is already waiting for approval.',
  NoPaymentAwaitingApproval: 'No payment is waiting for approval on this order.',
  SecondApproverMustDiffer: 'Someone else must approve a payment you recorded.',
  ApproverMustDiffer: 'Someone else must approve a payment you recorded.',
  PaymentNotPending: 'That payment is no longer waiting for approval.',
  OrderNotFullyAllocated: "This order's items are not all allocated to stock yet.",
  BatchRecalled: 'A batch on this order has been recalled. Reallocate that line first.',
  BatchExpired: 'A batch on this order has expired. Reallocate that line first.',
  CourierNotFound: 'Choose a courier.',
  CodCollectionRequired: 'Record the cash collected on delivery.',
  CollectionPaymentMismatch: "The amount doesn't match what is still to pay.",
  TerminalReferenceRequired: 'Enter the terminal slip number for a card payment.',
  DeliveryFailureReasonInvalid: "That reason doesn't apply to this order.",
  TendersDoNotCoverTotal: "The payments entered don't cover the total yet.",
  ReallocationReasonRequired: 'Enter a reason for moving this line to another batch.',
  AccountNotActive: "This customer's account is closed, so a sale can't be recorded against it.",
  StaleVersion: 'This order changed while you were working. It has been reloaded.',
  // returns and refunds
  ReturnNotFound: 'This return could not be found.',
  ReturnWindowClosed: 'The return window for this order has closed.',
  ReturnQuantityExceeded: 'You can return at most the quantity you bought.',
  ReturnNotEligibleOpenedFood:
    "Opened food can only be returned if there's a quality problem or a recall.",
  ReturnNotEligibleUsedLitter: 'Used litter cannot be returned.',
  DefectConfirmationRequired: 'Confirm the defect, with a note, on every line that needs it.',
  InvalidReturnTransition: "That isn't possible for a return in its current state.",
  ReturnReceiptExceedsApproved: "You can't receive more than was approved.",
  RefundExceedsAmountPaid: "That's more than can still be refunded.",
  RefundMethodNotAllowed: "That refund method isn't allowed for how this order was paid.",
  RefundMethodUnavailable: "Store credit isn't available yet.",
  RefundReferenceRequired: 'Enter the reference for this refund.',
  // configuration
  DeliveryZoneNotFound: 'This delivery zone could not be found.',
  DeliveryZoneNameAlreadyExists: 'A delivery zone with this name already exists.',
  InvalidDeliveryZone: 'Check the delivery zone settings.',
  CourierNameAlreadyExists: 'A courier with this name already exists.',
  InvalidBankTransferAccount: 'Check the bank account details.',
  ReconciliationIssueNotFound: 'This issue could not be found.',
};

/** Quote `blockingReasons[]` -> why Place order is disabled (codes are never rendered). */
export function describeBlockingReason(code: string): string {
  return ORDER_CODE_MESSAGES[code] ?? 'This order cannot be placed yet.';
}

/** A merge `droppedLines[].reason` or re-order `unavailable[].reason` code -> a sentence fragment. */
export function describeDroppedReason(code: string): string {
  switch (code) {
    case 'CartFull':
      return 'your cart was full';
    case 'NotPurchasable':
      return 'no longer sold';
    case 'OutOfStock':
      return 'out of stock';
    case 'PrescriptionNotAuthorised':
      return 'needs a current vet authorisation';
    default:
      return 'not available';
  }
}

/** A refunds-due `reasons[]` code -> a short label. */
export function describeRefundReason(code: string): string {
  if (code === 'MoneyOwedBack') return 'Money owed back';
  if (code === 'ReturnReceived') return 'Return received';
  return 'Refund due';
}

/** An excluded quote line's `reasonCode` -> a short label shown beside the product. */
export function describeExcludedReason(code: string): string {
  switch (code) {
    case 'NowUnavailable':
      return 'No longer available';
    case 'PrescriptionAuthorisationExpired':
      return 'Needs a current vet authorisation';
    case 'PrescriptionCatRequired':
      return 'Needs a cat';
    case 'TaxRateNotConfigured':
      return 'Cannot be priced right now';
    default:
      return 'Left out of this order';
  }
}

/** The violations of a `400`/`409`, in order. */
function orderingViolations(error: unknown): Array<{ code: string; message: string; property: string }> {
  const response =
    error instanceof ApiValidationError
      ? error.response
      : error instanceof ApiConflictError
        ? error.response
        : undefined;
  return (
    response?.errors.map((violation) => ({
      code: violation.errorCode,
      message: violation.errorMessage || ORDER_CODE_MESSAGES[violation.errorCode] || '',
      property: violation.propertyName,
    })) ?? []
  );
}

/** Every violation code of a failed Ordering request, for BEHAVIOUR only (never rendered). */
export function orderingViolationCodes(error: unknown): string[] {
  return orderingViolations(error).map((violation) => violation.code);
}

export function hasOrderingCode(error: unknown, code: string): boolean {
  return orderingViolationCodes(error).includes(code);
}

/** `422 IdempotencyKeyReused` arrives as a bare `ApiError` (`client.ts` maps only 400/404/409/…). */
export function isIdempotencyKeyReused(error: unknown): boolean {
  return error instanceof ApiError && error.status === 422;
}

/**
 * Should the Idempotency-Key be thrown away after this failure? The API stores and replays EVERY response below
 * 500 for a key (`IdempotencyFilter`), so after a definitive refusal whose cause lies outside the payload (a policy
 * accepted, a batch restocked, an account reactivated...) the same key with the same body would replay the stale
 * refusal. Keep the key only when no definitive answer arrived: a network failure, a 5xx, or
 * `409 RequestInProgress` (the original attempt is still running - the same key replays its result).
 */
export function shouldRenewIdempotencyKey(error: unknown): boolean {
  if (!(error instanceof ApiError)) return false;
  if (error.status < 400 || error.status >= 500) return false;
  return !orderingViolationCodes(error).includes('RequestInProgress');
}

/** One plain-language sentence for a failed Ordering request. A `403` reads as a permission message. */
export function describeOrderingError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  if (isIdempotencyKeyReused(error)) return ORDER_CODE_MESSAGES.IdempotencyKeyReused ?? GENERIC_ERROR_MESSAGE;
  const messages = orderingViolations(error)
    .map((violation) => violation.message)
    .filter(Boolean);
  if (messages.length > 0) return messages.join(' ');
  if (error instanceof ApiConflictError) return ORDER_CODE_MESSAGES.StaleVersion ?? GENERIC_ERROR_MESSAGE;
  return GENERIC_ERROR_MESSAGE;
}

/** `Lines[2].Quantity` -> `{ index: 2, field: 'quantity' }`; used for per-line return errors (`lines[i]`). */
export function parseLineProperty(propertyName: string): { index: number; field: string } | null {
  const match = /^lines\[(\d+)\](?:\.(\w+))?$/i.exec(propertyName);
  if (!match) return null;
  const field = match[2] ?? '';
  return { index: parseInt(match[1]!, 10), field: field ? toFieldName(field) : '' };
}

/**
 * `submitWithServerErrors` for Ordering forms: a `400`'s `propertyName`s are lower-camelled onto the
 * form's own field names (`ReceivedOn` -> `receivedOn`, `Collector.CourierCode` -> `courierCode`), an
 * unmapped one lands on `root`; any other failure reads as one sentence on `root`. Field values are
 * never reset (NFR-USA-5).
 */
export async function submitOrderingForm<TFieldValues extends FieldValues, TResult>(
  action: () => Promise<TResult>,
  setError: UseFormSetError<TFieldValues>,
  options: {
    knownFields?: ReadonlyArray<Path<TFieldValues>>;
    fieldMap?: Partial<Record<string, Path<TFieldValues>>>;
  } = {},
): Promise<TResult | undefined> {
  try {
    return await action();
  } catch (error) {
    const rootField = 'root' as Path<TFieldValues>;
    if (error instanceof ApiValidationError && error.response.errors.length > 0) {
      const fieldMap: Partial<Record<string, Path<TFieldValues>>> = {};
      for (const violation of error.response.errors) {
        fieldMap[violation.propertyName] =
          options.fieldMap?.[violation.propertyName] ??
          (billingPropertyName(violation.propertyName) as Path<TFieldValues>);
      }
      applyValidationErrors(error, setError, {
        fieldMap,
        knownFields: options.knownFields,
        codeMessages: ORDER_CODE_MESSAGES,
      });
    } else {
      setError(rootField, { type: 'server', message: describeOrderingError(error) });
    }
    return undefined;
  }
}

// ---------------------------------------------------------------------------------------------
// HO-POR-002 — the portal's re-order (Orders' endpoint) and its result reasons. Appended.
// ---------------------------------------------------------------------------------------------

/** Plain-language text for the `reason` code Orders puts on each line it could not add
 *  (`ReorderCommandHandler`). The set is open — a domain rule code can appear — so an unknown
 *  code gets the generic sentence and is never shown (NFR-USA-4). */
const REORDER_REASON_LABELS: Readonly<Record<string, string>> = {
  NotPurchasable: 'No longer sold.',
  OutOfStock: 'Out of stock right now.',
  PrescriptionNotAuthorised:
    'Needs a prescription or vet authorisation for the cat it was ordered for.',
  CartFull:
    'Your basket already holds the most different items it can. Remove something to add this.',
  QuantityOutOfRange: 'The quantity is not allowed in one order. Add it from the product page.',
};

export const REORDER_REASON_FALLBACK = 'This could not be added to your basket.';

export function describeReorderReason(reason: string): string {
  return REORDER_REASON_LABELS[reason] ?? REORDER_REASON_FALLBACK;
}

/** One title and one sentence for a failed re-order request: `404` (not on the account), `400`
 *  (refused), `409` (basket changed or a request is still running). Nothing else reaches the page. */
export function describeReorderError(error: unknown): { title: string; message: string } {
  if (error instanceof ApiNotFoundError) {
    return {
      title: 'We couldn’t find that order.',
      message:
        'It may have been removed, or it isn’t on your account. Nothing was added to your basket.',
    };
  }
  if (
    error instanceof ApiValidationError &&
    reorderViolationCodes(error).includes('IdempotencyKeyRequired')
  ) {
    return {
      title: 'We couldn’t send that request.',
      message: 'Please reload the page and try again.',
    };
  }
  if (error instanceof ApiValidationError) {
    return {
      title: 'This order can’t be ordered again.',
      message: 'Nothing was added to your basket.',
    };
  }
  if (hasOrderingCode(error, 'RequestInProgress')) {
    return {
      title: 'We’re still adding your items.',
      message: 'Your first request is still being processed. Please wait a moment, then try again.',
    };
  }
  if (error instanceof ApiConflictError) {
    return {
      title: 'Your basket changed while we were adding items.',
      message: 'Please try again.',
    };
  }
  return { title: 'We couldn’t add these items.', message: 'Please try again in a moment.' };
}

function reorderViolationCodes(error: unknown): string[] {
  const response =
    error instanceof ApiConflictError || error instanceof ApiValidationError
      ? error.response
      : undefined;
  return response?.errors?.map((e) => e.errorCode) ?? [];
}

// ---------------------------------------------------------------------------------------------
// HO-SRG-002 §11.11 — Surgery (HO-SRG-001 §10.4 codes). The SERVER'S message is preferred wherever it
// sent one (they are written for the user: they name the surgeon, the theatre, the time); these are the
// fallbacks for an empty message. `errorCode` is read only to pick a reaction or one of these sentences,
// never shown (NFR-USA-4). Checked against `BusinessViolationCodes.Surgery.cs` and `SurgeryMessages.cs`.
// ---------------------------------------------------------------------------------------------

export const SURGERY_CODE_MESSAGES: Readonly<Record<string, string>> = {
  SurgeryNotFound: 'That surgery could not be found.',
  InvalidSurgeryTransition:
    'That is not possible at this stage of the surgery. Reload the page to see where it is.',
  ServiceNotSurgical: 'This service is not a theatre procedure.',
  ProcedureSexMismatch: 'This procedure does not match the cat’s sex.',
  CatAlreadyNeutered: 'This cat is recorded as already neutered.',
  SurgeonNotQualified: 'That surgeon is not qualified to perform this procedure.',
  AssistantNotQualified: 'An assistant must be a nurse or a vet.',
  AssistantSameAsSurgeon: 'The assistant must be a different person from the surgeon.',
  TheatreNotAvailable: 'That room is not an active theatre at this branch.',
  SurgeonNotAvailable: 'The surgeon is not available at that time.',
  AssistantNotAvailable: 'The assistant is not available at that time.',
  SlotInPast: 'That time has already passed.',
  SlotNotOnGrid: 'That time is not on the booking grid. Choose one of the times offered.',
  TheatreBusy: 'The theatre is already booked at that time.',
  SurgeonBusy: 'The surgeon is already booked at that time.',
  AssistantBusy: 'The assistant is already booked at that time.',
  StaffBusy: 'The surgeon or assistant has just been booked elsewhere. Check availability again.',
  CatAlreadyBooked: 'This cat already has an appointment at that time.',
  CatNotBookable: 'This cat cannot be booked.',
  AdmissionNotToday: 'The cat can only be admitted on the day of surgery.',
  ConsentNotRecorded:
    'Consent has not been recorded for this surgery. Record consent, or a veterinarian may override in an emergency.',
  ConsentOverrideNotPermitted: 'Only a veterinarian can start a surgery without recorded consent.',
  ConsentOverrideReasonRequired:
    'Give the reason for starting without consent (at least 10 characters).',
  VeterinarianRequired: 'Only a veterinarian can do this.',
  PreOpChecklistIncomplete: 'The pre-op checklist is not complete.',
  ChecklistItemNotWaivable: 'That checklist item cannot be waived.',
  ChecklistItemAlreadyDone: 'That checklist item is already done.',
  ChecklistItemNotManual: 'That checklist item is completed automatically.',
  WeightRecordNotValidForSurgery:
    'Choose a weight record of this cat taken on the day of surgery or shortly before it.',
  AnaesthesiaLogClosed:
    'The anaesthesia log is only open while the cat is in pre-op, in theatre or in recovery.',
  ImplausibleVitalValue: 'That value looks unlikely. Check it, or save it anyway if it is correct.',
  MedicineNotFound: 'That medicine is not in the formulary or is no longer active.',
  BatchRequired: 'This is a stocked medicine: choose the batch and the quantity drawn.',
  BatchSkuMismatch: 'That batch is not one of this medicine’s batches held at the clinic.',
  BatchNotUsable: 'This batch can’t be used — it has expired or is quarantined. Choose another.',
  BatchExpired: 'This batch can’t be used — it has expired or is quarantined. Choose another.',
  AdministeringVetRequired:
    'A controlled drug is given by a veterinarian. Choose the administering vet.',
  WitnessRequired:
    'A controlled drug needs a witness. The witness enters their own sign-in details.',
  WitnessNotVerified:
    'The witness could not be verified. Ask them to enter their own sign-in details.',
  WitnessConfirmationInvalid:
    'The witness confirmation has expired or was already used. Ask the witness to confirm again.',
  WitnessSameAsAdministrator: 'The witness must be a different person from the administering vet.',
  WastageRequired: 'Record the quantity wasted (0 when none) for a controlled drug.',
  InvalidQuantity: 'Check the quantity.',
  CorrectionTargetNotFound:
    'That entry can no longer be corrected. Reload the log and correct the latest entry.',
  RecordIsImmutable: 'This record can no longer be changed.',
  AnaesthesiaEntryAlreadyCorrected:
    'That entry has just been corrected by someone else. Reload the log and correct the latest correction instead.',
  DuplicateAnaesthesiaEntry: 'This entry could not be saved. Start the entry again.',
  AnaesthesiaEntryRaced: 'This entry is already being saved. Wait a moment, then try again.',
  AnaesthesiaEntryIdMismatch: 'This entry could not be saved. Start the entry again.',
  RecoveryHistoryLimitReached: 'This surgery has reached its limit of recovery status changes.',
  ReportNotEditable: 'This report has been finalised and can only be added to with an addendum.',
  ReportAlreadyFinalised: 'This report has already been finalised.',
  ReportIncomplete: 'Fill in the missing parts of the report before finalising it.',
  ReportNotFinalised: 'Finalise the report before adding an addendum.',
  NotReadyForDischarge:
    'The cat is not ready for discharge. Set the recovery status to ready for discharge first.',
  PostOpInstructionsRequired: 'Write the post-op instructions for the owner.',
  FollowUpOutsideWindow: 'That follow-up date is outside the allowed window.',
  FollowUpNotPending: 'That follow-up is already closed.',
  SurgeryFollowUpNotFound: 'That follow-up could not be found.',
  CampaignNotApplicable: 'That campaign does not apply to this booking.',
  CampaignServiceNotSterilisation: 'A campaign can only price spay and neuter procedures.',
  CampaignPriceLocked: 'Prices can’t change once a surgery has been booked under this campaign.',
  CampaignCodeAlreadyExists: 'A campaign with this code already exists.',
  SurgeryCampaignNotFound: 'That campaign could not be found.',
  SurgeryCampaignInvalid: 'Check the campaign details.',
  SurgeryProtocolInvalid: 'Check the protocol details.',
  SurgeryTextInvalid: 'Check the text — it has characters that are not allowed.',
  AppointmentManagedBySurgery:
    'This appointment belongs to a surgery and can only be changed from the surgery.',
  StaleVersion: 'Someone else changed this. Reload to see their changes.',
  CancellationReasonRequired: 'Give a reason for cancelling.',
  CatIsDeceased: 'This cat is recorded as deceased.',
};

export interface SurgeryViolation {
  /** Lower-camel, as the form field is called (`theatreId`, `startsOn`). */
  propertyName: string;
  errorCode: string;
  message: string;
}

/** Every violation of a surgery `400`/`409` (and a `403` that carries one), with the message to show. */
export function surgeryViolations(error: unknown): SurgeryViolation[] {
  const raw: Array<{ propertyName?: string; errorCode: string; errorMessage: string }> =
    error instanceof ApiValidationError || error instanceof ApiConflictError
      ? (error.response?.errors ?? [])
      : error instanceof ApiForbiddenError
        ? (error.body.errors ?? [])
        : [];
  return raw.map((violation) => ({
    propertyName: toFieldName(violation.propertyName ?? ''),
    errorCode: violation.errorCode,
    message:
      violation.errorMessage || SURGERY_CODE_MESSAGES[violation.errorCode] || GENERIC_ERROR_MESSAGE,
  }));
}

/** The first violation's code if it is one of `codes` — behaviour only, never shown. */
export function surgeryCode(error: unknown, ...codes: string[]): string | null {
  const found = surgeryViolations(error).find((v) => codes.includes(v.errorCode));
  return found ? found.errorCode : null;
}

/** One sentence for a surgery failure (a plain `403` is the permission message; a bare `409` a stale record). */
export function describeSurgeryError(error: unknown): string {
  if (error instanceof ApiForbiddenError) {
    const specific = surgeryViolations(error).find((v) => v.errorCode !== 'ActionNotPermitted');
    return specific ? specific.message : describeForbidden(error);
  }
  const violations = surgeryViolations(error);
  if (violations.length > 0) return violations.map((v) => v.message).join(' ');
  if (error instanceof ApiConflictError) {
    return 'Someone else changed this record while you were working. Reload and try again.';
  }
  if (error instanceof ApiNotFoundError) return 'That could not be found.';
  return GENERIC_ERROR_MESSAGE;
}

// -------------------------------------------------------------------------------------------
// HO-XCT-002 §11.7 — retention, erasure, backup and restore. The server's own message is preferred
// (HO-XCT-001 `OperationsMessages` is written for a person); these sentences are the fallback when
// it is empty, and `errorCode` itself is never shown (NFR-USA-4).
// -------------------------------------------------------------------------------------------

export const OPERATIONS_CODE_MESSAGES: Readonly<Record<string, string>> = {
  RecordIsRetained: "This record is kept for legal reasons and can't be deleted.",
  CustomerRecordMerged:
    'This record was merged into another customer. Erase the surviving record instead.',
  CustomerAccountNotFound: "We couldn't find that customer.",
  RetentionRunAlreadyInProgress: 'A retention run is already running. Try again when it finishes.',
  ReapplyListTooLarge: "That's more than 500 customers. Split the list.",
  InvalidRestoreTestTimes:
    'The times do not run in order. Check the failure, recovery and restore times.',
  RestoreTestChecksRequired: 'Add at least one check from the drill output.',
  RestoreTestAlreadyRecorded:
    'A restore test is already recorded for this date and backup set. Choose the test it replaces.',
  SupersededRestoreTestNotFound: "The test you chose to replace couldn't be found.",
  ActiveBranchRequired: 'Choose a branch first.',
  InvalidDateRange: 'Choose a start and end date, at most 93 days apart.',
};

/** Violation codes on a `400`/`409` body, in order. */
export function operationsViolationCodes(error: unknown): string[] {
  const response =
    error instanceof ApiValidationError || error instanceof ApiConflictError
      ? error.response
      : undefined;
  return response?.errors?.map((e) => e.errorCode) ?? [];
}

export function hasOperationsCode(error: unknown, code: string): boolean {
  return operationsViolationCodes(error).includes(code);
}

/** One display string for an operations action that failed. Never contains an error code or id. */
export function describeOperationsError(error: unknown): string {
  if (error instanceof OfflineError) return OFFLINE_WRITE_MESSAGE;
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  if (error instanceof ApiNotFoundError) return "We couldn't find that record.";
  if (error instanceof ApiValidationError || error instanceof ApiConflictError) {
    const errors = error.response?.errors ?? [];
    if (errors.length > 0) {
      return errors
        .map((e) => e.errorMessage || OPERATIONS_CODE_MESSAGES[e.errorCode] || GENERIC_ERROR_MESSAGE)
        .join(' ');
    }
  }
  return GENERIC_ERROR_MESSAGE;
}

// ---------------------------------------------------------------------------------------------
// HO-RPT-002 §11.7, NFR-USA-4 — Reporting. The server's own message is preferred whenever it sends
// one (HO-RPT-001 §10.4 writes them in plain language with the next step); the text below is the
// fallback when it does not. `errorCode` picks a fallback and drives behaviour — it is never
// rendered (HO-ACC-002 §11.14).
// ---------------------------------------------------------------------------------------------

export const REPORT_CODE_MESSAGES: Readonly<Record<string, string>> = {
  UnknownReport: "That report doesn't exist.",
  ReportRangeRequired: 'Choose a date range: both a start date and an end date.',
  ReportRangeInverted:
    'The end date is before the start date. Choose an end date on or after the start date.',
  ReportRangeTooLong: 'That range is too long for this report. Choose a shorter range.',
  InvalidReportParameter: 'One of the filters has a value that is not allowed. Choose a listed value.',
  UnknownReportParameter: 'One of the filters does not belong to this report. Reset the filters.',
  CrossBranchReportNotPermitted: 'You can run this for your own branch.',
  BranchNotAssigned: "You aren't assigned to that branch. Choose one of your branches.",
  ReportTooLargeForCsv: 'Too many rows for a CSV export. Narrow the date range or the filters.',
  ReportTooLargeForPdf: 'Too many rows for a PDF — export CSV instead.',
  ReportTimedOut: 'That took too long. Try a shorter range or a single branch.',
  ReportScheduleNotFound: "That schedule doesn't exist.",
  InvalidReportSchedule: 'Check the schedule and try again.',
  InvalidReportSchedulePeriod: 'That period does not suit this report. Choose one of the periods offered.',
  ReportRecipientRequired: 'Choose at least one recipient.',
  ReportRecipientLimitExceeded: 'A schedule can have at most 20 recipients.',
  ReportRecipientNotPermitted: "That person can't open this report, so they can't receive it.",
  ReportRecipientHasNoEmail: 'That person has no email address, so they cannot receive a report.',
  ReportRecipientNotAssignedToBranch: "That person isn't assigned to the schedule's branch.",
  ReportScheduleCancelled: 'This schedule was cancelled and cannot be changed. Create a new one.',
  ReportFormatNotSupported: 'A schedule can send a CSV or a PDF.',
  StaleVersion: 'Someone else changed this schedule. Reload it and try again.',
};

export interface ReportViolation {
  propertyName: string;
  errorCode: string;
  message: string;
}

/** Every violation of a `400`/`403`/`409`/`413`, as plain text, in order. */
export function reportViolations(error: unknown): ReportViolation[] {
  const response =
    error instanceof ApiValidationError || error instanceof ApiConflictError
      ? error.response
      : error instanceof ApiRateLimitedError
        ? error.response
        : undefined;
  const fromResponse = (response?.errors ?? []).map((violation) => ({
    propertyName: violation.propertyName ?? '',
    errorCode: violation.errorCode,
    message:
      violation.errorMessage || REPORT_CODE_MESSAGES[violation.errorCode] || GENERIC_ERROR_MESSAGE,
  }));
  if (fromResponse.length > 0) return fromResponse;
  if (error instanceof ApiForbiddenError) {
    return (error.body.errors ?? []).map((violation) => ({
      propertyName: '',
      errorCode: violation.errorCode,
      message:
        violation.errorCode === 'ActionNotPermitted'
          ? PERMISSION_DENIED_MESSAGE
          : violation.errorMessage ||
            REPORT_CODE_MESSAGES[violation.errorCode] ||
            PERMISSION_DENIED_MESSAGE,
    }));
  }
  return [];
}

export function hasReportCode(error: unknown, code: string): boolean {
  return reportViolations(error).some((violation) => violation.errorCode === code);
}

/** A `429` carries `Retry-After` (the per-account concurrency limit, backlog 42) — say when. */
/**
 * A `429` may carry `Retry-After`. The API does not expose that header cross-origin (RPT backlog 88),
 * so `client.ts` and the export helper fall back to 60; a value of 60 is therefore treated as an
 * estimate and worded as one, and every other value is "about" that long.
 */
function describeRateLimited(error: ApiRateLimitedError): string {
  const seconds = Math.max(1, Math.round(error.retryAfterSeconds));
  const lead = "You're running reports too quickly.";
  if (seconds === 60) return `${lead} Try again in a minute or so.`;
  if (seconds >= 120) return `${lead} Try again in about ${Math.ceil(seconds / 60)} minutes.`;
  return `${lead} Try again in about ${seconds} ${seconds === 1 ? 'second' : 'seconds'}.`;
}

/** One sentence for a Reporting failure. A bare `5xx` (the clinical-access log failing closed,
 *  HO-RPT-001 Amended (12)) says that nothing was shown, because that is exactly what happened. */
export function describeReportError(error: unknown): string {
  if (error instanceof ApiRateLimitedError) return describeRateLimited(error);
  if (error instanceof ApiConflictError && reportViolations(error).length === 0) {
    return REPORT_CODE_MESSAGES.StaleVersion ?? GENERIC_ERROR_MESSAGE;
  }
  const violations = reportViolations(error);
  if (violations.length > 0) return violations.map((v) => v.message).join(' ');
  if (error instanceof ApiNotFoundError) return REPORT_CODE_MESSAGES.UnknownReport ?? GENERIC_ERROR_MESSAGE;
  if (error instanceof ApiError && error.status >= 500) {
    return "The report couldn't be opened just now, so nothing was shown. Please try again.";
  }
  return GENERIC_ERROR_MESSAGE;
}

// -------------------------------------------------------------------------------------------
// HO-BRD-002 §11.13 — Boarding. HO-BRD-001 §10.4 codes -> plain-language fallback text. The
// SERVER'S message is preferred whenever it is present (it names the cat, the dates and the
// capacity); the text below is only the fallback. `errorCode` is read to pick that fallback and to
// branch behaviour — it is never rendered (NFR-USA-4, "no `errorCode` appears in the DOM").
// -------------------------------------------------------------------------------------------

export const BOARDING_VIOLATION_MESSAGES: Readonly<Record<string, string>> = {
  BoardingUnitNotFound: 'This unit could not be found.',
  VetAuthorityReasonRequired:
    'Give a reason for changing the vet authorisation or the spending limit on a reserved stay.',
  UnitCodeAlreadyExists: 'A unit with this code already exists at this branch.',
  UnitHasFutureStays: "This unit has upcoming stays, so its type can't change and it can't be switched off.",
  MaintenanceOverlapsStays: 'Those dates overlap a stay in this unit. Move the stay first.',
  MaintenanceOverlapsMaintenance: 'This unit is already on maintenance during those dates.',
  RateCardCannotBeBackdated: "A new rate card can't start in the past.",
  DuplicateRateCardEffectiveDate: 'A rate card already starts at that time.',
  UnitTypeNotPriced: "This type of unit has no rate card yet, so it can't be booked.",
  InvalidStayDates: 'Departure must be after arrival.',
  StayTooLong: 'That stay is longer than we can take in one booking.',
  StayBeyondHorizon: 'That date is too far ahead to book yet.',
  ArrivalTooLate: 'The check-in time for that day has already passed.',
  StayNotFound: 'This stay could not be found.',
  InvalidStayTransition: "That isn't possible for a stay in its current state.",
  CatNotBoardable: "This cat can't be booked in right now.",
  DuplicateCatInStay: 'Each cat can only be added once.',
  UnitCapacityExceeded: "That unit can't hold this many cats. Choose a larger one.",
  NoUnitAvailable: 'No unit is free for those dates.',
  UnitNoLongerAvailable: 'That unit has just been taken. Choose again from the list below.',
  CatAlreadyBoarding: 'This cat already has a stay that overlaps those dates.',
  VaccinationGateBlocked: "A vaccination is overdue, so this cat can't be booked in yet.",
  OverrideReasonRequired: 'Enter the reason for the override.',
  CareProfileIncomplete: "A cat's care profile is incomplete. Finish it and try again.",
  InvalidCareProfile: 'Check the care profile — one of the entries is not valid.',
  DepositNotYetReceived: 'The deposit has not been recorded in Billing yet.',
  ReservationNotLapsable: "This reservation can't lapse right now.",
  CancellationReasonRequired: 'Enter a reason for cancelling.',
  CheckInNotOnArrivalDate: 'A stay can only be checked in on its arrival day, or the day after.',
  CheckInChecklistIncomplete: 'Some check-in items are missing. They are marked below.',
  CheckOutChecklistIncomplete: 'Some check-out items are missing. They are marked below.',
  InvalidSignature: 'The signature was not accepted. Draw it again, or type the name instead.',
  NoShowTooEarly: 'A no-show can only be recorded once the grace period has passed.',
  UnitNotFreeForExtension: "The unit isn't free for the extra nights. Choose another unit below.",
  RelocationReasonRequired: 'Enter the reason for moving the cat.',
  IsolationUnitRequired: 'A cat suspected of being infectious must be in an isolation unit.',
  NoIsolationUnitAvailable: 'No isolation unit is free, so the cat has not been moved.',
  NotInIsolation: "This cat isn't in an isolation unit.",
  CareTaskNotFound: 'This task could not be found.',
  CareTaskNotPending: 'This task has already been dealt with.',
  ObservationNotAllowed: "An observation can't be recorded for this cat right now.",
  IncidentNotFound: 'This incident could not be found.',
  IncidentStayNotInHouse: 'The cat is not in the care centre, so an incident cannot be recorded.',
  PhoneCallOutcomeRequired: 'Enter what happened on the call.',
  ReferralStayNotInHouse: 'The cat is not in the care centre, so it cannot be referred.',
  VetApprovalNotFound: 'This approval request could not be found.',
  VetApprovalAlreadyDecided: 'This approval has already been answered.',
  InvalidMediaReference: 'The photo or video reference was not accepted.',
  MediaStayNotInHouse: 'Media can only be recorded for a cat that is in the care centre.',
  UnitsStillAvailable: 'Units have just become available — choose one from the results.',
  AlreadyWaitlisted: 'You are already on the waitlist for those dates.',
  DaycareNotOffered: "Daycare isn't offered at this branch.",
  DaycareFull: 'Daycare is full that day. Try another day.',
};

/** Boarding command `propertyName`s (PascalCase on the wire) -> the lower-camel field of the same
 *  meaning. A form passes this once as `fieldMap`; an unmapped name becomes a form-level message. */
export const BOARDING_PROPERTY_NAMES = [
  'ArrivalOn',
  'DepartureOn',
  'UnitType',
  'CatIds',
  'BranchId',
  'CatCount',
  'LocalDate',
  'Basis',
  'ExpectedHours',
  'Reason',
  'Note',
  'Code',
  'Name',
  'CapacityCats',
  'Block',
  'SortOrder',
  'StartsOn',
  'EndsOn',
  'NightlyRate',
  'WeeklyRate',
  'MonthlyRate',
  'AdditionalCatNightlyRate',
  'HourlyRate',
  'DayRate',
  'TaxClassId',
  'EffectiveFrom',
  'NewDepartureOn',
  'TargetUnitId',
  'EffectiveFromLocalDate',
  'Description',
  'ImmediateAction',
  'Resolution',
  'Outcome',
  'Amount',
  'Purpose',
  'Caption',
  'Urgency',
  'Times',
] as const;

export const BOARDING_FIELD_MAP: Readonly<Record<string, string>> = Object.fromEntries(
  BOARDING_PROPERTY_NAMES.map((name) => [name, name.charAt(0).toLowerCase() + name.slice(1)]),
);

/** One sentence for a Boarding failure: the server's own message, else the mapped fallback, else
 *  the generic text; a `403` reads as a permission message (never a retry prompt). */
export function describeBoardingError(error: unknown): string {
  if (error instanceof ApiForbiddenError) return describeForbidden(error);
  const response =
    error instanceof ApiValidationError || error instanceof ApiConflictError
      ? error.response
      : undefined;
  const serverMessage = response?.errors
    ?.map((violation) => violation.errorMessage)
    .filter(Boolean)
    .join(' ');
  if (serverMessage) return serverMessage;
  const code = getViolationCode(error);
  if (code && BOARDING_VIOLATION_MESSAGES[code]) return BOARDING_VIOLATION_MESSAGES[code]!;
  if (error instanceof ApiConflictError) {
    return 'This stay changed since you loaded it. The latest version has been loaded — check it and try again.';
  }
  return 'Something went wrong. Please try again.';
}

/** `Cats[2].WeightKg` -> `{ catIndex: 2, field: 'weightKg' }` — how a check-in / check-out
 *  `…ChecklistIncomplete` violation names the cat and the item it is missing (ADR-FE-108). */
export function parseChecklistProperty(
  propertyName: string,
): { catIndex: number; field: string } | null {
  const match = /^Cats\[(\d+)\]\.?(.*)$/i.exec(propertyName);
  if (!match) return null;
  const rest = match[2] ?? '';
  return {
    catIndex: Number(match[1]),
    field: rest ? rest.charAt(0).toLowerCase() + rest.slice(1) : '',
  };
}

export interface RelocationOption {
  unitId: string;
  code: string;
  name: string;
  unitType: string;
  block: string | null;
}

/** `400 UnitNotFreeForExtension` carries `relocationOptions[]` beside the violations (HO-BRD-001
 *  §12.4); `ApiValidationError` keeps the whole parsed body, so the extra member is read here. */
export function getRelocationOptions(error: unknown): RelocationOption[] {
  if (!(error instanceof ApiValidationError)) return [];
  const body = error.response as unknown as { relocationOptions?: RelocationOption[] };
  return Array.isArray(body.relocationOptions) ? body.relocationOptions : [];
}
