import { describe, expect, it } from 'vitest';
import { ESLint } from 'eslint';
import { a11yMoney, moneySign, normaliseMoney, withoutSign, wireMoney } from '@/lib/money';
import { looksLikeCardNumber } from '@/lib/validation/cardNumberGuard';
import { buildAgeingCsv } from '@/components/billing/ageingCsv';
import { validateStatementFile } from '@/components/billing/StatementUpload';
import type { AgeingRow } from '@/lib/api/billing-staff';

describe('lib/money — ADR-FE-080', () => {
  it('normalises by string manipulation', () => {
    expect(normaliseMoney('12.5')).toBe('12.50');
    expect(normaliseMoney('12')).toBe('12.00');
    expect(normaliseMoney(' 0.1 ')).toBe('0.10');
    expect(normaliseMoney('abc')).toBe('abc');
  });

  it('U-X4: the accessible form always carries the currency code', () => {
    expect(a11yMoney({ amount: '1500.00', currencyCode: 'BDT' })).toBe('BDT 1,500.00');
    expect(a11yMoney({ amount: '1234567.50', currencyCode: 'BDT' })).toBe('BDT 1,234,567.50');
  });

  it('reads the sign of a server variance without arithmetic', () => {
    expect(moneySign('-10.00')).toBe('negative');
    expect(moneySign('0.00')).toBe('zero');
    expect(moneySign('5.25')).toBe('positive');
    expect(withoutSign({ amount: '-10.00', currencyCode: 'BDT' }).amount).toBe('10.00');
  });

  it('sends an amount as a JSON number literal built from the string', () => {
    expect(JSON.stringify({ amount: wireMoney('1500.50') })).toBe('{"amount":1500.50}');
    expect(JSON.stringify({ amount: wireMoney('0.10') })).toBe('{"amount":0.10}');
  });
});

describe('C-5 card-number guard — ADR-FE-084', () => {
  it.each(['4111 1111 1111 1111', '4111-1111-1111-1111', '4111111111111111', 'slip 4111 1111 1111 1111 ok', '𝟒𝟏𝟏𝟏 𝟏𝟏𝟏𝟏 𝟏𝟏𝟏𝟏 𝟏𝟏𝟏𝟏'])(
    'refuses %s',
    (value) => expect(looksLikeCardNumber(value)).toBe(true),
  );

  it.each(['SLIP-48213', 'TRF-20261001-001', '8801712345605', 'INV-MAIN-2627-000123', '1234', ''])(
    'allows %s',
    (value) => expect(looksLikeCardNumber(value)).toBe(false),
  );
});

describe('ageing CSV — U-F2', () => {
  it('copies the server strings unchanged', () => {
    const m = (amount: string) => ({ amount, currencyCode: 'BDT' });
    const row: AgeingRow = {
      customerId: 'c1',
      customerName: '=Evil',
      current: m('0.00'),
      days1To30: m('100.10'),
      days31To60: m('0.00'),
      days61To90: m('0.00'),
      over90: m('7.05'),
      total: m('107.15'),
      oldestDueOn: '2026-09-01',
    };
    const csv = buildAgeingCsv([row]);
    expect(csv).toContain('0.00,100.10,0.00,0.00,7.05,107.15');
    expect(csv).toContain("'=Evil");
  });
});

describe('statement file check — U-E1', () => {
  it('blocks 1 MiB + 1 byte and a .txt, allows exactly 1 MiB', () => {
    expect(validateStatementFile({ name: 'a.csv', size: 1_048_577 })).toMatch(/1 MiB/);
    expect(validateStatementFile({ name: 'a.txt', size: 10 })).toMatch(/\.csv/);
    expect(validateStatementFile({ name: 'a.CSV', size: 1_048_576 })).toBeNull();
    expect(validateStatementFile(undefined)).toMatch(/Choose/);
  });
});

describe('U-X3 — the money lint rule', () => {
  it('fails on Number(amount) in components/billing', async () => {
    const eslint = new ESLint({ cwd: process.cwd() });
    const [result] = await eslint.lintText('export const x = (amount: string) => Number(amount) + +amount;\n', {
      filePath: 'components/billing/Injected.ts',
    });
    const messages = result?.messages.filter((m) => m.ruleId === 'no-restricted-syntax') ?? [];
    expect(messages.length).toBeGreaterThanOrEqual(2);
  }, 60_000);
});
