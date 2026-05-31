"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { SearchX } from "lucide-react";
import { searchApi } from "@/lib/api/search";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle, SectionTitle } from "@/components/ui/page-title";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { PageLoader } from "@/components/ui/spinner";

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

  const { data: results, isFetching: loading } = useQuery({
    queryKey: ["search", q],
    enabled: !!q.trim(),
    queryFn: async () => {
      const res = await searchApi.search(q, 20);
      return res.success ? res.data : null;
    },
  });

  const hasResults = results && (results.wiki.length || results.users.length || results.posts.length || results.enemies.length);

  return (
    <PageShell size="lg">
      <PageTitle description={q ? <>Results for <span className="text-fg">&ldquo;{q}&rdquo;</span></> : undefined}>
        Search
      </PageTitle>

      {loading && <SkeletonList rows={5} />}

      {!loading && !hasResults && q && (
        <EmptyState icon={SearchX} title="No results found" description={`Nothing matched "${q}". Try different keywords.`} />
      )}

      {!loading && hasResults && (
        <div className="space-y-8">
          {results.wiki.length > 0 && (
            <Section title="Wiki">
              {results.wiki.map((item) => (
                <ResultRow key={item.id} href={`/wiki/${item.slug}`} title={item.title} sub={item.categorySlug} />
              ))}
            </Section>
          )}
          {results.enemies.length > 0 && (
            <Section title="Enemies">
              {results.enemies.map((item) => (
                <ResultRow key={item.enemyId} href={`/bestiary/${item.enemyId}`} title={item.name} sub={item.tier} />
              ))}
            </Section>
          )}
          {results.posts.length > 0 && (
            <Section title="Forum">
              {results.posts.map((item) => (
                <ResultRow key={item.id} href={`/forum/${item.threadId}`} title={item.threadTitle} sub={item.snippet} />
              ))}
            </Section>
          )}
          {results.users.length > 0 && (
            <Section title="Users">
              {results.users.map((item) => (
                <ResultRow key={item.id} href={`/u/${item.username}`} title={item.displayName ?? item.username} sub={`@${item.username}`} />
              ))}
            </Section>
          )}
        </div>
      )}
    </PageShell>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <SectionTitle>{title}</SectionTitle>
      <div className="mt-3 divide-y divide-border overflow-hidden rounded-card border border-border">{children}</div>
    </div>
  );
}

function ResultRow({ href, title, sub }: { href: string; title: string; sub: string }) {
  return (
    <Link href={href} className="group block bg-surface p-4 transition-colors hover:bg-surface-2">
      <p className="font-medium text-fg transition-colors group-hover:text-accent">{title}</p>
      <p className="mt-0.5 line-clamp-2 text-sm text-fg-muted">{sub}</p>
    </Link>
  );
}
