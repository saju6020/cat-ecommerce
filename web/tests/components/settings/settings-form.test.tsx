import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SettingsForm } from '@/components/settings/SettingsForm';
import type { SettingDefinitionView } from '@/lib/api/admin-settings';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

describe('SettingsForm — HO-ADM-002 §11.5, US-ADM-05', () => {
  it('U-E1: renders a brand-new, previously-unknown group — nothing hardcoded', () => {
    const definitions: SettingDefinitionView[] = [
      {
        key: 'surprise.newSetting',
        group: 'Surprise',
        type: 'Integer',
        unit: null,
        min: 0,
        max: 10,
        default: '5',
        branchOverridable: false,
        globalValue: '5',
        branchValue: null,
        effectiveValue: '5',
      },
    ];
    render(
      <SettingsForm
        definitions={definitions}
        scope="global"
        onSave={vi.fn()}
        onClearOverride={vi.fn()}
        isPending={false}
      />,
    );
    expect(screen.getByText('Surprise')).toBeInTheDocument();
    expect(screen.getByLabelText(/new setting/i)).toBeInTheDocument();
  });

  it('U-E2/U-E3: branch scope shows the global value beside an override, "Use global value" clears it via the callback, and Save sends only changed keys with a from → to confirm', async () => {
    const definitions: SettingDefinitionView[] = [
      {
        key: 'booking.slotHoldMinutes',
        group: 'Booking',
        type: 'Integer',
        unit: 'minutes',
        min: 2,
        max: 30,
        default: '10',
        branchOverridable: true,
        globalValue: '10',
        branchValue: '15',
        effectiveValue: '15',
      },
      {
        key: 'security.staffIdleTimeoutMinutes',
        group: 'Security',
        type: 'Integer',
        unit: 'minutes',
        min: 5,
        max: 240,
        default: '30',
        branchOverridable: false,
        globalValue: '30',
        branchValue: null,
        effectiveValue: '30',
      },
    ];
    const onSave = vi.fn().mockResolvedValue(undefined);
    const onClearOverride = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <SettingsForm
        definitions={definitions}
        scope={{ branchId: 'b1' }}
        onSave={onSave}
        onClearOverride={onClearOverride}
        isPending={false}
      />,
    );

    expect(screen.getByText(/global value: 10/i)).toBeInTheDocument();
    // The non-overridable setting is read-only in branch scope, with a note.
    expect(screen.getByLabelText(/idle timeout/i)).toHaveAttribute('readonly');
    expect(screen.getByText(/cannot be overridden per branch/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /use global value/i }));
    expect(onClearOverride).toHaveBeenCalledWith('booking.slotHoldMinutes');

    const slotHoldInput = screen.getByLabelText(/slot hold minutes/i);
    await user.clear(slotHoldInput);
    await user.type(slotHoldInput, '20');
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/15 → 20/)).toBeInTheDocument();
    expect(
      within(dialog).queryByText(/staffIdleTimeoutMinutes|idle timeout/i),
    ).not.toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: /save changes/i }));
    expect(onSave).toHaveBeenCalledWith([{ key: 'booking.slotHoldMinutes', value: '20' }]);
  });
});
