"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, Users, BookOpen, MessagesSquare, Skull, Image as ImageIcon, Music, Gamepad2,
} from "lucide-react";

const NAV = [
  { href: "/admin", label: "Dashboard", icon: LayoutDashboard, exact: true },
  { href: "/admin/users", label: "Users", icon: Users },
  { href: "/admin/wiki", label: "Wiki Queue", icon: BookOpen },
  { href: "/admin/forum", label: "Forum Reports", icon: MessagesSquare },
  { href: "/admin/enemies", label: "Enemies", icon: Skull },
  { href: "/admin/assets", label: "Assets", icon: ImageIcon },
  { href: "/admin/music", label: "Music", icon: Music },
  { href: "/admin/characters", label: "Characters", icon: Gamepad2 },
];

export function AdminNav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  const isActive = (href: string, exact?: boolean) =>
    exact ? pathname === href : pathname === href || pathname.startsWith(href + "/");

  return (
    <nav className="space-y-1">
      {NAV.map(({ href, label, icon: Icon, exact }) => {
        const active = isActive(href, exact);
        return (
          <Link
            key={href}
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
        );
      })}
    </nav>
  );
}
