import type { NavArea } from '../types';

/**
 * Subscriptions navigation entries.
 *
 * Owned entirely by the Subscriptions area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const subscriptionsNav: NavArea = {
  key: 'subscriptions',
  label: 'Subscriptions',
  audience: 'both',
  items: [],
};
