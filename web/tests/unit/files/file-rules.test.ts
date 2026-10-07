import { describe, expect, it } from 'vitest';
import {
  FILE_RULES,
  describeAllowedTypes,
  findFileRule,
  formatFileSize,
  humanizeContentType,
  validateFileClientSide,
} from '@/lib/files/fileRules';
import type { FilePurpose } from '@/lib/api/files';

function makeFile(name: string, type: string, sizeInBytes: number): File {
  const file = new File([new Uint8Array(Math.min(sizeInBytes, 1024))], name, { type });
  Object.defineProperty(file, 'size', { value: sizeInBytes });
  return file;
}

describe('FILE_RULES — mirrors HO-FIL-001 §8.3 (backend FileRules.cs)', () => {
  const expected: Record<FilePurpose, { types: string[]; maxMb: number }> = {
    LabResult: {
      types: ['application/pdf', 'image/jpeg', 'image/png', 'application/dicom'],
      maxMb: 50,
    },
    ClinicalPhoto: { types: ['image/jpeg', 'image/png', 'image/webp'], maxMb: 15 },
    ConsentForm: { types: ['application/pdf', 'image/jpeg', 'image/png'], maxMb: 15 },
    ProductImage: { types: ['image/jpeg', 'image/png', 'image/webp'], maxMb: 5 },
    BoardingMedia: { types: ['image/jpeg', 'image/png', 'image/webp', 'video/mp4'], maxMb: 50 },
    BoardingBelongings: { types: ['image/jpeg', 'image/png', 'image/webp'], maxMb: 15 },
    ReportExport: { types: ['application/pdf', 'text/csv', 'application/zip'], maxMb: 50 },
    CatProfilePhoto: { types: ['image/jpeg', 'image/png', 'image/webp'], maxMb: 5 },
  };

  it.each(Object.entries(expected))(
    '%s matches the backend allow-list and cap',
    (purpose, spec) => {
      const rule = findFileRule(purpose as FilePurpose);
      expect(rule.allowedContentTypes).toEqual(spec.types);
      expect(rule.maxSizeInBytes).toBe(spec.maxMb * 1024 * 1024);
    },
  );

  it('every FilePurpose has a rule (no purpose falls through to undefined)', () => {
    expect(Object.keys(FILE_RULES).sort()).toEqual(Object.keys(expected).sort());
  });
});

describe('validateFileClientSide — ADR-FE-149', () => {
  it('accepts a file within the allow-list and under the cap', () => {
    const file = makeFile('photo.png', 'image/png', 1024);
    expect(validateFileClientSide(file, 'ProductImage')).toBeNull();
  });

  it('refuses a disallowed declared type — FileTypeNotAllowed, no request needed (C2)', () => {
    const file = makeFile('notes.txt', 'text/plain', 100);
    expect(validateFileClientSide(file, 'ProductImage')).toEqual({
      code: 'FileTypeNotAllowed',
      fileName: 'notes.txt',
    });
  });

  it('refuses a file over the purpose cap — FileTooLarge (C2)', () => {
    const file = makeFile('huge.png', 'image/png', 6 * 1024 * 1024);
    expect(validateFileClientSide(file, 'ProductImage')).toEqual({
      code: 'FileTooLarge',
      fileName: 'huge.png',
    });
  });

  it('checks type before size when a file fails both', () => {
    const file = makeFile('huge.txt', 'text/plain', 6 * 1024 * 1024);
    expect(validateFileClientSide(file, 'ProductImage')?.code).toBe('FileTypeNotAllowed');
  });
});

describe('formatFileSize', () => {
  it.each([
    [500, '500 B'],
    [2048, '2 KB'],
    [1024 * 1024, '1 MB'],
    [Math.round(1.5 * 1024 * 1024), '1.5 MB'],
    [50 * 1024 * 1024, '50 MB'],
  ])('formats %i bytes as %s', (bytes, expectedLabel) => {
    expect(formatFileSize(bytes)).toBe(expectedLabel);
  });
});

describe('humanizeContentType / describeAllowedTypes', () => {
  it('humanizes known types and falls back to the raw MIME string otherwise', () => {
    expect(humanizeContentType('image/png')).toBe('PNG');
    expect(humanizeContentType('application/weird')).toBe('application/weird');
  });

  it('joins two or more types with "or", matching the §8.2 sentence shape', () => {
    expect(describeAllowedTypes('ProductImage')).toBe('JPEG, PNG or WebP');
    expect(describeAllowedTypes('ReportExport')).toBe('PDF, CSV or ZIP');
  });
});
