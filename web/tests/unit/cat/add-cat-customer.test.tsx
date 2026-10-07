import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CatForm } from '@/components/cat/CatForm';
import AddCatPage from '@/app/(portal)/portal/cats/new/page';
import { ApiValidationError } from '@/lib/api/client';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { resetCatProfilesMockData } from '@/mocks/handlers/areas/cat-profiles';

const push = vi.fn();
let search = '';
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace: vi.fn() }),
  usePathname: () => '/portal/cats/new',
  useSearchParams: () => new URLSearchParams(search),
}));

beforeEach(() => {
  push.mockReset();
  search = '';
  resetCatProfilesMockData();
});

describe('CatForm — customer mode (HO-CAT-002-ADDCAT)', () => {
  it('U-AC1 — has no owner field and says "Add cat"', () => {
    render(
      <CatForm mode="register" audience="customer" submitLabel="Add cat" onSubmit={vi.fn()} />,
    );
    expect(screen.queryByLabelText(/owner customer id/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add cat' })).toBeInTheDocument();
  });

  it('AC4 — the Cancel link has a focus-visible outline of its own', () => {
    render(
      <CatForm mode="register" audience="customer" cancelHref="/portal/cats" onSubmit={vi.fn()} />,
    );
    const cancel = screen.getByRole('link', { name: 'Cancel' });
    expect(cancel).toHaveAttribute('href', '/portal/cats');
    expect(cancel.className).toContain('focus-visible:outline-2');
    expect(cancel.className).toContain('focus-visible:outline-accent');
  });

  it('U-AC2 — an empty name is refused in plain words and nothing is sent', async () => {
    const onSubmit = vi.fn();
    render(<CatForm mode="register" audience="customer" onSubmit={onSubmit} />);
    await userEvent.type(screen.getByLabelText('Breed'), 'Ragdoll');
    await userEvent.click(screen.getByRole('button', { name: /register cat/i }));
    expect(await screen.findByText('Enter a name.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByLabelText('Breed')).toHaveValue('Ragdoll');
  });

  it('U-AC3 — an exact date of birth is required when "Exact date" is chosen', async () => {
    const onSubmit = vi.fn();
    render(<CatForm mode="register" audience="customer" onSubmit={onSubmit} />);
    await userEvent.type(screen.getByLabelText(/^Name/), 'Pip');
    await userEvent.click(screen.getByRole('radio', { name: 'Exact date' }));
    await userEvent.click(screen.getByRole('button', { name: /register cat/i }));
    expect(await screen.findByText(/enter a date of birth/i)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('U-AC10 — an OwnerId server error shows as a form-level message in customer mode', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'OwnerId',
            errorCode: 'AccountNotActive',
            errorMessage: 'This account is not active.',
            resourceName: '',
          },
        ],
      }),
    );
    render(<CatForm mode="register" audience="customer" onSubmit={onSubmit} />);
    await userEvent.type(screen.getByLabelText(/^Name/), 'Pip');
    await userEvent.click(screen.getByRole('button', { name: /register cat/i }));
    expect(await screen.findByText('This account is not active.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Name/)).toHaveValue('Pip');
  });

  it('U-AC4 — a server 400 lands on its field and every typed value is kept (NFR-USA-5)', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'Name',
            errorCode: 'MaximumLengthValidator',
            errorMessage: 'Name must be 100 characters or fewer.',
            resourceName: '',
          },
        ],
      }),
    );
    render(<CatForm mode="register" audience="customer" onSubmit={onSubmit} />);
    await userEvent.type(screen.getByLabelText(/^Name/), 'Pip');
    await userEvent.type(screen.getByLabelText('Breed'), 'Ragdoll');
    await userEvent.type(screen.getByLabelText('Colour and markings'), 'Grey');
    await userEvent.click(screen.getByRole('button', { name: /register cat/i }));

    expect(await screen.findByText('Name must be 100 characters or fewer.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Name/)).toHaveValue('Pip');
    expect(screen.getByLabelText('Breed')).toHaveValue('Ragdoll');
    expect(screen.getByLabelText('Colour and markings')).toHaveValue('Grey');
  });
});

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AddCatPage />
    </QueryClientProvider>,
  );
}

