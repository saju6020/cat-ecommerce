import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createRef } from 'react';
import { createEvent, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';
import { ConsentCaptureForm } from '@/components/consent/ConsentCaptureForm';
import { ConsentDetail } from '@/components/consent/ConsentDetail';
import { SignaturePad, type SignaturePadHandle } from '@/components/consent/SignaturePad';
import { getConsent } from '@/lib/api/consents';
import { ids, renderWithClient, freshClinicBeforeEach } from '../visit/helpers';
import { appointmentsFixtureIds } from '../../../mocks/handlers/appointments/fixtures';

freshClinicBeforeEach();

const RECT = {
  left: 0,
  top: 0,
  right: 200,
  bottom: 100,
  width: 200,
  height: 100,
  x: 0,
  y: 0,
  toJSON: () => ({}),
};

beforeEach(() => {
  vi.spyOn(HTMLCanvasElement.prototype, 'getBoundingClientRect').mockReturnValue(RECT);
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
    setTransform: vi.fn(),
    clearRect: vi.fn(),
    beginPath: vi.fn(),
    moveTo: vi.fn(),
    lineTo: vi.fn(),
    stroke: vi.fn(),
  } as unknown as CanvasRenderingContext2D);
});
afterEach(() => vi.restoreAllMocks());

function pointer(
  canvas: Element,
  type: 'pointerDown' | 'pointerMove' | 'pointerUp',
  x: number,
  y: number,
) {
  const event = createEvent[type](canvas, { clientX: x, clientY: y, pointerId: 1, bubbles: true });
  Object.defineProperty(event, 'clientX', { value: x });
  Object.defineProperty(event, 'clientY', { value: y });
  Object.defineProperty(event, 'pointerId', { value: 1 });
  fireEvent(canvas, event);
}

function draw(canvas: Element, points: Array<[number, number]>) {
  pointer(canvas, 'pointerDown', ...points[0]!);
  points.slice(1).forEach(([x, y]) => pointer(canvas, 'pointerMove', x, y));
  pointer(canvas, 'pointerUp', ...points[points.length - 1]!);
}

describe('SignaturePad — ADR-FE-054, US-CLN-21', () => {
  it('U-H1: captures strokes as [x, y] in [0, 1] to 4 decimals; Undo removes the last stroke; Clear removes all', async () => {
    const ref = createRef<SignaturePadHandle>();
    render(<SignaturePad ref={ref} />);
    const canvas = screen.getByRole('img', { name: /signature area/i });
    expect(canvas).toHaveStyle({ touchAction: 'none' });

    draw(canvas, [
      [10, 20],
      [50, 40],
      [100, 50],
      [150, 30],
    ]);
    draw(canvas, [
      [0, 0],
      [200, 100],
      [333, 150],
    ]); // beyond the box clamps to 1
    const strokes = ref.current!.getStrokes();
    expect(strokes).toHaveLength(2);
    expect(strokes[0]![0]).toEqual([0.05, 0.2]);
    expect(strokes[0]![3]).toEqual([0.75, 0.3]);
    expect(strokes[1]![2]).toEqual([1, 1]);
    for (const stroke of strokes)
      for (const [x, y] of stroke) {
        expect(x).toBeGreaterThanOrEqual(0);
        expect(x).toBeLessThanOrEqual(1);
        expect(y).toBeGreaterThanOrEqual(0);
        expect(y).toBeLessThanOrEqual(1);
        expect(String(x).split('.')[1]?.length ?? 0).toBeLessThanOrEqual(4);
      }

    await userEvent.click(screen.getByRole('button', { name: 'Undo stroke' }));
    expect(ref.current!.getStrokes()).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }));
    expect(ref.current!.getStrokes()).toHaveLength(0);
    expect(screen.getByText('Nothing signed yet.')).toBeInTheDocument();
  });

  it('U-H1: fewer than 10 points is "too short"; a single tap is not a stroke', () => {
    const onChange = vi.fn();
    render(<SignaturePad onChange={onChange} />);
    const canvas = screen.getByRole('img', { name: /signature area/i });
    draw(canvas, [[10, 10]]); // a tap
    expect(screen.getByText('Nothing signed yet.')).toBeInTheDocument();
    draw(canvas, [
      [10, 10],
      [20, 20],
      [30, 30],
    ]);
    expect(screen.getByText(/too short/i)).toBeInTheDocument();
    expect(onChange).toHaveBeenLastCalledWith({ strokes: 1, points: 3, isEnough: false });
    draw(canvas, [
      [10, 10],
      [20, 20],
      [30, 30],
      [40, 40],
      [50, 50],
      [60, 60],
      [70, 70],
      [80, 80],
    ]);
    expect(screen.getByText('Signature captured.')).toBeInTheDocument();
  });
});

