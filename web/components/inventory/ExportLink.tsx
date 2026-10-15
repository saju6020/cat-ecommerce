import { Download } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';

/**
 * `components/inventory/ExportLink.tsx` — HO-IMP-002 §3.1: CSV is downloaded FROM THE SERVER and
 * never generated in the browser. A plain link, used ONLY for the recall trace, which is not
 * branch-scoped. The branch-scoped CSVs use `ExportButton`, which sends `X-Branch-Id`.
 */
export function ExportLink({ href, label = 'Export CSV' }: { href: string; label?: string }) {
  return (
    <a href={href} download className={buttonVariants({ variant: 'secondary', size: 'md' })}>
      <Download size={14} aria-hidden="true" />
      {label}
    </a>
  );
}
