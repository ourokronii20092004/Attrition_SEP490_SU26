import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight, Clock, BookOpen } from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { BackButton } from "@/components/ui/back-button";
import { Reveal } from "@/components/ui/reveal";
import { getAllChapterMeta, ACTS } from "@/lib/chapters";

export const metadata: Metadata = {
  title: "Read the Story",
  description: "The full Eldravir manuscript — seventeen chapters across three acts.",
};

export const dynamic = "force-static";

export default function ChapterIndexPage() {
  const chapters = getAllChapterMeta();
  const totalMinutes = chapters.reduce((m, c) => m + c.readingMinutes, 0);

  return (
    <PageShell size="lg">
      <BackButton fallbackHref="/story" label="The Story" />

      <Reveal className="mt-6">
        <p className="font-mono text-[11px] uppercase tracking-[0.3em] text-accent">The Manuscript</p>
        <h1 className="mt-3 font-display text-4xl font-bold tracking-tight text-fg sm:text-5xl">Read the Story</h1>
        <p className="mt-4 max-w-2xl leading-relaxed text-fg-muted">
          The complete descent through Eldravir — seventeen chapters, three acts. Tight third on Ren,
          with Iris a voice in his ear the whole way down.
        </p>
        <p className="mt-3 flex items-center gap-2 font-mono text-xs uppercase tracking-[0.15em] text-fg-subtle">
          <BookOpen size={13} /> {chapters.length} chapters
          <span aria-hidden className="h-1 w-1 rounded-full bg-accent/50" />
          <Clock size={13} /> ~{Math.round(totalMinutes / 60 * 10) / 10} hrs
        </p>
        <Link
          href="/story/read/1"
          className="group mt-6 inline-flex items-center gap-2 rounded-md bg-accent px-6 py-3 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg transition-[filter,box-shadow] hover:brightness-105 hover:shadow-[var(--shadow-glow)]"
        >
          Start from the beginning
          <ArrowRight size={16} className="transition-transform duration-200 group-hover:translate-x-1" />
        </Link>
      </Reveal>

      {ACTS.map(({ act, name, subtitle }) => {
        const inAct = chapters.filter((c) => c.act === act);
        if (inAct.length === 0) return null;
        return (
          <Reveal as="section" key={act} className="mt-14">
            <div className="mb-5 border-b border-border pb-4">
              <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">{name}</h2>
              <p className="mt-2 text-sm leading-relaxed text-fg-muted">{subtitle}</p>
            </div>
            <ol className="space-y-px">
              {inAct.map((c, i) => (
                <Reveal as="li" key={c.n} delay={i}>
                  <Link
                    href={`/story/read/${c.n}`}
                    className="group flex items-center gap-4 rounded-md border border-transparent px-4 py-3.5 transition-colors hover:border-border hover:bg-surface sm:gap-6"
                  >
                    <span className="font-mono text-sm tabular-nums text-fg-subtle transition-colors group-hover:text-accent">
                      {String(c.n).padStart(2, "0")}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block font-display text-lg font-semibold text-fg transition-colors group-hover:text-accent">
                        {c.title}
                      </span>
                      <span className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 font-mono text-[11px] uppercase tracking-[0.15em] text-fg-subtle">
                        {c.stratum && <span>{c.stratum}</span>}
                        <span className="flex items-center gap-1"><Clock size={11} /> {c.readingMinutes} min</span>
                      </span>
                    </span>
                    <ArrowRight size={16} className="shrink-0 -translate-x-2 text-accent opacity-0 transition-all duration-300 group-hover:translate-x-0 group-hover:opacity-100" />
                  </Link>
                </Reveal>
              ))}
            </ol>
          </Reveal>
        );
      })}
    </PageShell>
  );
}