describe('ConsentCaptureForm — US-CLN-21', () => {
  const baseProps = {
    catId: ids.catMittensId,
    appointmentId: appointmentsFixtureIds.queuedBookedAppointmentId,
    kind: 'Surgery' as const,
    onKindChange: vi.fn(),
    ownerName: 'Sam Rahman',
    ownerCustomerId: 'a1000000-0000-0000-0000-000000000001',
    witnesses: [{ id: 'w1', name: 'Dr Rahman' }],
  };

  it('U-H2: an unpublished form is stated and there is nothing to submit', async () => {
    renderWithClient(<ConsentCaptureForm {...baseProps} kind="Euthanasia" onSubmit={vi.fn()} />);
    expect(
      await screen.findByText(
        'The Euthanasia consent form hasn’t been published yet. Ask an administrator.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Record consent' })).toBeNull();
  });

  it('U-H3: the form body is rendered by LimitedMarkup — a crafted body injects no HTML', async () => {
    server.use(
      http.get('*/api/staff/consent-forms/:kind', () =>
        HttpResponse.json({
          policyKey: 'consent-surgery',
          versionNumber: 4,
          title: 'Consent to surgery',
          body: '## Heading\n\n<img src=x onerror=alert(1)> and <script>alert(2)</script> **bold**\n\n- item',
        }),
      ),
    );
    const { container } = renderWithClient(
      <ConsentCaptureForm {...baseProps} onSubmit={vi.fn()} />,
    );
    expect(await screen.findByText('Version 4')).toBeInTheDocument();
    expect(container.querySelector('img[src="x"]')).toBeNull();
    expect(container.querySelector('script')).toBeNull();
    expect(container.textContent).toContain('<script>alert(2)</script>');
    expect(container.querySelector('strong')).toHaveTextContent('bold');
    expect(container.querySelector('li')).toHaveTextContent('item');
  });

  it('shows the version, pre-fills the signer, validates, confirms, and records the version shown', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    renderWithClient(<ConsentCaptureForm {...baseProps} onSubmit={onSubmit} />);
    expect(await screen.findByText('Version 3')).toBeInTheDocument();
    expect(screen.getByLabelText('Signer’s name')).toHaveValue('Sam Rahman');

    await user.click(screen.getByRole('button', { name: 'Record consent' }));
    expect(
      await screen.findByText('Describe the procedure being consented to.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Sign in the box above, or upload a signed form.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText('Procedure'), 'Spay');
    draw(screen.getByRole('img', { name: /signature area/i }), [
      [1, 1],
      [2, 3],
      [4, 5],
      [6, 7],
      [8, 9],
      [10, 11],
      [12, 13],
      [14, 15],
      [16, 17],
      [18, 19],
    ]);
    await user.click(screen.getByRole('button', { name: 'Record consent' }));
    const confirm = await screen.findByRole('dialog', { name: /record this consent/i });
    expect(within(confirm).getByText(/form version 3/)).toBeInTheDocument();
    await user.click(within(confirm).getByRole('button', { name: 'Record consent' }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    const request = onSubmit.mock.calls[0]![0];
    expect(request).toMatchObject({
      catId: ids.catMittensId,
      kind: 'Surgery',
      method: 'OnScreenSignature',
      signedByName: 'Sam Rahman',
      signerRelationship: 'Owner',
      policyVersionNumber: 3,
    });
    expect(request.signature).toHaveLength(1);
  });

  it('a version that changed under the signer is reported and the wording reloaded', async () => {
    const user = userEvent.setup();
    const { ApiValidationError } = await import('@/lib/api/client');
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'PolicyVersionNumber',
            errorCode: 'ConsentFormVersionNotCurrent',
            errorMessage: '',
            resourceName: 'Consent',
          },
        ],
      }),
    );
    renderWithClient(<ConsentCaptureForm {...baseProps} onSubmit={onSubmit} />);
    await screen.findByText('Version 3');
    await user.type(screen.getByLabelText('Procedure'), 'Spay');
    draw(screen.getByRole('img', { name: /signature area/i }), [
      [1, 1],
      [2, 3],
      [4, 5],
      [6, 7],
      [8, 9],
      [10, 11],
      [12, 13],
      [14, 15],
      [16, 17],
      [18, 19],
    ]);
    await user.click(screen.getByRole('button', { name: 'Record consent' }));
    await user.click(
      within(await screen.findByRole('dialog', { name: /record this consent/i })).getByRole(
        'button',
        { name: 'Record consent' },
      ),
    );
    expect(
      await screen.findByText(/The consent wording has changed since this page opened/),
    ).toBeInTheDocument();
    expect(screen.queryByText('ConsentFormVersionNotCurrent')).toBeNull();
  });

  it('the upload alternative needs a file; the signature tab is described by the upload option', async () => {
    const user = userEvent.setup();
    renderWithClient(<ConsentCaptureForm {...baseProps} onSubmit={vi.fn()} />);
    await screen.findByText('Version 3');
    expect(screen.getByRole('img', { name: /signature area/i })).toHaveAttribute(
      'aria-describedby',
      'consent-pad-help',
    );
    expect(document.getElementById('consent-pad-help')).toHaveTextContent(/Upload signed form/);
    await user.click(screen.getByRole('tab', { name: 'Upload signed form' }));
    await user.type(screen.getByLabelText('Procedure'), 'Spay');
    await user.click(screen.getByRole('button', { name: 'Record consent' }));
    expect(await screen.findByText('Upload the signed form first.')).toBeInTheDocument();
  });
});

