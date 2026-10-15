import type { SurgeryDetail } from '@/lib/api/surgeries';
import { PreOpChecklist } from './PreOpChecklist';
import { StartPanel } from './StartPanel';

/** The Pre-op tab: admission and the start gate, then the checklist (HO-SRG-002 §11.3). */
export function PreOpTab({ surgery }: { surgery: SurgeryDetail }) {
  return (
    <div className="flex flex-col gap-6">
      <StartPanel surgery={surgery} />
      <PreOpChecklist surgery={surgery} />
    </div>
  );
}
