import { DaycareDayList } from '@/components/boarding/daycare/DaycareDayList';

/** `/staff/boarding/daycare` — HO-BRD-002 §11.12 (US-BRD-21): the daycare day list. */
export default function StaffDaycarePage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Daycare</h1>
      <DaycareDayList />
    </div>
  );
}
