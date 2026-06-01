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
  { href: "/admin/assets", label: "Assets" },
  { href: "/admin/music", label: "Music" },
  { href: "/admin/music/albums", label: "Music · Albums" },
  { href: "/admin/music/tracks", label: "Music · Tracks" },
  { href: "/admin/characters", label: "Characters" },
  { href: "/admin/account", label: "My Account" },
];

/** Best-effort label for an admin path (falls back to a title-cased last segment). */
export function adminLabelFor(path: string): string {
  const exact = ADMIN_ROUTES.find((r) => r.href === path);
  if (exact) return exact.label;
  const seg = path.split("/").filter(Boolean).pop() ?? "";
  return seg.charAt(0).toUpperCase() + seg.slice(1);
}
