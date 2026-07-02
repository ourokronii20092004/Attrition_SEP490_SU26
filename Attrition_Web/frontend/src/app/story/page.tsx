import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight, Users, Globe, Lightbulb, ArrowDown, BookOpen } from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Reveal } from "@/components/ui/reveal";
import { LOGLINE, entriesByCategory, getStoryEntry, DESCENT_ORDER } from "@/lib/story-data";

export const metadata: Metadata = {
  title: "The Story",
  description: "The lore of Eldravir — the dead world of Attrition, its five fallen pillars, and the living man sent down to read it.",
};

const GROUPS = [
  { category: "character" as const, title: "Characters", icon: Users, blurb: "The living man, the god in his ear, and the five who could not let it end." },
  { category: "world" as const, title: "The World", icon: Globe, blurb: "A dead world sealed in the Void, and the rot that grows from holding it." },
  { category: "concept" as const, title: "Concepts", icon: Lightbulb, blurb: "The rules a self is built from, and the offer that unmakes one." },
];

export default function StoryHubPage() {
  const strata = DESCENT_ORDER.map(getStoryEntry).filter((e) => e !== undefined);

  return (
    <PageShell size="xl">
      {/* Hero */}
      <Reveal as="section" className="relative overflow-hidden rounded-card border border-border bg-surface px-6 py-16 text-center sm:px-12 sm:py-20">
        <span aria-hidden className="pointer-events-none absolute left-1/2 top-0 h-72 w-72 -translate-x-1/2 -translate-y-1/3 rounded-full bg-accent/10 blur-[110px]" />
        <p className="font-mono text-[11px] uppercase tracking-[0.35em] text-accent">The Lore of Eldravir</p>
        <h1 className="mx-auto mt-5 max-w-3xl font-display text-3xl font-bold leading-tight tracking-tight text-balance text-fg sm:text-4xl lg:text-5xl">
          A world that won&rsquo;t die, and the man sent down to read it.
        </h1>
        <p className="mx-auto mt-6 max-w-2xl text-base leading-relaxed text-fg-muted">{LOGLINE}</p>
        <Link
          href="/story/read"
          className="group mt-8 inline-flex items-center gap-2 rounded-md bg-accent px-6 py-3 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg transition-[filter,box-shadow] hover:brightness-105 hover:shadow-[var(--shadow-glow)]"
        >
          <BookOpen size={16} /> Read the manuscript
          <ArrowRight size={16} className="transition-transform duration-200 group-hover:translate-x-1" />
        </Link>
      </Reveal>

      {/* The Descent */}
      <Reveal as="section" className="mt-20">
        <div className="mb-8 border-b border-border pb-4">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Descent</h2>
          <p className="mt-2 max-w-2xl text-sm leading-relaxed text-fg-muted">
            Seven strata, surface to core. Going down is going inward — each must be understood to pass, not beaten.
          </p>
        </div>
        <ol className="space-y-px">
          {strata.map((s, i) => (
            <Reveal as="li" key={s!.slug} delay={i}>
              <Link
                href={`/story/${s!.slug}`}
                className="group flex items-baseline gap-4 rounded-md border border-transparent px-4 py-3 transition-colors hover:border-border hover:bg-surface sm:gap-6"
              >
                <span className="font-mono text-xs tabular-nums text-fg-subtle transition-colors group-hover:text-accent">
                  {String(i + 1).padStart(2, "0")}
                </span>
                <span className="flex min-w-0 flex-1 flex-col gap-0.5 sm:flex-row sm:items-baseline sm:gap-4">
                  <span className="font-display text-lg font-semibold text-fg transition-colors group-hover:text-accent sm:w-40 sm:shrink-0">
                    {s!.name}
                  </span>
                  <span className="truncate text-sm leading-relaxed text-fg-muted">{s!.tagline}</span>
                </span>
                <ArrowRight size={16} className="shrink-0 -translate-x-2 text-accent opacity-0 transition-all duration-300 group-hover:translate-x-0 group-hover:opacity-100" />
              </Link>
            </Reveal>
          ))}
        </ol>
        {/* Subtle descent cue */}
        <div aria-hidden className="mt-4 flex justify-center"><ArrowDown size={16} className="animate-float text-fg-subtle" /></div>
      </Reveal>

      {/* Entity groups */}
      {GROUPS.map(({ category, title, icon: Icon, blurb }) => {
        const entries = entriesByCategory(category).filter((e) => !DESCENT_ORDER.includes(e.slug));
        return (
          <Reveal as="section" key={category} className="mt-20">
            <div className="mb-8 flex items-center gap-3 border-b border-border pb-4">
              <Icon size={18} className="text-accent" />
              <div>
                <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">{title}</h2>
                <p className="mt-1 text-sm text-fg-muted">{blurb}</p>
              </div>
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {entries.map((e, i) => (
                <Reveal key={e.slug} delay={i}>
                  <Link
                    href={`/story/${e.slug}`}
                    className="group flex h-full flex-col overflow-hidden rounded-card border border-border bg-surface p-5 transition-[transform,border-color,box-shadow] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] hover:-translate-y-1.5 hover:border-accent/60 hover:shadow-[var(--shadow-glow)]"
                  >
                    {e.kicker && <p className="font-mono text-[10px] uppercase tracking-[0.2em] text-accent">{e.kicker}</p>}
                    <h3 className="mt-2 font-display text-xl font-semibold text-fg transition-colors group-hover:text-accent">{e.name}</h3>
                    <p className="mt-2 flex-1 text-sm leading-relaxed text-fg-muted">{e.tagline}</p>
                    <span className="mt-4 inline-flex items-center gap-1.5 text-xs font-medium text-accent opacity-0 transition-opacity duration-300 group-hover:opacity-100">
                      Read entry <ArrowRight size={13} />
                    </span>
                  </Link>
                </Reveal>
              ))}
            </div>
          </Reveal>
        );
      })}
    </PageShell>
  );
}