describe('ConsentDetail — US-CLN-21', () => {
  it('shows what was signed, the stored signature as an image, and withdraws with a required reason', async () => {
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: () => 'blob:sig', revokeObjectURL: vi.fn() }));
    const user = userEvent.setup();
    const consent = await getConsent(ids.consentRecordedId);
    const onWithdraw = vi.fn().mockResolvedValue(undefined);
    render(<ConsentDetail consent={consent} canWithdraw onWithdraw={onWithdraw} />);
    expect(screen.getByText('Version 3')).toBeInTheDocument();
    expect(
      (await screen.findByRole('img', { name: /signature of sam rahman/i })).getAttribute('src'),
    ).toBe('blob:sig');
    await user.click(screen.getByRole('button', { name: 'Withdraw consent' }));
    const dialog = await screen.findByRole('dialog', { name: /withdraw this consent/i });
    await user.click(within(dialog).getByRole('button', { name: 'Withdraw consent' }));
    expect(
      await within(dialog).findByText('Say why the consent is being withdrawn.'),
    ).toBeInTheDocument();
    expect(onWithdraw).not.toHaveBeenCalled();
    await user.type(within(dialog).getByLabelText('Reason'), 'Owner changed their mind');
    await user.click(within(dialog).getByRole('button', { name: 'Withdraw consent' }));
    await waitFor(() => expect(onWithdraw).toHaveBeenCalledWith('Owner changed their mind'));
  });

  it('a withdrawn consent shows when, by whom and why, and offers no further withdrawal', async () => {
    const consent = await getConsent(ids.consentWithdrawnId);
    render(<ConsentDetail consent={consent} canWithdraw onWithdraw={vi.fn()} />);
    expect(screen.getByText('Owner postponed the procedure.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Withdraw consent' })).toBeNull();
  });
});
