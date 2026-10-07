import { apiFetch } from './client';

/**
 * `lib/api/admin-notification-templates.ts` — HO-NTF-002 §8.2/§11.3, US-NTF-02. Shapes verified
 * against `AdminNotificationTemplatesController.cs`, `SaveNotificationTemplateRequest` and
 * `NotificationTemplateViewModels.cs`. The client never substitutes placeholders: `previewNotificationTemplate`
 * is the only way to see rendered output (ADR-FE-023 -> ADR-FE-163 in this repo's numbering).
 */
export type TemplateChannel = 'Sms' | 'Email' | 'InApp' | 'Push';
export type TemplateCategory = 'Transactional' | 'Marketing';
export type TemplateUrgency = 'Urgent' | 'Normal';
export type TemplateVariableKind = 'Text' | 'Url' | 'Date';

export interface NotificationTemplateSummary {
  key: string;
  displayName: string;
  description: string;
  category: TemplateCategory;
  urgency: TemplateUrgency;
  channels: TemplateChannel[];
  containsSecret: boolean;
  isCustomised: boolean;
  /** 0 while the shipped defaults are in effect. Sent back as `expectedContentVersion`. */
  contentVersion: number;
  lastEditedOn: string | null;
}

export interface NotificationTemplateVariable {
  name: string;
  description: string;
  kind: TemplateVariableKind;
  sample: string;
  requiredOn: TemplateChannel[];
  isStandard: boolean;
}

export interface NotificationTemplateContent {
  channel: TemplateChannel;
  subject: string | null;
  title: string | null;
  body: string;
  linkPath: string | null;
}

export interface NotificationTemplateChannelLimits {
  channel: TemplateChannel;
  subjectMax: number | null;
  titleMax: number | null;
  bodyMax: number;
}

export interface NotificationTemplateDetail extends NotificationTemplateSummary {
  variables: NotificationTemplateVariable[];
  contents: NotificationTemplateContent[];
  defaults: NotificationTemplateContent[];
  limits: NotificationTemplateChannelLimits[];
}

export async function getNotificationTemplates(): Promise<NotificationTemplateSummary[]> {
  return apiFetch<NotificationTemplateSummary[]>('/api/admin/notification-templates');
}

export async function getNotificationTemplate(key: string): Promise<NotificationTemplateDetail> {
  return apiFetch<NotificationTemplateDetail>(
    `/api/admin/notification-templates/${encodeURIComponent(key)}`,
  );
}

export interface SaveNotificationTemplateRequest {
  expectedContentVersion: number;
  contents: NotificationTemplateContent[];
}

export async function saveNotificationTemplate(
  key: string,
  request: SaveNotificationTemplateRequest,
): Promise<void> {
  await apiFetch<void>(`/api/admin/notification-templates/${encodeURIComponent(key)}`, {
    method: 'PUT',
    body: request,
  });
}

export async function resetNotificationTemplate(
  key: string,
  expectedContentVersion: number,
): Promise<void> {
  await apiFetch<void>(`/api/admin/notification-templates/${encodeURIComponent(key)}/reset`, {
    method: 'POST',
    body: { expectedContentVersion },
  });
}

export interface PreviewTemplateRequest {
  contents: NotificationTemplateContent[];
  /** Variable name -> value, for this preview only. Never saved. */
  sampleValues?: Record<string, string>;
}

export interface SmsMeasure {
  encoding: 'Gsm7' | 'Ucs2';
  characters: number;
  segments: number;
}

export interface TemplatePreviewChannel {
  channel: TemplateChannel;
  subject: string | null;
  title: string | null;
  textBody: string;
  /** Server-rendered email HTML. Shown only inside `SandboxedEmailPreview` (ADR-FE-020 -> 160). */
  htmlBody: string | null;
  linkPath: string | null;
  sms: SmsMeasure | null;
}

export interface TemplatePreview {
  channels: TemplatePreviewChannel[];
}

export async function previewNotificationTemplate(
  key: string,
  request: PreviewTemplateRequest,
  signal?: AbortSignal,
): Promise<TemplatePreview> {
  return apiFetch<TemplatePreview>(
    `/api/admin/notification-templates/${encodeURIComponent(key)}/preview`,
    { method: 'POST', body: request, signal },
  );
}

/** Staff roles `NotificationTemplates.View` / `.Manage` are granted to (`ActionRoles.Notifications.cs`)
 *  — presentation only (nav and link visibility); the server still decides every request. */
export const TEMPLATE_VIEW_ROLES = ['Administrator', 'ShopManager'] as const;
export const TEMPLATE_MANAGE_ROLES = ['Administrator'] as const;
