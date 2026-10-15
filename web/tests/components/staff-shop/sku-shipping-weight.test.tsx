import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { VariantsTable } from '@/components/staff-shop/VariantsTable';
import { AddSkuDialog, type AddSkuValues } from '@/components/staff-shop/AddSkuDialog';
import { ApiValidationError } from '@/lib/api/client';
import { addSku, getStaffProduct, updateSku, type StaffSku } from '@/lib/api/staff-shop';
import { parseShippingWeight } from '@/lib/shop/shippingWeight';
import { resetShopMockData } from '@/mocks/handlers/shop';
import { shopFixtureIds } from '@/mocks/handlers/shop/fixtures';
import type { StaffProductDetail } from '@/lib/api/staff-shop';

// HO-SHP-001-FU3 (backlog SHP row 63): optional SKU shipping weight. An update that omits the
// field CLEARS it server-side, so the form must prefill it and send it back on every save.

const SKU = (over: Partial<StaffSku> = {}): StaffSku => ({
  skuId: 'sku-1',
  code: 'RC-K-2KG',
  barcode: null,
  sizeOrWeight: '2 kg',
  flavour: null,
  colour: null,
  packQuantity: null,
  shippingWeightGrams: 2100,
  label: '2 kg',
  status: 'Active',
  currentPrice: null,
  scheduledPrice: null,
  stock: null,
  ...over,
});

const table = (sku: StaffSku, onUpdateSku = vi.fn().mockResolvedValue(undefined)) => {
  render(
    <VariantsTable
      product={{ productId: 'p1', version: 3, skus: [sku] } as unknown as StaffProductDetail}
      onAddSku={vi.fn()}
      onUpdateSku={onUpdateSku}
      onSetStatus={vi.fn()}
      priceSkuId={null}
      onSelectPrices={() => {}}
      onSchedulePrice={vi.fn()}
    />,
  );
  return onUpdateSku;
};

const violation = (propertyName = 'ShippingWeightGrams', errorMessage = '') =>
  new ApiValidationError({
    isValid: false,
    errors: [
      { propertyName, errorCode: 'InvalidShippingWeight', errorMessage, resourceName: 'Sku' },
    ],
  });

beforeEach(() => resetShopMockData());

describe('parseShippingWeight', () => {
  it('empty is not set; 1..100000 whole numbers pass; everything else is refused', () => {
    expect(parseShippingWeight('  ')).toEqual({ value: null });
    expect(parseShippingWeight('1')).toEqual({ value: 1 });
    expect(parseShippingWeight('100000')).toEqual({ value: 100000 });
    for (const bad of ['0', '100001', '-5', '1.5', 'abc', '1e3']) {
      expect(parseShippingWeight(bad).error).toMatch(/whole number of grams from 1 to 100000/);
    }
  });
});

describe('Edit SKU — shipping weight', () => {
  it('shows the weight in the table and prefills the field from the read', async () => {
    const user = userEvent.setup();
    table(SKU());
    expect(screen.getByText('2100 g')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Edit RC-K-2KG' }));
    const dialog = await screen.findByRole('dialog', { name: 'Edit SKU RC-K-2KG' });
    expect(within(dialog).getByLabelText(/Shipping weight \(g\)/)).toHaveValue(2100);
    expect(within(dialog).getByLabelText('Size or weight')).toHaveValue('2 kg');
  });

  it('round-trips the unchanged value on save', async () => {
    const user = userEvent.setup();
    const onUpdateSku = table(SKU());
    await user.click(screen.getByRole('button', { name: 'Edit RC-K-2KG' }));
    await user.click(await screen.findByRole('button', { name: 'Save SKU' }));
    expect(onUpdateSku).toHaveBeenCalledWith(
      expect.objectContaining({ skuId: 'sku-1' }),
      expect.objectContaining({ sizeOrWeight: '2 kg', shippingWeightGrams: 2100 }),
    );
  });

  it('sends null (not undefined) for a SKU without a weight, and a changed value when edited', async () => {
    const user = userEvent.setup();
    const onUpdateSku = table(SKU({ shippingWeightGrams: null }));
    expect(screen.getAllByText('—').length).toBeGreaterThan(0);
    await user.click(screen.getByRole('button', { name: 'Edit RC-K-2KG' }));
    const field = await screen.findByLabelText(/Shipping weight \(g\)/);
    expect(field).toHaveValue(null);
    await user.click(screen.getByRole('button', { name: 'Save SKU' }));
    expect(onUpdateSku.mock.calls[0]![1].shippingWeightGrams).toBeNull();

    await user.click(await screen.findByRole('button', { name: 'Edit RC-K-2KG' }));
    await user.type(await screen.findByLabelText(/Shipping weight \(g\)/), '450');
    await user.click(screen.getByRole('button', { name: 'Save SKU' }));
    expect(onUpdateSku.mock.calls[1]![1].shippingWeightGrams).toBe(450);
  });

  it('refuses a value outside 1..100000 without calling the API', async () => {
    const user = userEvent.setup();
    const onUpdateSku = table(SKU());
    await user.click(screen.getByRole('button', { name: 'Edit RC-K-2KG' }));
    const field = await screen.findByLabelText(/Shipping weight \(g\)/);
    await user.clear(field);
    await user.type(field, '100001');
    await user.click(screen.getByRole('button', { name: 'Save SKU' }));
    expect(await screen.findByText(/whole number of grams from 1 to 100000/)).toBeInTheDocument();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(onUpdateSku).not.toHaveBeenCalled();
    await user.clear(field);
    await user.type(field, '0');
    await user.click(screen.getByRole('button', { name: 'Save SKU' }));
    expect(onUpdateSku).not.toHaveBeenCalled();
  });

  it('maps the server InvalidShippingWeight to the field error and keeps the typed value', async () => {
    const user = userEvent.setup();
    const onUpdateSku = table(SKU(), vi.fn().mockRejectedValue(violation()));
    await user.click(screen.getByRole('button', { name: 'Edit RC-K-2KG' }));
    await user.click(await screen.findByRole('button', { name: 'Save SKU' }));
    expect(
      await screen.findByText('Enter a shipping weight between 1 and 100000 g.'),
    ).toBeInTheDocument();
    expect(onUpdateSku).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText(/Shipping weight \(g\)/)).toHaveValue(2100);
    expect(screen.getByLabelText(/Shipping weight \(g\)/)).toHaveAttribute('aria-invalid', 'true');
  });
});

