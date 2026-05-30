"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { wikiApi } from "@/lib/api/wiki";
import { PageLoader } from "@/components/ui/spinner";
import { useAuth } from "@/lib/providers";
import type { WikiArticleDto } from "@/lib/types";

export default function WikiArticlePage() {
  const params = useParams<{ slug: string }>();
  const { user } = useAuth();
  const [article, setArticle] = useState<WikiArticleDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!params.slug) return;
    setLoading(true);
    wikiApi
      .getArticle(params.slug)
      .then((res) => {
        if (res.success) setArticle(res.data);
      })
      .finally(() => setLoading(false));
  }, [params.slug]);

  if (loading) return <PageLoader />;
  if (!article) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12 text-center">
        <h1 className="font-display text-3xl font-bold text-fg">Article Not Found</h1>
        <Link href="/wiki" className="mt-4 inline-block text-accent hover:underline">Back to Wiki</Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      <div className="mb-6 flex items-start justify-between gap-4">
        <div>
          <Link href="/wiki" className="text-sm text-accent hover:underline">&larr; Wiki</Link>
          <h1 className="mt-2 font-display text-4xl font-bold text-fg">{article.title}</h1>
          <p className="mt-1 text-sm text-fg-muted">
            {article.categorySlug} &middot; by {article.authorName ?? "Unknown"} &middot; {new Date(article.updatedAt).toLocaleDateString()}
          </p>
        </div>
        {user && (
          <Link
            href={`/wiki/${params.slug}/suggest`}
            className="shrink-0 rounded-md border border-border px-3 py-1.5 text-sm text-fg-muted hover:bg-surface-2"
          >
            Suggest Edit
          </Link>
        )}
      </div>

      <article className="prose-content">
        <ReactMarkdown remarkPlugins={[remarkGfm]}>{article.content}</ReactMarkdown>
      </article>

      <div className="mt-8 border-t border-border pt-4">
        <Link href={`/wiki/${params.slug}/revisions`} className="text-sm text-accent hover:underline">
          View revision history
        </Link>
      </div>
    </div>
  );
}
