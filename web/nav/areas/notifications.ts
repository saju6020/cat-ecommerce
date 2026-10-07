import type { NavArea } from '../types';

/**
 * Notifications navigation entries.
 *
 * Owned entirely by the Notifications area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const notificationsNav: NavArea = {
  key: 'notifications',
  label: 'Notifications',
  audience: 'both',
  items: [
    // HO-NTF-002 §8.1/§11.5 — the staff "Messages" entry. The portal's inbox link is the unread
    // badge in the portal header (components/inbox/InboxBadge.tsx), not a nav item. Shown to every
    // staff role (presentation only): a role without NotificationLog.View gets the server's 403 as
    // PermissionDeniedState (E-E2).
    { label: 'Messages', href: '/staff/notifications', icon: 'mail', audience: 'staff' },
  ],
};