describe('Add SKU — shipping weight', () => {
  const open = (onAdd: (values: AddSkuValues) => Promise<unknown>) =>
    render(<AddSkuDialog open onOpenChange={() => {}} onAdd={onAdd} />);

  it('is optional: empty sends null, a value sends the number', async () => {
    const user = userEvent.setup();
    const onAdd = vi.fn().mockResolvedValue(undefined);
    open(onAdd);
    await user.type(screen.getByLabelText('SKU code'), 'NEW-1');
    await user.click(screen.getByRole('button', { name: 'Add SKU' }));
    expect(onAdd.mock.calls[0]![0].shippingWeightGrams).toBeNull();
    await user.type(screen.getByLabelText('SKU code'), 'NEW-2');
    await user.type(screen.getByLabelText(/Shipping weight \(g\)/), '750');
    await user.click(screen.getByRole('button', { name: 'Add SKU' }));
    expect(onAdd.mock.calls[1]![0].shippingWeightGrams).toBe(750);
  });

  it('validates the range and maps the server error to the field', async () => {
    const user = userEvent.setup();
    const onAdd = vi.fn().mockRejectedValue(violation('ShippingWeightGrams', 'Too heavy.'));
    open(onAdd);
    await user.type(screen.getByLabelText('SKU code'), 'NEW-1');
    await user.type(screen.getByLabelText(/Shipping weight \(g\)/), '0');
    await user.click(screen.getByRole('button', { name: 'Add SKU' }));
    expect(await screen.findByText(/whole number of grams from 1 to 100000/)).toBeInTheDocument();
    expect(onAdd).not.toHaveBeenCalled();
    await user.clear(screen.getByLabelText(/Shipping weight \(g\)/));
    await user.type(screen.getByLabelText(/Shipping weight \(g\)/), '5');
    await user.click(screen.getByRole('button', { name: 'Add SKU' }));
    expect(await screen.findByText('Too heavy.')).toBeInTheDocument();
  });
});

describe('Shop mock parity — store, return and validate', () => {
  const id = shopFixtureIds.multiVariantProductId;
  const sku2kg = async () =>
    (await getStaffProduct(id)).skus.find((s) => s.code === 'RC-K-2KG') as StaffSku;

  it('returns the weight on the staff read (null when unset)', async () => {
    const product = await getStaffProduct(id);
    expect(product.skus.find((s) => s.code === 'RC-K-2KG')?.shippingWeightGrams).toBe(2100);
    expect(product.skus.find((s) => s.code === 'RC-K-400G')?.shippingWeightGrams).toBeNull();
  });

  it('stores the weight on add and update; an update that omits it clears it', async () => {
    let p = await getStaffProduct(id);
    const added = await addSku(id, {
      expectedVersion: p.version,
      code: 'RC-K-NEW',
      sizeOrWeight: '800 g',
      shippingWeightGrams: 800,
    });
    p = await getStaffProduct(id);
    expect(p.skus.find((s) => s.skuId === added.skuId)?.shippingWeightGrams).toBe(800);

    const before = await sku2kg();
    await updateSku(id, before.skuId, {
      expectedVersion: p.version,
      sizeOrWeight: '2 kg',
      shippingWeightGrams: 2100,
    });
    p = await getStaffProduct(id);
    expect((await sku2kg()).shippingWeightGrams).toBe(2100);

    await updateSku(id, before.skuId, { expectedVersion: p.version, sizeOrWeight: '2 kg' });
    expect((await sku2kg()).shippingWeightGrams).toBeNull();
  });

  it('refuses 0, 100001 and fractions with InvalidShippingWeight on add and update', async () => {
    const p = await getStaffProduct(id);
    const target = await sku2kg();
    for (const bad of [0, 100001, 1.5]) {
      await expect(
        updateSku(id, target.skuId, {
          expectedVersion: p.version,
          sizeOrWeight: '2 kg',
          shippingWeightGrams: bad,
        }),
      ).rejects.toMatchObject({
        response: { errors: [expect.objectContaining({ errorCode: 'InvalidShippingWeight' })] },
      });
    }
    await expect(
      addSku(id, {
        expectedVersion: p.version,
        code: 'RC-K-BAD',
        sizeOrWeight: '1 kg',
        shippingWeightGrams: 100001,
      }),
    ).rejects.toMatchObject({
      response: { errors: [expect.objectContaining({ errorCode: 'InvalidShippingWeight' })] },
    });
  });
});
