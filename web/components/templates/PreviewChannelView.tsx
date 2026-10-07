import { SandboxedEmailPreview } from '@/components/notifications/SandboxedEmailPreview';
import { InboxItemBody } from '@/components/inbox/InboxItem';
import type { TemplatePreviewChannel } from '@/lib/api/admin-notification-templates';

export const UNICODE_SMS_NOTE =
  'This message uses characters outside the standard SMS alphabet (for example Bangla or emoji), so each SMS holds fewer characters.';

/** "{characters} characters · {segments} SMS · {GSM-7 | Unicode}" (HO-NTF-002 §11.3). */
export function smsSummary(sms: NonNullable<TemplatePreviewChannel['sms']>): string {
  return `${sms.characters} characters · ${sms.segments} SMS · ${sms.encoding === 'Gsm7' ? 'GSM-7' : 'Unicode'}`;
}

/**
 * `components/templates/PreviewChannelView.tsx` — HO-NTF-002 §11.3. One channel of the SERVER's
 * rendered preview (ADR-FE-163 — nothing here substitutes or renders a template):
 *  - **SMS**: the text, then the character / segment / encoding line, and a note for Unicode;
 *  - **Email**: the subject line, the sandboxed HTML (`SandboxedEmailPreview` is the only place the
 *    HTML is shown) and a disclosure with the plain-text version;
 *  - **In-app**: a card mirroring the inbox item;
 *  - **Push**: the text with the "not sent" note.
 * Every text value is a React text child in a `whitespace-pre-wrap` element.
 */
export function PreviewChannelView({ preview }: { preview: TemplatePreviewChannel }) {
  if (preview.channel === 'Sms') {
    return (
      <div className="flex flex-col gap-2">
        <p
          data-testid="sms-preview-text"
          className="whitespace-pre-wrap break-words rounded-[var(--radius-md)] bg-surface-sunken p-3 text-sm text-ink"
        >
          {preview.textBody}
        </p>
        {preview.sms && (
          <>
            <p className="text-xs font-semibold text-ink-muted">{smsSummary(preview.sms)}</p>
            {preview.sms.encoding === 'Ucs2' && (
              <p className="text-xs text-warning-ink">{UNICODE_SMS_NOTE}</p>
            )}
          </>
        )}
      </div>
    );
  }

  if (preview.channel === 'Email') {
    return (
      <div className="flex flex-col gap-3">
        <p className="break-words text-sm text-ink">
          <span className="font-semibold">Subject: </span>
          {preview.subject}
        </p>
        {preview.htmlBody ? (
          <SandboxedEmailPreview htmlBody={preview.htmlBody} />
        ) : (
          <p className="text-sm text-ink-muted">No HTML version was produced.</p>
        )}
        <details className="rounded-[var(--radius-sm)] border border-border p-3">
          <summary className="cursor-pointer text-sm font-semibold text-ink">
            Plain-text version
          </summary>
          <p className="mt-2 whitespace-pre-wrap break-words text-sm text-ink">
            {preview.textBody}
          </p>
        </details>
      </div>
    );
  }

  if (preview.channel === 'InApp') {
    return (
      <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4">
        <p className="whitespace-pre-wrap break-words text-[15px] font-bold text-ink">
          {preview.title}
        </p>
        <InboxItemBody body={preview.textBody} linkPath={null} />
        {preview.linkPath && (
          <p className="break-all text-xs text-ink-muted">
            Link: <span className="text-ink">{preview.linkPath}</span>
          </p>
        )}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <p className="whitespace-pre-wrap break-words text-sm font-semibold text-ink">
        {preview.title}
      </p>
      <p className="whitespace-pre-wrap break-words text-sm text-ink">{preview.textBody}</p>
      <p className="text-xs text-ink-muted">Push messages are not sent in this version.</p>
    </div>
  );
}
