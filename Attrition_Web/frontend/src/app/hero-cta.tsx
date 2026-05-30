"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowRight } from "lucide-react";
import { useAuth } from "@/lib/providers";

/**
 * Hero call-to-action buttons. When the visitor is signed in, they go straight
 * to the destination; when signed out, they route to /login first (with a
 * redirect back) so the archive and forum prompt sign-in.
 */
export function HeroCta() {
  const { user, loading } = useAuth();
  const router = useRouter();

  const go = (dest: string) => {
    if (loading) return;
    router.push(user ? dest : `/login?redirect=${encodeURIComponent(dest)}`);
  };

  return (
    <div className="animate-rise-in mt-10 flex flex-wrap items-center justify-center gap-4 [animation-delay:600ms]">
      <button
        onClick={() => go("/wiki")}
        className="group inline-flex items-center gap-2 rounded-md bg-accent px-7 py-3.5 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_45%,transparent)] transition-[transform,box-shadow,filter] duration-200 hover:shadow-[var(--shadow-glow)] hover:brightness-105 active:scale-[0.97]"
      >
        Enter the Archive
        <ArrowRight size={17} className="transition-transform duration-200 group-hover:translate-x-1" />
      </button>
      <button
        onClick={() => go("/forum")}
        className="inline-flex items-center rounded-md border border-border-strong px-7 py-3.5 text-sm font-semibold uppercase tracking-[0.15em] text-fg transition-colors duration-200 hover:border-accent hover:text-accent active:scale-[0.97]"
      >
        Visit the Forum
      </button>
    </div>
  );
}
