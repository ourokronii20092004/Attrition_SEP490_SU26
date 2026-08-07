"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeft, ArrowRight, List, X, Check, ChevronUp } from "lucide-react";
import type { ChapterMeta } from "@/lib/chapters";

/**
 * Reader chrome for a single manuscript chapter.
 *
 * The reader used to be a bare column: the only way to reach the next chapter was to scroll
 * ~20 minutes of prose to the footer, and there was no table of contents once you were inside a
 * chapter. This adds a slim sticky bar that stays with the reader the whole way down —
 * scroll progress, prev/next, and a chapter drawer — so navigation never requires reaching the end.
 */
export function ReaderChrome({
  chapter,
  prev,
  next,
  chapters,
  total,
}: {
  chapter: ChapterMeta;
  prev: ChapterMeta | null;
  next: ChapterMeta | null;
  chapters: ChapterMeta[];
  total: number;
}) {
  const [progress, setProgress] = useState(0);
  const [tocOpen, setTocOpen] = useState(false);
  const [showTop, setShowTop] = useState(false);

  // Track read progress through the article, not the whole document, so the bar hits 100% at the
  // end of the prose rather than after the footer.
  useEffect(() => {
    const onScroll = () => {
      const article = document.getElementById("chapter-prose");
      if (!article) return;
      const start = article.offsetTop;
      const height = article.offsetHeight - window.innerHeight;
      const scrolled = window.scrollY - start;
      const pct = height <= 0 ? 100 : (scrolled / height) * 100;
      setProgress(Math.min(100, Math.max(0, pct)));
      setShowTop(window.scrollY > 1200);
    };
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll);
    return () => {
      window.removeEventListener("scroll", onScroll);
      window.removeEventListener("resize", onScroll);
    };
  }, []);

  // Left/right arrows move between chapters — the expectation set by every e-reader. Suppressed
  // while the drawer is open or focus is in a field.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.metaKey || e.ctrlKey || e.altKey) return;
      const el = e.target as HTMLElement | null;
      if (el && (el.tagName === "INPUT" || el.tagName === "TEXTAREA" || el.isContentEditable)) return;
      if (e.key === "Escape") { setTocOpen(false); return; }
      if (tocOpen) return;
      if (e.key === "ArrowLeft" && prev) window.location.assign(`/story/read/${prev.n}`);
      if (e.key === "ArrowRight" && next) window.location.assign(`/story/read/${next.n}`);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [prev, next, tocOpen]);

  // Lock page scroll while the drawer is open.
  useEffect(() => {
    if (!tocOpen) return;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { document.body.style.overflow = prevOverflow; };
  }, [tocOpen]);

  return (
    <>
      {/* Sticky reader bar. Sits directly under the site header. */}
      <div className="glass sticky top-16 z-[150] border-x-0 border-t-0">
        {/* Progress rail */}
        <div className="h-0.5 w-full bg-border/40" role="presentation">
          <div
            className="h-full bg-accent transition-[width] duration-150 ease-out"
            style={{ width: `${progress}%` }}
          />
        </div>

        <div className="mx-auto flex h-12 max-w-3xl items-center justify-between gap-3 px-5 sm:px-8">
          <button
            onClick={() => setTocOpen(true)}
            className="inline-flex items-center gap-2 rounded-md px-2 py-1.5 text-xs font-medium uppercase tracking-[0.15em] text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg"
            aria-label="Open chapter list"
          >
            <List size={15} />
            <span className="hidden sm:inline">Chapters</span>
            <span className="font-mono tabular-nums text-fg-subtle">
              {String(chapter.n).padStart(2, "0")}/{total}
            </span>
          </button>

          <span className="min-w-0 flex-1 truncate text-center font-display text-sm font-semibold text-fg">
            {chapter.title}
          </span>

          <div className="flex shrink-0 items-center gap-1">
            {prev ? (
              <Link
                href={`/story/read/${prev.n}`}
                className="inline-flex h-8 w-8 items-center justify-center rounded-md text-fg-muted transition-colors hover:bg-surface-2 hover:text-accent"
                aria-label={`Previous chapter: ${prev.title}`}
                title={`Previous: ${prev.title}`}
              >
                <ArrowLeft size={16} />
              </Link>
            ) : (
              <span className="inline-flex h-8 w-8 items-center justify-center text-fg-subtle/40" aria-hidden>
                <ArrowLeft size={16} />
              </span>
            )}
            {next ? (
              <Link
                href={`/story/read/${next.n}`}
                className="inline-flex h-8 w-8 items-center justify-center rounded-md text-fg-muted transition-colors hover:bg-surface-2 hover:text-accent"
                aria-label={`Next chapter: ${next.title}`}
                title={`Next: ${next.title}`}
              >
                <ArrowRight size={16} />
              </Link>
            ) : (
              <span className="inline-flex h-8 w-8 items-center justify-center text-fg-subtle/40" aria-hidden>
                <ArrowRight size={16} />
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Chapter drawer */}
      {tocOpen && (
        <div className="fixed inset-0 z-[400] flex" role="dialog" aria-modal="true" aria-label="Chapters">
          <div className="absolute inset-0 bg-black/80 motion-safe:animate-fade-in" onClick={() => setTocOpen(false)} aria-hidden />
          <aside className="relative ml-auto flex h-full w-full max-w-sm flex-col border-l border-border bg-surface motion-safe:animate-fade-in">
            <div className="flex items-center justify-between border-b border-border px-5 py-4">
              <h2 className="font-display text-sm font-semibold uppercase tracking-[0.2em] text-fg-muted">Chapters</h2>
              <button
                onClick={() => setTocOpen(false)}
                className="rounded-md p-1 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-fg"
                aria-label="Close chapter list"
                autoFocus
              >
                <X size={18} />
              </button>
            </div>
            <ol className="flex-1 overflow-y-auto p-2">
              {chapters.map((c) => {
                const current = c.n === chapter.n;
                const read = c.n < chapter.n;
                return (
                  <li key={c.n}>
                    <Link
                      href={`/story/read/${c.n}`}
                      onClick={() => setTocOpen(false)}
                      aria-current={current ? "page" : undefined}
                      className={`flex items-baseline gap-3 rounded-md px-3 py-2.5 transition-colors ${
                        current ? "bg-accent-soft text-accent" : "text-fg-muted hover:bg-surface-2 hover:text-fg"
                      }`}
                    >
                      <span className="font-mono text-xs tabular-nums opacity-70">
                        {String(c.n).padStart(2, "0")}
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className={`block text-sm font-medium ${current ? "text-accent" : "text-fg"}`}>
                          {c.title}
                        </span>
                        <span className="mt-0.5 block font-mono text-[10px] uppercase tracking-[0.15em] text-fg-subtle">
                          {c.stratum ?? `Act ${c.act}`} · {c.readingMinutes} min
                        </span>
                      </span>
                      {read && <Check size={13} className="shrink-0 text-fg-subtle" aria-label="Already passed" />}
                    </Link>
                  </li>
                );
              })}
            </ol>
          </aside>
        </div>
      )}

      {/* Back to top — appears once the reader is deep in the chapter. */}
      {showTop && (
        <button
          onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}
          className="fixed bottom-24 right-5 z-[140] inline-flex h-10 w-10 items-center justify-center rounded-full border border-border bg-surface text-fg-muted shadow-[var(--shadow-md)] transition-colors hover:border-accent/60 hover:text-accent motion-safe:animate-fade-in sm:right-8"
          aria-label="Back to top"
        >
          <ChevronUp size={18} />
        </button>
      )}
    </>
  );
}
