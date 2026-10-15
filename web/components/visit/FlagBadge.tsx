import { ArrowDown, ArrowUp, CircleAlert, CircleCheck, CircleHelp } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { formatFlag } from '@/lib/format/clinical';
import type { ResultFlag } from '@/lib/api/diagnostics';

/**
 * `FlagBadge` — HO-EMR-002 §8.4/§11.3, UI-6. A result flag is TEXT plus an ICON ("High ↑"), never
 * colour alone: the colour variant only reinforces what the words and the icon already say (U-C2).
 */
const CONFIG: Record<
  ResultFlag,
  { variant: 'danger' | 'warning' | 'success' | 'neutral'; icon: React.ReactNode }
> = {
  High: { variant: 'danger', icon: <ArrowUp size={12} aria-hidden="true" /> },
  Low: { variant: 'warning', icon: <ArrowDown size={12} aria-hidden="true" /> },
  Abnormal: { variant: 'warning', icon: <CircleAlert size={12} aria-hidden="true" /> },
  Normal: { variant: 'success', icon: <CircleCheck size={12} aria-hidden="true" /> },
  NotAssessed: { variant: 'neutral', icon: <CircleHelp size={12} aria-hidden="true" /> },
};

export function FlagBadge({ flag, className }: { flag: ResultFlag; className?: string }) {
  const config = CONFIG[flag] ?? CONFIG.NotAssessed;
  return (
    <Badge variant={config.variant} icon={config.icon} className={className}>
      {formatFlag(flag)}
    </Badge>
  );
}
