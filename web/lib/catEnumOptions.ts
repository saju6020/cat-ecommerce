import type {
  CatSex,
  DateOfBirthPrecision,
  NeuterStatus,
  AllergySeverity,
  ParasiteTreatmentType,
} from './api/cats';

/**
 * The enum-shaped option lists forms render as `<select>`/radio choices (`CAT_SEX_OPTIONS` etc.),
 * split out of `lib/api/cats.ts` (ARCHITECTURE gate second pass) so `lib/api/**` holds only the
 * HTTP layer and its types, and `components/**` takes static option data from a module that does
 * no I/O (§8.2 rule 4). No lint rule enforces this; it is a convention. The *types* these arrays
 * are built from still live in `lib/api/cats.ts`.
 */
export const CAT_SEX_OPTIONS: CatSex[] = ['Male', 'Female', 'Unknown'];
export const DATE_OF_BIRTH_PRECISION_OPTIONS: DateOfBirthPrecision[] = ['Exact', 'Estimated'];
export const NEUTER_STATUS_OPTIONS: NeuterStatus[] = ['Intact', 'Neutered', 'Unknown'];
export const ALLERGY_SEVERITY_OPTIONS: AllergySeverity[] = ['Mild', 'Moderate', 'Severe'];
export const PARASITE_TREATMENT_TYPE_OPTIONS: ParasiteTreatmentType[] = [
  'Deworming',
  'FleaAndTick',
  'Combined',
];
