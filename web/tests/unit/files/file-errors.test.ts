import { describe, expect, it } from 'vitest';
import {
  describeFileError,
  isAutoRemovedFileError,
  isRetryableFileError,
} from '@/lib/files/fileErrors';

/** HO-FIL-002 §8.2's own mapping table, verified sentence-for-sentence (ADR-FE-151). */
describe('describeFileError — §8.2 mapping table', () => {
  it('FileTooLarge names the actual size and the purpose cap', () => {
    const message = describeFileError({
      code: 'FileTooLarge',
      fileName: 'huge.png',
      fileSizeInBytes: 6 * 1024 * 1024,
      purpose: 'ProductImage',
    });
    expect(message).toBe('That file is 6 MB. The largest allowed here is 5 MB.');
  });

  it('FileTypeNotAllowed names the offending type and the allowed list', () => {
    const message = describeFileError({
      code: 'FileTypeNotAllowed',
      fileName: 'notes.txt',
      fileContentType: 'text/plain',
      purpose: 'ProductImage',
    });
    expect(message).toBe('notes.txt is a text/plain file. You can upload JPEG, PNG or WebP.');
  });

  it('FileContentMismatch says the file may be renamed or damaged', () => {
    const message = describeFileError({
      code: 'FileContentMismatch',
      fileName: 'photo.png',
      fileContentType: 'image/png',
      purpose: 'ProductImage',
    });
    expect(message).toBe('photo.png does not look like a PNG file. It may be renamed or damaged.');
  });

  it('FileRejected is unambiguous about the virus scanner, never the raw server message', () => {
    const message = describeFileError({
      code: 'FileRejected',
      fileName: 'eicar.png',
      purpose: 'ProductImage',
    });
    expect(message).toBe('eicar.png was rejected by the virus scanner and has not been saved.');
  });

  it('FileScanUnavailable reads as a service state, never blaming the person (A4)', () => {
    const message = describeFileError({
      code: 'FileScanUnavailable',
      fileName: 'photo.png',
      purpose: 'ProductImage',
    });
    expect(message).toBe(
      'The virus scanner is unavailable, so files cannot be uploaded right now. Please try again shortly.',
    );
    expect(message).not.toMatch(/you|your/i);
  });

  it('NetworkFailure (network/abort) says the file did not finish uploading', () => {
    const message = describeFileError({
      code: 'NetworkFailure',
      fileName: 'photo.png',
      purpose: 'ProductImage',
    });
    expect(message).toBe('photo.png did not finish uploading.');
  });

  it("never echoes a server-supplied message string — every branch is this module's own wording", () => {
    const serverSaid = 'DO NOT SHOW THIS RAW SERVER TEXT';
    const message = describeFileError({
      code: 'FileRejected',
      fileName: 'x.png',
      purpose: 'ProductImage',
    });
    expect(message).not.toContain(serverSaid);
  });
});

describe('isRetryableFileError — OQ-2, manual retry only', () => {
  it('offers retry for a scanner outage and a network failure', () => {
    expect(isRetryableFileError('FileScanUnavailable')).toBe(true);
    expect(isRetryableFileError('NetworkFailure')).toBe(true);
  });

  it('does not offer retry for a problem retrying cannot fix', () => {
    expect(isRetryableFileError('FileTooLarge')).toBe(false);
    expect(isRetryableFileError('FileTypeNotAllowed')).toBe(false);
    expect(isRetryableFileError('FileContentMismatch')).toBe(false);
    expect(isRetryableFileError('FileRejected')).toBe(false);
  });
});

describe('isAutoRemovedFileError — OQ-1 default', () => {
  it('a scanner-rejected file is removed, not left as a lingering red row', () => {
    expect(isAutoRemovedFileError('FileRejected')).toBe(true);
  });

  it('every other code stays visible until the person dismisses or retries it', () => {
    expect(isAutoRemovedFileError('FileTooLarge')).toBe(false);
    expect(isAutoRemovedFileError('FileScanUnavailable')).toBe(false);
    expect(isAutoRemovedFileError('NetworkFailure')).toBe(false);
  });
});
