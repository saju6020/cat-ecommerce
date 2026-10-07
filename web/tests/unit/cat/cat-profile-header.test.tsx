import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CatProfileHeader } from '@/components/cat/CatProfileHeader';
import type { CatProfile } from '@/lib/api/cats';

function makeCat(overrides: Partial<CatProfile> = {}): CatProfile {
  return {
    id: 'cat-1',
    ownerId: 'owner-1',
    name: 'Whiskers',
    species: 'Cat',
    sex: 'Female',
    dateOfBirth: '2021-03-14',
    dateOfBirthPrecision: 'Exact',
    breed: 'British Shorthair',
    colourAndMarkings: 'Blue',
    neuterStatus: 'Neutered',
    neuteredOn: '2021-09-01',
    isHighRisk: false,
    handlingNote: null,
    lifeStatus: 'Alive',
    deceasedOn: null,
    deceasedNote: null,
    dietaryNotes: null,
    behaviouralNotes: null,
    latestWeightKg: 4.2,
    hasRecordedAllergies: false,
    allergies: [],
    chronicConditions: [],
    medications: [],
    photos: [],
    ...overrides,
  };
}

describe('CatProfileHeader', () => {
  it('U4 — renders every allergen when the cat has allergies', () => {
    render(
      <CatProfileHeader
        cat={makeCat({
          hasRecordedAllergies: true,
          allergies: [
            {
              id: 'a1',
              allergen: 'Chicken',
              reaction: 'Vomiting',
              severity: 'Moderate',
              recordedOn: '2025-01-01',
            },
            {
              id: 'a2',
              allergen: 'Beef',
              reaction: 'Itching',
              severity: 'Mild',
              recordedOn: '2025-01-01',
            },
          ],
        })}
      />,
    );
    expect(screen.getByText('Chicken')).toBeInTheDocument();
    expect(screen.getByText('Beef')).toBeInTheDocument();
  });

  it('U5 — renders "None recorded" when hasRecordedAllergies is false (US-CAT-03 AC2)', () => {
    render(<CatProfileHeader cat={makeCat({ hasRecordedAllergies: false, allergies: [] })} />);
    const noneRecorded = screen.getAllByText('None recorded');
    expect(noneRecorded.length).toBeGreaterThan(0);
  });

  it('U6 — high-risk cat: badge and handling note present, before the name in DOM order (US-CAT-04 AC1)', () => {
    const { container } = render(
      <CatProfileHeader
        cat={makeCat({ isHighRisk: true, handlingNote: 'Bites when restrained.' })}
      />,
    );
    expect(screen.getByText('High risk — handle with care')).toBeInTheDocument();
    expect(screen.getByText('Bites when restrained.')).toBeInTheDocument();

    const banner = screen.getByText('High risk — handle with care');
    const heading = screen.getByRole('heading', { level: 1, name: 'Whiskers' });
    // compareDocumentPosition: DOCUMENT_POSITION_FOLLOWING (4) means `heading` follows `banner`.
    const position = banner.compareDocumentPosition(heading);
    expect(Boolean(position & Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true);
    expect(container).toBeInTheDocument();
  });

  it('U7 — not high-risk: no badge', () => {
    render(<CatProfileHeader cat={makeCat({ isHighRisk: false })} />);
    expect(screen.queryByText('High risk — handle with care')).not.toBeInTheDocument();
  });

  it('U8 — deceased cat: deceased banner with the date', () => {
    render(
      <CatProfileHeader
        cat={makeCat({
          lifeStatus: 'Deceased',
          deceasedOn: '2026-01-10',
          deceasedNote: 'Passed peacefully.',
        })}
      />,
    );
    expect(screen.getByText(/recorded as deceased/i)).toHaveTextContent('10 Jan 2026');
  });

  it('U9 — latest weight rendered with "kg" (UI-8)', () => {
    render(<CatProfileHeader cat={makeCat({ latestWeightKg: 4.2 })} />);
    expect(screen.getByText('4.2 kg')).toBeInTheDocument();
  });

  it('renders "Not recorded" when there is no weight yet', () => {
    render(<CatProfileHeader cat={makeCat({ latestWeightKg: null })} />);
    expect(screen.getByText('Not recorded')).toBeInTheDocument();
  });
});
