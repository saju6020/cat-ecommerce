import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import { CatForm } from '@/components/cat/CatForm';
import { NeuterStatusForm } from '@/components/cat/NeuterStatusForm';
import { WeightEntryForm } from '@/components/cat/WeightEntryForm';
import { MedicalProfileForm } from '@/components/cat/MedicalProfileForm';
import { VaccinationForm } from '@/components/cat/VaccinationForm';
import { ParasiteTreatmentForm } from '@/components/cat/ParasiteTreatmentForm';
import { VaccineForm } from '@/components/cat/VaccineForm';

/** U31 — every input in every Cat Profiles form has an associated `<label>` (UI-6). */
function expectEveryFieldToHaveALabel(container: HTMLElement) {
  const fields = container.querySelectorAll('input, select, textarea');
  expect(fields.length).toBeGreaterThan(0);

  for (const field of Array.from(fields)) {
    const id = field.getAttribute('id');
    const hasAriaLabel = field.hasAttribute('aria-label') || field.hasAttribute('aria-labelledby');
    const hasAssociatedLabel = !!id && !!container.querySelector(`label[for="${id}"]`);
    // The date-of-birth-precision radios are implicitly labelled by nesting inside a <label>
    // (CatForm.tsx) rather than an explicit htmlFor/id pair — both are valid per WCAG 1.3.1/4.1.2.
    const hasImplicitLabel = !!field.closest('label');
    expect(
      hasAriaLabel || hasAssociatedLabel || hasImplicitLabel,
      `Field ${field.tagName}#${id ?? '(no id)'} has no accessible label`,
    ).toBe(true);
  }
}

const noop = vi.fn().mockResolvedValue(undefined);

describe('Every Cat Profiles form — U31', () => {
  it('CatForm', () => {
    const { container } = render(<CatForm mode="register" onSubmit={noop} />);
    expectEveryFieldToHaveALabel(container);
  });

  it('NeuterStatusForm', () => {
    const { container } = render(
      <NeuterStatusForm onSubmit={noop} defaultValues={{ neuterStatus: 'Neutered' }} />,
    );
    expectEveryFieldToHaveALabel(container);
  });

  it('WeightEntryForm', () => {
    const { container } = render(<WeightEntryForm onSubmit={noop} />);
    expectEveryFieldToHaveALabel(container);
  });

  it('MedicalProfileForm', () => {
    const { container } = render(
      <MedicalProfileForm
        onSubmit={noop}
        defaultValues={{
          allergies: [{ allergen: '', reaction: '', severity: 'Mild', recordedOn: '' }],
          chronicConditions: [{ condition: '', diagnosedOn: '', notes: '' }],
          medications: [
            { medicationName: '', dosage: '', frequency: '', startedOn: '', endsOn: '' },
          ],
        }}
      />,
    );
    expectEveryFieldToHaveALabel(container);
  });

  it('VaccinationForm', () => {
    const { container } = render(
      <VaccinationForm
        vaccines={[
          {
            id: 'v1',
            code: 'RABIES',
            name: 'Rabies',
            isCore: true,
            validityMonths: 12,
            isActive: true,
          },
        ]}
        onSubmit={noop}
      />,
    );
    expectEveryFieldToHaveALabel(container);
  });

  it('ParasiteTreatmentForm', () => {
    const { container } = render(<ParasiteTreatmentForm onSubmit={noop} />);
    expectEveryFieldToHaveALabel(container);
  });

  it('VaccineForm', () => {
    const { container } = render(<VaccineForm onSubmit={noop} />);
    expectEveryFieldToHaveALabel(container);
  });
});
