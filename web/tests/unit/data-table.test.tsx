import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';

const replace = vi.fn();
let search = '';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace }),
  usePathname: () => '/staff/cats',
  useSearchParams: () => new URLSearchParams(search),
}));

interface Row {
  id: string;
  name: string;
}

const rows: Row[] = [
  { id: '1', name: 'Whiskers' },
  { id: '2', name: 'Mittens' },
];

const columns: Array<DataTableColumn<Row>> = [
  { id: 'name', header: 'Name', cell: (row) => row.name, sortable: true },
];

beforeEach(() => {
  replace.mockClear();
  search = '';
});

describe('DataTable', () => {
  it('renders a loading skeleton, not the table, while loading', () => {
    render(
      <DataTable
        caption="Cats"
        columns={columns}
        rows={undefined}
        getRowId={(r) => r.id}
        isLoading
        emptyState={{ title: 'No cats' }}
      />,
    );
    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('renders ErrorState on error, with retry wired to onRetry', async () => {
    const onRetry = vi.fn();
    render(
      <DataTable
        caption="Cats"
        columns={columns}
        rows={undefined}
        getRowId={(r) => r.id}
        isError
        onRetry={onRetry}
        emptyState={{ title: 'No cats' }}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('renders EmptyState in words (e.g. "None recorded"-style text), not a blank table, when there are no rows', () => {
    render(
      <DataTable
        caption="Cats"
        columns={columns}
        rows={[]}
        getRowId={(r) => r.id}
        emptyState={{ title: 'No cats registered yet.' }}
      />,
    );
    expect(screen.getByText('No cats registered yet.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('renders every row when data is present', () => {
    render(
      <DataTable
        caption="Cats"
        columns={columns}
        rows={rows}
        getRowId={(r) => r.id}
        emptyState={{ title: 'No cats' }}
      />,
    );
    expect(screen.getByRole('table', { name: 'Cats' })).toBeInTheDocument();
    expect(screen.getByText('Whiskers')).toBeInTheDocument();
    expect(screen.getByText('Mittens')).toBeInTheDocument();
  });

  it('writes a sort change to the URL — U29', async () => {
    render(
      <DataTable
        caption="Cats"
        columns={columns}
        rows={rows}
        getRowId={(r) => r.id}
        emptyState={{ title: 'No cats' }}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Sort by Name' }));
    expect(replace).toHaveBeenCalledWith(
      expect.stringContaining('sort=name'),
      expect.objectContaining({ scroll: false }),
    );
    expect(replace).toHaveBeenCalledWith(expect.stringContaining('dir=asc'), expect.anything());
  });

  it('writes a page change to the URL — U29', async () => {
    render(
      <DataTable
        caption="Cats"
        columns={columns}
        rows={rows}
        getRowId={(r) => r.id}
        pagination={{ totalCount: 30, pageSize: 10 }}
        emptyState={{ title: 'No cats' }}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(replace).toHaveBeenCalledWith(
      expect.stringContaining('page=2'),
      expect.objectContaining({ scroll: false }),
    );
  });

  it('writes a search submission to the URL — U29', async () => {
    render(
      <DataTable
        caption="Cats"
        columns={columns}
        rows={rows}
        getRowId={(r) => r.id}
        search={{ label: 'Search cats' }}
        emptyState={{ title: 'No cats' }}
      />,
    );
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search cats' }), 'royal');
    await userEvent.click(screen.getByRole('button', { name: 'Search' }));
    expect(replace).toHaveBeenCalledWith(
      expect.stringContaining('q=royal'),
      expect.objectContaining({ scroll: false }),
    );
  });
});
