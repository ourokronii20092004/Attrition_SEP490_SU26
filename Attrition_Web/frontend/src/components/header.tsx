"use client";

import Link from "next/link";
import { useState } from "react";
import { Search, Menu, X, User, LogOut, Shield, Gamepad2 } from "lucide-react";
import { useAuth } from "@/lib/providers";
import { SITE_NAME } from "@/lib/config";
import { resolveMediaUrl } from "@/lib/api/media";
import { SearchModal } from "./search-modal";

const NAV_LINKS = [
  { href: "/wiki", label: "Wiki" },
  { href: "/bestiary", label: "Bestiary" },
  { href: "/forum", label: "Forum" },
  { href: "/music", label: "Music" },
  { href: "/gallery", label: "Gallery" },
];

export function Header() {
  const { user, loading, logout } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);

  return (
    <>
      <header className="sticky top-0 z-40 border-b border-border bg-surface/80 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4">
          <div className="flex items-center gap-6">
            <Link href="/" className="font-display text-xl font-bold text-accent">
              {SITE_NAME}
            </Link>
            <nav className="hidden items-center gap-1 md:flex">
              {NAV_LINKS.map((l) => (
                <Link
                  key={l.href}
                  href={l.href}
                  className="rounded-md px-3 py-2 text-sm text-fg-muted transition hover:bg-surface-2 hover:text-fg"
                >
                  {l.label}
                </Link>
              ))}
            </nav>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => setSearchOpen(true)}
              className="rounded-md p-2 text-fg-muted transition hover:bg-surface-2 hover:text-fg"
              aria-label="Search"
            >
              <Search size={20} />
            </button>

            {!loading && !user && (
              <Link href="/login" className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-fg transition hover:opacity-90">
                Sign In
              </Link>
            )}

            {!loading && user && (
              <div className="relative">
                <button
                  onClick={() => setUserMenuOpen(!userMenuOpen)}
                  className="flex items-center gap-2 rounded-md p-1.5 transition hover:bg-surface-2"
                >
                  {user.avatarUrl ? (
                    <img
                      src={resolveMediaUrl(user.avatarUrl) ?? ""}
                      alt=""
                      className="h-8 w-8 rounded-full object-cover"
                    />
                  ) : (
                    <div className="flex h-8 w-8 items-center justify-center rounded-full bg-accent-soft text-accent">
                      <User size={16} />
                    </div>
                  )}
                </button>

                {userMenuOpen && (
                  <div className="absolute right-0 top-full mt-2 w-48 rounded-lg border border-border bg-surface p-1 shadow-lg">
                    <div className="border-b border-border px-3 py-2">
                      <p className="text-sm font-medium text-fg">{user.displayName}</p>
                      <p className="text-xs text-fg-muted">@{user.username}</p>
                    </div>
                    <Link
                      href={`/u/${user.username}`}
                      onClick={() => setUserMenuOpen(false)}
                      className="flex items-center gap-2 rounded-md px-3 py-2 text-sm text-fg-muted hover:bg-surface-2 hover:text-fg"
                    >
                      <User size={14} /> Profile
                    </Link>
                    <Link
                      href="/characters"
                      onClick={() => setUserMenuOpen(false)}
                      className="flex items-center gap-2 rounded-md px-3 py-2 text-sm text-fg-muted hover:bg-surface-2 hover:text-fg"
                    >
                      <Gamepad2 size={14} /> Characters
                    </Link>
                    <Link
                      href="/settings"
                      onClick={() => setUserMenuOpen(false)}
                      className="flex items-center gap-2 rounded-md px-3 py-2 text-sm text-fg-muted hover:bg-surface-2 hover:text-fg"
                    >
                      Settings
                    </Link>
                    {user.role === "Admin" && (
                      <Link
                        href="/admin"
                        onClick={() => setUserMenuOpen(false)}
                        className="flex items-center gap-2 rounded-md px-3 py-2 text-sm text-fg-muted hover:bg-surface-2 hover:text-fg"
                      >
                        <Shield size={14} /> Admin
                      </Link>
                    )}
                    <button
                      onClick={() => { logout(); setUserMenuOpen(false); }}
                      className="flex w-full items-center gap-2 rounded-md px-3 py-2 text-sm text-danger hover:bg-surface-2"
                    >
                      <LogOut size={14} /> Sign Out
                    </button>
                  </div>
                )}
              </div>
            )}

            <button
              onClick={() => setMobileOpen(!mobileOpen)}
              className="rounded-md p-2 text-fg-muted md:hidden"
              aria-label="Toggle menu"
            >
              {mobileOpen ? <X size={20} /> : <Menu size={20} />}
            </button>
          </div>
        </div>

        {mobileOpen && (
          <nav className="border-t border-border px-4 py-3 md:hidden">
            {NAV_LINKS.map((l) => (
              <Link
                key={l.href}
                href={l.href}
                onClick={() => setMobileOpen(false)}
                className="block rounded-md px-3 py-2 text-fg-muted hover:bg-surface-2 hover:text-fg"
              >
                {l.label}
              </Link>
            ))}
          </nav>
        )}
      </header>

      {searchOpen && <SearchModal onClose={() => setSearchOpen(false)} />}
    </>
  );
}
