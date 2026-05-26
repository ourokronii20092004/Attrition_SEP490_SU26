"use client";

import React, { useState, useEffect, useRef } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useTheme, ACCENT_COLORS, type ThemeMode, type AccentName } from "@/contexts/ThemeContext";
import { cn, getInitials, getAvatarUrl } from "@/lib/utils";
import styles from "./Navbar.module.css";

// ─── Nav Links ─────────────────────────────────────────────

const NAV_LINKS = [
  { href: "/rooms", label: "Lobbies" },
  { href: "/wiki", label: "Wiki" },
  { href: "/forum", label: "Forum" },
  { href: "/collection", label: "Collection" },
  { href: "/about", label: "About" },
];


// ─── Theme Toggle ──────────────────────────────────────────

function ThemeToggle() {
  const { mode, setMode, resolvedMode } = useTheme();

  const cycle = () => {
    const modes: ThemeMode[] = ["light", "dark", "system"];
    const idx = modes.indexOf(mode);
    setMode(modes[(idx + 1) % modes.length]);
  };

  const icon = resolvedMode === "dark" ? "☾" : "☀";
  const label =
    mode === "system"
      ? `System (${resolvedMode})`
      : mode === "dark"
        ? "Dark"
        : "Light";

  return (
    <button
      className={cn("btn btn-ghost btn-icon btn-sm", styles.themeToggle)}
      onClick={cycle}
      aria-label={`Theme: ${label}`}
      title={`Theme: ${label}`}
    >
      <span className={styles.themeIcon}>{icon}</span>
    </button>
  );
}

// ─── User Menu ─────────────────────────────────────────────

function UserMenu() {
  const { user, logout, isAdmin } = useAuth();
  const [open, setOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    if (open) document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [open]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    if (open) document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open]);

  if (!user) return null;

  const avatarUrl = getAvatarUrl(user.avatarUrl);
  const initials = getInitials(user.displayName || user.username);

  return (
    <div className={styles.userMenu} ref={menuRef}>
      <button
        className={styles.userMenuButton}
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        aria-haspopup="true"
      >
        <span className="avatar avatar-sm">
          {avatarUrl ? (
            <img src={avatarUrl} alt={user.username} />
          ) : (
            initials
          )}
        </span>
        <span className={cn(styles.userName, "hide-mobile")}>
          {user.displayName || user.username}
        </span>
      </button>

      {open && (
        <div className="dropdown-menu" role="menu">
          <div className={styles.menuUserInfo}>
            <span className={styles.menuDisplayName}>
              {user.displayName || user.username}
            </span>
            <span className={styles.menuRole}>
              {user.role}
            </span>
          </div>
          <div className="dropdown-divider" />
          <Link
            href={`/profile/${user.username}`}
            className="dropdown-item"
            role="menuitem"
            onClick={() => setOpen(false)}
          >
            Profile
          </Link>
          <Link
            href="/profile/settings"
            className="dropdown-item"
            role="menuitem"
            onClick={() => setOpen(false)}
          >
            Settings
          </Link>
          {isAdmin && (
            <>
              <div className="dropdown-divider" />
              <Link
                href="/admin"
                className="dropdown-item"
                role="menuitem"
                onClick={() => setOpen(false)}
              >
                Admin Panel
              </Link>
            </>
          )}
          <div className="dropdown-divider" />
          <button
            className="dropdown-item dropdown-item-danger"
            role="menuitem"
            onClick={() => {
              logout();
              setOpen(false);
              window.location.href = "/";
            }}
          >
            Logout
          </button>
        </div>
      )}
    </div>
  );
}

// ─── Mobile Menu ───────────────────────────────────────────

