import { apiFetch } from './client';
import type { PagedResult } from './types';

/**
 * `lib/api/clinical-catalogue.ts` — HO-EMR-002 §8.2/§11.8. Diagnosis codes, lab analytes and note
 * templates (`StaffClinicalCatalogueController`). Reads need `ClinicalCatalogue.View`
 * (nurse, vet, administrator); writes `ClinicalCatalogue.Manage` (vet, administrator).
 */

export interface DiagnosisCode {
  id: string;
  code: string;
  name: string;
  category: string | null;
  externalSystem: string | null;
  externalCode: string | null;
  isActive: boolean;
  isSystem: boolean;
  version: number;
}

export interface LabAnalyte {
  id: string;
  code: string;
  name: string;
  valueKind: 'Numeric' | 'Qualitative';
  diagnosticKind: string;
  unit: string | null;
  referenceLow: string | null;
  referenceHigh: string | null;
  normalText: string | null;
  isActive: boolean;
  version: number;
}

export interface NoteTemplate {
  id: string;
  serviceId: string;
  serviceName: string;
  name: string;
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
  templateVersion: number;
  isActive: boolean;
  version: number;
}

export interface CatalogueQuery {
  search?: string;
  activeOnly?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

function qs(params: Record<string, string | number | boolean | undefined>): string {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== '');
  if (entries.length === 0) return '';
  return `?${entries.map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`).join('&')}`;
}

const base = '/api/staff';

// ---- diagnosis codes ---------------------------------------------------------------------

export function getDiagnosisCodes(query: CatalogueQuery = {}): Promise<PagedResult<DiagnosisCode>> {
  return apiFetch(`${base}/diagnosis-codes${qs({ ...query })}`);
}

export function getDiagnosisCode(id: string): Promise<DiagnosisCode> {
  return apiFetch(`${base}/diagnosis-codes/${id}`);
}

export interface DiagnosisCodeInput {
  name: string;
  category?: string;
  externalSystem?: string;
  externalCode?: string;
}

export function createDiagnosisCode(
  request: DiagnosisCodeInput & { code: string },
): Promise<{ diagnosisCodeId: string }> {
  return apiFetch(`${base}/diagnosis-codes`, { method: 'POST', body: request });
}

export function updateDiagnosisCode(id: string, request: DiagnosisCodeInput): Promise<void> {
  return apiFetch(`${base}/diagnosis-codes/${id}`, { method: 'PUT', body: request });
}

export function setDiagnosisCodeActive(id: string, isActive: boolean): Promise<void> {
  return apiFetch(`${base}/diagnosis-codes/${id}/active`, { method: 'PUT', body: { isActive } });
}

// ---- lab analytes ------------------------------------------------------------------------

export function getLabAnalytes(query: CatalogueQuery = {}): Promise<PagedResult<LabAnalyte>> {
  return apiFetch(`${base}/lab-analytes${qs({ ...query })}`);
}

export function getLabAnalyte(id: string): Promise<LabAnalyte> {
  return apiFetch(`${base}/lab-analytes/${id}`);
}

export interface LabAnalyteInput {
  name: string;
  valueKind: 'Numeric' | 'Qualitative';
  diagnosticKind: string;
  unit?: string;
  referenceLow?: number;
  referenceHigh?: number;
  normalText?: string;
}

export function createLabAnalyte(
  request: LabAnalyteInput & { code: string },
): Promise<{ labAnalyteId: string }> {
  return apiFetch(`${base}/lab-analytes`, { method: 'POST', body: request });
}

export function updateLabAnalyte(id: string, request: LabAnalyteInput): Promise<void> {
  return apiFetch(`${base}/lab-analytes/${id}`, { method: 'PUT', body: request });
}

export function setLabAnalyteActive(id: string, isActive: boolean): Promise<void> {
  return apiFetch(`${base}/lab-analytes/${id}/active`, { method: 'PUT', body: { isActive } });
}

// ---- note templates ----------------------------------------------------------------------

export function getNoteTemplates(
  query: { serviceId?: string; activeOnly?: boolean; pageNumber?: number; pageSize?: number } = {},
): Promise<PagedResult<NoteTemplate>> {
  return apiFetch(`${base}/note-templates${qs({ ...query })}`);
}

export function getNoteTemplate(id: string): Promise<NoteTemplate> {
  return apiFetch(`${base}/note-templates/${id}`);
}

export interface NoteTemplateInput {
  name: string;
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
}

export function createNoteTemplate(
  request: NoteTemplateInput & { serviceId: string },
): Promise<{ noteTemplateId: string }> {
  return apiFetch(`${base}/note-templates`, { method: 'POST', body: request });
}

export function updateNoteTemplate(id: string, request: NoteTemplateInput): Promise<void> {
  return apiFetch(`${base}/note-templates/${id}`, { method: 'PUT', body: request });
}

export function setNoteTemplateActive(id: string, isActive: boolean): Promise<void> {
  return apiFetch(`${base}/note-templates/${id}/active`, { method: 'PUT', body: { isActive } });
}
