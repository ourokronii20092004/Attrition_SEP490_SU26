"use client";

import { useState, useCallback, useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { Search, X } from "lucide-react";
import { searchApi } from "@/lib/api/search";
import type { GlobalSearchResponse, SearchWikiResultDto, SearchUserResultDto, SearchPostResultDto, SearchEnemyResultDto } from "@/lib/types";

export function SearchModal({ onClose }: { onClose: () => void }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<GlobalSearchResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const router = useRouter();
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    inputRef.current?.focus();
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [onClose]);

  const doSearch = useCallback((q: string) => {
    if (q.trim().length < 2) {
      setResults(null);
      return;
    }
    setLoading(true);
    searchApi
      .search(q.trim())
      .then((res) => {
        if (res.success) setResults(res.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleChange = (val: string) => {
    setQuery(val);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => doSearch(val), 300);
  };

  const navigate = (url: string) => {
    onClose();
    router.push(url);
  };

  const hasResults = results && (results.wiki.length || results.users.length || results.posts.length || results.enemies.length);

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center bg-bg/70 pt-[15vh] backdrop-blur-sm" onClick={onClose}>
      <div className="w-full max-w-lg rounded-xl border border-border bg-surface p-4 shadow-xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center gap-3 border-b border-border pb-3">
          <Search size={18} className="text-fg-muted" />
          <input
            ref={inputRef}
            value={query}
            onChange={(e) => handleChange(e.target.value)}
            placeholder="Search wiki, users, forum, enemies..."
            className="flex-1 bg-transparent text-fg outline-none placeholder:text-fg-subtle"
          />
          <button onClick={onClose} className="text-fg-muted hover:text-fg">
            <X size={18} />
          </button>
        </div>

        <div className="mt-3 max-h-80 overflow-y-auto">
          {loading && <p className="py-4 text-center text-sm text-fg-muted">Searching...</p>}

          {!loading && query.trim().length >= 2 && !hasResults && (
            <p className="py-4 text-center text-sm text-fg-muted">No results found.</p>
          )}

          {!loading && hasResults && (
            <div className="space-y-3">
              {results.wiki.length > 0 && (
                <SearchSection title="Wiki">
                  {results.wiki.map((item) => (
                    <SearchItem key={item.id} label={item.title} sub={item.categorySlug} onClick={() => navigate(`/wiki/${item.slug}`)} />
                  ))}
                </SearchSection>
              )}
              {results.enemies.length > 0 && (
                <SearchSection title="Enemies">
                  {results.enemies.map((item) => (
                    <SearchItem key={item.enemyId} label={item.name} sub={item.tier} onClick={() => navigate(`/bestiary/${item.enemyId}`)} />
                  ))}
                </SearchSection>
              )}
              {results.posts.length > 0 && (
                <SearchSection title="Forum">
                  {results.posts.map((item) => (
                    <SearchItem key={item.id} label={item.threadTitle} sub={item.snippet} onClick={() => navigate(`/forum/${item.threadId}`)} />
                  ))}
                </SearchSection>
              )}
              {results.users.length > 0 && (
                <SearchSection title="Users">
                  {results.users.map((item) => (
                    <SearchItem key={item.id} label={item.displayName ?? item.username} sub={`@${item.username}`} onClick={() => navigate(`/u/${item.username}`)} />
                  ))}
                </SearchSection>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function SearchSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wider text-fg-subtle">{title}</h3>
      {children}
    </div>
  );
}

function SearchItem({ label, sub, onClick }: { label: string; sub: string; onClick: () => void }) {
  return (
    <button onClick={onClick} className="block w-full rounded-md px-3 py-2 text-left hover:bg-surface-2">
      <p className="text-sm font-medium text-fg">{label}</p>
      <p className="text-xs text-fg-muted line-clamp-1">{sub}</p>
    </button>
  );
}
