import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { act } from 'react';

// use-toast.ts holds one module-level store shared by every caller (by design — it's a single
// app-wide toast surface, HO-CAT-002 §8.4's "ephemeral UI state"). Reset the module registry
// between tests so one test's toast can't leak into the next.
beforeEach(() => {
  vi.resetModules();
});

describe('Toaster', () => {
  it('renders a toast raised via toast() with its title and description', async () => {
    const { Toaster } = await import('@/components/ui/toaster');
    const { toast } = await import('@/components/ui/use-toast');
    render(<Toaster />);

    act(() => {
      toast({ title: 'Saved', description: 'The vaccination was recorded.' });
    });

    expect(await screen.findByText('Saved')).toBeInTheDocument();
    expect(screen.getByText('The vaccination was recorded.')).toBeInTheDocument();
  });

  it('dismisses when its close control is activated', async () => {
    const { Toaster } = await import('@/components/ui/toaster');
    const { toast } = await import('@/components/ui/use-toast');
    render(<Toaster />);

    act(() => {
      toast({ title: 'Dismiss me' });
    });
    const heading = await screen.findByText('Dismiss me');
    const closeButton = screen.getByRole('button', { name: 'Dismiss' });
    closeButton.click();

    await waitFor(() => expect(heading).not.toBeInTheDocument());
  });
});
