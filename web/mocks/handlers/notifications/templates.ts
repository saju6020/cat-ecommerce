import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  notificationFixtureIds,
  checkNotificationAction,
  validationError,
  notFound,
  noContent,
  loadPersisted,
  savePersisted,
  clearPersisted,
  hoursAgo,
} from './fixtures';

const PERSIST_KEY = '__purrcare_notifications_templates_mock_db__';

/**
 * `mocks/handlers/notifications/templates.ts` — HO-NTF-002 §12, HO-NTF-001 §8.2/§8.3/§12.4. Three
 * shipped templates (one secret, one customised) plus the test marketing template with
 * `unsubscribeLink`.
 *
 * **This file contains a small renderer. It exists only because MSW has to stand in for the
 * server's `NotificationRenderer` and `TemplateRules.Validate`** — it is the mock of the one
 * renderer that sends (ADR-FE-163), never imported by app code. It reproduces the observable
 * behaviour the UI depends on: the same violation codes, messages and `propertyName` shapes
 * (`Contents[i].Body`), values inserted as literal, HTML-encoded text, SMS segment measurement,
 * and an email shell that deliberately contains a `<script>` canary so the sandboxed preview is
 * proved by the browser, not by assertion.
 */

type Channel = 'Sms' | 'Email' | 'InApp' | 'Push';
type Kind = 'Text' | 'Url' | 'Date';

interface Variable {
  name: string;
  description: string;
  kind: Kind;
  sample: string;
  requiredOn: Channel[];
  isStandard: boolean;
}
interface Content {
  channel: Channel;
  subject: string | null;
  title: string | null;
  body: string;
  linkPath: string | null;
}
interface Definition {
  key: string;
  displayName: string;
  description: string;
  category: 'Transactional' | 'Marketing';
  urgency: 'Urgent' | 'Normal';
  channels: Channel[];
  containsSecret: boolean;
  variables: Variable[];
  defaults: Content[];
}
interface Customisation {
  contents: Content[] | null; // null => reset to default (aggregate exists, version kept)
  version: number;
  lastEditedOn: string;
}

const LIMITS: Record<
  Channel,
  { subjectMax: number | null; titleMax: number | null; bodyMax: number }
> = {
  Email: { subjectMax: 150, titleMax: null, bodyMax: 10_000 },
  Sms: { subjectMax: null, titleMax: null, bodyMax: 480 },
  InApp: { subjectMax: null, titleMax: 100, bodyMax: 1000 },
  Push: { subjectMax: null, titleMax: 65, bodyMax: 240 },
};

const STANDARD: Variable[] = [
  {
    name: 'recipientName',
    description: "The recipient's full name",
    kind: 'Text',
    sample: 'Sam Rahman',
    requiredOn: [],
    isStandard: true,
  },
  {
    name: 'clinicName',
    description: 'The clinic branch name',
    kind: 'Text',
    sample: 'PurrCare Clinic',
    requiredOn: [],
    isStandard: true,
  },
  {
    name: 'clinicPhone',
    description: 'The clinic branch phone number',
    kind: 'Text',
    sample: '+880 2 5555 0100',
    requiredOn: [],
    isStandard: true,
  },
];

const v = (
  name: string,
  description: string,
  kind: Kind,
  sample: string,
  requiredOn: Channel[] = [],
): Variable => ({ name, description, kind, sample, requiredOn, isStandard: false });

const content = (
  channel: Channel,
  body: string,
  extra: Partial<Pick<Content, 'subject' | 'title' | 'linkPath'>> = {},
): Content => ({ channel, subject: null, title: null, linkPath: null, body, ...extra });

const ALL: Channel[] = ['Sms', 'Email', 'InApp'];

