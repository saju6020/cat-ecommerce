import type { NavArea } from '../types';

/**
 * Shop navigation entries.
 *
 * Owned entirely by the Shop area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const shopNav: NavArea = {
  key: 'shop',
  label: 'Shop',
  audience: 'both',
  items: [
    // HO-SHP-002 §13: three portal entries and one staff entry (the registry, not the layouts, holds nav).
    { label: 'Shop', href: '/shop', icon: 'store', audience: 'portal' },
    { label: 'Wishlist', href: '/portal/wishlist', icon: 'heart', audience: 'portal' },
    { label: 'My reviews', href: '/portal/reviews', icon: 'star', audience: 'portal' },
    { label: 'Shop', href: '/staff/shop/products', icon: 'store', audience: 'staff' },
  ],
};
