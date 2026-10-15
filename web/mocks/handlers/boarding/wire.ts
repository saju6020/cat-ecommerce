import type { BoardingStayBase, CareProfile } from '@/lib/api/types';

/**
 * `mocks/handlers/boarding/wire.ts` — HO-BRD-002 §12. The mock stores the shapes the screens use and
 * emits the BUILT wire shapes of HO-BRD-001 §1.1 (feedings carry `time`; the spending limit is a plain
 * string; a stay has `unit`, `deposit.dueBy/received`, per-cat `gate`, `needsAttention`), so the
 * adapters in `lib/api/my-boarding.ts` are exercised end to end. (Not imported from `lib/api`: that
 * would load the client while `mocks/server` is still being imported — see `appointments/fixtures.ts`.)
 */
type Wire = Record<string, unknown>;

export function profileToWire(profile: CareProfile | null): Wire | null {
  if (!profile) return null;
  return {
    ...profile,
    feedings: profile.feedings.map(({ localTime, ...rest }) => ({ time: localTime, ...rest })),
    vetSpendingLimit: profile.vetSpendingLimit.amount,
  };
}

export function profileFromWire(raw: Wire, currencyCode = 'BDT'): CareProfile {
  const limit = raw.vetSpendingLimit;
  return {
    ...(raw as unknown as CareProfile),
    feedings: ((raw.feedings as Wire[]) ?? []).map((f) => ({
      localTime: String(f.time ?? f.localTime ?? ''),
      portion: String(f.portion ?? ''),
      foodSource: f.foodSource as CareProfile['feedings'][number]['foodSource'],
      foodDescription: String(f.foodDescription ?? ''),
      houseFoodSkuId: (f.houseFoodSkuId as string | null | undefined) ?? null,
    })),
    vetSpendingLimit:
      typeof limit === 'object' && limit !== null
        ? (limit as CareProfile['vetSpendingLimit'])
        : { amount: String(limit ?? '0.00'), currencyCode },
  };
}

/** The internal stay view re-shaped as the built contract sends it. */
export function stayToBuilt<T extends BoardingStayBase>(view: T): Wire {
  const { unitName, unitBlock, cats, deposit, charges, invoices, allowedTransitions, ...rest } =
    view as BoardingStayBase & Wire;
  void invoices; // the built stay carries no invoices: they come from the summary
  void unitBlock;
  const attention = (rest as Wire).attention as
    { needsAttention: boolean; reason: string | null } | undefined;
  const { attention: _attention, canOverrideGate: _can, ...kept } = rest as Wire;
  void _attention;
  void _can;
  return {
    ...kept,
    allowedTransitions: allowedTransitions
      .filter((t) => t !== 'Extend' && t !== 'Relocate')
      .map((t) => (t === 'RecordNoShow' ? 'NoShow' : t)),
    charges: charges.map((c) => ({
      chargeId: c.id,
      lineRef: `${c.kind}.${c.id}`,
      description: c.description,
      net: c.amount,
      serviceDate: null,
      billed: !!c.invoiceId,
      voided: false,
    })),
    unit: unitName
      ? { unitId: null, code: unitName, name: unitName, unitType: view.requestedUnitType }
      : null,
    needsAttention: attention?.needsAttention ?? false,
    attentionReason: attention?.reason ?? null,
    deposit: {
      required: deposit.required,
      state: deposit.state,
      dueBy: deposit.payBy,
      received: deposit.accepted,
      outstanding: deposit.outstanding,
      instructionsPath: deposit.instructionsPath,
    },
    cats: cats.map((c) => ({
      catId: c.catId,
      catName: c.catName,
      catPhotoUrl: c.catPhotoUrl,
      catIsHighRisk: c.catIsHighRisk,
      handlingNote: c.handlingNote,
      gate: {
        status: c.gateStatus,
        reasons: c.gateReasons,
        overridden: c.gateStatus === 'Overridden',
        overriddenBy: c.gateOverriddenBy,
        overrideReason: c.gateOverrideReason,
      },
      careProfile: profileToWire(c.careProfile),
      suspectedInfectious: c.suspectedInfectious,
      awayFromUnit: c.awayFromUnit,
      awayReason: c.awayReason,
      checkIn: c.checkedInWeightKg
        ? {
            weightKg: c.checkedInWeightKg,
            belongings: (c.belongings ?? []).map((b) => ({ ...b, fileIds: [] })),
          }
        : null,
    })),
  };
}