const DEFINITIONS: Definition[] = [
  {
    key: notificationFixtureIds.passwordResetKey,
    displayName: 'Password reset',
    description: 'Sent when someone asks to reset their password.',
    category: 'Transactional',
    urgency: 'Urgent',
    channels: ['Sms', 'Email'],
    containsSecret: true,
    variables: [
      v(
        'resetLink',
        'The one-time reset link',
        'Url',
        'https://purrcare.example/reset-password#token=sample',
        ['Sms', 'Email'],
      ),
      v('expiresInMinutes', 'Minutes until the link expires', 'Text', '30'),
      ...STANDARD,
    ],
    defaults: [
      content(
        'Sms',
        'PurrCare: reset your password with {{resetLink}} (valid for {{expiresInMinutes}} minutes).',
      ),
      content(
        'Email',
        'Hello {{recipientName}},\n\nUse the link below to choose a new password. It expires in {{expiresInMinutes}} minutes.\n\n[Reset password]({{resetLink}})',
        { subject: 'Reset your PurrCare password' },
      ),
    ],
  },
  {
    key: notificationFixtureIds.vaccinationKey,
    displayName: 'Vaccination due',
    description: 'Reminds an owner that a vaccination is coming up.',
    category: 'Transactional',
    urgency: 'Normal',
    channels: ALL,
    containsSecret: false,
    variables: [
      v('catName', "The cat's name", 'Text', 'Momo'),
      v('vaccineName', 'The vaccine that is due', 'Text', 'Feline Core'),
      v('dueOn', 'The date it is due', 'Date', '2026-10-12'),
      v('bookingLink', 'A direct booking link', 'Url', 'https://purrcare.example/book?catId=1', [
        'Sms',
        'Email',
        'InApp',
      ]),
      ...STANDARD,
    ],
    defaults: [
      content(
        'Sms',
        "PurrCare: {{catName}}'s {{vaccineName}} vaccine is due on {{dueOn}}. Book: {{bookingLink}}",
      ),
      content(
        'Email',
        "Hello {{recipientName}},\n\n{{catName}}'s {{vaccineName}} vaccine is due on {{dueOn}}.\n\n[Book a visit]({{bookingLink}})\n\n{{clinicName}} - {{clinicPhone}}",
        { subject: '{{catName}} is due a vaccination' },
      ),
      content(
        'InApp',
        "{{catName}}'s {{vaccineName}} vaccine is due on {{dueOn}}. Book a visit: {{bookingLink}}",
        {
          title: '{{catName}} is due a vaccination',
          linkPath: '{{bookingLink}}',
        },
      ),
    ],
  },
  {
    key: notificationFixtureIds.dewormingKey,
    displayName: 'Deworming due',
    description: 'Reminds an owner that a deworming treatment is coming up.',
    category: 'Transactional',
    urgency: 'Normal',
    channels: ALL,
    containsSecret: false,
    variables: [
      v('catName', "The cat's name", 'Text', 'Momo'),
      v('treatmentName', 'The treatment that is due', 'Text', 'Broad-spectrum dewormer'),
      v('dueOn', 'The date it is due', 'Date', '2026-10-12'),
      v('bookingLink', 'A direct booking link', 'Url', 'https://purrcare.example/book?catId=1', [
        'Sms',
        'Email',
        'InApp',
      ]),
      ...STANDARD,
    ],
    defaults: [
      content(
        'Sms',
        'PurrCare: {{catName}} is due {{treatmentName}} on {{dueOn}}. Book: {{bookingLink}}',
      ),
      content(
        'Email',
        'Hello {{recipientName}},\n\n{{catName}} is due {{treatmentName}} on {{dueOn}}.\n\n[Book a visit]({{bookingLink}})',
        { subject: '{{catName}} is due a deworming treatment' },
      ),
      content(
        'InApp',
        '{{catName}} is due {{treatmentName}} on {{dueOn}}. Book a visit: {{bookingLink}}',
        {
          title: '{{catName}} is due a deworming treatment',
          linkPath: '{{bookingLink}}',
        },
      ),
    ],
  },
  {
    key: notificationFixtureIds.marketingKey,
    displayName: 'Test newsletter',
    description:
      'A marketing template used to prove unsubscribe links. Not sent by any feature yet.',
    category: 'Marketing',
    urgency: 'Normal',
    channels: ALL,
    containsSecret: false,
    variables: [
      v(
        'unsubscribeLink',
        'Stops marketing messages on this channel',
        'Url',
        'https://purrcare.example/unsubscribe#token=sample',
        ['Sms', 'Email', 'InApp'],
      ),
      ...STANDARD,
    ],
    defaults: [
      content('Sms', '{{clinicName}}: spring offers are here. Stop: {{unsubscribeLink}}'),
      content(
        'Email',
        'Hello {{recipientName}},\n\nSpring offers are here.\n\n[Unsubscribe]({{unsubscribeLink}})',
        { subject: 'News from {{clinicName}}' },
      ),
      content('InApp', 'Spring offers are here. Stop these messages: {{unsubscribeLink}}', {
        title: 'News from {{clinicName}}',
      }),
    ],
  },
];

