'use client';

import {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { CustomerPicker, type PickedCustomer } from '@/components/appointments/CustomerPicker';
import {
  walkInSchema,
  MAX_APPOINTMENT_TEXT,
  type WalkInValues,
} from '@/lib/validation/appointments';
import { submitWithServerErrors } from '@/lib/api/errors';
import { CAT_SEX_OPTIONS } from '@/lib/catEnumOptions';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';
import type { CatSex } from '@/lib/api/cats';
import { PRIORITY_OPTIONS } from './PriorityBadge';

export interface WalkInCat {
  id: string;
  name: string;
}

export interface WalkInPanelHandle {
  focusSearch: () => void;
}

export interface WalkInPanelProps {
  services: Array<{ id: string; name: string }>;
  selectedCustomer: PickedCustomer | null;
  cats: WalkInCat[];
  onSelectCustomer: (customer: StaffCustomerSummary) => void;
  onClearCustomer: () => void;
  onSearch: (term: string) => void;
  results: StaffCustomerSummary[] | undefined;
  isSearching: boolean;
  searchedTerm: string | null;
  /** Renders HO-ACC-002's inline walk-in customer form; call `onCreated(customerId)` when done. */
  renderNewCustomerForm: (onCreated: (customerId: string) => void) => ReactNode;
  onNewCustomerCreated: (customerId: string) => void;
  /** HO-CAT-002 quick-add: registers a cat for the chosen customer. */
  onQuickAddCat: (name: string, sex: CatSex) => Promise<WalkInCat>;
  onSubmit: (values: WalkInValues) => Promise<void>;
}

const FIELD_MAP = {
  CustomerId: 'customerId',
  CatId: 'catId',
  ServiceId: 'serviceId',
  TriagePriority: 'triagePriority',
  PresentingComplaint: 'presentingComplaint',
} as const;

const selectClass =
  'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';

/**
 * ⭐ `components/front-desk/WalkInPanel.tsx` — HO-APT-002 §11.5, **NFR-USA-2** (a walk-in queued in
 * under 60 s, keyboard only). One panel, top to bottom: customer search → (New customer inline) →
 * cat (or quick-add) → service → priority (radio, Routine default) → presenting complaint → **Add
 * to queue**. On success the panel resets and focus returns to the search field. The customer step
 * sits OUTSIDE the `<form>` (it contains its own forms; forms do not nest), and the form's cat/
 * service/priority fields stay disabled until a customer is chosen.
 */
export const WalkInPanel = forwardRef<WalkInPanelHandle, WalkInPanelProps>(function WalkInPanel(
  {
    services,
    selectedCustomer,
    cats,
    onSelectCustomer,
    onClearCustomer,
    onSearch,
    results,
    isSearching,
    searchedTerm,
    renderNewCustomerForm,
    onNewCustomerCreated,
    onQuickAddCat,
    onSubmit,
  },
  ref,
) {
  const searchRef = useRef<HTMLInputElement>(null);
  const refocusSearch = useRef(false);
  const [justAddedCatId, setJustAddedCatId] = useState<string | null>(null);
  const [newCustomerOpen, setNewCustomerOpen] = useState(false);
  const [quickAddOpen, setQuickAddOpen] = useState(false);
  const [extraCats, setExtraCats] = useState<WalkInCat[]>([]);
  const [quickName, setQuickName] = useState('');
  const [quickSex, setQuickSex] = useState<CatSex>('Unknown');
  const [quickError, setQuickError] = useState<string | null>(null);
  const [quickPending, setQuickPending] = useState(false);

  const {
    register,
    handleSubmit,
    setError,
    setValue,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<WalkInValues>({
    resolver: zodResolver(walkInSchema),
    defaultValues: {
      customerId: '',
      catId: '',
      serviceId: '',
      triagePriority: 'Routine',
      presentingComplaint: '',
    },
  });
  const customerId = selectedCustomer?.id ?? '';
  const allCats = [...cats, ...extraCats.filter((extra) => !cats.some((c) => c.id === extra.id))];

  useImperativeHandle(ref, () => ({ focusSearch: () => searchRef.current?.focus() }));

  useEffect(() => {
    setValue('customerId', customerId);
    setValue('catId', '');
    setExtraCats([]);
    setQuickAddOpen(false);
  }, [customerId, setValue]);

  // Choosing a customer removes the focused result button; move on to the next field (the cat)
  // instead of dropping keyboard focus onto the page.
  useEffect(() => {
    if (customerId) document.getElementById('walkin-cat')?.focus();
  }, [customerId]);

  // After a success the picker is back to its search field only once the customer is cleared.
  useEffect(() => {
    if (!customerId && refocusSearch.current) {
      refocusSearch.current = false;
      searchRef.current?.focus();
    }
  }, [customerId]);

  // A quick-added cat can only be selected once its option has rendered.
  useEffect(() => {
    if (justAddedCatId) {
      setValue('catId', justAddedCatId);
      setJustAddedCatId(null);
    }
  }, [justAddedCatId, extraCats, setValue]);

  // One cat: preselect it, so the keyboard path is one stop shorter.
  const onlyCatId = allCats.length === 1 ? allCats[0]!.id : '';
  useEffect(() => {
    if (onlyCatId && !watch('catId')) setValue('catId', onlyCatId);
  }, [onlyCatId, setValue, watch]);

  async function addCat() {
    if (!quickName.trim()) {
      setQuickError('Enter the cat’s name.');
      return;
    }
    setQuickPending(true);
    setQuickError(null);
    try {
      const created = await onQuickAddCat(quickName.trim(), quickSex);
      setExtraCats((current) => [...current, created]);
      setJustAddedCatId(created.id);
      setQuickName('');
      setQuickAddOpen(false);
    } catch {
      setQuickError('The cat could not be added. Please try again.');
    } finally {
      setQuickPending(false);
    }
  }

  async function submit(values: WalkInValues) {
    const done = await submitWithServerErrors(
      async () => {
        await onSubmit(values);
        return true;
      },
      setError,
      { fieldMap: FIELD_MAP, knownFields: Object.values(FIELD_MAP) },
    );
    if (done) {
      refocusSearch.current = true;
      reset();
      onClearCustomer();
    }
  }

  const disabled = !selectedCustomer;

  return (
    <section
      aria-labelledby="walk-in-heading"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <h2 id="walk-in-heading" className="font-display text-lg font-semibold text-ink">
        Register a walk-in
      </h2>

      <CustomerPicker
        ref={searchRef}
        selected={selectedCustomer}
        onSelect={onSelectCustomer}
        onClear={onClearCustomer}
        onSearch={onSearch}
        results={results}
        isSearching={isSearching}
        searchedTerm={searchedTerm}
        onNewCustomer={() => setNewCustomerOpen((open) => !open)}
      />

      {newCustomerOpen && !selectedCustomer && (
        <div className="rounded-[var(--radius-sm)] border border-dashed border-border-strong p-3">
          {renderNewCustomerForm((id) => {
            setNewCustomerOpen(false);
            onNewCustomerCreated(id);
          })}
        </div>
      )}

      <form onSubmit={handleSubmit(submit)} noValidate className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="walkin-cat" className="text-sm font-semibold text-ink">
            Cat
          </label>
          <select
            id="walkin-cat"
            disabled={disabled}
            aria-invalid={!!errors.catId}
            aria-describedby={errors.catId ? 'walkin-cat-error' : undefined}
            className={selectClass}
            {...register('catId')}
          >
            <option value="">
              {allCats.length === 0 && !disabled ? 'No cats yet' : 'Choose…'}
            </option>
            {allCats.map((cat) => (
              <option key={cat.id} value={cat.id}>
                {cat.name}
              </option>
            ))}
          </select>
          {errors.catId && (
            <p id="walkin-cat-error" role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.catId.message}
            </p>
          )}
          {!disabled && (
            <div>
              <Button variant="ghost" size="sm" onClick={() => setQuickAddOpen((open) => !open)}>
                Add a cat
              </Button>
            </div>
          )}
          {quickAddOpen && !disabled && (
            <div className="flex flex-wrap items-end gap-2 rounded-[var(--radius-sm)] border border-dashed border-border-strong p-3">
              <div className="flex flex-col gap-1">
                <label htmlFor="quick-cat-name" className="text-xs font-semibold text-ink">
                  Cat’s name
                </label>
                <input
                  id="quick-cat-name"
                  value={quickName}
                  onChange={(event) => setQuickName(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter') {
                      event.preventDefault();
                      void addCat();
                    }
                  }}
                  className={selectClass}
                />
              </div>
              <div className="flex flex-col gap-1">
                <label htmlFor="quick-cat-sex" className="text-xs font-semibold text-ink">
                  Sex
                </label>
                <select
                  id="quick-cat-sex"
                  value={quickSex}
                  onChange={(event) => setQuickSex(event.target.value as CatSex)}
                  className={selectClass}
                >
                  {CAT_SEX_OPTIONS.map((sex) => (
                    <option key={sex} value={sex}>
                      {sex}
                    </option>
                  ))}
                </select>
              </div>
              <Button size="sm" onClick={() => void addCat()} disabled={quickPending}>
                {quickPending ? 'Adding…' : 'Add cat'}
              </Button>
              {quickError && (
                <p role="alert" className="w-full text-xs font-semibold text-danger-ink">
                  {quickError}
                </p>
              )}
            </div>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="walkin-service" className="text-sm font-semibold text-ink">
            Service
          </label>
          <select
            id="walkin-service"
            disabled={disabled}
            aria-invalid={!!errors.serviceId}
            aria-describedby={errors.serviceId ? 'walkin-service-error' : undefined}
            className={selectClass}
            {...register('serviceId')}
          >
            <option value="">Choose…</option>
            {services.map((service) => (
              <option key={service.id} value={service.id}>
                {service.name}
              </option>
            ))}
          </select>
          {errors.serviceId && (
            <p
              id="walkin-service-error"
              role="alert"
              className="text-xs font-semibold text-danger-ink"
            >
              {errors.serviceId.message}
            </p>
          )}
        </div>

        <fieldset disabled={disabled} className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-sm font-semibold text-ink">Priority</legend>
          {PRIORITY_OPTIONS.map((option) => (
            <label key={option.value} className="flex items-start gap-2 text-sm text-ink">
              <input
                type="radio"
                value={option.value}
                className="mt-0.5"
                {...register('triagePriority')}
              />
              <span>
                <span className="font-semibold">{option.label}</span>
                <span className="text-ink-muted"> — {option.hint}</span>
              </span>
            </label>
          ))}
        </fieldset>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="walkin-complaint" className="text-sm font-semibold text-ink">
            Presenting complaint <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <textarea
            id="walkin-complaint"
            rows={2}
            disabled={disabled}
            aria-invalid={!!errors.presentingComplaint}
            className={selectClass}
            {...register('presentingComplaint')}
          />
          <p className="text-xs text-ink-muted">
            Plain text, up to {MAX_APPOINTMENT_TEXT} characters.
          </p>
          {errors.presentingComplaint && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.presentingComplaint.message}
            </p>
          )}
        </div>

        {errors.root && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            {errors.root.message}
          </p>
        )}
        {errors.customerId && !disabled && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            {errors.customerId.message}
          </p>
        )}

        <div>
          <Button type="submit" disabled={disabled || isSubmitting} aria-busy={isSubmitting}>
            {isSubmitting ? 'Adding…' : 'Add to queue'}
          </Button>
        </div>
      </form>
    </section>
  );
});
