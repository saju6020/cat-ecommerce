import Link from 'next/link';

export interface FooterLink {
  label: string;
  href: string;
}

/** HO-SHP-002 §11.1 `ShopFooter` — links to the published policy and help pages (HO-ADM-002). */
export function ShopFooter({ links }: { links: FooterLink[] }) {
  return (
    <footer className="mt-12 border-t border-border bg-surface px-4 py-6 md:px-8">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 text-sm text-ink-muted">
        <p>© PurrCare — cat clinic, boarding, pharmacy and shop.</p>
        {links.length > 0 && (
          <nav aria-label="Help and policies" className="flex flex-wrap gap-4">
            {links.map((link) => (
              <Link key={link.href} href={link.href} className="text-ink-muted underline">
                {link.label}
              </Link>
            ))}
          </nav>
        )}
      </div>
    </footer>
  );
}