function seedCustomisations(): Record<string, Customisation> {
  const vaccination = DEFINITIONS.find((d) => d.key === notificationFixtureIds.vaccinationKey)!;
  return {
    [notificationFixtureIds.vaccinationKey]: {
      version: 2,
      lastEditedOn: hoursAgo(72),
      contents: vaccination.defaults.map((c) =>
        c.channel === 'Sms'
          ? {
              ...c,
              body: "PurrCare: it's time for {{catName}}'s {{vaccineName}} vaccine ({{dueOn}}). Book: {{bookingLink}}",
            }
          : c,
      ),
    },
  };
}

let customisations: Record<string, Customisation> =
  loadPersisted<Record<string, Customisation>>(PERSIST_KEY) ?? seedCustomisations();

function persist() {
  savePersisted(PERSIST_KEY, customisations);
}

export function resetNotificationsTemplatesMockData(): void {
  customisations = seedCustomisations();
  clearPersisted(PERSIST_KEY);
}

// ---- views --------------------------------------------------------------------------------

function isCustomised(key: string): boolean {
  return !!customisations[key]?.contents;
}

function effectiveContents(def: Definition): Content[] {
  return customisations[def.key]?.contents ?? def.defaults;
}

function summaryOf(def: Definition) {
  const c = customisations[def.key];
  return {
    key: def.key,
    displayName: def.displayName,
    description: def.description,
    category: def.category,
    urgency: def.urgency,
    channels: def.channels,
    containsSecret: def.containsSecret,
    isCustomised: isCustomised(def.key),
    contentVersion: c?.version ?? 0,
    lastEditedOn: c?.contents ? c.lastEditedOn : null,
  };
}

function detailOf(def: Definition) {
  return {
    ...summaryOf(def),
    variables: def.variables,
    contents: effectiveContents(def),
    defaults: def.defaults,
    limits: def.channels.map((channel) => ({ channel, ...LIMITS[channel] })),
  };
}

// ---- validation (the mock of TemplateRules.Validate) ----------------------------------------

const PLACEHOLDER = /\{\{([a-z][A-Za-z0-9]*)\}\}/g;

interface Violation {
  propertyName: string;
  errorCode: string;
  errorMessage: string;
}

function fieldName(field: 'subject' | 'title' | 'body' | 'linkPath') {
  return { subject: 'Subject', title: 'Title', body: 'Body', linkPath: 'LinkPath' }[field];
}

function validateContents(def: Definition, contents: Array<Content | null>): Violation | null {
  if (contents.some((c) => c === null || typeof c !== 'object')) {
    return {
      propertyName: 'Contents',
      errorCode: 'TemplateChannelMismatch',
      errorMessage: 'Each channel needs content.',
    };
  }
  const given = (contents as Content[]).map((c) => c.channel);
  const mismatch =
    given.length !== def.channels.length ||
    def.channels.some((c) => given.filter((g) => g === c).length !== 1);
  if (mismatch) {
    return {
      propertyName: 'Contents',
      errorCode: 'TemplateChannelMismatch',
      errorMessage: `This template has wording for exactly: ${def.channels.join(', ')}.`,
    };
  }
  const known = new Set(def.variables.map((x) => x.name));
  for (let i = 0; i < contents.length; i += 1) {
    const c = contents[i] as Content;
    const limits = LIMITS[c.channel];
    const fields = ['subject', 'title', 'body', 'linkPath'] as const;
    for (const field of fields) {
      const text = c[field] ?? '';
      const prop = `Contents[${i}].${fieldName(field)}`;
      const withoutValid = text.replace(PLACEHOLDER, '');
      if (withoutValid.includes('{{') || withoutValid.includes('}}')) {
        const fragment = /\{\{[^}]*\}?\}?|\}\}/.exec(text.replace(PLACEHOLDER, ''))?.[0] ?? '{{';
        return {
          propertyName: prop,
          errorCode: 'MalformedTemplatePlaceholder',
          errorMessage: `"${fragment}" is not a valid placeholder. Write placeholders like {{catName}}.`,
        };
      }
      for (const match of text.matchAll(PLACEHOLDER)) {
        if (!known.has(match[1]!)) {
          return {
            propertyName: prop,
            errorCode: 'UnknownTemplatePlaceholder',
            errorMessage: `{{${match[1]}}} is not a variable of this template. Available: ${def.variables.map((x) => x.name).join(', ')}.`,
          };
        }
      }
    }
    for (const variable of def.variables) {
      if (variable.requiredOn.includes(c.channel) && !c.body.includes(`{{${variable.name}}}`)) {
        return {
          propertyName: `Contents[${i}].Body`,
          errorCode: 'RequiredTemplatePlaceholderMissing',
          errorMessage: `{{${variable.name}}} is required in the ${c.channel === 'InApp' ? 'in-app' : c.channel.toLowerCase()} message.`,
        };
      }
    }
    if (c.channel === 'Email' && (c.subject ?? '').length < 1) {
      return {
        propertyName: `Contents[${i}].Subject`,
        errorCode: 'InvalidTemplateLength',
        errorMessage: 'The email subject cannot be empty.',
      };
    }
    if (c.body.length < 1 || c.body.length > limits.bodyMax) {
      return {
        propertyName: `Contents[${i}].Body`,
        errorCode: 'InvalidTemplateLength',
        errorMessage: `The message must be between 1 and ${limits.bodyMax} characters.`,
      };
    }
    if (limits.subjectMax && (c.subject ?? '').length > limits.subjectMax) {
      return {
        propertyName: `Contents[${i}].Subject`,
        errorCode: 'InvalidTemplateLength',
        errorMessage: `The subject can be at most ${limits.subjectMax} characters.`,
      };
    }
    if (limits.titleMax && c.channel !== 'Email' && (c.title ?? '').length > limits.titleMax) {
      return {
        propertyName: `Contents[${i}].Title`,
        errorCode: 'InvalidTemplateLength',
        errorMessage: `The title can be at most ${limits.titleMax} characters.`,
      };
    }
    if (c.channel !== 'Email' && /[<>]/.test(c.body)) {
      return {
        propertyName: `Contents[${i}].Body`,
        errorCode: 'InvalidTemplateText',
        errorMessage: 'Text and in-app messages are plain text: < and > are not allowed.',
      };
    }
  }
  return null;
}

