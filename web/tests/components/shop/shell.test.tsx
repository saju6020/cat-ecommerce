import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { MaintenanceGate } from '@/app/(shop)/_components/MaintenanceGate';
import { MaintenanceNotice } from '@/components/shop/MaintenanceNotice';
import { OriginGrid } from '@/components/shop/OriginGrid';
import { LanguageSwitcher } from '@/components/shop/LanguageSwitcher';
import { BannerStrip } from '@/components/shop/BannerStrip';
import { isMaintenanceError } from '@/lib/shop/helpers';
import { ApiError } from '@/lib/api/client';

const replace = vi.fn();
let search = '';
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, push: vi.fn() }),
  usePathname: () => '/shop/products',
  useSearchParams: () => new URLSearchParams(search),
}));

function withClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{ui}</QueryClientProvider>;
}

beforeEach(() => {
  replace.mockClear();
  search = '';
});

describe('MaintenanceGate — HO-SHP-002 §11.1, US-ADM-08', () => {
  it('U-F1: maintenance on — the notice replaces the page, with portal and booking links', () => {
    render(
      withClient(
        <MaintenanceGate initial={{ maintenanceMode: true, message: 'Back at 6pm.' }}>
          <p>storefront page</p>
        </MaintenanceGate>,
      ),
    );
    expect(screen.queryByText('storefront page')).not.toBeInTheDocument();
    expect(screen.getByText('Back at 6pm.')).toBeInTheDocument();
    expect(
      screen.getByText(/Clinic bookings and your account are still available/),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to your account' })).toHaveAttribute(
      'href',
      '/portal',
    );
    expect(screen.getByRole('link', { name: /Book a clinic visit/ })).toHaveAttribute(
      'href',
      '/book',
    );
  });

  it('U-F1: maintenance started after the page was rendered — the client watch swaps it', async () => {
    server.use(
      http.get('*/api/system/status', () =>
        HttpResponse.json({ maintenanceMode: true, message: 'Stock take.' }),
      ),
    );
    render(
      withClient(
        <MaintenanceGate initial={{ maintenanceMode: false, message: null }}>
          <p>storefront page</p>
        </MaintenanceGate>,
      ),
    );
    expect(await screen.findByText('Stock take.')).toBeInTheDocument();
    expect(screen.queryByText('storefront page')).not.toBeInTheDocument();
  });

  it('U-F1: shows the page when maintenance is off', () => {
    render(
      withClient(
        <MaintenanceGate initial={{ maintenanceMode: false, message: null }}>
          <p>storefront page</p>
        </MaintenanceGate>,
      ),
    );
    expect(screen.getByText('storefront page')).toBeInTheDocument();
  });

  it('U-F1: a storefront 503 is recognised as maintenance', () => {
    expect(isMaintenanceError(new ApiError('x', 503))).toBe(true);
    expect(isMaintenanceError(new ApiError('x', 500))).toBe(false);
    render(<MaintenanceNotice />);
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
  });

  it('shows the admin message as text, never markup', () => {
    render(<MaintenanceNotice message="<b>Closed</b>" />);
    expect(screen.getByText('<b>Closed</b>')).toBeInTheDocument();
  });
});

describe('LanguageSwitcher — ADR-FE-037', () => {
  it('U-F2: absent with one locale', () => {
    const { container } = render(<LanguageSwitcher defaultLocale="en" locales={['en']} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('U-F2: present with two locales and rewrites ?lang=, keeping other params', async () => {
    const user = userEvent.setup();
    search = 'q=salmon';
    render(<LanguageSwitcher defaultLocale="en" locales={['en', 'bn']} />);
    await user.selectOptions(screen.getByLabelText('Product language'), 'bn');
    expect(replace).toHaveBeenCalledWith('/shop/products?q=salmon&lang=bn', { scroll: false });
  });

  it('going back to the default language drops ?lang=', async () => {
    const user = userEvent.setup();
    search = 'lang=bn';
    render(<LanguageSwitcher defaultLocale="en" locales={['en', 'bn']} />);
    await user.selectOptions(screen.getByLabelText('Product language'), 'en');
    expect(replace).toHaveBeenCalledWith('/shop/products', { scroll: false });
  });
});

describe('OriginGrid — HO-SHP-002 §11.4, US-SHP-05 AC1', () => {
  it('U-C1: a flagless origin shows a globe, the name as text, the count and the notes excerpt', () => {
    const notes = `**Careful** sourcing. ${'word '.repeat(60)}`;
    const { container } = render(
      <OriginGrid
        origins={[
          { code: 'TH', name: 'Thailand', sourcingNotes: notes, productCount: 9, isFeatured: true },
          { code: 'BR', name: 'Brazil', sourcingNotes: null, productCount: 1, isFeatured: false },
        ]}
      />,
    );
    expect(screen.getByRole('heading', { level: 2, name: 'Thailand' })).toBeInTheDocument();
    expect(screen.getByText('9 products')).toBeInTheDocument();
    expect(screen.getByText('1 product')).toBeInTheDocument();
    expect(screen.getByText('No sourcing notes yet.')).toBeInTheDocument();
    // The excerpt is plain text, 160 characters at most.
    const excerpt = screen.getByText(/^Careful sourcing\./);
    expect(excerpt.textContent!.length).toBeLessThanOrEqual(161);
    expect(excerpt.innerHTML).not.toContain('**');
    // Flags are decorative: alt="" for Thailand; Brazil has no flag file, so it is the globe icon.
    const flags = container.querySelectorAll('img');
    expect(flags).toHaveLength(1);
    expect(flags[0]).toHaveAttribute('alt', '');
    expect(container.querySelectorAll('svg[aria-hidden="true"]').length).toBeGreaterThanOrEqual(1);
  });
});

describe('BannerStrip — FR-ADM-7', () => {
  it('renders title and body through LimitedMarkup, a link only when present and safe', () => {
    render(
      <BannerStrip
        banners={[
          {
            slug: 'a',
            title: 'Free delivery',
            body: 'Over **৳2,000**.',
            placement: 'Shop',
            linkUrl: '/shop/products',
          },
          {
            slug: 'b',
            title: 'Sale',
            body: 'Hot.',
            placement: 'Shop',
            linkUrl: 'javascript:alert(1)',
          },
        ]}
      />,
    );
    expect(screen.getByText('৳2,000').tagName).toBe('STRONG');
    expect(screen.getAllByRole('link')).toHaveLength(1);
    expect(screen.getByRole('link')).toHaveAttribute('href', '/shop/products');
  });

  it('renders nothing without banners', () => {
    const { container } = render(<BannerStrip banners={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
