import * as React from 'react';

/**
 * The Hearth sitting-cat line illustration (docs/design/shop/ShopHome.dc.html, hero). Stroke is
 * `currentColor`, so the caller sets the colour (warm-ink on a white disc). Decorative: hidden
 * from assistive technology unless a `title` is passed.
 */
export function LineArtCat({
  width = 190,
  height = 210,
  title,
  className,
}: {
  width?: number;
  height?: number;
  /** Only when the cat carries meaning; omit for decoration. */
  title?: string;
  className?: string;
}) {
  return (
    <svg
      width={width}
      height={height}
      viewBox="0 0 120 132"
      fill="none"
      stroke="currentColor"
      strokeWidth={2.4}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      role={title ? 'img' : undefined}
      aria-label={title}
      aria-hidden={title ? undefined : true}
    >
      <path d="M34 30 L40 6 L54 24" />
      <path d="M86 30 L80 6 L66 24" />
      <path d="M30 44c0-14 13-22 30-22s30 8 30 22-13 24-30 24-30-10-30-24z" />
      <path d="M48 42h.01M72 42h.01" />
      <path d="M56 52l4 3 4-3" />
      <path d="M40 64c-10 10-14 26-12 44 1 12 10 18 32 18s31-6 32-18c2-18-2-34-12-44" />
      <path d="M92 108c10 0 16-6 14-14" />
    </svg>
  );
}
