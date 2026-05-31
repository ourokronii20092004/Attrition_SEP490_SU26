"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { wikiApi } from "@/lib/api/wiki";
import { PageLoader } from "@/components/ui/spinner";
import { formatDateTime } from "@/lib/format-date";

export default function RevisionsPage() {
  const params = useParams<{ slug: string }>();

  const { data: revisions = [], isPending, isError, error } = useQuery({
    queryKey: ["wiki", "revisions", params.slug],
    enabled: !!params.slug,
    queryFn: async () => {
      const res = await wikiApi.getArticle(params.slug);
      if (!res.success || !res.data) throw new Error("Article not found.");
      const r = await wikiApi.getRevisions(res.data.id);
      return r.success ? r.data : [];
    },
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
      <h1 className="mt-4 font-display text-3xl font-bold text-fg">Revision History</h1>

      {errorMessage ? (
        <p className="mt-6 text-danger">{errorMessage}</p>
      ) : (
        <div className="mt-6 space-y-3">
          {revisions.map((rev) => (
            <div key={rev.id} className="card flex items-center justify-between p-4">
              <div>
                <p className="text-sm font-medium text-fg">{rev.changeNote || "No summary"}</p>
                <p className="text-xs text-fg-muted">by {rev.editedByName ?? "Unknown"} &middot; {formatDateTime(rev.editedAt)}</p>
              </div>
            </div>
          ))}
          {revisions.length === 0 && <p className="text-fg-muted">No revisions yet.</p>}
        </div>
      )}
    </div>
  );
}
