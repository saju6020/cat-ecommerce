import { AwaitingPaymentList } from '@/components/orders/AwaitingPaymentList';

/** `/staff/orders/awaiting-payment` — HO-ORD-002 §11.9 (US-ORD-19 AC2): transfers to confirm. */
export default function AwaitingPaymentPage() {
  return <AwaitingPaymentList />;
}
