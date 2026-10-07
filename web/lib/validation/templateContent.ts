import { z } from 'zod';
import type {
  NotificationTemplateChannelLimits,
  NotificationTemplateContent,
  NotificationTemplateVariable,
  TemplateChannel,
} from '@/lib/api/admin-notification-templates';

/**
 * `lib/validation/templateContent.ts` — HO-NTF-002 §11.3, NFR-USA-5. Form values, request mapping and
 * the length rules of the template editor. **Placeholder rules are deliberately NOT here**: whether
 * `{{catname}}` exists, whether a required variable is present, whether markup is allowed — all of
 * that is `TemplateRules.Validate` on the server, shown via the preview and save `400`s
 * (ADR-FE-163). The client only knows lengths (so the counters and a fast "too long" message work)
 * and which fields each channel has.
 */
export type TemplateField = 'subject' | 'title' | 'body' | 'linkPath';

export interface TemplateContentFormValue {
  channel: TemplateChannel;
  subject: string;
  title: string;
  body: string;
  linkPath: string;
}

export interface TemplateFormValues {
  contents: TemplateContentFormValue[];
}

/** The fields each channel has (HO-NTF-002 §11.3): email Subject+Body; SMS Body; in-app
 *  Title+Body+Link; push Title+Body. */
export function fieldsFor(channel: TemplateChannel): TemplateField[] {
  switch (channel) {
    case 'Email':
      return ['subject', 'body'];
    case 'Sms':
      return ['body'];
    case 'InApp':
      return ['title', 'body', 'linkPath'];
    case 'Push':
      return ['title', 'body'];
  }
}

export const CHANNEL_LABELS: Record<TemplateChannel, string> = {
  Email: 'Email',
  Sms: 'Text message (SMS)',
  InApp: 'In-app',
  Push: 'Push',
};

const CHANNEL_ORDER: TemplateChannel[] = ['Email', 'Sms', 'InApp', 'Push'];

export function sortChannels(channels: readonly TemplateChannel[]): TemplateChannel[] {
  return [...channels].sort((a, b) => CHANNEL_ORDER.indexOf(a) - CHANNEL_ORDER.indexOf(b));
}

/** The editor's form values, one entry per channel of the definition, in tab order. */
export function toFormValues(
  contents: NotificationTemplateContent[],
  channels: readonly TemplateChannel[],
): TemplateFormValues {
  return {
    contents: sortChannels(channels).map((channel) => {
      const found = contents.find((c) => c.channel === channel);
      return {
        channel,
        subject: found?.subject ?? '',
        title: found?.title ?? '',
        body: found?.body ?? '',
        linkPath: found?.linkPath ?? '',
      };
    }),
  };
}

/** The request body for save and preview: only a channel's own fields are sent, the rest `null`. */
export function toRequestContents(values: TemplateFormValues): NotificationTemplateContent[] {
  return values.contents.map((c) => {
    const fields = fieldsFor(c.channel);
    return {
      channel: c.channel,
      subject: fields.includes('subject') ? c.subject : null,
      title: fields.includes('title') ? c.title : null,
      body: c.body,
      linkPath: fields.includes('linkPath') && c.linkPath !== '' ? c.linkPath : null,
    };
  });
}

/** Length rules from the definition's `limits` (HO-NTF-001 §8.2 rule 5). */
export function buildTemplateSchema(limits: NotificationTemplateChannelLimits[]) {
  const limitFor = (channel: string) => limits.find((l) => l.channel === channel);
  return z.object({
    contents: z.array(
      z
        .object({
          channel: z.enum(['Email', 'Sms', 'InApp', 'Push']),
          subject: z.string(),
          title: z.string(),
          body: z.string(),
          linkPath: z.string(),
        })
        .superRefine((c, ctx) => {
          const limit = limitFor(c.channel);
          const fields = fieldsFor(c.channel);
          if (fields.includes('subject')) {
            if (c.subject.trim() === '') {
              ctx.addIssue({ code: 'custom', path: ['subject'], message: 'Enter a subject.' });
            } else if (limit?.subjectMax && c.subject.length > limit.subjectMax) {
              ctx.addIssue({
                code: 'custom',
                path: ['subject'],
                message: `The subject can be at most ${limit.subjectMax} characters.`,
              });
            }
          }
          if (fields.includes('title')) {
            if (c.title.trim() === '') {
              ctx.addIssue({ code: 'custom', path: ['title'], message: 'Enter a title.' });
            } else if (limit?.titleMax && c.title.length > limit.titleMax) {
              ctx.addIssue({
                code: 'custom',
                path: ['title'],
                message: `The title can be at most ${limit.titleMax} characters.`,
              });
            }
          }
          if (c.body.trim() === '') {
            ctx.addIssue({ code: 'custom', path: ['body'], message: 'Enter the message text.' });
          } else if (limit && c.body.length > limit.bodyMax) {
            ctx.addIssue({
              code: 'custom',
              path: ['body'],
              message: `The message can be at most ${limit.bodyMax} characters.`,
            });
          }
        }),
    ),
  });
}

/** `{{name}}` — what a click on the palette inserts (the client never substitutes, ADR-FE-163). */
export function placeholderToken(variable: Pick<NotificationTemplateVariable, 'name'>): string {
  return `{{${variable.name}}}`;
}

/**
 * What the palette inserts for `variable` into `field` of `channel`. A `Url` variable in the email
 * BODY is offered as a link, `[Link text]({{name}})` (HO-NTF-001 §8.2 rule 6: a link target may be
 * one `Url` placeholder); everywhere else it is the bare token. `selectStart`/`selectEnd` mark the
 * "Link text" placeholder so the person can type over it.
 */
export function snippetFor(
  variable: Pick<NotificationTemplateVariable, 'name' | 'kind'>,
  channel: TemplateChannel,
  field: TemplateField,
): { text: string; selectStart: number; selectEnd: number } {
  if (variable.kind === 'Url' && channel === 'Email' && field === 'body') {
    const text = `[Link text](${placeholderToken(variable)})`;
    return { text, selectStart: 1, selectEnd: 10 };
  }
  const text = placeholderToken(variable);
  return { text, selectStart: text.length, selectEnd: text.length };
}

/** Replaces `[start, end)` of `value` with `text`. Pure; the editor restores the caret. */
export function spliceText(value: string, start: number, end: number, text: string): string {
  const from = Math.max(0, Math.min(start, value.length));
  const to = Math.max(from, Math.min(end, value.length));
  return value.slice(0, from) + text + value.slice(to);
}
