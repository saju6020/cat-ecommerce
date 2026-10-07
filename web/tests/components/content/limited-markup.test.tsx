import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';

describe('LimitedMarkup — HO-ADM-002 §8.5, ADR-FE-018', () => {
  it('U-F1: script tags render as literal text, a javascript: link is not a link, the rest renders as elements', () => {
    const body = [
      '## Heading',
      '',
      '- a bullet',
      '',
      '**bold text** and <script>alert(1)</script>',
      '',
      '[bad link](javascript:alert(1))',
      '',
      '[good link](https://example.com)',
    ].join('\n');

    const { container } = render(<LimitedMarkup body={body} />);

    expect(screen.getByRole('heading', { level: 2, name: 'Heading' })).toBeInTheDocument();
    expect(screen.getByRole('listitem')).toHaveTextContent('a bullet');
    expect(container.querySelector('strong')).toHaveTextContent('bold text');

    // The <script> tag is never parsed as an element — React renders it as a literal text node.
    expect(container.querySelector('script')).not.toBeInTheDocument();
    expect(container.textContent).toContain('<script>alert(1)</script>');

    // A javascript: URL never becomes an <a> — its source text renders literally instead.
    const links = screen.getAllByRole('link');
    expect(links).toHaveLength(1);
    expect(links[0]).toHaveAttribute('href', 'https://example.com');
    expect(container.textContent).toContain('[bad link](javascript:alert(1))');
  });

  it('U-F1: a site-relative link renders without target=_blank; an https link opens in a new tab', () => {
    const { container } = render(
      <LimitedMarkup body="[home](/pages/about) and [ext](https://example.com)" />,
    );
    const links = container.querySelectorAll('a');
    expect(links[0]).toHaveAttribute('href', '/pages/about');
    expect(links[0]).not.toHaveAttribute('target');
    expect(links[1]).toHaveAttribute('target', '_blank');
    expect(links[1]).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('U-F2: the same body renders identical DOM wherever LimitedMarkup is used (editor preview vs public page)', () => {
    const body = '## Title\n\n- one\n- two\n\n**bold**';
    const first = render(<LimitedMarkup body={body} />);
    const second = render(<LimitedMarkup body={body} />);
    expect(first.container.innerHTML).toEqual(second.container.innerHTML);
  });

  it('ADM 13: protocol-relative, backslash and tab "site-relative" URLs never become links', () => {
    const BS = String.fromCharCode(92);
    const bad = [
      '//evil.example',
      '/' + BS + 'evil.example',
      '/\t/evil.example',
      '/ok\t/evil.example',
    ];
    for (const url of bad) {
      const { container, unmount } = render(<LimitedMarkup body={`[x](${url})`} />);
      expect(container.querySelector('a'), JSON.stringify(url)).toBeNull();
      unmount();
    }
  });

  it('ADM 13: an ordinary site-relative path with a query or fragment is still a link', () => {
    const { container } = render(<LimitedMarkup body="[a](/pages/faq?x=1#top)" />);
    expect(container.querySelector('a')).toHaveAttribute('href', '/pages/faq?x=1#top');
  });
});
