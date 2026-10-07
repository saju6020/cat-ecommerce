import { BannerStrip } from '@/components/shop/BannerStrip';
import { LanguageSwitcher } from '@/components/shop/LanguageSwitcher';
import { ShopFooter } from '@/components/shop/ShopFooter';
import { ShopHeader } from '@/components/shop/ShopHeader';
import {
  getBannersServer,
  getCategoriesServer,
  getLocalesServer,
  getSystemStatusServer,
} from '@/lib/api/shop-catalogue';
import { settle } from '@/lib/shop/helpers';
import { MaintenanceGate } from './_components/MaintenanceGate';
import { ShopAccountLinks } from './_components/ShopAccountLinks';

// Policy and help pages published through HO-ADM-002 (there is no list endpoint to enumerate them).
const FOOTER_LINKS = [
  { label: 'FAQs', href: '/faqs' },
  { label: 'Terms', href: '/policies/terms' },
];

/**
 * `(shop)` — HO-SHP-002 §8.1, ADR-FE-029: the PUBLIC storefront group. Its own shell (header with
 * search and categories, banners, maintenance gate, footer), no session guard — a guest browses
 * freely — and the warm customer layer (`data-theme="customer"`) like the portal. Server
 * Component: the shell's data comes from public endpoints through `getPublic` (ADR-FE-030). Every
 * optional read degrades quietly (no nav / no banners) rather than failing the page.
 */
export default async function ShopLayout({ children }: { children: React.ReactNode }) {
  const [status, categories, locales, banners] = await Promise.all([
    settle(getSystemStatusServer()),
    settle(getCategoriesServer()),
    settle(getLocalesServer()),
    settle(getBannersServer('Shop')),
  ]);

  const initialStatus = status.ok ? status.value : { maintenanceMode: false, message: null };
  const showLanguages = locales.ok && locales.value.locales.length > 1;

  return (
    <div data-theme="customer" className="flex min-h-screen flex-col bg-surface-page">
      <ShopHeader
        categories={categories.ok ? categories.value : []}
        accountSlot={<ShopAccountLinks />}
        languageSlot={
          showLanguages && locales.ok ? (
            <LanguageSwitcher
              defaultLocale={locales.value.defaultLocale}
              locales={locales.value.locales}
            />
          ) : null
        }
      />
      <main id="main-content" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 md:px-8">
        <MaintenanceGate initial={initialStatus}>
          {banners.ok && banners.value.length > 0 && (
            <div className="mb-6">
              <BannerStrip banners={banners.value} />
            </div>
          )}
          {children}
        </MaintenanceGate>
      </main>
      <ShopFooter links={FOOTER_LINKS} />
    </div>
  );
}
