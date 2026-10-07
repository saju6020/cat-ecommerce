'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { submitSection } from '@/lib/shop/submitSection';
import type { StaffProductDetail, StaffTranslation } from '@/lib/api/staff-shop';
import { Field, SectionFrame, inputClass } from './SectionFrame';

const LANGUAGE_NAMES: Record<string, string> = { en: 'English', bn: 'Bangla' };

interface Values {
  name: string;
  description: string;
  ingredientList: string;
  feedingGuide: string;
}

function LocaleForm({
  locale,
  existing,
  product,
  onSave,
  onRemove,
  onReload,
}: {
  locale: string;
  existing?: StaffTranslation;
  product: StaffProductDetail;
  onSave: (locale: string, values: Values) => Promise<unknown>;
  onRemove: (locale: string) => Promise<unknown>;
  onReload: () => void;
}) {
  const language = LANGUAGE_NAMES[locale] ?? locale;
  const [confirm, setConfirm] = React.useState(false);
  const { register, handleSubmit, setError, watch, reset, formState } = useForm<Values>({
    defaultValues: {
      name: existing?.name ?? '',
      description: existing?.description ?? '',
      ingredientList: existing?.ingredientList ?? '',
      feedingGuide: existing?.feedingGuide ?? '',
    },
  });
  // Removing the translation empties its form (the editor itself is not remounted, S9).
  const hadTranslation = React.useRef(!!existing);
  React.useEffect(() => {
    if (hadTranslation.current && !existing) {
      reset({ name: '', description: '', ingredientList: '', feedingGuide: '' });
    }
    hadTranslation.current = !!existing;
  }, [existing, reset]);
  const description = watch('description');
  const feeding = watch('feedingGuide');
  return (
    <>
      <SectionFrame
        title={`${language} (${locale})`}
        description={`The default text is shown beside each field for reference. Leave a field empty to fall back to ${LANGUAGE_NAMES.en}.`}
        errors={formState.errors}
        isDirty={formState.isDirty}
        isSubmitting={formState.isSubmitting}
        isSubmitSuccessful={formState.isSubmitSuccessful}
        onReload={onReload}
        saveLabel={`Save ${language}`}
        onSubmit={handleSubmit(async (values) => {
          const ok = await submitSection(() => onSave(locale, values), setError, [
            'name',
            'description',
            'ingredientList',
            'feedingGuide',
          ]);
          if (ok) reset(values);
        })}
      >
        <Field id={`tr-${locale}-name`} label="Name" hint={<>Default: {product.name}</>}>
          <input
            id={`tr-${locale}-name`}
            lang={locale}
            className={inputClass}
            {...register('name')}
          />
        </Field>
        <Field
          id={`tr-${locale}-desc`}
          label="Description"
          hint={<>Default: {product.description || 'none'}</>}
        >
          <textarea
            id={`tr-${locale}-desc`}
            lang={locale}
            rows={5}
            className={inputClass}
            {...register('description')}
          />
        </Field>
        <div
          role="group"
          aria-label={`${language} description preview`}
          className="rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm"
        >
          <LimitedMarkup body={description || ''} className="space-y-2" />
        </div>
        <Field id={`tr-${locale}-ing`} label="Ingredient list" hint="Plain text.">
          <textarea
            id={`tr-${locale}-ing`}
            lang={locale}
            rows={3}
            className={inputClass}
            {...register('ingredientList')}
          />
        </Field>
        <Field id={`tr-${locale}-feed`} label="Feeding guide">
          <textarea
            id={`tr-${locale}-feed`}
            lang={locale}
            rows={4}
            className={inputClass}
            {...register('feedingGuide')}
          />
        </Field>
        <div
          role="group"
          aria-label={`${language} feeding guide preview`}
          className="rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm"
        >
          <LimitedMarkup body={feeding || ''} className="space-y-2" />
        </div>
      </SectionFrame>
      {existing && (
        <Button variant="ghost" className="self-start" onClick={() => setConfirm(true)}>
          Remove translation ({locale})
        </Button>
      )}
      <ConfirmDialog
        open={confirm}
        onOpenChange={setConfirm}
        title={`Remove the ${language} translation?`}
        consequence={`Customers reading ${language} will see the default text instead of ${language} for ${product.name}.`}
        confirmLabel="Remove translation"
        variant="destructive"
        onConfirm={async () => {
          setConfirm(false);
          await onRemove(locale);
        }}
      />
    </>
  );
}

export interface TranslationsEditorProps {
  product: StaffProductDetail;
  defaultLocale: string;
  locales: string[];
  onSave: (locale: string, values: Values) => Promise<unknown>;
  onRemove: (locale: string) => Promise<unknown>;
  onReload: () => void;
}

/**
 * HO-SHP-002 §11.6 `TranslationsEditor` — US-SHP-14 AC3. HIDDEN with one enabled language; with more,
 * one sub-form per non-default locale (name, description, ingredient list, feeding guide) beside the
 * default text, plus **Remove translation** (confirmed).
 */
export function TranslationsEditor({
  product,
  defaultLocale,
  locales,
  ...rest
}: TranslationsEditorProps) {
  const others = locales.filter((l) => l !== defaultLocale);
  if (others.length === 0) return null;
  return (
    <div className="flex flex-col gap-8">
      {others.map((locale) => (
        <LocaleForm
          key={locale}
          locale={locale}
          product={product}
          existing={product.translations?.find((t) => t.locale === locale)}
          {...rest}
        />
      ))}
    </div>
  );
}
