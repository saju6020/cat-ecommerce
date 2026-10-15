import { TriangleAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

/** A high-risk cat on the offline board: a badge with an icon AND the words, plus the handling
 *  note (HO-XCT-002 U-A13). Never colour alone (UI-6). */
export function HighRiskNote({ note }: { note: string | null }) {
  return (
    <span className="mt-1 flex flex-wrap items-center gap-2">
      <Badge variant="danger" icon={<TriangleAlert size={12} aria-hidden="true" />}>
        High risk
      </Badge>
      {note && <span className="text-sm text-ink">{note}</span>}
    </span>
  );
}
