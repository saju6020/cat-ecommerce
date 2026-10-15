import { LoadingState } from '@/components/common/LoadingState';

/**
 * `(staff)` route loading UI — shown inside the group's shell the moment a link is followed, until the
 * next page's content is ready, so a slow page never looks frozen (Issues.md #1).
 */
export default function Loading() {
  return <LoadingState rows={6} className="py-2" />;
}
