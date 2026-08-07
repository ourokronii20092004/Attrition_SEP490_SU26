"use client";

import { useState, useCallback, useEffect, useRef, useMemo } from "react";
import { useRouter } from "next/navigation";
import { Search, X, CornerDownLeft, LayoutDashboard, Clock, ArrowRight } from "lucide-react";
import { searchApi } from "@/lib/api/search";
import { resolveMediaUrl } from "@/lib/api/media";
import { ADMIN_ROUTES } from "@/app/admin/admin-routes";
import {
  SEARCH_SCOPES, PUBLIC_PAGES, parseQuery, backendScope, matchPages, type SearchScope,
} from "@/lib/search-config";
import { useRecentSearches } from "./use-recent-searches";
import { useFocusTrap } from "@/lib/hooks/use-focus-trap";
import type { GlobalSearchResponse, SearchSuggestionDto } from "@/lib/types";

const ADMIN_RECENT_KEY = "attrition:admin:recent";
// Minimum characters before we hit the search API. 1 so a single letter/number already searches.
const MIN_QUERY = 1;

export function SearchModal({ onClose, adminMode = false }: { onClose: () => void; adminMode?: boolean }) {
  const [query, setQuery] = useState("");
  const [activeScope, setActiveScope] = useState<SearchScope | null>(null);
  const [results, setResults] = useState<GlobalSearchResponse | null>(null);
  const [suggestions, setSuggestions] = useState<SearchSuggestionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const router = useRouter();
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const suggestRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const { recent, add: addRecent, remove: removeRecent, clear: clearRecent } = useRecentSearches();
  // The search overlay is a dialog; Tab must stay inside it and focus must return to the trigger
  // (header search button / ⌘K) on close. The query input is the trap's preferred first stop.
  const panelRef = useFocusTrap<HTMLDivElement>(true);

  // The effective scope is whichever chip is active, OR a prefix typed into the box
  // ("wiki:foo"). Typed prefix wins so power users keep that path; chips are the fast path.
  const parsed = useMemo(() => parseQuery(query), [query]);
  const effectiveScope = parsed.scope ?? activeScope;
  const term = parsed.scope ? parsed.term : query.trim();

  // Pages matched locally (instant) — the registry covers routes the API can't reach.
  // Admin mode deliberately matches nothing here: the public site's Story/Lore/Gallery pages are
  // not what an admin is navigating to, and the admin jump-list below covers /admin routes.
  const pageMatches = useMemo(() => {
    if (adminMode) return [];
    if (effectiveScope && effectiveScope.id !== "pages") return [];
    return matchPages(term, term ? 6 : PUBLIC_PAGES.length);
  }, [term, effectiveScope, adminMode]);

  // Admin page jump-list (admin modal only).
  const adminPages = useMemo(() => {
    if (!adminMode) return [];
    const q = term.toLowerCase();
    if (q.length === 0) {
      let recentHrefs: string[] = [];
      try { recentHrefs = JSON.parse(typeof window !== "undefined" ? localStorage.getItem(ADMIN_RECENT_KEY) || "[]" : "[]"); } catch { /* ignore */ }
      const recents = ADMIN_ROUTES.filter((r) => recentHrefs.includes(r.href)).slice(0, 5);
      // A fresh admin has no history; offer the whole map rather than dropping through to the
      // public site's Story/Gallery browse grid, which is not what this modal is for.
      return recents.length > 0 ? recents : ADMIN_ROUTES;
    }
    return ADMIN_ROUTES.filter((r) => r.label.toLowerCase().includes(q) || r.href.includes(q)).slice(0, 6);
  }, [adminMode, term]);

  useEffect(() => {
    inputRef.current?.focus();
    const handler = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [onClose]);

  const doSearch = useCallback((q: string, scope: SearchScope | null) => {
    if (q.trim().length < MIN_QUERY) { setResults(null); return; }
    // If the scope only filters pages (no API kind), skip the API call entirely.
    if (scope && !scope.kind) { setResults(null); setLoading(false); return; }
    setLoading(true);
    const prefixed = backendScope(scope) ? `${backendScope(scope)}:${q.trim()}` : q.trim();
    searchApi.search(prefixed)
      .then((res) => { if (res.success) setResults(res.data); })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const doSuggest = useCallback((q: string, scope: SearchScope | null) => {
    if (q.trim().length < MIN_QUERY || (scope && !scope.kind)) { setSuggestions([]); return; }
    const prefixed = backendScope(scope) ? `${backendScope(scope)}:${q.trim()}` : q.trim();
    searchApi.suggest(prefixed)
      .then((res) => { if (res.success) setSuggestions(res.data); })
      .catch(() => {});
  }, []);

  // Re-run whenever the query OR the active chip changes.
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (suggestRef.current) clearTimeout(suggestRef.current);
    if (term.length < MIN_QUERY) { setResults(null); setSuggestions([]); return; }
    suggestRef.current = setTimeout(() => doSuggest(term, effectiveScope), 150);
    debounceRef.current = setTimeout(() => doSearch(term, effectiveScope), 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
      if (suggestRef.current) clearTimeout(suggestRef.current);
    };
  }, [term, effectiveScope, doSearch, doSuggest]);

  const navigate = (url: string) => {
    if (term.length >= MIN_QUERY) addRecent(term);
    onClose();
    router.push(url);
  };

  const runRecent = (t: string) => { setActiveScope(null); setQuery(t); inputRef.current?.focus(); };

  const filtered = useMemo(() => filterResults(results, effectiveScope), [results, effectiveScope]);
  const hasResults = !!filtered && (
    filtered.wiki.length + filtered.users.length + filtered.posts.length +
    filtered.enemies.length + (filtered.items?.length ?? 0) + (filtered.skills?.length ?? 0)
  ) > 0;
  const hasAdminPages = adminMode && adminPages.length > 0;
  const isEmpty = term.length < MIN_QUERY;

  return (
    <div
      className="fixed inset-0 z-[300] flex items-start justify-center bg-black/80 px-4 pt-[10vh] motion-safe:animate-fade-in"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-label="Site search"
    >
      <div
        ref={panelRef}
        className="card flex max-h-[78vh] w-full max-w-2xl origin-top flex-col rounded-2xl shadow-[var(--shadow-lg)] motion-safe:animate-rise-in"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Input row */}
        <div className="flex items-center gap-3 px-5 pt-5">
          <Search size={20} className="shrink-0 text-fg-muted" />
          <input
            ref={inputRef}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={effectiveScope ? `Search ${effectiveScope.label.toLowerCase()}…` : (adminMode ? "Search pages, users, content…" : "Search everything…")}
            className="flex-1 bg-transparent text-lg text-fg outline-none placeholder:text-fg-subtle"
          />
          <kbd className="hidden rounded border border-border bg-surface-2 px-1.5 py-0.5 text-[10px] font-medium text-fg-subtle sm:inline">ESC</kbd>
          <button onClick={onClose} className="text-fg-muted transition-colors hover:text-fg" aria-label="Close search">
            <X size={20} />
          </button>
        </div>

        {/* Scope chips — the fast path: click instead of typing a prefix */}
        <div className="flex flex-wrap items-center gap-1.5 border-b border-border px-5 pb-3 pt-3">
          <ScopeChip active={effectiveScope === null} onClick={() => { setActiveScope(null); if (parsed.scope) setQuery(term); }}>
            All
          </ScopeChip>
          {SEARCH_SCOPES.map((s) => {
            const Icon = s.icon;
            const active = effectiveScope?.id === s.id;
            return (
              <ScopeChip key={s.id} active={active} onClick={() => { setActiveScope(active ? null : s); if (parsed.scope) setQuery(term); inputRef.current?.focus(); }}>
                <Icon size={13} /> {s.label}
              </ScopeChip>
            );
          })}
        </div>

        {/* Results / empty-state */}
        <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3">
          {/* ── EMPTY STATE: recent searches + browse pages (public modal only) ── */}
          {isEmpty && !adminMode && (
            <div className="space-y-4">
              {recent.length > 0 && (
                <div>
                  <div className="mb-1.5 flex items-center justify-between px-2">
                    <h3 className="text-xs font-semibold uppercase tracking-wider text-fg-subtle">Recent</h3>
                    <button onClick={clearRecent} className="text-[11px] text-fg-subtle transition-colors hover:text-accent">Clear</button>
                  </div>
                  <div className="space-y-0.5">
                    {recent.map((t) => (
                      <div key={t} className="group flex items-center rounded-md transition-colors hover:bg-surface-2">
                        <button onClick={() => runRecent(t)} className="flex min-w-0 flex-1 items-center gap-2.5 px-3 py-2 text-left text-sm">
                          <Clock size={14} className="shrink-0 text-fg-subtle" />
                          <span className="flex-1 truncate text-fg">{t}</span>
                        </button>
                        <button onClick={() => removeRecent(t)} className="px-2.5 text-fg-subtle opacity-0 transition-opacity hover:text-danger group-hover:opacity-100" aria-label={`Remove ${t}`}>
                          <X size={13} />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              <div>
                <h3 className="mb-1.5 px-2 text-xs font-semibold uppercase tracking-wider text-fg-subtle">Browse</h3>
                <div className="grid grid-cols-2 gap-1 sm:grid-cols-3">
                  {PUBLIC_PAGES.map((p) => {
                    const Icon = p.icon;
                    return (
                      <button
                        key={p.href}
                        onClick={() => navigate(p.href)}
                        className="group flex items-center gap-2.5 rounded-lg border border-border bg-surface px-3 py-2.5 text-left transition-colors hover:border-accent/50 hover:bg-surface-2"
                      >
                        <Icon size={15} className="shrink-0 text-fg-subtle transition-colors group-hover:text-accent" />
                        <span className="truncate text-sm text-fg">{p.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>
          )}

          {loading && !hasResults && !isEmpty && <p className="py-6 text-center text-sm text-fg-muted">Searching…</p>}

          {/* Admin page jump-list */}
          {hasAdminPages && (
            <div className="mb-2">
              <h3 className="mb-1 px-2 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
                {isEmpty ? "Recent pages" : "Pages"}
              </h3>
              <div className="space-y-0.5">
                {adminPages.map((p) => (
                  <button key={p.href} onClick={() => navigate(p.href)} className="flex w-full items-center gap-2.5 rounded-md px-3 py-2 text-left text-sm transition-colors hover:bg-surface-2">
                    <LayoutDashboard size={14} className="shrink-0 text-fg-subtle" />
                    <span className="flex-1 truncate text-fg">{p.label}</span>
                    <span className="shrink-0 text-[10px] uppercase tracking-wider text-fg-subtle">admin</span>
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Matched public pages (live, local) */}
          {!isEmpty && pageMatches.length > 0 && (
            <SearchSection title="Pages">
              {pageMatches.map((p) => {
                const Icon = p.icon;
                return (
                  <button key={p.href} onClick={() => navigate(p.href)} className="group flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left transition-colors hover:bg-surface-2">
                    <Icon size={15} className="shrink-0 text-fg-subtle transition-colors group-hover:text-accent" />
                    <span className="flex-1 truncate text-sm text-fg group-hover:text-accent">{p.label}</span>
                    <ArrowRight size={13} className="shrink-0 text-fg-subtle opacity-0 transition-opacity group-hover:opacity-100" />
                  </button>
                );
              })}
            </SearchSection>
          )}

          {/* Quick suggestions while full results load */}
          {!isEmpty && suggestions.length > 0 && !hasResults && (effectiveScope?.kind || !effectiveScope) && (
            <div className="space-y-0.5">
              {suggestions.map((s, i) => (
                <button key={`${s.url}-${i}`} onClick={() => navigate(s.url)} className="flex w-full items-center gap-2.5 rounded-md px-3 py-2 text-left text-sm transition-colors hover:bg-surface-2">
                  <CornerDownLeft size={14} className="shrink-0 text-fg-subtle" />
                  <span className="flex-1 truncate text-fg">{s.label}</span>
                  <span className="shrink-0 text-[10px] uppercase tracking-wider text-fg-subtle">{s.type}</span>
                </button>
              ))}
            </div>
          )}

          {!loading && !isEmpty && !hasResults && pageMatches.length === 0 && !hasAdminPages && (
            <p className="py-6 text-center text-sm text-fg-muted">No results for &ldquo;{term}&rdquo;.</p>
          )}

          {/* Full results */}
          {!loading && hasResults && filtered && (
            <div className="space-y-3">
              {filtered.wiki.length > 0 && (
                <SearchSection title="Wiki">
                  {filtered.wiki.map((item) => (
                    <SearchItem key={item.id} label={item.title} sub={item.categorySlug} onClick={() => navigate(`/wiki/${item.slug}`)} />
                  ))}
                </SearchSection>
              )}
              {filtered.enemies.length > 0 && (
                <SearchSection title="Bestiary">
                  {filtered.enemies.map((item) => (
                    <SearchItem key={item.enemyId} label={item.name} sub={item.tier} onClick={() => navigate(`/bestiary/${item.enemyId}`)} />
                  ))}
                </SearchSection>
              )}
              {(filtered.items?.length ?? 0) > 0 && (
                <SearchSection title="Items">
                  {filtered.items.map((item) => (
                    <SearchItem
                      key={item.itemId}
                      label={item.name}
                      sub={`${item.rarity} · ${item.category}`}
                      image={resolveMediaUrl(item.imageUrl)}
                      onClick={() => navigate(adminMode ? "/admin/items" : `/items/${encodeURIComponent(item.itemId)}`)}
                    />
                  ))}
                </SearchSection>
              )}
              {(filtered.skills?.length ?? 0) > 0 && (
                <SearchSection title="Skills">
                  {filtered.skills.map((item) => (
                    <SearchItem
                      key={item.skillId}
                      label={item.name}
                      sub={`${item.rarity} · ${item.element}`}
                      image={resolveMediaUrl(item.imageUrl)}
                      onClick={() => navigate(adminMode ? "/admin/skills" : `/skills/${encodeURIComponent(item.skillId)}`)}
                    />
                  ))}
                </SearchSection>
              )}
              {filtered.posts.length > 0 && (
                <SearchSection title="Forum">
                  {filtered.posts.map((item) => (
                    <SearchItem key={item.id} label={item.threadTitle} sub={item.snippet} onClick={() => navigate(`/forum/${item.threadId}`)} />
                  ))}
                </SearchSection>
              )}
              {filtered.users.length > 0 && (
                <SearchSection title="Users">
                  {filtered.users.map((item) => (
                    <SearchItem key={item.id} label={item.displayName ?? item.username} sub={`@${item.username}`} onClick={() => navigate(adminMode ? `/admin/users/${item.id}` : `/u/${item.username}`)} />
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

function ScopeChip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition-colors ${
        active
          ? "border-accent bg-accent-soft text-accent"
          : "border-border text-fg-muted hover:border-border-strong hover:text-fg"
      }`}
    >
      {children}
    </button>
  );
}

/** Narrow API results to a single kind when a kind-bearing scope is active; else pass through. */
function filterResults(results: GlobalSearchResponse | null, scope: SearchScope | null): GlobalSearchResponse | null {
  if (!results) return null;
  if (!scope || !scope.kind) return results;
  return {
    ...results,
    wiki: scope.kind === "wiki" ? results.wiki : [],
    enemies: scope.kind === "enemy" ? results.enemies : [],
    posts: scope.kind === "forum" ? results.posts : [],
    users: scope.kind === "user" ? results.users : [],
    // Older service builds predate these buckets, so default rather than assume they're present.
    items: scope.kind === "item" ? results.items ?? [] : [],
    skills: scope.kind === "skill" ? results.skills ?? [] : [],
  };
}

function SearchSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="mb-1 px-2 text-xs font-semibold uppercase tracking-wider text-fg-subtle">{title}</h3>
      {children}
    </div>
  );
}

function SearchItem({ label, sub, image, onClick }: {
  label: string;
  sub: string;
  /** Optional thumbnail — item/skill artwork, where a picture identifies the row faster than text. */
  image?: string | null;
  onClick: () => void;
}) {
  return (
    <button onClick={onClick} className="group flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left transition-colors hover:bg-surface-2">
      {image && (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={image} alt="" className="size-8 shrink-0 rounded bg-surface-2 object-cover" />
      )}
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-medium text-fg transition-colors group-hover:text-accent">{label}</span>
        {sub && <span className="block line-clamp-1 text-xs text-fg-muted">{sub}</span>}
      </span>
    </button>
  );
}

