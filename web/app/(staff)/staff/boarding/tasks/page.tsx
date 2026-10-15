import { TaskBoard } from '@/components/boarding/tasks/TaskBoard';

/** `/staff/boarding/tasks` — HO-BRD-002 §11.7 (US-BRD-09, 10, 11): the tablet task board. */
export default function BoardingTasksPage() {
  return (
    <div className="flex flex-col gap-3">
      <h1 className="font-display text-2xl font-semibold text-ink">Care tasks</h1>
      <TaskBoard />
    </div>
  );
}
