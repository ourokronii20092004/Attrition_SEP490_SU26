"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Menu, X, Shield, LogOut } from "lucide-react";
import { useAuth, useConfirm } from "@/lib/providers";
import { Avatar } from "@/components/ui/avatar";
import { PageLoader } from "@/components/ui/spinner";
import { AdminMusicPlayer } from "@/components/admin-music-player";
import { AdminNav } from "./admin-nav";
import { AdminTopBar } from "./admin-top-bar";
import { AdminHotkeys } from "./admin-hotkeys";

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const { user, loading, logout } = useAuth();
  const router = useRouter();
  const confirm = useConfirm();
  const [drawerOpen, setDrawerOpen] = useState(false);

  const handleLogout = async () => {
    const ok = await confirm({
      title: "Sign out?",
      message: "You'll be returned to the homepage and need to sign in again to access the admin area.",
      confirmLabel: "Sign out",
    });
    if (!ok) return;
    logout();
    router.push("/");
  };

  // Single guard for the whole admin section (replaces per-page role checks).
  if (loading) return <PageLoader />;

  if (!user || user.role !== "Admin") {
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center px-4 text-center">
        <Shield size={40} className="text-fg-subtle" />
        <h1 className="mt-4 font-display text-2xl font-bold text-fg">Admin access only</h1>
        <p className="mt-2 max-w-sm text-fg-muted">
          You need an administrator account to view this area.
        </p>
        <button
          onClick={() => router.push("/")}
          className="mt-6 rounded-lg bg-accent px-5 py-2.5 text-sm font-medium text-accent-fg transition hover:brightness-110 active:scale-[0.98]"
        >
          Back to site
        </button>
      </div>
    );
  }

  return (
    <div className="flex min-h-dvh bg-bg">
      <AdminHotkeys />
      {/* Desktop sidebar */}
      <aside className="sticky top-0 hidden h-dvh w-64 shrink-0 flex-col border-r border-border bg-surface/50 p-4 lg:flex">
        <AdminSidebarHeader />
        <div className="mt-6 flex-1 overflow-y-auto">
          <AdminNav />
        </div>
        <AdminAccountBlock user={user} onLogout={handleLogout} />
      </aside>

      {/* Mobile drawer */}
      {drawerOpen && (
        <div className="fixed inset-0 z-[300] lg:hidden">
          <div className="absolute inset-0 bg-black/80" onClick={() => setDrawerOpen(false)} aria-hidden />
          <aside className="absolute left-0 top-0 flex h-full w-72 flex-col border-r border-border bg-surface p-4 motion-safe:animate-fade-in">
            <div className="flex items-center justify-between">
              <AdminSidebarHeader />
              <button onClick={() => setDrawerOpen(false)} className="text-fg-muted hover:text-fg" aria-label="Close menu">
                <X size={20} />
              </button>
            </div>
            <div className="mt-6 flex-1 overflow-y-auto">
              <AdminNav onNavigate={() => setDrawerOpen(false)} />
            </div>
            <AdminAccountBlock user={user} onLogout={handleLogout} onNavigate={() => setDrawerOpen(false)} />
          </aside>
        </div>
      )}

      {/* Main column */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="glass sticky top-0 z-[200] flex h-14 items-center gap-3 border-x-0 border-t-0 px-4 lg:hidden">
          <button onClick={() => setDrawerOpen(true)} className="text-fg-muted hover:text-fg" aria-label="Open menu">
            <Menu size={20} />
          </button>
          <span className="font-display font-bold text-fg">Admin</span>
        </header>
        <AdminTopBar />
        <main id="main-content" className="flex-1 p-4 sm:p-6 lg:p-8">
          {children}
        </main>
      </div>
    </div>
  );
}

function AdminSidebarHeader() {
  return (
    <Link href="/admin" className="flex items-center gap-2 px-2">
      <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent-soft text-accent">
        <Shield size={18} />
      </span>
      <span className="font-display text-lg font-bold tracking-tight text-fg">Admin</span>
    </Link>
  );
}

function AdminAccountBlock({ user, onLogout, onNavigate }: {
  user: { username: string; displayName: string | null; avatarUrl: string | null };
  onLogout: () => void;
  onNavigate?: () => void;
}) {
  return (
    <div className="mt-4 border-t border-border pt-3">
      <Link
        href="/admin/account"
        onClick={onNavigate}
        className="flex items-center gap-3 rounded-lg px-2 py-2 transition-colors hover:bg-surface-2"
      >
        <Avatar src={user.avatarUrl} name={user.displayName ?? user.username} size="sm" />
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-fg">{user.displayName ?? user.username}</p>
          <p className="truncate text-xs text-fg-muted">@{user.username}</p>
        </div>
      </Link>

      {/* Mini now-playing line: only while a track is loaded; vanishes on stop. */}
      <AdminMusicPlayer />

      <div className="mt-1">
        <button
          onClick={() => { onNavigate?.(); onLogout(); }}
          className="flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-sm text-danger transition-colors hover:bg-danger/10"
        >
          <LogOut size={16} /> Sign Out
        </button>
      </div>
    </div>
  );
}