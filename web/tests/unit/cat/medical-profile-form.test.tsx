import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MedicalProfileForm } from '@/components/cat/MedicalProfileForm';

/**
 * CODE_REVIEW gate MEDIUM finding on HO-CAT-002 — repeatable-row validation errors (allergies'
 * `reaction`/`recordedOn`, chronic conditions' `condition`, medications' `medicationName`/
 * `dosage`/`frequency`/`startedOn`) were computed by zod but never rendered, so a blocked
 * submission gave no visible or announced reason (UI-6). This proves each one is now rendered and
 * associated with its field via `aria-invalid`/`aria-describedby`, the same wiring
 * `WeightEntryForm`'s `recordedOn` field uses elsewhere in this app.
 */
describe('MedicalProfileForm — blocked submission with empty repeatable rows', () => {
  it('renders and announces every required-field error, and never calls onSubmit', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<MedicalProfileForm onSubmit={onSubmit} />);

    await userEvent.click(screen.getByRole('button', { name: /add allergy/i }));
    await userEvent.click(screen.getByRole('button', { name: /add condition/i }));
    await userEvent.click(screen.getByRole('button', { name: /add medication/i }));

    await userEvent.click(screen.getByRole('button', { name: /save medical profile/i }));

    // Allergies — allergen (already rendered pre-fix), reaction, recordedOn.
    const allergen = await screen.findByLabelText('Allergen');
    const reaction = screen.getByLabelText('Reaction');
    const allergyRecordedOn = screen.getByLabelText('Recorded on');

    // Chronic conditions — condition.
    const condition = screen.getByLabelText('Condition');

    // Medications — medicationName, dosage, frequency, startedOn.
    const medicationName = screen.getByLabelText('Medication');
    const dosage = screen.getByLabelText('Dosage');
    const frequency = screen.getByLabelText('Frequency');
    const startedOn = screen.getByLabelText('Started on');

    const fieldsAndMessages: Array<[HTMLElement, RegExp]> = [
      [allergen, /enter the allergen/i],
      [reaction, /enter the reaction/i],
      [allergyRecordedOn, /enter the date this was recorded/i],
      [condition, /enter the condition/i],
      [medicationName, /enter the medication name/i],
      [dosage, /enter the dosage/i],
      [frequency, /enter the frequency/i],
      [startedOn, /enter the start date/i],
    ];

    for (const [field, messagePattern] of fieldsAndMessages) {
      expect(field).toHaveAttribute('aria-invalid', 'true');
      const describedBy = field.getAttribute('aria-describedby');
      expect(describedBy).toBeTruthy();
      const errorElement = document.getElementById(describedBy!);
      expect(errorElement).not.toBeNull();
      expect(errorElement).toHaveTextContent(messagePattern);
      expect(errorElement).toHaveAttribute('role', 'alert');
    }

    expect(onSubmit).not.toHaveBeenCalled();
  });
});
