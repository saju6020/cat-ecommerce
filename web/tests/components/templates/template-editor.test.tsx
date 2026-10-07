import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import {
  resetNotificationsMockData,
  notificationFixtureIds as ids,
} from '@/mocks/handlers/notifications';
import { renderEditor } from './editor-harness';

// UnsavedChangesGuard (rendered by the editor) calls useRouter().
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

const PREVIEW_URL = '*/api/admin/notification-templates/:key/preview';

function validation400(propertyName: string, errorCode: string, errorMessage: string) {
  return HttpResponse.json(
    {
      errors: [{ propertyName, errorCode, errorMessage, resourceName: 'NotificationTemplate' }],
      isValid: false,
    },
    { status: 400 },
  );
}

async function openTab(user: ReturnType<typeof userEvent.setup>, name: RegExp | string) {
  await user.click(await screen.findByRole('tab', { name }));
}

function setValue(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

/** Resolves once the email preview (the default tab) has rendered at least once. */
async function waitForFirstPreview() {
  await waitFor(() => expect(screen.getByTitle('Email preview')).toBeInTheDocument(), {
    timeout: 3000,
  });
}

beforeEach(() => resetNotificationsMockData());

describe('TemplateEditor — HO-NTF-002 §11.3', () => {
  it('shows the definition: channel tabs with the ARIA tabs pattern, counters, required badges', async () => {
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);

    const tabs = await screen.findAllByRole('tab');
    expect(tabs.map((t) => t.textContent)).toEqual(['Email', 'Text message (SMS)', 'In-app']);
    expect(screen.getByRole('tablist', { name: 'Message channels' })).toBeInTheDocument();
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true');
    expect(tabs[0]).toHaveAttribute('aria-controls');
    expect(screen.getByRole('tabpanel', { name: 'Email' })).toBeVisible();
    expect(screen.getByLabelText('Email subject')).toBeVisible();
    expect(screen.getByText(/\/ 150 characters/)).toBeInTheDocument();
    expect(screen.getByText('Customised')).toBeInTheDocument();
    expect(screen.getAllByText(/Required on Text message \(SMS\), Email, In-app/).length).toBe(1);

    // Arrow keys move between tabs.
    tabs[0]!.focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'Text message (SMS)' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    expect(screen.getByLabelText('SMS message')).toBeVisible();
    expect(screen.getByLabelText('Email subject')).not.toBeVisible();
  });

  it('U-C1: clicking a placeholder inserts {{catName}} at the caret of the focused SMS body, caret after it', async () => {
    const user = userEvent.setup();
    renderEditor(ids.dewormingKey);
    await openTab(user, 'Text message (SMS)');
    const body = screen.getByLabelText('SMS message') as HTMLTextAreaElement;
    fireEvent.change(body, { target: { value: 'Hello  there' } });
    await user.click(body);
    body.setSelectionRange(6, 6);

    await user.click(screen.getByRole('button', { name: 'Insert {{catName}}' }));

    expect(body.value).toBe('Hello {{catName}} there');
    expect(body.selectionStart).toBe(6 + '{{catName}}'.length);
    expect(body.selectionEnd).toBe(body.selectionStart);
    expect(body).toHaveFocus();
  });

  it('inserts into the last focused field of the active tab, and offers a Url variable as a link in the email body', async () => {
    const user = userEvent.setup();
    renderEditor(ids.dewormingKey);
    await screen.findByLabelText('Email body');
    const body = screen.getByLabelText('Email body') as HTMLTextAreaElement;
    await user.click(body);
    body.setSelectionRange(0, 0);
    await user.click(screen.getByRole('button', { name: 'Insert {{bookingLink}} as a link' }));
    expect(body.value.startsWith('[Link text]({{bookingLink}})')).toBe(true);

    const subject = screen.getByLabelText('Email subject') as HTMLInputElement;
    await user.click(subject);
    subject.setSelectionRange(subject.value.length, subject.value.length);
    await user.click(screen.getByRole('button', { name: 'Insert {{clinicName}}' }));
    expect(subject.value.endsWith('{{clinicName}}')).toBe(true);
  });

  it('U-C2: five keystrokes in 300 ms produce one preview request after 600 ms, and an earlier in-flight one is aborted', async () => {
    const posts: number[] = [];
    server.use(
      http.post(PREVIEW_URL, async () => {
        posts.push(Date.now());
        await delay(1500); // keep a request in flight
        return HttpResponse.json({ channels: [] });
      }),
    );
    const signals: AbortSignal[] = [];
    const realFetch = globalThis.fetch;
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      if (
        String(input instanceof Request ? input.url : input).includes('/preview') &&
        init?.signal
      ) {
        signals.push(init.signal);
      }
      return realFetch(input, init);
    });

    const user = userEvent.setup();
    renderEditor(ids.dewormingKey);
    await openTab(user, 'Text message (SMS)');
    await waitFor(() => expect(posts.length).toBe(1)); // the initial preview

    const body = screen.getByLabelText('SMS message') as HTMLTextAreaElement;
    const start = body.value;
    for (const ch of ['a', 'b', 'c', 'd', 'e']) {
      fireEvent.change(body, { target: { value: body.value + ch } });
      await new Promise((r) => setTimeout(r, 50));
    }
    expect(body.value).toBe(`${start}abcde`);
    // 250 ms of typing + 300 ms quiet: still inside the debounce window -> nothing new yet.
    await new Promise((r) => setTimeout(r, 300));
    expect(posts.length).toBe(1);
    await waitFor(() => expect(posts.length).toBe(2), { timeout: 2000 });

    // Typing again while that second request is in flight starts a third and cancels the second.
    fireEvent.change(body, { target: { value: `${body.value}f` } });
    await waitFor(() => expect(posts.length).toBe(3), { timeout: 2500 });
    await waitFor(() => expect(signals.some((s) => s.aborted)).toBe(true));
    vi.restoreAllMocks();
  });

  it('U-C3: a preview 400 names the variable in the panel and on the field, and typing is not blocked', async () => {
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await openTab(user, 'Text message (SMS)');
    setValue('SMS message', 'Hello {{catname}}, see {{bookingLink}}');

    const panelError = await screen.findByTestId('preview-error', undefined, { timeout: 3000 });
    expect(panelError).toHaveTextContent('{{catname}} is not a variable of this template.');
    expect(panelError).toHaveTextContent('Available: catName');
    const field = screen.getByLabelText('SMS message');
    await waitFor(() =>
      expect(field).toHaveAccessibleDescription(
        /\{\{catname\}\} is not a variable of this template/,
      ),
    );
    expect(field).toHaveAttribute('aria-invalid', 'true');

    // Not blocked: the field still takes input.
    await user.type(field, ' more');
    expect((field as HTMLTextAreaElement).value).toMatch(/more$/);
    expect(document.body.textContent).not.toContain('UnknownTemplatePlaceholder');
  });

  it('U-C4: a Unicode SMS preview shows characters, segments, encoding and the note', async () => {
    server.use(
      http.post(PREVIEW_URL, () =>
        HttpResponse.json({
          channels: [
            {
              channel: 'Sms',
              subject: null,
              title: null,
              textBody: 'বিড়াল '.repeat(15),
              htmlBody: null,
              linkPath: null,
              sms: { encoding: 'Ucs2', characters: 105, segments: 2 },
            },
          ],
        }),
      ),
    );
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await openTab(user, 'Text message (SMS)');
    expect(await screen.findByText('105 characters · 2 SMS · Unicode')).toBeInTheDocument();
    expect(screen.getByText(/outside the standard SMS alphabet/)).toBeInTheDocument();
  });

  it('shows a GSM-7 SMS preview without the Unicode note', async () => {
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await openTab(user, 'Text message (SMS)');
    expect(await screen.findByText(/characters · 1 SMS · GSM-7/)).toBeInTheDocument();
    expect(screen.queryByText(/outside the standard SMS alphabet/)).not.toBeInTheDocument();
  });

  it('U-C5: the email preview is a fully sandboxed iframe, and the script in it never runs', async () => {
    renderEditor(ids.vaccinationKey);
    await waitForFirstPreview();
    const frame = screen.getByTitle('Email preview') as HTMLIFrameElement;
    expect(frame.getAttribute('sandbox')).toBe('');
    await waitFor(() => expect(frame.getAttribute('srcdoc')).toContain('<script>'));
    expect(
      (window as unknown as { __previewScriptRan?: boolean }).__previewScriptRan,
    ).toBeUndefined();
    // A "Plain-text version" disclosure exists too.
    expect(screen.getByText('Plain-text version')).toBeInTheDocument();
  });

  it('shows an in-app preview card', async () => {
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await openTab(user, 'In-app');
    expect(await screen.findByText('Momo is due a vaccination')).toBeInTheDocument();
  });

  it('sample values are sent with the preview and are not part of the save', async () => {
    const previewBodies: Array<{ sampleValues?: Record<string, string> }> = [];
    const putBodies: unknown[] = [];
    server.use(
      http.post(PREVIEW_URL, async ({ request }) => {
        previewBodies.push((await request.json()) as { sampleValues?: Record<string, string> });
        return HttpResponse.json({ channels: [] });
      }),
      http.put('*/api/admin/notification-templates/:key', async ({ request }) => {
        putBodies.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await screen.findByText('Sample values for the preview');
    await user.click(screen.getByText('Sample values for the preview'));
    fireEvent.change(screen.getByLabelText('catName'), { target: { value: 'Pixel' } });
    await waitFor(() => expect(previewBodies.at(-1)?.sampleValues).toEqual({ catName: 'Pixel' }), {
      timeout: 3000,
    });

    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Save changes' }),
    );
    await waitFor(() => expect(putBodies.length).toBe(1));
    expect(JSON.stringify(putBodies[0])).not.toContain('Pixel');
    expect(JSON.stringify(putBodies[0])).not.toContain('sampleValues');
  });

  it('save: confirms what is affected, then sends expectedContentVersion and the contents', async () => {
    const puts: Array<{ expectedContentVersion: number; contents: Array<{ channel: string }> }> =
      [];
    server.use(
      http.put('*/api/admin/notification-templates/:key', async ({ request }) => {
        puts.push((await request.json()) as (typeof puts)[number]);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await screen.findByLabelText('Email subject');
    setValue('Email subject', '{{catName}} needs a vaccination');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(
        'Messages sent from now on will use this wording. Messages already sent keep theirs.',
      ),
    ).toBeInTheDocument();
    expect(puts).toHaveLength(0); // nothing sent before the confirm
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(puts).toHaveLength(1));
    expect(puts[0]!.expectedContentVersion).toBe(2);
    expect(puts[0]!.contents.map((c) => c.channel)).toEqual(['Email', 'Sms', 'InApp']);
    expect(await screen.findByText(/Saved\. Messages sent from now on/)).toBeInTheDocument();
  });

  it('U-C7: a 409 shows the conflict message with Reload and keeps what was typed; Reload re-reads', async () => {
    server.use(
      http.put(
        '*/api/admin/notification-templates/:key',
        () => new HttpResponse(null, { status: 409 }),
      ),
    );
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await screen.findByLabelText('Email subject');
    setValue('Email subject', 'My unsaved subject {{catName}}');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Save changes' }),
    );

    const alert = await screen.findByText(
      'Someone else changed this template. Reload to see their version.',
    );
    expect(alert).toBeVisible();
    expect((screen.getByLabelText('Email subject') as HTMLInputElement).value).toBe(
      'My unsaved subject {{catName}}',
    );

    await user.click(screen.getByRole('button', { name: 'Reload' }));
    await waitFor(() =>
      expect((screen.getByLabelText('Email subject') as HTMLInputElement).value).toBe(
        '{{catName}} is due a vaccination',
      ),
    );
    expect(
      screen.queryByText('Someone else changed this template. Reload to see their version.'),
    ).not.toBeInTheDocument();
  });

  it('U-C8: a secret template shows the notice; a normal one does not', async () => {
    const { unmount } = renderEditor(ids.passwordResetKey);
    expect(await screen.findByTestId('secret-notice')).toHaveTextContent(
      'Contains a password-reset link. It is never copied to the inbox and cannot be resent.',
    );
    expect(screen.getByText('Ignores quiet hours')).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'In-app' })).not.toBeInTheDocument();
    unmount();
    renderEditor(ids.vaccinationKey);
    await screen.findByLabelText('Email subject');
    expect(screen.queryByTestId('secret-notice')).not.toBeInTheDocument();
  });

  it('U-C9 / U-X1: a save error on the email subject while the SMS tab is active activates the Email tab and focuses the field; errorCode is not shown', async () => {
    server.use(
      http.put('*/api/admin/notification-templates/:key', () =>
        validation400(
          'Contents[0].Subject',
          'InvalidTemplateLength',
          'The email subject is too long for some mail clients.',
        ),
      ),
    );
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await openTab(user, 'Text message (SMS)');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Save changes' }),
    );

    await waitFor(() =>
      expect(screen.getByRole('tab', { name: /^Email/ })).toHaveAttribute('aria-selected', 'true'),
    );
    const subject = screen.getByLabelText('Email subject');
    await waitFor(() => expect(subject).toHaveFocus());
    expect(subject).toHaveAccessibleDescription(
      /The email subject is too long for some mail clients\./,
    );
    expect(document.body.textContent).not.toContain('InvalidTemplateLength');
  });

  it('keeps every typed value across a server validation error (NFR-USA-5)', async () => {
    server.use(
      http.put('*/api/admin/notification-templates/:key', () =>
        validation400(
          'Contents[1].Body',
          'UnknownTemplatePlaceholder',
          '{{x}} is not a variable of this template.',
        ),
      ),
    );
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await screen.findByLabelText('Email subject');
    setValue('Email subject', 'Typed subject {{catName}}');
    setValue('SMS message', 'Typed sms {{x}} {{bookingLink}}');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Save changes' }),
    );
    await screen.findByText('{{x}} is not a variable of this template.', undefined, {
      timeout: 3000,
    });
    expect((screen.getByLabelText('Email subject') as HTMLInputElement).value).toBe(
      'Typed subject {{catName}}',
    );
    expect((screen.getByLabelText('SMS message') as HTMLTextAreaElement).value).toBe(
      'Typed sms {{x}} {{bookingLink}}',
    );
  });

  it('client length rules block an empty body before any request', async () => {
    const puts: unknown[] = [];
    server.use(
      http.put('*/api/admin/notification-templates/:key', async ({ request }) => {
        puts.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    await screen.findByLabelText('Email subject');
    setValue('Email subject', '');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(await screen.findByText('Enter a subject.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(puts).toHaveLength(0);
  });

  it('Reset to default exists only for a customised template; it confirms, then returns it to Default', async () => {
    const user = userEvent.setup();
    const { unmount } = renderEditor(ids.dewormingKey);
    await screen.findByLabelText('Email subject');
    expect(screen.queryByRole('button', { name: 'Reset to default' })).not.toBeInTheDocument();
    unmount();

    renderEditor(ids.vaccinationKey);
    await user.click(await screen.findByRole('button', { name: 'Reset to default' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/Messages already sent keep theirs\./)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Reset to default' }));

    expect(await screen.findByText('Reset to the default wording.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Default')).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Reset to default' })).not.toBeInTheDocument();
  });

  it('a view-only user sees read-only fields and no Save or Reset', async () => {
    renderEditor(ids.vaccinationKey, false);
    const subject = await screen.findByLabelText('Email subject');
    expect(subject).toHaveAttribute('readonly');
    expect(screen.queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reset to default' })).not.toBeInTheDocument();
  });

  it('U-X2: a dirty editor shows the unsaved-changes confirm on in-app navigation', async () => {
    const user = userEvent.setup();
    renderEditor(ids.vaccinationKey);
    document.body.insertAdjacentHTML('beforeend', '<a id="away" href="/somewhere-else">Away</a>');
    await screen.findByLabelText('Email subject');
    setValue('Email subject', 'changed {{catName}}');
    await user.click(document.getElementById('away')!);
    expect(
      await screen.findByText('You have unsaved changes. Leave this page without saving?'),
    ).toBeVisible();
    document.getElementById('away')?.remove();
  });
});
