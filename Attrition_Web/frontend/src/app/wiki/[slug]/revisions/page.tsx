"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { ChevronDown, ChevronRight, History } from "lucide-react";
import { wikiApi } from "@/lib/api/wiki";
import { PageLoader } from "@/components/ui/spinner";
import { MarkdownContent } from "@/components/post-content";
import { formatDateTime } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";

export default function RevisionsPage() {
  const params = useParams<{ slug: string }>();
  const [open, setOpen] = useState<Set<string>>(new Set());

  const { data: revisions = [], isPending, isError, error } = useQuery({
    queryKey: qk.wiki.revisions(params.slug),
    enabled: !!params.slug,
    queryFn: async () => {
      const res = await wikiApi.getArticle(params.slug);
      if (!res.success || !res.data) throw new Error("Article not found.");
      const r = await wikiApi.getRevisions(res.data.id);
      return r.success ? r.data : [];
    },
  });

  const toggle = (id: string) =>
    setOpen((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });

  const errorMessage = isError
    ? error instanceof Error && error.message === "Article not found."
      ? "Article not found."
      : "Failed to load revision history."
    : "";

  if (isPending) return <PageLoader />;

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      <Link href={`/wiki/${params.slug}`} className="text-sm text-accent hover:underline">&larr; Back to article</Link>
      <h1 className="mt-4 flex items-center gap-2 font-display text-3xl font-bold text-fg">
        <History size={24} /> Revision History
      </h1>
      <p className="mt-2 text-sm text-fg-muted">Each entry is a snapshot of the article as it was before that edit. Click one to view its content.</p>

      {errorMessage ? (
        <p className="mt-6 text-danger">{errorMessage}</p>
      ) : revisions.length === 0 ? (
        <p className="mt-6 text-fg-muted">No revisions yet.</p>
      ) : (
        <div className="mt-6 space-y-3">
          {revisions.map((rev, i) => {
            const isOpen = open.has(rev.id);
            return (
              <div key={rev.id} className="card overflow-hidden">
                <button
                  onClick={() => toggle(rev.id)}
                  aria-expanded={isOpen}
                  className="flex w-full items-center gap-3 p-4 text-left transition-colors hover:bg-surface-2"
                >
                  {isOpen
                    ? <ChevronDown size={16} className="shrink-0 text-fg-subtle" />
                    : <ChevronRight size={16} className="shrink-0 text-fg-subtle" />}
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium text-fg">{rev.changeNote || "No summary"}</span>
                    <span className="mt-0.5 block text-xs text-fg-muted">by {rev.editedByName ?? "Unknown"} &middot; {formatDateTime(rev.editedAt)}</span>
                  </span>
                  <span className="shrink-0 font-mono text-xs text-fg-subtle">#{revisions.length - i}</span>
                </button>
                {isOpen && (
                  <div className="border-t border-border p-4">
                    <MarkdownContent content={rev.content} className="prose-content text-sm" />
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
