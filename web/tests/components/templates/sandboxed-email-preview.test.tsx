import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ESLint } from 'eslint';
import path from 'node:path';
import {
  PREVIEW_DOCUMENT_PREFIX,
  SandboxedEmailPreview,
} from '@/components/notifications/SandboxedEmailPreview';

const WEB_ROOT = path.resolve(import.meta.dirname, '../../..');

const HOSTILE_HTML =
  '<!doctype html><html><body><h1>&lt;b&gt;Mitu&lt;/b&gt;</h1>' +
  '<script>window.parent.__previewScriptRan = true; window.top.__previewScriptRan = true;</script>' +
  '</body></html>';

describe('SandboxedEmailPreview — HO-NTF-002 §8.4, ADR-FE-160', () => {
  it('U-C5: renders into an iframe whose sandbox attribute is the empty string, with a title; no script runs', () => {
    render(<SandboxedEmailPreview htmlBody={HOSTILE_HTML} />);
    const frame = screen.getByTitle('Email preview');
    expect(frame.tagName).toBe('IFRAME');
    expect(frame.getAttribute('sandbox')).toBe('');
    expect(frame.getAttribute('srcdoc')).toBe(PREVIEW_DOCUMENT_PREFIX + HOSTILE_HTML);
    // The mock's canary would set this flag if the script ever ran outside a sandbox.
    expect(
      (window as unknown as { __previewScriptRan?: boolean }).__previewScriptRan,
    ).toBeUndefined();
  });

  it('N9: the document starts with a no-network CSP meta and a no-referrer meta, so previewed images cannot load', () => {
    render(<SandboxedEmailPreview htmlBody={'<img src="https://tracker.example/p.gif">'} />);
    const srcdoc = screen.getByTitle('Email preview').getAttribute('srcdoc')!;
    expect(
      srcdoc.startsWith(
        `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'">`,
      ),
    ).toBe(true);
    expect(srcdoc).toContain('<meta name="referrer" content="no-referrer">');
    expect(srcdoc.indexOf('<meta name="referrer"')).toBeLessThan(srcdoc.indexOf('<img'));
  });
});

async function lint(code: string, filePath: string) {
  const eslint = new ESLint({ cwd: WEB_ROOT });
  const [result] = await eslint.lintText(code, { filePath: path.join(WEB_ROOT, filePath) });
  return result!.messages.filter((m) => m.ruleId === 'no-restricted-syntax');
}

describe('srcDoc lint rule — HO-NTF-002 §8.4, ADR-FE-160', () => {
  const jsx = `export function Bad({ html }: { html: string }) {
  return <iframe title="x" srcDoc={html} />;
}
`;

  it('U-C6: a file using srcDoc outside SandboxedEmailPreview fails the lint rule', async () => {
    const messages = await lint(jsx, 'components/inbox/Bad.tsx');
    expect(messages).toHaveLength(1);
    expect(messages[0]!.message).toContain('SandboxedEmailPreview');
  }, 60_000);

  it('U-C6: the same holds in a test file, and for object-property and assignment forms', async () => {
    expect(await lint(jsx, 'tests/components/templates/Bad.test.tsx')).toHaveLength(1);
    const objectForm = `const props = { srcDoc: '<b>x</b>' };\nexport default props;\n`;
    expect(await lint(objectForm, 'lib/bad.ts')).toHaveLength(1);
    const assignment = `export function f(el: HTMLIFrameElement) { el.srcdoc = '<b>x</b>'; }\n`;
    expect(await lint(assignment, 'lib/bad2.ts')).toHaveLength(1);
  }, 60_000);

  it('U-C6: SandboxedEmailPreview itself is the one place it is allowed', async () => {
    expect(await lint(jsx, 'components/notifications/SandboxedEmailPreview.tsx')).toHaveLength(0);
  }, 60_000);
});
