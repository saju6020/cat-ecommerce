import { CartScreen } from '@/components/cart/CartScreen';

/**
 * `/cart` — HO-ORD-002 §3.3/§8.1, US-ORD-01. Public: a guest's cart lives behind the API's `HttpOnly`
 * cookie, and Checkout asks a signed-out visitor to sign in (`?next=/checkout`).
 */
export const metadata = { title: 'Your cart — PurrCare' };

export default function CartPage() {
  return <CartScreen />;
}
