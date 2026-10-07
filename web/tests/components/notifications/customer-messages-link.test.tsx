import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CustomerMessagesLink } from '@/components/notifications/CustomerMessagesLink';

describe('CustomerMessagesLink — HO-NTF-002 §11.5', () => {
  it.each([['Administrator'], ['Reception']])(
    'is shown to %s and filters the log to the customer',
    (role) => {
      render(<CustomerMessagesLink customerId="c-1" roleIds={[role]} />);
      expect(screen.getByRole('link', { name: 'Messages' })).toHaveAttribute(
        'href',
        '/staff/notifications?recipientId=c-1',
      );
    },
  );

  it.each([['Nurse'], ['Vet'], ['Inventory']])(
    'is hidden from %s (the server still decides)',
    (role) => {
      render(<CustomerMessagesLink customerId="c-1" roleIds={[role]} />);
      expect(screen.queryByRole('link', { name: 'Messages' })).not.toBeInTheDocument();
    },
  );
});