describe('/portal/cats/new', () => {
  it('U-AC5 — success goes to the new cat page, posting the empty-GUID owner, species and a null date of birth', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post('*/api/cats', async ({ request }) => {
        bodies.push(await request.json());
        return HttpResponse.json({ catId: 'cat-1' }, { status: 201 });
      }),
    );
    renderPage();
    await userEvent.type(screen.getByLabelText(/^Name/), 'Nutmeg');
    await userEvent.click(screen.getByRole('button', { name: 'Add cat' }));
    await waitFor(() => expect(push).toHaveBeenCalledOnce());
    expect(push.mock.calls[0]?.[0]).toBe('/portal/cats/cat-1');
    expect(bodies).toEqual([
      {
        ownerId: '00000000-0000-0000-0000-000000000000',
        name: 'Nutmeg',
        species: 'Cat',
        sex: 'Unknown',
        dateOfBirth: null,
        dateOfBirthPrecision: 'Estimated',
        breed: null,
        colourAndMarkings: null,
      },
    ]);
  });

  it('U-AC11 — after a successful POST the button stays disabled and a second click sends no second POST', async () => {
    let posts = 0;
    server.use(
      http.post('*/api/cats', () => {
        posts += 1;
        return HttpResponse.json({ catId: 'cat-1' }, { status: 201 });
      }),
    );
    renderPage();
    await userEvent.type(screen.getByLabelText(/^Name/), 'Nutmeg');
    await userEvent.click(screen.getByRole('button', { name: 'Add cat' }));
    await waitFor(() => expect(push).toHaveBeenCalledOnce());
    const busy = screen.getByRole('button', { name: /saving/i });
    expect(busy).toBeDisabled();
    await userEvent.click(busy);
    expect(posts).toBe(1);
    expect(push).toHaveBeenCalledOnce();
  });

  it('U-AC6 — a /book next path returns there with the booking parameters and the new cat', async () => {
    server.use(
      http.post('*/api/cats', () => HttpResponse.json({ catId: 'cat-2' }, { status: 201 })),
    );
    search = 'next=' + encodeURIComponent('/book?category=Vaccination');
    renderPage();
    await userEvent.type(screen.getByLabelText(/^Name/), 'Nutmeg');
    await userEvent.click(screen.getByRole('button', { name: 'Add cat' }));
    await waitFor(() => expect(push).toHaveBeenCalledOnce());
    expect(push.mock.calls[0]?.[0]).toBe('/book?category=Vaccination&catId=cat-2');
  });

  it('U-AC7 — an off-site or non-book next path is ignored', async () => {
    search = 'next=' + encodeURIComponent('//evil.example/book');
    renderPage();
    await userEvent.type(screen.getByLabelText(/^Name/), 'Nutmeg');
    await userEvent.click(screen.getByRole('button', { name: 'Add cat' }));
    await waitFor(() => expect(push).toHaveBeenCalledOnce());
    expect(push.mock.calls[0]?.[0]).toMatch(/^\/portal\/cats\//);
  });

  it('U-AC8 — a server error keeps the typed values on the page', async () => {
    server.use(
      http.post('*/api/cats', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'Name',
                errorCode: 'MaximumLengthValidator',
                errorMessage: 'Name must be 100 characters or fewer.',
                resourceName: '',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    renderPage();
    await userEvent.type(screen.getByLabelText(/^Name/), 'Nutmeg');
    await userEvent.type(screen.getByLabelText('Breed'), 'Ragdoll');
    await userEvent.click(screen.getByRole('button', { name: 'Add cat' }));
    expect(await screen.findByText(/100 characters or fewer/i)).toBeInTheDocument();
    expect(screen.getByLabelText('Breed')).toHaveValue('Ragdoll');
    expect(push).not.toHaveBeenCalled();
  });
});
