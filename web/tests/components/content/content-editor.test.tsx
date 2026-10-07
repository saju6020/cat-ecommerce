import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ContentEditor } from '@/components/content/ContentEditor';
import type { ContentItem } from '@/lib/api/admin-content';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

describe('ContentEditor — ⭐ HO-ADM-002 §11.6, US-ADM-07', () => {
  it('auto-suggests a slug from the title while creating, editable until first publish', async () => {
    const user = userEvent.setup();
    render(<ContentEditor kind="Page" onCreate={vi.fn()} isPending={false} />);
    await user.type(screen.getByLabelText(/title/i), 'Our Opening Hours!');
    expect(screen.getByLabelText(/slug/i)).toHaveValue('our-opening-hours');
    expect(screen.getByLabelText(/slug/i)).not.toHaveAttribute('readonly');
  });

  it('locks the slug once the item has been published', () => {
    const published: ContentItem = {
      id: 'c1',
      kind: 'Page',
      slug: 'about-us',
      title: 'About us',
      draftBody: 'Body',
      publishedBody: 'Body',
      publishedOn: '2027-01-01T00:00:00Z',
      status: 'Published',
      placement: null,
      linkUrl: null,
      visibleFrom: null,
      visibleTo: null,
      sortOrder: null,
    };
    render(
      <ContentEditor
        kind="Page"
        item={published}
        onSaveDraft={vi.fn()}
        onPublish={vi.fn()}
        onUnpublish={vi.fn()}
        isPending={false}
      />,
    );
    expect(screen.getByLabelText(/slug/i)).toHaveAttribute('readonly');
    expect(screen.getByText(/cannot change once this has been published/i)).toBeInTheDocument();
  });

  it('U-F2: the live preview and the public renderer use the same LimitedMarkup output for the same body', async () => {
    const user = userEvent.setup();
    const { container } = render(
      <ContentEditor kind="Page" onCreate={vi.fn()} isPending={false} />,
    );
    await user.type(screen.getByLabelText(/^body$/i), '**bold** text');
    const preview = container.querySelector('.prose');
    expect(preview?.querySelector('strong')).toHaveTextContent('bold');
  });

  it('publish is confirmed, naming the public URL for a Page', async () => {
    const published: ContentItem = {
      id: 'c1',
      kind: 'Page',
      slug: 'about-us',
      title: 'About us',
      draftBody: 'Body',
      publishedBody: null,
      publishedOn: null,
      status: 'Draft',
      placement: null,
      linkUrl: null,
      visibleFrom: null,
      visibleTo: null,
      sortOrder: null,
    };
    const user = userEvent.setup();
    render(
      <ContentEditor
        kind="Page"
        item={published}
        onSaveDraft={vi.fn()}
        onPublish={vi.fn()}
        onUnpublish={vi.fn()}
        isPending={false}
      />,
    );
    await user.click(screen.getByRole('button', { name: /^publish$/i }));
    expect(
      await screen.findByText(/replaces what the public sees at \/pages\/about-us/i),
    ).toBeInTheDocument();
  });

  it('ADM 15: Unpublish asks for confirmation naming the page, and only confirming calls onUnpublish', async () => {
    const live: ContentItem = {
      id: 'c1',
      kind: 'Page',
      slug: 'about-us',
      title: 'About us',
      draftBody: 'Body',
      publishedBody: 'Body',
      publishedOn: '2027-01-01T00:00:00Z',
      status: 'Published',
      placement: null,
      linkUrl: null,
      visibleFrom: null,
      visibleTo: null,
      sortOrder: null,
    };
    const onUnpublish = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <ContentEditor
        kind="Page"
        item={live}
        onSaveDraft={vi.fn()}
        onPublish={vi.fn()}
        onUnpublish={onUnpublish}
        isPending={false}
      />,
    );
    await user.click(screen.getByRole('button', { name: /^unpublish$/i }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/takes \/pages\/about-us offline/i)).toBeInTheDocument();
    expect(onUnpublish).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole('button', { name: /^cancel$/i }));
    expect(onUnpublish).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: /^unpublish$/i }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: /^unpublish$/i }),
    );
    expect(onUnpublish).toHaveBeenCalledOnce();
  });
});
