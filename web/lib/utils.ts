import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** Merges conditional class names and resolves Tailwind conflicts (last one wins). The one
 *  helper every shadcn/ui component copied into components/ui/ depends on. */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
