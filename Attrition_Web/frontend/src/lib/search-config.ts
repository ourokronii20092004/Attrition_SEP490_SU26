import {
  BookOpen, Skull, MessagesSquare, Users, Gem, Music, Images, ScrollText, Globe, Sparkles,
  type LucideIcon,
} from "lucide-react";

// Search configuration — a single registry that drives the search modal's scope
// chips, prefix parsing, and the empty-state browse links. Adding a new search
// area later means appending ONE entry here; the modal needs no other change.
//
//   - `backend`: scopes the global API search (the prefix the Search.Service
//     understands). Its chip narrows API results client-side by result kind.
//   - `aliases`: typed-prefix forms ("enemy:" / "bestiary:") that map to this scope.
//   - `pages`: static/client-only routes (Items, Music, Story, …) that the API
//     search doesn't cover — matched locally by label so they're instantly findable.

export type ResultKind = "wiki" | "enemy" | "forum" | "user" | "item" | "skill";

export interface SearchScope {
  /** Stable id; also the canonical prefix (e.g. "wiki" → type `wiki:`). */
  id: string;
  label: string;
  icon: LucideIcon;
  /** Alternate prefixes that resolve to this scope. */
  aliases: string[];
  /** Which API result kind this scope filters to, if any. */
  kind?: ResultKind;
  /** Backend scope token sent to the API (defaults to id when kind is set). */
  backend?: string;
}

/**
 * The scope registry. Order = chip order. `kind` ties a chip to an API result
 * bucket; scopes without `kind` (e.g. "pages") only filter the local page list.
 */
export const SEARCH_SCOPES: SearchScope[] = [
  { id: "wiki", label: "Wiki", icon: BookOpen, aliases: ["lore", "article"], kind: "wiki" },
  { id: "enemy", label: "Bestiary", icon: Skull, aliases: ["bestiary", "monster"], kind: "enemy" },
  { id: "item", label: "Items", icon: Gem, aliases: ["items", "loot", "gear"], kind: "item" },
  { id: "skill", label: "Skills", icon: Sparkles, aliases: ["skills", "ability"], kind: "skill" },
  { id: "forum", label: "Forum", icon: MessagesSquare, aliases: ["post", "thread"], kind: "forum" },
  { id: "user", label: "Users", icon: Users, aliases: ["users", "member", "profile"], kind: "user" },
  { id: "pages", label: "Pages", icon: Globe, aliases: ["page", "go", "nav"] },
];

export interface SearchPage {
  label: string;
  href: string;
  icon: LucideIcon;
  /** Extra terms that should match this page (beyond its label). */
  keywords?: string[];
}

/**
 * Public routes the API search can't reach (static/client-rendered). Matched by
 * label + keywords for instant local navigation. Append here to make a new page
 * findable in search.
 */
export const PUBLIC_PAGES: SearchPage[] = [
  { label: "Wiki", href: "/wiki", icon: BookOpen, keywords: ["lore", "canon"] },
  { label: "Bestiary", href: "/bestiary", icon: Skull, keywords: ["enemies", "monsters"] },
  { label: "Items", href: "/items", icon: Gem, keywords: ["loot", "gear", "drops"] },
  { label: "World", href: "/world", icon: Globe, keywords: ["map", "regions", "biomes"] },
  { label: "Forum", href: "/forum", icon: MessagesSquare, keywords: ["community", "threads"] },
  { label: "Music", href: "/music", icon: Music, keywords: ["soundtrack", "ost", "tracks"] },
  { label: "Gallery", href: "/gallery", icon: Images, keywords: ["art", "concept"] },
  { label: "The Story", href: "/story", icon: ScrollText, keywords: ["lore", "eldravir", "plot", "characters"] },
  { label: "Read the Manuscript", href: "/story/read", icon: BookOpen, keywords: ["chapters", "read", "novel"] },
];

const SCOPE_BY_TOKEN = new Map<string, SearchScope>();
for (const s of SEARCH_SCOPES) {
  SCOPE_BY_TOKEN.set(s.id, s);
  for (const a of s.aliases) SCOPE_BY_TOKEN.set(a, s);
}

export function resolveScopeToken(token: string): SearchScope | undefined {
  return SCOPE_BY_TOKEN.get(token.trim().toLowerCase());
}

/** Parse a "prefix:term" query into a scope + remaining term. Unknown prefix → no scope. */
export function parseQuery(raw: string): { scope: SearchScope | null; term: string } {
  const trimmed = raw.trim();
  const colon = trimmed.indexOf(":");
  if (colon > 0) {
    const scope = resolveScopeToken(trimmed.slice(0, colon));
    if (scope) return { scope, term: trimmed.slice(colon + 1).trim() };
  }
  return { scope: null, term: trimmed };
}

/** The backend scope token for an active scope (what the API expects), or undefined for "all". */
export function backendScope(scope: SearchScope | null): string | undefined {
  if (!scope) return undefined;
  return scope.backend ?? (scope.kind ? scope.id : undefined);
}

export function matchPages(term: string, limit = 6, pages: SearchPage[] = PUBLIC_PAGES): SearchPage[] {
  const q = term.trim().toLowerCase();
  if (!q) return pages;
  return pages.filter(
    (p) => p.label.toLowerCase().includes(q) || (p.keywords ?? []).some((k) => k.includes(q)),
  ).slice(0, limit);
}
