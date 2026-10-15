import { useEffect, useState } from 'react';
import { fetchStaffBlob } from '@/lib/api/visits';

/**
 * `useStaffBlobUrl` — HO-EMR-002 fix round 1. Loads a staff binary (a consent's signature SVG)
 * through `fetchStaffBlob` (cookie + `X-Branch-Id`, errors mapped like `apiFetch`) and exposes an
 * OBJECT URL for an `<img>`. The URL is revoked when the path changes or the component unmounts, and a
 * failure is returned as `error` so the screen can show a fallback instead of a broken image.
 */
export function useStaffBlobUrl(path: string | null) {
  const [state, setState] = useState<{ url: string | null; error: boolean; loading: boolean }>({
    url: null,
    error: false,
    loading: path !== null,
  });
  useEffect(() => {
    if (!path) return;
    let cancelled = false;
    let objectUrl: string | null = null;
    setState({ url: null, error: false, loading: true });
    fetchStaffBlob(path)
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setState({ url: objectUrl, error: false, loading: false });
      })
      .catch(() => {
        if (!cancelled) setState({ url: null, error: true, loading: false });
      });
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [path]);
  return state;
}
