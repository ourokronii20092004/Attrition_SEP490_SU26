"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, Users, BookOpen, MessagesSquare, Skull, Image as ImageIcon, Music, Gamepad2, Flag, Gem, Sparkles, DoorOpen,
} from "lucide-react";

type NavItem = { href: string; label: string; icon: React.ComponentType<{ size?: number; className?: string }>; exact?: boolean; children?: { href: string; label: string }[] };
type NavGroup = { label?: string; items: NavItem[] };

// Grouped by content type. Wiki/Forum/Music expand into sub-routes that are ALWAYS visible (no
// collapse) — a dashboard should surface navigation, not hide it behind a click. Each child is one
// click away directly from the sidebar.
const NAV_GROUPS: NavGroup[] = [
  {
    items: [
      { href: "/admin", label: "Dashboard", icon: LayoutDashboard, exact: true },
      { href: "/admin/users", label: "Users", icon: Users },
      // Post reports and user reports are one tabbed workspace now — a single "is anything waiting?"
      // destination rather than two entries in two different groups.
      { href: "/admin/forum/reports", label: "Reports", icon: Flag, children: [
        { href: "/admin/forum/reports", label: "Post reports" },
        { href: "/admin/user-reports", label: "User reports" },
      ] },
    ],
  },
  {
    label: "Community",
    items: [
      { href: "/admin/wiki", label: "Wiki", icon: BookOpen, children: [
        { href: "/admin/wiki/articles", label: "Articles" },
        { href: "/admin/wiki/queue", label: "Contribution Queue" },
        { href: "/admin/wiki/categories", label: "Categories" },
      ] },
      { href: "/admin/forum", label: "Forum", icon: MessagesSquare, children: [
        { href: "/admin/forum/threads", label: "Threads" },
        { href: "/admin/forum/posts", label: "Removed Posts" },
        { href: "/admin/forum/categories", label: "Categories" },
      ] },
    ],
  },
  {
    label: "Game Content",
    items: [
      { href: "/admin/enemies", label: "Enemies", icon: Skull },
      { href: "/admin/items", label: "Items", icon: Gem },
      { href: "/admin/skills", label: "Skills", icon: Sparkles },
      { href: "/admin/assets", label: "Assets", icon: ImageIcon },
      { href: "/admin/music", label: "Music", icon: Music },
      { href: "/admin/characters", label: "Characters", icon: Gamepad2 },
      { href: "/admin/rooms", label: "Co-op Rooms", icon: DoorOpen },
    ],
  },
];

export function AdminNav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  // Items with children own an explicit route list, so their parent link only lights up for its
  // own exact path or one of those listed children — a blanket prefix match would also catch
  // sibling routes that happen to nest under the same segment (e.g. /admin/forum/reports living
  // under "Reports", not "Forum", even though both start with /admin/forum).
  const isActive = (item: NavItem) => {
    if (item.children) {
      return pathname === item.href || item.children.some((c) => pathname === c.href || pathname.startsWith(c.href + "/"));
    }
    return item.exact ? pathname === item.href : pathname === item.href || pathname.startsWith(item.href + "/");
  };

  return (
    <nav className="space-y-5">
      {NAV_GROUPS.map((group, gi) => (
        <div key={group.label ?? `group-${gi}`} className="space-y-1">
          {group.label && (
            <p className="px-3 pb-1 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
              {group.label}
            </p>
          )}
          {group.items.map((item) => {
            const { href, label, icon: Icon, children } = item;
            const active = isActive(item);
            return (
              <div key={href}>
                <Link
                  href={href}
                  onClick={onNavigate}
                  aria-current={active ? "page" : undefined}
                  className={`flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                    active
                      ? "bg-accent-soft text-accent"
                      : "text-fg-muted hover:bg-surface-2 hover:text-fg"
                  }`}
                >
                  <Icon size={18} className="shrink-0" />
                  {label}
                </Link>
                {children && (
                  <div className="mt-0.5 ml-5 space-y-0.5 border-l border-border pl-3">
                    {children.map((child) => {
                      const childActive = pathname === child.href || pathname.startsWith(child.href + "/");
                      return (
                        <Link
                          key={child.href}
                          href={child.href}
                          onClick={onNavigate}
                          aria-current={childActive ? "page" : undefined}
                          className={`block rounded-md px-3 py-1.5 text-sm transition-colors ${
                            childActive ? "bg-accent-soft text-accent" : "text-fg-muted hover:bg-surface-2 hover:text-fg"
                          }`}
                        >
                          {child.label}
                        </Link>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      ))}
    </nav>
  );
}
