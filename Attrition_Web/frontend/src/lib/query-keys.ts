/**
 * Centralized TanStack Query keys. One source of truth so queries and the mutations that
 * invalidate them can never drift out of shape (a typo'd inline key = silently stale UI).
 *
 * Convention: each resource exposes broad keys (for invalidation) and specific keys (for a
 * single query). Invalidating a broad prefix (e.g. qk.forum.posts(threadId)) clears all the
 * paged/filtered variants beneath it because TanStack matches by key prefix.
 */
export const qk = {
  enemies: {
    list: (filter?: { tier?: string; search?: string }) =>
      filter ? (["enemies", filter] as const) : (["enemies"] as const),
    detail: (id: string) => ["enemy", id] as const,
  },

  wiki: {
    categories: () => ["wiki", "categories"] as const,
    articles: (filter?: unknown) =>
      filter ? (["wiki", "articles", filter] as const) : (["wiki", "articles"] as const),
    article: (slug: string | undefined) => ["wiki", "article", slug] as const,
    revisions: (slug: string) => ["wiki", "revisions", slug] as const,
  },

  forum: {
    categories: () => ["forum", "categories"] as const,
    threads: (filter?: unknown) =>
      filter ? (["forum", "threads", filter] as const) : (["forum", "threads"] as const),
    thread: (id: string) => ["forum", "thread", id] as const,
    posts: (threadId: string) => ["forum", "posts", threadId] as const,
    postsPage: (threadId: string, page: number) => ["forum", "posts", threadId, page] as const,
  },

  music: {
    albums: (page?: number) =>
      page === undefined ? (["music", "albums"] as const) : (["music", "albums", page] as const),
    album: (id: string) => ["album", id] as const,
  },

  assets: {
    list: (page: number) => ["assets", page] as const,
    all: () => ["assets"] as const,
  },

  characters: {
    mine: () => ["characters", "mine"] as const,
    detail: (id: string) => ["character", id] as const,
  },

  search: (q: string) => ["search", q] as const,
  profile: (username: string) => ["profile", username] as const,

  admin: {
    stats: () => ["admin", "stats"] as const,
    users: (page?: number) =>
      page === undefined ? (["admin", "users"] as const) : (["admin", "users", page] as const),
    enemies: () => ["admin", "enemies"] as const,
    assets: (page?: number) =>
      page === undefined ? (["admin", "assets"] as const) : (["admin", "assets", page] as const),
    characters: (page?: number) =>
      page === undefined ? (["admin", "characters"] as const) : (["admin", "characters", page] as const),
    character: (id: string) => ["admin", "character", id] as const,
    music: {
      albums: () => ["admin", "music", "albums"] as const,
      tracks: () => ["admin", "music", "tracks"] as const,
    },
    forum: {
      reports: (page?: number) =>
        page === undefined ? (["admin", "forum", "reports"] as const) : (["admin", "forum", "reports", page] as const),
      threads: (page?: number) =>
        page === undefined ? (["admin", "forum", "threads"] as const) : (["admin", "forum", "threads", page] as const),
    },
    wiki: {
      articles: () => ["admin", "wiki", "articles"] as const,
      contributions: () => ["admin", "wiki", "contributions"] as const,
    },
  },
} as const;
