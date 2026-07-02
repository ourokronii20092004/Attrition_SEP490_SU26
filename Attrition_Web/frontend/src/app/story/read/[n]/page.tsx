import type { Metadata } from "next";
import { notFound } from "next/navigation";
import Link from "next/link";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { ArrowLeft, ArrowRight, Clock, List } from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { getChapter, getAllChapterMeta, CHAPTER_COUNT } from "@/lib/chapters";

export function generateStaticParams() {
  return getAllChapterMeta().map((c) => ({ n: String(c.n) }));
}

// Read at build time only — these are fully prerendered, no runtime fs access.
export const dynamic = "force-static";

export async function generateMetadata({ params }: { params: Promise<{ n: string }> }): Promise<Metadata> {
  const { n } = await params;
  const chapter = getChapter(Number(n));
  if (!chapter) return { title: "Chapter" };
  return {
    title: `${chapter.title} · Chapter ${chapter.n}`,
    description: `Chapter ${chapter.n} of the Eldravir manuscript.`,
  };
}

export default async function ChapterReaderPage({ params }: { params: Promise<{ n: string }> }) {
  const { n } = await params;
  const num = Number(n);
  const chapter = getChapter(num);
  if (!chapter) notFound();

  const prev = num > 1 ? getChapter(num - 1) : null;
  const next = num < CHAPTER_COUNT ? getChapter(num + 1) : null;

  return (
    <PageShell size="md">
      <div className="flex items-center justify-between gap-3">
        <Link href="/story/read" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
          <List size={16} /> All chapters
        </Link>
        <span className="font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle">
          Chapter {String(chapter.n).padStart(2, "0")} / {CHAPTER_COUNT}
        </span>
      </div>

      {/* Chapter header */}
      <header className="mt-8 border-b border-border pb-8">
        <p className="font-mono text-[11px] uppercase tracking-[0.3em] text-accent">
          {chapter.stratum ? chapter.stratum : `Act ${chapter.act}`}
        </p>
        <h1 className="mt-3 font-display text-4xl font-bold tracking-tight text-balance text-fg sm:text-5xl">
          {chapter.title}
        </h1>
        <p className="mt-4 flex flex-wrap items-center gap-x-3 gap-y-1 font-mono text-[11px] uppercase tracking-[0.15em] text-fg-subtle">
          <span>POV · {chapter.pov}</span>
          <span aria-hidden className="h-1 w-1 rounded-full bg-accent/50" />
          <span className="flex items-center gap-1"><Clock size={11} /> {chapter.readingMinutes} min read</span>
        </p>
      </header>

      {/* Prose */}
      <article className="prose-content mt-10 text-[1.05rem] leading-[1.8]">
        <ReactMarkdown remarkPlugins={[remarkGfm]}>{chapter.content}</ReactMarkdown>
      </article>

      {/* Prev / next */}
      <nav className="mt-14 grid gap-3 border-t border-border pt-8 sm:grid-cols-2">
        {prev ? (
          <Link
            href={`/story/read/${prev.n}`}
            className="group flex flex-col gap-1 rounded-card border border-border bg-surface p-5 transition-colors hover:border-accent/60"
          >
            <span className="flex items-center gap-1.5 font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle">
              <ArrowLeft size={13} /> Previous
            </span>
            <span className="font-display font-semibold text-fg transition-colors group-hover:text-accent">{prev.title}</span>
          </Link>
        ) : (
          <span />
        )}
        {next ? (
          <Link
            href={`/story/read/${next.n}`}
            className="group flex flex-col items-end gap-1 rounded-card border border-border bg-surface p-5 text-right transition-colors hover:border-accent/60"
          >
            <span className="flex items-center gap-1.5 font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle">
              Next <ArrowRight size={13} />
            </span>
            <span className="font-display font-semibold text-fg transition-colors group-hover:text-accent">{next.title}</span>
          </Link>
        ) : (
          <Link
            href="/story"
            className="group flex flex-col items-end gap-1 rounded-card border border-border bg-surface p-5 text-right transition-colors hover:border-accent/60"
          >
            <span className="font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle">The end</span>
            <span className="font-display font-semibold text-fg transition-colors group-hover:text-accent">Back to the Story</span>
          </Link>
        )}
      </nav>
    </PageShell>
  );
}
