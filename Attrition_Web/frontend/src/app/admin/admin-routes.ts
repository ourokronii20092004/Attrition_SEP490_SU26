/** Admin route → human label registry, shared by the nav, breadcrumb, and recent-pages list. */
export const ADMIN_ROUTES: { href: string; label: string }[] = [
  { href: "/admin", label: "Dashboard" },
  { href: "/admin/users", label: "Users" },
  { href: "/admin/user-reports", label: "User Reports" },
  { href: "/admin/wiki", label: "Wiki" },
  { href: "/admin/wiki/articles", label: "Wiki · Articles" },
  { href: "/admin/wiki/queue", label: "Wiki · Contribution Queue" },
  { href: "/admin/wiki/categories", label: "Wiki · Categories" },
  { href: "/admin/forum", label: "Forum" },
  { href: "/admin/forum/reports", label: "Forum · Reports" },
  { href: "/admin/forum/threads", label: "Forum · Threads" },
  { href: "/admin/forum/categories", label: "Forum · Categories" },
  { href: "/admin/enemies", label: "Enemies" },
  { href: "/admin/items", label: "Items" },
  { href: "/admin/skills", label: "Skills" },
  { href: "/admin/assets", label: "Assets" },
  { href: "/admin/music", label: "Music" },
  { href: "/admin/music/albums", label: "Music · Albums" },
  { href: "/admin/music/tracks", label: "Music · Tracks" },
  { href: "/admin/characters", label: "Characters" },
  { href: "/admin/account", label: "My Account" },
];

const LABEL_CACHE_KEY = "attrition:admin:pageLabels";
/** Fired when a dynamic page registers its human label, so the top-bar can re-render. */
export const ADMIN_LABEL_EVENT = "attrition:admin:label";

const GUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const isOpaqueId = (seg: string) => GUID_RE.test(seg) || /^\d+$/.test(seg);

function readCache(): Record<string, string> {
  if (typeof window === "undefined") return {};
  try {
    return JSON.parse(localStorage.getItem(LABEL_CACHE_KEY) || "{}");
  } catch {
    return {};
  }
}

/**
 * Detail pages call this once the entity name is known so the breadcrumb and recent-pages
 * chips show e.g. "Users · alice" instead of a raw GUID. The label is cached in localStorage
 * and a window event nudges the top-bar to re-read it.
 */
export function setAdminPageLabel(path: string, label: string) {
  if (typeof window === "undefined") return;
  const cache = readCache();
  if (cache[path] === label) return;
  cache[path] = label;
  // Cap the cache so it can't grow unbounded.
  const entries = Object.entries(cache);
  if (entries.length > 50) {
    for (const [k] of entries.slice(0, entries.length - 50)) delete cache[k];
  }
  localStorage.setItem(LABEL_CACHE_KEY, JSON.stringify(cache));
  window.dispatchEvent(new CustomEvent(ADMIN_LABEL_EVENT));
}

/** Best-effort label for an admin path. Order: cached entity name → static route → derived. */
export function adminLabelFor(path: string): string {
  const cached = readCache()[path];
  if (cached) return cached;

  const exact = ADMIN_ROUTES.find((r) => r.href === path);
  if (exact) return exact.label;

  // Dynamic detail route (e.g. /admin/users/<guid>): derive "<Parent> · Detail" rather than
  // surfacing an unreadable GUID/numeric id.
  const segs = path.split("/").filter(Boolean);
  const last = segs[segs.length - 1] ?? "";
  if (isOpaqueId(last) && segs.length > 1) {
    const parent = "/" + segs.slice(0, -1).join("/");
    const parentLabel = ADMIN_ROUTES.find((r) => r.href === parent)?.label;
    if (parentLabel) return `${parentLabel} · Detail`;
  }
  return last.charAt(0).toUpperCase() + last.slice(1);
}
