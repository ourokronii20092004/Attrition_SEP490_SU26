"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft } from "lucide-react";

interface BackButtonProps {
  /**
   * Where to go when there's no in-app history to return to — a pasted link, a new tab, or a
   * fresh page load. Also names the destination the label describes.
   */
  fallbackHref?: string;
  label: string;
  /**
   * Force a plain link, ignoring history. Only for pages with exactly one possible parent, where
   * going back could plausibly land somewhere unrelated.
   */
  forceHref?: string;
}

/**
 * "Back to parent" control for nested pages.
 *
 * Prefers browser history, because list pages keep their page and filters in the URL: going back
 * returns you to page 50 of a filtered list, whereas linking to the bare list URL drops you on
 * page 1 with filters cleared. These pages are also reachable from search, the world map, item
 * drop lists and profile activity, so history returns you where you actually came from rather than
 * to one assumed parent.
 *
 * Falls back to `fallbackHref` when this is the first entry in the session's history — a pasted
 * link or a new tab — where there is nothing to go back to.
 */
export function BackButton({ fallbackHref = "/", label, forceHref }: BackButtonProps) {
  const router = useRouter();
  const cls = "inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg";

  if (forceHref) {
    return (
      <Link href={forceHref} className={cls}>
        <ArrowLeft size={16} /> {label}
      </Link>
    );
  }

  const goBack = () => {
    if (typeof window === "undefined") return;
    // Deliberately NOT document.referrer: the App Router navigates with pushState, which leaves
    // referrer at whatever loaded the document. Someone who arrives from a search engine, browses
    // to page 50 and opens a row still has an external referrer, so keying off it would send them
    // to the bare list and lose their place — the exact thing this avoids.
    //
    // history.state.idx is Next's own position in the session history: 0 means this page is the
    // first entry, so there is genuinely nothing behind it.
    const idx = (window.history.state as { idx?: number } | null)?.idx;
    const hasPrevious = typeof idx === "number" ? idx > 0 : window.history.length > 1;
    if (hasPrevious) router.back();
    else router.push(fallbackHref);
  };

  return (
    <button type="button" onClick={goBack} className={cls}>
      <ArrowLeft size={16} /> {label}
    </button>
  );
}
