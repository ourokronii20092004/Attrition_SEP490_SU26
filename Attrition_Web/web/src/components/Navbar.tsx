'use client';
import { useState, useRef, useEffect } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';
import Avatar from './Avatar';

const COLLECTION_BASE_URL = 'https://collection.hault.io.vn';

function getCollectionUrl(): string {
  if (typeof window === 'undefined') return COLLECTION_BASE_URL;
  const token = localStorage.getItem('attrition-token');
  const refresh = localStorage.getItem('attrition-refresh');
  if (token && refresh) {
    return `${COLLECTION_BASE_URL}?token=${encodeURIComponent(token)}&refresh=${encodeURIComponent(refresh)}`;
  }
  return COLLECTION_BASE_URL;
}

const NAV_LINKS = [
  { href: '/', label: 'Home' },
  { href: '/wiki', label: 'Wiki' },
  { href: '/forum', label: 'Forum' },
  { href: '/about', label: 'About' },
];

export default function Navbar() {
  const { user, loading, logout } = useAuth();
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Close dropdown on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

  // Close mobile menu on route change
  useEffect(() => {
    setMobileOpen(false);
    setDropdownOpen(false);
  }, [pathname]);

  const isActive = (href: string) => {
    if (href === '/') return pathname === '/';
    return pathname.startsWith(href);
  };

  return (
    <nav className="navbar">
      <div className="navbar-inner">
        {/* Brand */}
        <Link href="/" className="navbar-brand">
          ATTRITION
        </Link>

        {/* Desktop links */}
        <div className="navbar-links">
          {NAV_LINKS.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={`nav-link ${isActive(link.href) ? 'active' : ''}`}
            >
              {link.label}
            </Link>
          ))}
          <a
            href={getCollectionUrl()}
            target="_blank"
            rel="noopener noreferrer"
            className="nav-link nav-link-gold"
          >
            ✦ Collection
          </a>
          {!loading && user?.role === 'Admin' && (
            <Link
              href="/admin"
              className={`nav-link ${pathname.startsWith('/admin') ? 'active' : ''}`}
            >
              ⚙ Admin
            </Link>
          )}
        </div>

        {/* Right side */}
        <div className="navbar-right">
          {loading ? (
            <div className="spinner" />
          ) : user ? (
            <div className="navbar-user" ref={dropdownRef}>
              <button
                className="navbar-user-btn"
                onClick={() => setDropdownOpen(!dropdownOpen)}
              >
                <Avatar src={user.avatarUrl} alt={user.username} size="sm" />
                <span>{user.username}</span>
                <span>{dropdownOpen ? '▴' : '▾'}</span>
              </button>

              {dropdownOpen && (
                <div className="navbar-dropdown">
                  <Link href={`/profile/${user.username}`}>
                    👤 Profile
                  </Link>
                  <Link href="/profile/settings">
                    ⚙ Settings
                  </Link>
                  <div className="dropdown-divider" />
                  <button onClick={logout}>
                    ↪ Logout
                  </button>
                </div>
              )}
            </div>
          ) : (
            <div className="navbar-right gap-sm">
              <Link href="/auth/login" className="btn btn-ghost btn-sm">
                Sign In
              </Link>
              <Link href="/auth/register" className="btn btn-primary btn-sm">
                Sign Up
              </Link>
            </div>
          )}

          {/* Mobile hamburger */}
          <button
            className="hamburger"
            onClick={() => setMobileOpen(!mobileOpen)}
            aria-label="Toggle menu"
          >
            {mobileOpen ? '✕' : '☰'}
          </button>
        </div>
      </div>

      {/* Mobile menu */}
      <div className={`mobile-menu ${mobileOpen ? 'open' : ''}`}>
        {NAV_LINKS.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            className={`nav-link ${isActive(link.href) ? 'active' : ''}`}
          >
            {link.label}
          </Link>
        ))}
        <a
          href={getCollectionUrl()}
          target="_blank"
          rel="noopener noreferrer"
          className="nav-link nav-link-gold"
        >
          ✦ Collection
        </a>
        {!loading && user?.role === 'Admin' && (
          <Link href="/admin" className="nav-link">⚙ Admin</Link>
        )}
        {!loading && !user && (
          <>
            <Link href="/auth/login" className="nav-link">Sign In</Link>
            <Link href="/auth/register" className="nav-link">Sign Up</Link>
          </>
        )}
        {!loading && user && (
          <>
            <Link href={`/profile/${user.username}`} className="nav-link">👤 Profile</Link>
            <Link href="/profile/settings" className="nav-link">⚙ Settings</Link>
            <button className="nav-link" onClick={logout} style={{ background: 'none', border: 'none', textAlign: 'left', width: '100%', cursor: 'pointer' }}>
              ↪ Logout
            </button>
          </>
        )}
      </div>
    </nav>
  );
}