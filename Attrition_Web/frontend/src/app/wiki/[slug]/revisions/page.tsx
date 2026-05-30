"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { wikiApi } from "@/lib/api/wiki";
import { PageLoader } from "@/components/ui/spinner";
import type { WikiRevisionDto } from "@/lib/types";

export default function RevisionsPage() {
  const params = useParams<{ slug: string }>();
  const [revisions, setRevisions] = useState<WikiRevisionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [articleId, setArticleId] = useState<string>("");

  useEffect(() => {
    if (!params.slug) return;
    wikiApi.getArticle(params.slug).then((res) => {
      if (res.success && res.data) {
        setArticleId(res.data.id);
        wikiApi.getRevisions(res.data.id).then((r) => {
          if (r.success) setRevisions(r.data);
          setLoading(false);
        });
      } else {
        setLoading(false);
      }
    });
  }, [params.slug]);

  if (loading) return <PageLoader />;

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      <Link href={`/wiki/${params.slug}`} className="text-sm text-accent hover:underline">&larr; Back to article</Link>
      <h1 className="mt-4 font-display text-3xl font-bold text-fg">Revision History</h1>

      <div className="mt-6 space-y-3">
        {revisions.map((rev) => (
          <div key={rev.id} className="card flex items-center justify-between p-4">
            <div>
              <p className="text-sm font-medium text-fg">{rev.changeNote || "No summary"}</p>
              <p className="text-xs text-fg-muted">by {rev.editedByName ?? "Unknown"} &middot; {new Date(rev.editedAt).toLocaleString()}</p>
            </div>
          </div>
        ))}
        {revisions.length === 0 && <p className="text-fg-muted">No revisions yet.</p>}
      </div>
    </div>
  );
}
