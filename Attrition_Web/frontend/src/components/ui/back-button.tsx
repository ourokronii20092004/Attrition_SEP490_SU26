"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft } from "lucide-react";

interface BackButtonProps {
  /** Explicit parent route. Preferred for pages with one clear parent (e.g. an item under /items). */
  href?: string;
  label: string;
  /** When no single parent fits (profiles reached from many places), fall back to history back. */
  fallbackHref?: string;
}

/**
 * "Back to parent" link for nested user-side pages. Prefer an explicit `href` so the destination is
 * deterministic. For pages reachable from many entry points, omit `href`: it uses browser history,
 * falling back to `fallbackHref` (or "/") when there's no in-app history to return to.
 */
export function BackButton({ href, label, fallbackHref = "/" }: BackButtonProps) {
  const router = useRouter();
  const cls = "inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg";

  if (href) {
    return (
      <Link href={href} className={cls}>
        <ArrowLeft size={16} /> {label}
      </Link>
    );
  }

  const goBack = () => {
    // history.length > 1 means there's a prior in-app entry; otherwise go to a sane default.
    if (typeof window !== "undefined" && window.history.length > 1) router.back();
    else router.push(fallbackHref);
  };

  return (
    <button type="button" onClick={goBack} className={cls}>
      <ArrowLeft size={16} /> {label}
    </button>
  );
}
