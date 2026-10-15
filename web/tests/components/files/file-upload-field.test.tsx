import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { FileUploadField } from '@/components/files/FileUploadField';
import { fakeXhrRequests, installFakeXhr, restoreRealXhr } from '../../utils/fakeXhr';
import type { FilePurpose, StoredFileViewModel } from '@/lib/api/files';

function makeFile(name: string, type: string, sizeInBytes = 1024): File {
  const file = new File([new Uint8Array(Math.min(sizeInBytes, 4096))], name, { type });
  Object.defineProperty(file, 'size', { value: sizeInBytes });
  return file;
}

function Harness({
  purpose = 'LabResult',
  onChange = vi.fn(),
}: {
  purpose?: FilePurpose;
  onChange?: (f: StoredFileViewModel | null) => void;
}) {
  const [value, setValue] = useState<StoredFileViewModel | null>(null);
  return (
    <FileUploadField
      id="lab-file"
      label="Attach a lab file"
      purpose={purpose}
      value={value}
      onChange={(file) => {
        setValue(file);
        onChange(file);
      }}
    />
  );
}

beforeEach(() => installFakeXhr());
afterEach(() => restoreRealXhr());

describe('FileUploadField — HO-EMR-002 §11.7, ADR-FE-055', () => {
  it('U-C4: a wrong type is refused before any request leaves the machine', async () => {
    const { container } = render(<Harness />);
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [makeFile('notes.docx', 'application/msword')] } });
    expect(await screen.findAllByText(/notes.docx is a/i)).not.toHaveLength(0);
    expect(fakeXhrRequests()).toHaveLength(0);
  });

  it('U-C4: an oversize file is refused before upload, naming both sizes', async () => {
    const { container } = render(<Harness />);
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, {
      target: { files: [makeFile('huge.pdf', 'application/pdf', 60 * 1024 * 1024)] },
    });
    expect(await screen.findAllByText(/The largest allowed here is 50 MB/i)).not.toHaveLength(0);
    expect(fakeXhrRequests()).toHaveLength(0);
  });

  it('U-C4: FileScanUnavailable from the server is shown as its plain sentence (no workaround)', async () => {
    const { container } = render(<Harness />);
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, {
      target: { files: [makeFile('scan-unavailable-lab.pdf', 'application/pdf')] },
    });
    expect(await screen.findAllByText(/virus scanner is unavailable/i)).not.toHaveLength(0);
    expect(screen.queryByText('FileScanUnavailable')).toBeNull();
  });

  it('a good file becomes a chip with its name and size, handed to the form by file id, and can be removed', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const { container } = render(<Harness onChange={onChange} />);
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, {
      target: { files: [makeFile('lab-report.pdf', 'application/pdf', 2048)] },
    });
    await screen.findByRole('button', { name: /remove lab-report.pdf/i });
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ originalFileName: 'lab-report.pdf', fileId: expect.any(String) }),
    );
    expect(fakeXhrRequests()[0]!.url).toContain('/api/staff/files?purpose=LabResult');
    await user.click(screen.getByRole('button', { name: /remove lab-report\.pdf/i }));
    expect(onChange).toHaveBeenLastCalledWith(null);
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Attach a lab file' })).toBeInTheDocument(),
    );
  });

  it('a clinical photo purpose refuses a PDF', async () => {
    const { container } = render(<Harness purpose="ClinicalPhoto" />);
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [makeFile('x.pdf', 'application/pdf')] } });
    expect(await screen.findAllByText(/x.pdf is a/i)).not.toHaveLength(0);
  });
});