// ---- rendering (the mock of NotificationRenderer) -------------------------------------------

const GSM7 =
  /^[A-Za-z0-9 \r\n@£$¥èéùìòÇØøÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ!"#¤%&'()*+,\-./:;<=>?¡ÄÖÑÜ§¿äöñüà^{}\\[~\]|€]*$/;

function measureSms(text: string) {
  const gsm = GSM7.test(text);
  const characters = text.length;
  const single = gsm ? 160 : 70;
  const multi = gsm ? 153 : 67;
  return {
    encoding: gsm ? 'Gsm7' : 'Ucs2',
    characters,
    segments: characters <= single ? 1 : Math.ceil(characters / multi),
  };
}

function formatDate(value: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!m) return value;
  const months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ];
  return `${Number(m[3])} ${months[Number(m[2]) - 1]} ${m[1]}`;
}

function encodeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function render(
  def: Definition,
  c: Content,
  samples: Record<string, string>,
): {
  subject: string | null;
  title: string | null;
  textBody: string;
  htmlBody: string | null;
  linkPath: string | null;
} {
  const values: Record<string, string> = {};
  for (const variable of def.variables) {
    const raw = samples[variable.name] ?? variable.sample;
    values[variable.name] = variable.kind === 'Date' ? formatDate(raw) : raw;
  }
  // Parse first, substitute second: values are inserted as literal text and never re-scanned.
  const sub = (template: string) =>
    template.replace(PLACEHOLDER, (_all, name: string) => values[name] ?? '');

  const subject = c.subject === null ? null : sub(c.subject);
  const title = c.title === null ? null : sub(c.title);
  const linkPath = c.linkPath ? sub(c.linkPath) : null;

  const LINK = /\[([^\]]*)\]\(([^)]*)\)/g;
  const textBody = sub(
    c.body.replace(LINK, (_all, label: string, url: string) => `${label} (${url})`),
  );

  let htmlBody: string | null = null;
  if (c.channel === 'Email') {
    const parts: string[] = [];
    let last = 0;
    for (const match of c.body.matchAll(LINK)) {
      parts.push(encodeHtml(sub(c.body.slice(last, match.index))));
      parts.push(`<a href="${encodeHtml(sub(match[2]!))}">${encodeHtml(sub(match[1]!))}</a>`);
      last = (match.index ?? 0) + match[0].length;
    }
    parts.push(encodeHtml(sub(c.body.slice(last))));
    htmlBody =
      `<!doctype html><html><body style="font-family:Arial,sans-serif;color:#1c1a17">` +
      `<h2 style="color:#0e4a45">${encodeHtml(values.clinicName ?? '')}</h2>` +
      `<div style="white-space:pre-wrap">${parts.join('')}</div>` +
      `<p style="color:#5c574f;font-size:12px">${encodeHtml(values.clinicPhone ?? '')}</p>` +
      // Canary: if this ever ran outside a sandbox it would set the flag the tests look for.
      `<script>window.parent.__previewScriptRan = true;</script>` +
      `</body></html>`;
  }
  return { subject, title, textBody, htmlBody, linkPath };
}

