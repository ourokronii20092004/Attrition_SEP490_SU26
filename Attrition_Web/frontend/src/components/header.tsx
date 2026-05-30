"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { Search, Menu, X, User, LogOut, Shield, Gamepad2, Settings as SettingsIcon } from "lucide-react";
import { useAuth } from "@/lib/providers";
import { SITE_NAME } from "@/lib/config";
import { Avatar } from "@/components/ui/avatar";
import { IconButton } from "@/components/ui/icon-button";
import { SearchModal } from "./search-modal";
import { ThemeSwitcher } from "./theme-switcher";

const NAV_LINKS = [
  { href: "/wiki", label: "Wiki" },
  { href: "/bestiary", label: "Bestiary" },
  { href: "/forum", label: "Forum" },
  { href: "/music", label: "Music" },
  { href: "/gallery", label: "Gallery" },
  { href: "/about", label: "About" },
];

export function Header() {
  const { user, loading, logout } = useAuth();
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);

  const isActive = (href: string) => pathname === href || pathname.startsWith(href + "/");

  return (
    <>
      <header className="glass sticky top-0 z-[200] border-x-0 border-t-0">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-5 sm:px-8">
          <div className="flex items-center gap-9">
            <Link
              href="/"
              className="group flex items-center gap-2 transition-opacity hover:opacity-90"
            >
              <span className="h-2 w-2 rounded-full bg-accent animate-pulse-glow transition-transform group-hover:scale-125" />
              <span className="font-display text-lg font-bold uppercase tracking-[0.2em] text-fg">
                {SITE_NAME}
              </span>
            </Link>
            <nav className="hidden items-center gap-1 md:flex">
              {NAV_LINKS.map((l) => (
                <Link
                  key={l.href}
                  href={l.href}
                  className={`relative rounded-md px-3 py-2 text-xs font-medium uppercase tracking-[0.15em] transition-colors ${
                    isActive(l.href) ? "text-accent" : "text-fg-muted hover:text-fg"
                  }`}
                >
                  {l.label}
                  {isActive(l.href) && (
                    <span className="absolute inset-x-3 -bottom-0.5 h-px bg-accent shadow-[var(--shadow-glow)]" />
                  )}
                </Link>
              ))}
            </nav>
          </div>

          <div className="flex items-center gap-2">
            <IconButton label="Search" onClick={() => setSearchOpen(true)}>
              <Search size={20} />
            </IconButton>

            <ThemeSwitcher />

            {!loading && !user && (
              <Link
                href="/login"
                className="rounded-md bg-accent px-4 py-2 text-xs font-semibold uppercase tracking-[0.15em] text-accent-fg shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_40%,transparent)] transition-[transform,box-shadow,filter] duration-200 hover:shadow-[var(--shadow-glow)] hover:brightness-105 active:scale-[0.97]"
              >
                Sign In
              </Link>
            )}

            {!loading && user && (
              <div className="relative">
                <button
                  onClick={() => setUserMenuOpen(!userMenuOpen)}
                  className="flex items-center rounded-full p-0.5 ring-2 ring-transparent transition hover:ring-border-strong focus-visible:outline-none focus-visible:ring-accent"
                  aria-label="Account menu"
                  aria-expanded={userMenuOpen}
                >
                  <Avatar src={user.avatarUrl} name={user.displayName} size="sm" />
                </button>

                {userMenuOpen && (
                  <>
                    <div className="fixed inset-0 z-[90]" onClick={() => setUserMenuOpen(false)} aria-hidden />
                    <div className="glass absolute right-0 top-full z-[100] mt-2 w-56 origin-top-right animate-fade-in rounded-xl p-1.5 shadow-[var(--shadow-lg)]">
                      <div className="flex items-center gap-3 border-b border-border px-3 py-2.5">
                        <Avatar src={user.avatarUrl} name={user.displayName} size="md" />
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold text-fg">{user.displayName}</p>
                          <p className="truncate text-xs text-fg-muted">@{user.username}</p>
                        </div>
                      </div>
                      <div className="py-1">
                        <MenuLink href={`/u/${user.username}`} icon={User} onClick={() => setUserMenuOpen(false)}>
                          Profile
                        </MenuLink>
                        <MenuLink href="/characters" icon={Gamepad2} onClick={() => setUserMenuOpen(false)}>
                          Characters
                        </MenuLink>
                        <MenuLink href="/settings" icon={SettingsIcon} onClick={() => setUserMenuOpen(false)}>
                          Settings
                        </MenuLink>
                        {user.role === "Admin" && (
                          <MenuLink href="/admin" icon={Shield} onClick={() => setUserMenuOpen(false)}>
                            Admin
                          </MenuLink>
                        )}
                      </div>
                      <div className="border-t border-border pt-1">
                        <button
                          onClick={() => { logout(); setUserMenuOpen(false); }}
                          className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-danger transition-colors hover:bg-danger/10"
                        >
                          <LogOut size={16} /> Sign Out
                        </button>
                      </div>
                    </div>
                  </>
                )}
              </div>
            )}

            <button
              onClick={() => setMobileOpen(!mobileOpen)}
              className="rounded-lg p-2 text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg md:hidden"
              aria-label="Toggle menu"
            >
              {mobileOpen ? <X size={20} /> : <Menu size={20} />}
            </button>
          </div>
        </div>

        {mobileOpen && (
          <nav className="stagger border-t border-border px-4 py-3 md:hidden">
            {NAV_LINKS.map((l) => (
              <Link
                key={l.href}
                href={l.href}
                onClick={() => setMobileOpen(false)}
                className={`block rounded-lg px-3 py-2.5 font-medium transition-colors ${
                  isActive(l.href) ? "bg-accent-soft text-accent" : "text-fg-muted hover:bg-surface-2 hover:text-fg"
                }`}
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

function MenuLink({
  href,
  icon: Icon,
  onClick,
  children,
}: {
  href: string;
  icon: React.ComponentType<{ size?: number }>;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      onClick={onClick}
      className="flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg"
    >
      <Icon size={16} /> {children}
    </Link>
  );
}