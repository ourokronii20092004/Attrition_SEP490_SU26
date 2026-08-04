import type { Metadata } from "next";
import { notFound } from "next/navigation";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { BackButton } from "@/components/ui/back-button";
import { Reveal } from "@/components/ui/reveal";
import { STORY_ENTRIES, getStoryEntry, storyLink } from "@/lib/story-data";
import { SpoilerBlock } from "./spoiler-block";

const CATEGORY_LABEL: Record<string, string> = {
  character: "Character",
  world: "World",
  concept: "Concept",
  stratum: "Stratum",
};

export function generateStaticParams() {
  return STORY_ENTRIES.map((e) => ({ slug: e.slug }));
}

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const entry = getStoryEntry(slug);
  if (!entry) return { title: "Story" };
  return { title: `${entry.name} · Story`, description: entry.tagline };
}

export default async function StoryEntryPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const entry = getStoryEntry(slug);
  if (!entry) notFound();

  const related = entry.related.map(storyLink).filter((l) => l !== null);

  return (
    <PageShell size="md">
      <BackButton fallbackHref="/story" label="The Story" />

      <Reveal as="article" className="mt-6">
        <p className="font-mono text-[11px] uppercase tracking-[0.3em] text-accent">
          {entry.kicker ?? CATEGORY_LABEL[entry.category]}
        </p>
        <h1 className="mt-3 font-display text-4xl font-bold tracking-tight text-balance text-fg sm:text-5xl">
          {entry.name}
        </h1>
        <p className="mt-4 border-l-2 border-accent/50 pl-4 text-lg italic leading-relaxed text-fg-muted">
          {entry.tagline}
        </p>

        <div className="mt-8 space-y-5">
          {entry.body.map((p, i) => (
            <p key={i} className="leading-relaxed text-fg">{p}</p>
          ))}
        </div>

        {entry.spoiler && entry.spoiler.length > 0 && <SpoilerBlock paragraphs={entry.spoiler} />}
      </Reveal>

      {related.length > 0 && (
        <Reveal as="section" className="mt-14 border-t border-border pt-8">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">Connected</h2>
          <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2">
            {related.map((l) => {
              const e = getStoryEntry(l!.slug)!;
              return (
                <Link
                  key={l!.slug}
                  href={`/story/${l!.slug}`}
                  className="group flex items-center justify-between gap-3 rounded-md border border-border bg-surface px-4 py-3 transition-colors hover:border-accent/60"
                >
                  <span className="min-w-0">
                    <span className="block font-display font-semibold text-fg transition-colors group-hover:text-accent">{e.name}</span>
                    <span className="block truncate text-xs text-fg-muted">{e.kicker ?? CATEGORY_LABEL[e.category]}</span>
                  </span>
                  <ArrowRight size={15} className="shrink-0 text-fg-subtle transition-all duration-300 group-hover:translate-x-0.5 group-hover:text-accent" />
                </Link>
              );
            })}
          </div>
        </Reveal>
      )}
    </PageShell>
  );
}
