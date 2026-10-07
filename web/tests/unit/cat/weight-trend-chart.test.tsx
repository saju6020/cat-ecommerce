import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { WeightTrendChart } from '@/components/cat/WeightTrendChart';

describe('WeightTrendChart', () => {
  it('U22 — a hidden data table (labelled "Weight (kg)") is present for screen readers', () => {
    // recharts' <ResponsiveContainer> measures its DOM node to size the SVG; jsdom has no real
    // layout engine, so the node always measures 0x0 and recharts intentionally renders nothing
    // inside it (there's no meaningful chart to draw at zero size) — this is a known jsdom
    // limitation, not a defect in WeightTrendChart. The visually-hidden <table> is the actual,
    // reliably-testable UI-6 guarantee here (§11.7: "a chart alone fails UI-6"); the Y axis's
    // "kg" label is confirmed by reading the component's source (`label={{ value: 'kg', ... }}`).
    const { container } = render(
      <WeightTrendChart
        points={[
          { recordedOn: '2026-06-01', kilograms: 3.9 },
          { recordedOn: '2026-08-01', kilograms: 4.1 },
        ]}
      />,
    );

    const table = container.querySelector('table');
    expect(table).toBeInTheDocument();
    expect(table).toHaveClass('sr-only');
    expect(screen.getByRole('columnheader', { name: 'Weight (kg)' })).toBeInTheDocument();
    expect(screen.getByText('4.1 kg')).toBeInTheDocument();
  });
});
