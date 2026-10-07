import { Badge } from '@/components/ui/badge';
import type { StaffRoleCatalogue } from '@/lib/api/staff-admin';

/**
 * `components/admin/CapabilityMatrixPreview.tsx` — ⭐ US-ACC-07, HO-ACC-002 §11.10, ADR-FE-009,
 * SRS Appendix D. The union of the SELECTED roles' access, computed from the API's own catalogue
 * data — never a hardcoded copy. Captioned "Indicative" per the handover's wording.
 */
export function CapabilityMatrixPreview({
  catalogue,
  selectedRoleIds,
}: {
  catalogue: StaffRoleCatalogue;
  selectedRoleIds: string[];
}) {
  if (selectedRoleIds.length === 0) {
    return <p className="text-sm text-ink-muted">Select a role to preview its capabilities.</p>;
  }

  return (
    <div className="flex flex-col gap-2">
      <table className="w-full border-collapse text-sm">
        <caption className="text-left text-xs text-ink-muted">
          Indicative — from SRS Appendix D
        </caption>
        <thead>
          <tr>
            <th scope="col" className="border-b border-border p-2 text-left font-semibold text-ink">
              Capability
            </th>
            <th scope="col" className="border-b border-border p-2 text-left font-semibold text-ink">
              Access
            </th>
          </tr>
        </thead>
        <tbody>
          {catalogue.capabilities.map((capability) => {
            const cells = catalogue.cells.filter(
              (cell) => cell.capability === capability && selectedRoleIds.includes(cell.roleId),
            );
            const hasFull = cells.some((cell) => cell.access !== 'None');
            return (
              <tr key={capability}>
                <th
                  scope="row"
                  className="border-b border-border p-2 text-left font-normal text-ink"
                >
                  {capability}
                </th>
                <td className="border-b border-border p-2">
                  <Badge variant={hasFull ? 'success' : 'neutral'}>
                    {hasFull ? cells.find((c) => c.access !== 'None')!.access : 'None'}
                  </Badge>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
