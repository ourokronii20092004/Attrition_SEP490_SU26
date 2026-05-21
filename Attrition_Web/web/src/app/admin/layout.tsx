"use client";

import React, { useEffect } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { cn } from "@/lib/utils";
import styles from "./admin.module.css";

const SIDEBAR_LINKS = [
  { href: "/admin", label: "Dashboard", icon: "◉" },
  { href: "/admin/users", label: "Users", icon: "👤" },
  {
    label: "Wiki",
    icon: "📖",
    children: [
      { href: "/admin/wiki", label: "Articles" },
      { href: "/admin/wiki/categories", label: "Categories" },
      { href: "/admin/wiki/contributions", label: "Contributions" },
    ],
  },
  {
    label: "Forum",
    icon: "💬",
    children: [
      { href: "/admin/forum", label: "Threads" },
      { href: "/admin/forum/posts", label: "Posts" },
    ],
  },
  {
    label: "Music",
    icon: "🎵",
    children: [
      { href: "/admin/music", label: "Albums" },
      { href: "/admin/music/tracks", label: "Tracks" },
    ],
  },
];

function AdminSidebar() {
  const pathname = usePathname();

  return (
    <aside className={styles.sidebar} aria-label="Admin navigation">
      <div className={styles.sidebarHeader}>
        <Link href="/admin" className={styles.sidebarLogo}>
          ATTRITION
        </Link>
        <span className={styles.sidebarBadge}>Admin</span>
      </div>

      <nav className={styles.sidebarNav}>
        {SIDEBAR_LINKS.map((link) => {
          if ("children" in link && link.children) {
            const isActive = link.children.some((child) =>
              pathname.startsWith(child.href)
            );
            return (
              <div key={link.label} className={styles.navGroup}>
                <span
                  className={cn(
                    styles.navGroupLabel,
                    isActive && styles.navGroupLabelActive
                  )}
                >
                  <span className={styles.navIcon}>{link.icon}</span>
                  {link.label}
                </span>
                <div className={styles.navGroupItems}>
                  {link.children.map((child) => (
                    <Link
                      key={child.href}
                      href={child.href}
                      className={cn(
                        styles.navItem,
                        styles.navItemChild,
                        pathname === child.href && styles.navItemActive
                      )}
                    >
                      {child.label}
                    </Link>
                  ))}
                </div>
              </div>
            );
          }

          return (
            <Link
              key={link.href}
              href={link.href!}
              className={cn(
                styles.navItem,
                pathname === link.href && styles.navItemActive
              )}
            >
              <span className={styles.navIcon}>{link.icon}</span>
              {link.label}
            </Link>
          );
        })}
      </nav>

      <div className={styles.sidebarFooter}>
        <Link
          href="/"
          className={styles.exitAdmin}
          title="Leave admin panel"
        >
          ← Back to Site
        </Link>
      </div>
    </aside>
  );
}

function AdminHeader() {
  const { user, logout } = useAuth();
  const router = useRouter();

  return (
    <header className={styles.header}>
      <div className={styles.headerLeft}>
        <h2 className={styles.headerTitle}>Admin Panel</h2>
      </div>
      <div className={styles.headerRight}>
        {user && (
          <div className={styles.headerUser}>
            <span className={styles.headerUserName}>
              {user.displayName || user.username}
            </span>
            <button
              className="btn btn-ghost btn-sm"
              onClick={() => {
                logout();
                router.push("/");
              }}
            >
              Logout
            </button>
          </div>
        )}
      </div>
    </header>
  );
}

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { user, isLoading, isAdmin } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && (!user || !isAdmin)) {
      router.replace("/login?redirect=/admin");
    }
  }, [isLoading, user, isAdmin, router]);

  // Show nothing while checking auth
  if (isLoading) {
    return (
      <div className={styles.loadingScreen}>
        <div className={styles.loadingSpinner} />
      </div>
    );
  }

  if (!user || !isAdmin) {
    return null;
  }

  return (
    <div className={styles.adminLayout}>
      <AdminSidebar />
      <div className={styles.adminMain}>
        <AdminHeader />
        <main className={styles.adminContent}>{children}</main>
      </div>
    </div>
  );
}
