"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { ArrowLeft, History, PencilLine } from "lucide-react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { wikiApi } from "@/lib/api/wiki";
import { PageShell } from "@/components/ui/page-shell";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { formatDate } from "@/lib/format-date";
import { useAuth } from "@/lib/providers";
import { qk } from "@/lib/query-keys";
import { WikiComments } from "./wiki-comments";

export default function WikiArticlePage() {
  const params = useParams<{ slug: string }>();
  const { user } = useAuth();

  const { data: article, isPending } = useQuery({
    queryKey: qk.wiki.article(params.slug),
    enabled: !!params.slug,
    queryFn: async () => {
      const res = await wikiApi.getArticle(params.slug);
      return res.success ? res.data : null;
    },
  });

  if (isPending) {
    return (
      <PageShell size="md">
        <Skeleton className="h-4 w-16" />
        <Skeleton className="mt-4 h-10 w-2/3" />
        <Skeleton className="mt-3 h-4 w-1/2" />
        <div className="mt-8 space-y-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-4 w-full" />
          ))}
        </div>
      </PageShell>
    );
  }

  if (!article) {
    return (
      <PageShell size="md">
        <EmptyState
          title="Article not found"
          description="This article may have been moved or removed."
          action={<Link href="/wiki"><Button variant="secondary">Back to Wiki</Button></Link>}
        />
      </PageShell>
    );
  }

  return (
    <PageShell size="md">
      <Link
        href="/wiki"
        className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg"
      >
        <ArrowLeft size={16} /> Wiki
      </Link>

      <div className="mt-4 flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="font-display text-3xl font-bold tracking-tight text-balance text-fg sm:text-4xl">
            {article.title}
          </h1>
          <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-fg-muted">
            <span className="rounded-full bg-accent-soft px-2.5 py-0.5 text-xs font-medium text-accent">
              {article.categorySlug}
            </span>
            <span>by {article.authorName ?? "Unknown"}</span>
            <span className="text-fg-subtle">&middot;</span>
            <span>Updated {formatDate(article.updatedAt)}</span>
          </div>
        </div>
        {user && (
          <Link href={`/wiki/${params.slug}/suggest`} className="shrink-0">
            <Button variant="secondary" size="sm">
              <PencilLine size={15} className="mr-1.5" /> Suggest Edit
            </Button>
          </Link>
        )}
      </div>

      <article className="prose-content mt-8 border-t border-border pt-8">
        <ReactMarkdown remarkPlugins={[remarkGfm]}>{article.content}</ReactMarkdown>
      </article>

      <div className="mt-10 border-t border-border pt-4">
        <Link
          href={`/wiki/${params.slug}/revisions`}
          className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg"
        >
          <History size={16} /> View revision history
        </Link>
      </div>

      <WikiComments articleId={article.id} articleTitle={article.title} />
    </PageShell>
  );
}
