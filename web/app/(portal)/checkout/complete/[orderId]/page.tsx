import { redirect } from 'next/navigation';

/** `/checkout/complete/[orderId]` — owner, 2026-10-19: replaced by the order's details page; kept so an old link
 *  still lands on the order. */
export default async function CheckoutCompletePage({
  params,
}: {
  params: Promise<{ orderId: string }>;
}) {
  const { orderId } = await params;
  redirect(`/portal/orders/${encodeURIComponent(orderId)}?placed=1`);
}
