import type { NavArea } from '../types';

/**
 * File Storage navigation entries.
 *
 * Owned entirely by the File Storage area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const fileStorageNav: NavArea = {
  key: 'file-storage',
  label: 'File Storage',
  audience: 'staff',
  items: [],
};