// ---- handlers ------------------------------------------------------------------------------

function findDef(key: string | readonly string[] | undefined): Definition | undefined {
  return DEFINITIONS.find((d) => d.key === key);
}

function conflict() {
  return HttpResponse.json(
    {
      errors: [
        {
          propertyName: 'ExpectedContentVersion',
          errorCode: 'TemplateVersionConflict',
          errorMessage: 'Someone else changed this template.',
          resourceName: 'NotificationTemplate',
        },
      ],
      isValid: false,
    },
    { status: 409 },
  );
}

export const notificationsTemplatesHandlers: HttpHandler[] = [
  http.get('*/api/admin/notification-templates', ({ request }) => {
    const denied = checkNotificationAction(request, 'NotificationTemplates.View');
    if (denied) return denied;
    return HttpResponse.json(DEFINITIONS.map(summaryOf));
  }),

  http.get('*/api/admin/notification-templates/:key', ({ request, params }) => {
    const denied = checkNotificationAction(request, 'NotificationTemplates.View');
    if (denied) return denied;
    const def = findDef(params.key);
    return def ? HttpResponse.json(detailOf(def)) : notFound();
  }),

  http.put('*/api/admin/notification-templates/:key', async ({ request, params }) => {
    const denied = checkNotificationAction(request, 'NotificationTemplates.Manage');
    if (denied) return denied;
    const def = findDef(params.key);
    if (!def) return notFound();
    const body = (await request.json()) as {
      expectedContentVersion: number;
      contents: Array<Content | null>;
    };
    if ((customisations[def.key]?.version ?? 0) !== body.expectedContentVersion) return conflict();
    const violation = validateContents(def, body.contents ?? []);
    if (violation) {
      return validationError(
        violation.propertyName,
        violation.errorCode,
        violation.errorMessage,
        'NotificationTemplate',
      );
    }
    customisations[def.key] = {
      contents: (body.contents as Content[]).map((c) => ({
        channel: c.channel,
        subject: c.subject ?? null,
        title: c.title ?? null,
        body: c.body,
        linkPath: c.linkPath ?? null,
      })),
      version: body.expectedContentVersion + 1,
      lastEditedOn: new Date().toISOString(),
    };
    persist();
    return noContent();
  }),

  http.post('*/api/admin/notification-templates/:key/reset', async ({ request, params }) => {
    const denied = checkNotificationAction(request, 'NotificationTemplates.Manage');
    if (denied) return denied;
    const def = findDef(params.key);
    if (!def) return notFound();
    const body = (await request.json()) as { expectedContentVersion: number };
    const current = customisations[def.key]?.version ?? 0;
    if (current !== body.expectedContentVersion) return conflict();
    customisations[def.key] = {
      contents: null,
      version: current + 1,
      lastEditedOn: new Date().toISOString(),
    };
    persist();
    return noContent();
  }),

  http.post('*/api/admin/notification-templates/:key/preview', async ({ request, params }) => {
    const denied = checkNotificationAction(request, 'NotificationTemplates.View');
    if (denied) return denied;
    const def = findDef(params.key);
    if (!def) return notFound();
    const body = (await request.json()) as {
      contents: Array<Content | null>;
      sampleValues?: Record<string, string>;
    };
    const violation = validateContents(def, body.contents ?? []);
    if (violation) {
      return validationError(
        violation.propertyName,
        violation.errorCode,
        violation.errorMessage,
        'NotificationTemplate',
      );
    }
    const channels = (body.contents as Content[]).map((c) => {
      const out = render(def, c, body.sampleValues ?? {});
      return {
        channel: c.channel,
        subject: out.subject,
        title: out.title,
        textBody: out.textBody,
        htmlBody: out.htmlBody,
        linkPath: out.linkPath,
        sms: c.channel === 'Sms' ? measureSms(out.textBody) : null,
      };
    });
    return HttpResponse.json({ channels });
  }),
];
