import * as React from 'react';

/**
 * `components/content/LimitedMarkup.tsx` — HO-ADM-002 §8.5, ADR-FE-018 (renumbered from the
 * handover's own ADR-FE-017; see that ADR's note on the numbering collision with HO-ACC-002).
 *
 * A small, in-repo parser for exactly HO-ADM-001 §8.8's markup — paragraphs, `## `/`### `
 * headings, `- ` bullet lines, `**bold**`, `[text](url)` links (https or site-relative only) —
 * producing React elements directly. **No `dangerouslySetInnerHTML`, no markdown package.**
 * Anything that doesn't match one of these shapes renders as literal text (React already escapes
 * it — there is no HTML path here at all, so `<script>`, `javascript:`/`data:` links and the rest
 * of HO-ADM-001's `InvalidContentMarkup` rejection list are inert even before the server ever
 * refuses them).
 *
 * `ContentEditor`'s live preview and `PublicContentPage`/`PolicyDetail`/`FaqList` all render
 * through this SAME component (U-F2 — "what the editor sees is what the public gets").
 */

const LINK_PATTERN = /\[([^\]]+)\]\(([^)]+)\)/g;

/** ADM backlog 13: a single leading `/` that is not followed by `/` or `\` (`//evil`, `/\evil`
 *  resolve off-site) and carries no tab/CR/LF (browsers strip them, so `/<TAB>/evil` becomes
 *  `//evil`). */
const SITE_RELATIVE_PATH = /^\/(?![/\\])[^\t\r\n]*$/;

function isSiteRelativePath(url: string): boolean {
  return SITE_RELATIVE_PATH.test(url);
}

/** https, or a site-relative path starting with `/` — anything else (`javascript:`, `data:`, a
 *  bare `mailto:`, …) is rejected and the link text renders as plain text instead of an `<a>`. */
function isAllowedLinkUrl(url: string): boolean {
  if (isSiteRelativePath(url)) return true;
  if (url.startsWith('/')) return false;
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

/** Renders `**bold**` spans and `[text](url)` links inside one line of plain text, in that
 *  left-to-right order, with everything else left as literal text nodes. */
function renderInline(text: string, keyPrefix: string): React.ReactNode[] {
  const nodes: React.ReactNode[] = [];
  let cursor = 0;
  let linkMatch: RegExpExecArray | null;
  LINK_PATTERN.lastIndex = 0;

  function pushBoldSegments(segment: string, keySuffix: string) {
    const boldPattern = /\*\*([^*]+)\*\*/g;
    let boldCursor = 0;
    let boldMatch: RegExpExecArray | null;
    let boldIndex = 0;
    while ((boldMatch = boldPattern.exec(segment))) {
      if (boldMatch.index > boldCursor) {
        nodes.push(segment.slice(boldCursor, boldMatch.index));
      }
      nodes.push(<strong key={`${keyPrefix}-${keySuffix}-b${boldIndex++}`}>{boldMatch[1]}</strong>);
      boldCursor = boldMatch.index + boldMatch[0].length;
    }
    if (boldCursor < segment.length) nodes.push(segment.slice(boldCursor));
  }

  let linkIndex = 0;
  while ((linkMatch = LINK_PATTERN.exec(text))) {
    if (linkMatch.index > cursor) {
      pushBoldSegments(text.slice(cursor, linkMatch.index), `pre${linkIndex}`);
    }
    const linkText = linkMatch[1] ?? '';
    const url = linkMatch[2] ?? '';
    if (isAllowedLinkUrl(url)) {
      const isExternal = !url.startsWith('/');
      nodes.push(
        <a
          key={`${keyPrefix}-link${linkIndex}`}
          href={url}
          {...(isExternal ? { target: '_blank', rel: 'noopener noreferrer' } : {})}
        >
          {linkText}
        </a>,
      );
    } else {
      // Disallowed scheme (javascript:, data:, ...) — the literal source text, never a link.
      nodes.push(linkMatch[0]);
    }
    cursor = linkMatch.index + linkMatch[0].length;
    linkIndex += 1;
  }
  if (cursor < text.length) pushBoldSegments(text.slice(cursor), 'tail');

  return nodes;
}

/** Splits `body` into block-level elements: `## `/`### ` headings, contiguous `- ` runs as one
 *  `<ul>`, and everything else grouped into paragraphs by blank-line separation. */
export function renderLimitedMarkup(body: string): React.ReactNode {
  const lines = body.split(/\r\n|\r|\n/);
  const blocks: React.ReactNode[] = [];
  let paragraphLines: string[] = [];
  let listItems: string[] = [];
  let blockIndex = 0;

  function flushParagraph() {
    if (paragraphLines.length === 0) return;
    const text = paragraphLines.join(' ');
    blocks.push(<p key={`p${blockIndex++}`}>{renderInline(text, `p${blockIndex}`)}</p>);
    paragraphLines = [];
  }

  function flushList() {
    if (listItems.length === 0) return;
    blocks.push(
      <ul key={`ul${blockIndex++}`}>
        {listItems.map((item, index) => (
          <li key={index}>{renderInline(item, `li${blockIndex}-${index}`)}</li>
        ))}
      </ul>,
    );
    listItems = [];
  }

  for (const rawLine of lines) {
    const line = rawLine.trimEnd();
    if (line.trim() === '') {
      flushParagraph();
      flushList();
      continue;
    }
    const headingMatch = /^(#{2,3})\s+(.*)$/.exec(line);
    if (headingMatch) {
      flushParagraph();
      flushList();
      const level = (headingMatch[1] ?? '').length; // 2 => h2 ("## "), 3 => h3 ("### ")
      const content = renderInline(headingMatch[2] ?? '', `h${blockIndex}`);
      blocks.push(
        level === 2 ? (
          <h2 key={`h${blockIndex++}`}>{content}</h2>
        ) : (
          <h3 key={`h${blockIndex++}`}>{content}</h3>
        ),
      );
      continue;
    }
    const bulletMatch = /^- \s?(.*)$/.exec(line);
    if (bulletMatch) {
      flushParagraph();
      listItems.push(bulletMatch[1] ?? '');
      continue;
    }
    flushList();
    paragraphLines.push(line.trim());
  }
  flushParagraph();
  flushList();

  return blocks;
}

export function LimitedMarkup({ body, className }: { body: string; className?: string }) {
  return <div className={className}>{renderLimitedMarkup(body)}</div>;
}
