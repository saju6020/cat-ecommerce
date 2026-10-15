import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import PhotosPage from '@/app/(staff)/staff/cats/[catId]/photos/page';
import {
  catProfilesFixtureIds,
  resetCatProfilesMockData,
} from '@/mocks/handlers/areas/cat-profiles';
import { getApiUrl } from '@/lib/api/client';
import { getCat, type CatProfile } from '@/lib/api/cats';
import { fakeXhrRequests, installFakeXhr, restoreRealXhr } from '../../utils/fakeXhr';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/staff/cats/[catId]/photos',
  useSearchParams: () => new URLSearchParams(),
}));

function makeFile(name: string, type: string, sizeInBytes = 1024): File {
  const file = new File([new Uint8Array(Math.min(sizeInBytes, 4096))], name, { type });
  Object.defineProperty(file, 'size', { value: sizeInBytes });
  return file;
}

async function renderPage(catId: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  await act(async () => {
    render(
      <QueryClientProvider client={queryClient}>
        <PhotosPage params={Promise.resolve({ catId })} />
      </QueryClientProvider>,
    );
  });
}

function pickFile(file: File) {
  const input = document.querySelector('input[type="file"]');
  if (!input) throw new Error('No file input rendered');
  // `applyAccept: false` reproduces a browser "All files" picker bypassing `accept`.
  return userEvent.setup({ applyAccept: false }).upload(input as HTMLInputElement, file);
}

beforeEach(() => {
  resetCatProfilesMockData();
  installFakeXhr();
});
afterEach(() => restoreRealXhr());

const BISCUIT = catProfilesFixtureIds.catNoAllergiesEstimatedDob; // no photos
const WHISKERS = catProfilesFixtureIds.catWithAllergiesExactDob; // two photos

