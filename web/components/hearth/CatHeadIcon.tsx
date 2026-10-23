/** The small line-art cat head used on "For <cat>" chips (docs/design/orders/Cart.dc.html). Decorative. */
export function CatHeadIcon({ size = 13 }: { size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M7 3l2.2 4.2M17 3l-2.2 4.2" />
      <path d="M5 13a7 6 0 0 0 14 0c0-3.3-3.1-6-7-6s-7 2.7-7 6z" />
    </svg>
  );
}
