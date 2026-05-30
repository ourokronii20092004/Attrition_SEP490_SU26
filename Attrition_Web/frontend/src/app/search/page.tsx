"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { searchApi } from "@/lib/api/search";
import { PageLoader } from "@/components/ui/spinner";
import type { GlobalSearchResponse } from "@/lib/types";

export default function SearchPage() {
  return (
    <Suspense fallback={<PageLoader />}>
      <SearchContent />
    </Suspense>
  );
}

function SearchContent() {
  const searchParams = useSearchParams();
  const q = searchParams.get("q") ?? "";
  const [results, setResults] = useState<GlobalSearchResponse | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!q.trim()) return;
    setLoading(true);
    searchApi
      .search(q, 20)
      .then((res) => {
        if (res.success) setResults(res.data);
      })
      .finally(() => setLoading(false));
  }, [q]);

  const hasResults = results && (results.wiki.length || results.users.length || results.posts.length || results.enemies.length);

  return (
    <div className="mx-auto max-w-4xl px-4 py-8">
      <h1 className="font-display text-3xl font-bold text-fg">Search Results</h1>
      {q && <p className="mt-2 text-fg-muted">Results for &ldquo;{q}&rdquo;</p>}

      {loading && <PageLoader />}

      {!loading && !hasResults && q && (
        <p className="mt-8 text-center text-fg-muted">No results found.</p>
      )}

      {!loading && hasResults && (
        <div className="mt-6 space-y-8">
          {results.wiki.length > 0 && (
            <Section title="Wiki">
              {results.wiki.map((item) => (
                <Link key={item.id} href={`/wiki/${item.slug}`} className="block rounded-lg p-3 transition hover:bg-surface-2">
                  <p className="font-medium text-fg">{item.title}</p>
                  <p className="mt-1 text-sm text-fg-muted">{item.categorySlug}</p>
                </Link>
              ))}
            </Section>
          )}
          {results.enemies.length > 0 && (
            <Section title="Enemies">
              {results.enemies.map((item) => (
                <Link key={item.enemyId} href={`/bestiary/${item.enemyId}`} className="block rounded-lg p-3 transition hover:bg-surface-2">
                  <p className="font-medium text-fg">{item.name}</p>
                  <p className="mt-1 text-sm text-fg-muted">{item.tier}</p>
                </Link>
              ))}
            </Section>
          )}
          {results.posts.length > 0 && (
            <Section title="Forum">
              {results.posts.map((item) => (
                <Link key={item.id} href={`/forum/${item.threadId}`} className="block rounded-lg p-3 transition hover:bg-surface-2">
                  <p className="font-medium text-fg">{item.threadTitle}</p>
                  <p className="mt-1 text-sm text-fg-muted line-clamp-2">{item.snippet}</p>
                </Link>
              ))}
            </Section>
          )}
          {results.users.length > 0 && (
            <Section title="Users">
              {results.users.map((item) => (
                <Link key={item.id} href={`/u/${item.username}`} className="block rounded-lg p-3 transition hover:bg-surface-2">
                  <p className="font-medium text-fg">{item.displayName ?? item.username}</p>
                  <p className="mt-1 text-sm text-fg-muted">@{item.username}</p>
                </Link>
              ))}
            </Section>
          )}
        </div>
      )}
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h2 className="font-display text-xl font-semibold text-fg">{title}</h2>
      <div className="mt-3 space-y-2">{children}</div>
    </div>
  );
}