function MobileMenu({
  isOpen,
  onClose,
}: {
  isOpen: boolean;
  onClose: () => void;
}) {
  const { user, logout, isAdmin } = useAuth();
  const { mode, setMode, accent, setAccent } = useTheme();

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => { document.body.style.overflow = ""; };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <>
      <div className={styles.mobileOverlay} onClick={onClose} />
      <nav className={styles.mobileMenu} aria-label="Mobile navigation">
        <div className={styles.mobileMenuHeader}>
          <span className={styles.mobileMenuTitle}>Menu</span>
          <button className="btn btn-ghost btn-icon btn-sm" onClick={onClose}>
            ✕
          </button>
        </div>

        <div className={styles.mobileMenuLinks}>
          {NAV_LINKS.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={styles.mobileLink}
              onClick={onClose}
            >
              {link.label}
            </Link>
          ))}
        </div>

        <div className={styles.mobileSection}>
          <span className={styles.mobileSectionLabel}>Theme</span>
          <div className={styles.themeOptions}>
            {(["light", "dark", "system"] as ThemeMode[]).map((m) => (
              <button
                key={m}
                className={cn(styles.themeOption, mode === m && styles.themeOptionActive)}
                onClick={() => setMode(m)}
              >
                {m === "light" ? "☀" : m === "dark" ? "☾" : "⚙"}{" "}
                {m.charAt(0).toUpperCase() + m.slice(1)}
              </button>
            ))}
          </div>
        </div>

        <div className={styles.mobileSection}>
          <span className={styles.mobileSectionLabel}>Accent</span>
          <div className={styles.accentOptions}>
            {(Object.keys(ACCENT_COLORS) as AccentName[]).map((name) => (
              <button
                key={name}
                className={cn(styles.accentSwatch, accent === name && styles.accentSwatchActive)}
                style={{ background: ACCENT_COLORS[name].hex }}
                onClick={() => setAccent(name)}
                title={ACCENT_COLORS[name].label}
                aria-label={`Accent: ${ACCENT_COLORS[name].label}`}
              />
            ))}
          </div>
        </div>

        <div className={styles.mobileMenuFooter}>
          {user ? (
            <>
              <Link href={`/profile/${user.username}`} className={styles.mobileLink} onClick={onClose}>
                Profile
              </Link>
              <Link href="/profile/settings" className={styles.mobileLink} onClick={onClose}>
                Settings
              </Link>
              {isAdmin && (
                <Link href="/admin" className={styles.mobileLink} onClick={onClose}>
                  Admin Panel
                </Link>
              )}
              <button
                className={cn(styles.mobileLink, styles.mobileLinkDanger)}
                onClick={() => {
                  logout();
                  onClose();
                  window.location.href = "/";
                }}
              >
                Logout
              </button>
            </>
          ) : (
            <Link href="/login" className="btn btn-primary btn-md" onClick={onClose} style={{ width: "100%" }}>
              Login
            </Link>
          )}
        </div>
      </nav>
    </>
  );
}

// ─── Navbar ────────────────────────────────────────────────

export default function Navbar({ transparent = false }: { transparent?: boolean }) {
  const pathname = usePathname();
  const { isAuthenticated, isLoading } = useAuth();
  const [scrolled, setScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    if (!transparent) {
      setScrolled(true);
      return;
    }

    const handler = () => setScrolled(window.scrollY > 50);
    handler();
    window.addEventListener("scroll", handler, { passive: true });
    return () => window.removeEventListener("scroll", handler);
  }, [transparent]);

  // Close mobile menu on route change
  useEffect(() => {
    setMobileOpen(false);
  }, [pathname]);

  return (
    <>
      <header
        className={cn(
          styles.navbar,
          scrolled && styles.navbarScrolled,
          transparent && !scrolled && styles.navbarTransparent
        )}
      >
        <div className={cn("container", styles.navbarInner)}>
          {/* Logo */}
          <Link href="/" className={styles.logo}>
            <span className={styles.logoText}>ATTRITION</span>
          </Link>

          {/* Desktop Nav */}
          <nav className={cn(styles.navLinks, "hide-mobile")} aria-label="Main navigation">
            {NAV_LINKS.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className={cn(
                  styles.navLink,
                  pathname.startsWith(link.href) && styles.navLinkActive
                )}
              >
                {link.label}
              </Link>
            ))}
          </nav>

          {/* Right side */}
          <div className={styles.navRight}>
            <ThemeToggle />

            {!isLoading && (
              <>
                {isAuthenticated ? (
                  <div className="hide-mobile">
                    <UserMenu />
                  </div>
                ) : (
                  <Link href="/login" className={cn("btn btn-primary btn-sm", "hide-mobile")}>
                    Login
                  </Link>
                )}
              </>
            )}

            {/* Mobile hamburger */}
            <button
              className={cn("btn btn-ghost btn-icon btn-sm", styles.hamburger)}
              onClick={() => setMobileOpen(true)}
              aria-label="Open menu"
            >
              <span className={styles.hamburgerIcon}>☰</span>
            </button>
          </div>
        </div>
      </header>

      <MobileMenu isOpen={mobileOpen} onClose={() => setMobileOpen(false)} />
    </>
  );
}
