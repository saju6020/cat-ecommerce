import { OfflineDeviceSettings } from '@/components/offline/OfflineDeviceSettings';

// HO-XCT-002 §8.1 — the device opt-in and "Clear this device". Static, like the board.
export const dynamic = 'force-static';

export default function OfflineSettingsPage() {
  return <OfflineDeviceSettings />;
}