describe('Staff cat photos page — upload then attach (HO-CAT-002 §1 Amended 2026-09-27/2026-10-09)', () => {
  it('lists existing photos from their signed urls, with the primary marked', async () => {
    await renderPage(WHISKERS);
    expect(await screen.findByText('whiskers-front.jpg')).toBeInTheDocument();
    expect(screen.getByText('whiskers-side.jpg')).toBeInTheDocument();
    expect(screen.getByText('Primary')).toBeInTheDocument();
    const images = screen.getAllByRole('img');
    expect(images[0]).toHaveAttribute('src', expect.stringContaining('/api/files/'));
  });

  /** The cat as the real mock returns it, so a test can swap just the photos. */
  async function catWithPhotos(
    catId: string,
    photosOf: (base: CatProfile) => CatProfile['photos'],
  ): Promise<CatProfile> {
    const base = await getCat(catId);
    return { ...base, photos: photosOf(base) };
  }

  it('renders an API-relative signed url with an EXACT absolute src on the API origin', async () => {
    const relative = '/api/files/9f1c/content?exp=1999999999&disp=inline&sig=abc_-';
    const cat = await catWithPhotos(WHISKERS, (base) => [
      { ...base.photos[0]!, url: relative, urlExpiresOn: '2999-01-01T00:00:00.000Z' },
    ]);
    server.use(http.get('*/api/cats/:catId', () => HttpResponse.json(cat)));
    // The wire value is relative; strip what getCat already anchored by returning raw JSON above.
    await renderPage(WHISKERS);
    const img = await screen.findByRole('img');
    expect(img).toHaveAttribute('src', getApiUrl(relative));
  });

  it('a protocol-relative url is unusable: no <img> is rendered for it', async () => {
    const cat = await catWithPhotos(WHISKERS, (base) => [
      { ...base.photos[0]!, url: '//evil.test/x.png', urlExpiresOn: '2999-01-01T00:00:00.000Z' },
    ]);
    server.use(http.get('*/api/cats/:catId', () => HttpResponse.json(cat)));
    await renderPage(WHISKERS);
    await screen.findByText('whiskers-front.jpg');
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(screen.getByText(/could not be loaded/)).toBeInTheDocument();
  });

  it('10-photo cap: shows the limit note and no uploader', async () => {
    const cat = await catWithPhotos(WHISKERS, (base) =>
      Array.from({ length: 10 }, (_, i) => ({
        ...base.photos[0]!,
        id: `photo-${i}`,
        originalFileName: `p${i}.jpg`,
        urlExpiresOn: '2999-01-01T00:00:00.000Z',
      })),
    );
    server.use(http.get('*/api/cats/:catId', () => HttpResponse.json(cat)));
    await renderPage(WHISKERS);
    expect(await screen.findByText(/already has the maximum of 10 photos/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add photo' })).not.toBeInTheDocument();
    expect(document.querySelector('input[type="file"]')).toBeNull();
  });

  it('an expired signed url is re-read once, and the fresh url is shown', async () => {
    const stale = await catWithPhotos(WHISKERS, (base) => [
      {
        ...base.photos[0]!,
        url: '/api/files/aaa/content?exp=1&disp=inline&sig=old',
        urlExpiresOn: '2000-01-01T00:00:00.000Z',
      },
    ]);
    const fresh = {
      ...stale,
      photos: [
        {
          ...stale.photos[0]!,
          url: '/api/files/aaa/content?exp=9&disp=inline&sig=new',
          urlExpiresOn: '2999-01-01T00:00:00.000Z',
        },
      ],
    };
    let reads = 0;
    server.use(
      http.get('*/api/cats/:catId', () => {
        reads += 1;
        return HttpResponse.json(reads === 1 ? stale : fresh);
      }),
    );
    await renderPage(WHISKERS);
    const img = await screen.findByRole('img');
    await waitFor(() =>
      expect(img).toHaveAttribute('src', getApiUrl('/api/files/aaa/content?exp=9&disp=inline&sig=new')),
    );
    expect(reads).toBe(2);
  });

  it('happy path — uploads with purpose CatProfilePhoto, attaches only { fileId, isPrimary }, refreshes the list', async () => {
    let attachBody: unknown;
    server.events.on('request:start', async ({ request }) => {
      if (request.method === 'POST' && request.url.includes('/photos')) {
        attachBody = await request.clone().json();
      }
    });
    await renderPage(BISCUIT);
    expect(await screen.findByText('No photos on file yet.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add photo' })).toBeDisabled();

    await pickFile(makeFile('biscuit.png', 'image/png', 2048));

    expect(await screen.findByText('biscuit.png')).toBeInTheDocument();
    expect(fakeXhrRequests()).toHaveLength(1);
    expect(fakeXhrRequests()[0]?.url).toContain('/api/staff/files');
    expect(fakeXhrRequests()[0]?.url).toContain('purpose=CatProfilePhoto');

    await userEvent.click(screen.getByRole('button', { name: 'Add photo' }));

    await waitFor(() => expect(screen.queryByText('No photos on file yet.')).not.toBeInTheDocument());
    expect(screen.getByText('Primary')).toBeInTheDocument();
    // The mock attach stores an API-relative signed path (like the real API); the page anchors it
    // on the API origin so the browser does not resolve it against the web origin.
    expect(screen.getByRole('img').getAttribute('src')?.startsWith(getApiUrl('/api/files/mock-file-'))).toBe(
      true,
    );
    expect(attachBody).toEqual({ fileId: expect.stringMatching(/^mock-file-/), isPrimary: true });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    server.events.removeAllListeners();
  });

  it('A3/U14 — an 8 MB file is refused before any request, and the message names the 5 MB limit', async () => {
    await renderPage(BISCUIT);
    await screen.findByText('No photos on file yet.');
    await pickFile(makeFile('big.jpg', 'image/jpeg', 8.2 * 1024 * 1024));

    expect(await screen.findAllByText(/5 MB/)).not.toHaveLength(0);
    expect(fakeXhrRequests()).toHaveLength(0);
    expect(screen.getByRole('button', { name: 'Add photo' })).toBeDisabled();
  });

  it('A3/U15 — a disallowed type (image/gif) is refused before any request, naming JPEG, PNG or WebP', async () => {
    await renderPage(BISCUIT);
    await screen.findByText('No photos on file yet.');
    await pickFile(makeFile('animated.gif', 'image/gif', 1024));

    const messages = await screen.findAllByText(/JPEG, PNG or WebP/);
    expect(messages.length).toBeGreaterThan(0);
    expect(fakeXhrRequests()).toHaveLength(0);
    expect(screen.getByRole('button', { name: 'Add photo' })).toBeDisabled();
  });

  it('upload failure — a server refusal is shown, nothing is attached', async () => {
    let attachCalled = false;
    server.use(
      http.post('*/api/cats/:catId/photos', () => {
        attachCalled = true;
        return HttpResponse.json({}, { status: 201 });
      }),
    );
    await renderPage(BISCUIT);
    await screen.findByText('No photos on file yet.');
    await pickFile(makeFile('reject-cat.png', 'image/png', 1024));

    await waitFor(() => expect(screen.getAllByText(/reject/i).length).toBeGreaterThan(0));
    expect(screen.getByRole('button', { name: 'Add photo' })).toBeDisabled();
    expect(attachCalled).toBe(false);
  });

  it('attach failure — the refusal is shown inline, the chosen file is kept, and the list is unchanged', async () => {
    await renderPage(BISCUIT);
    await screen.findByText('No photos on file yet.');
    await pickFile(makeFile('attach-rejected-cat.png', 'image/png', 1024));
    await userEvent.click(await screen.findByRole('button', { name: 'Add photo' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Upload it again');
    expect(screen.getByText('attach-rejected-cat.png')).toBeInTheDocument();
    expect(screen.getByText('No photos on file yet.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add photo' })).toBeEnabled();
  });

  it('a deceased cat shows no uploader', async () => {
    await renderPage(catProfilesFixtureIds.catDeceased);
    await screen.findByRole('heading', { name: 'Photos' });
    expect(screen.queryByRole('button', { name: 'Add photo' })).not.toBeInTheDocument();
  });
});
