'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { FiHome, FiDisc, FiHeart, FiLogIn, FiLogOut, FiExternalLink } from 'react-icons/fi';
import { useAuth } from '@/contexts/AuthContext';

const LOGIN_URL =
  'https://attrition.hault.io.vn/auth/login?returnUrl=https://collection.hault.io.vn/auth/relay';
const GAME_URL = 'https://attrition.hault.io.vn';

const NAV_ITEMS = [
  { href: '/', label: 'Home', icon: FiHome },
  { href: '/albums', label: 'Albums', icon: FiDisc },
  { href: '/favorites', label: 'Favorites', icon: FiHeart },
];

export default function Sidebar() {
  const pathname = usePathname();
  const { user, loading, logout } = useAuth();

  const isActive = (href: string) => {
    if (href === '/') return pathname === '/';
    return pathname.startsWith(href);
  };

  const getBackToGameUrl = () => {
    if (user) {
      const token = typeof window !== 'undefined' ? localStorage.getItem('attrition-token') : '';
      const refresh = typeof window !== 'undefined' ? localStorage.getItem('attrition-refresh') : '';
      if (token && refresh) {
        return `${GAME_URL}/auth/relay?token=${encodeURIComponent(token)}&refresh=${encodeURIComponent(refresh)}&returnUrl=${encodeURIComponent(GAME_URL)}`;
      }
    }
    return GAME_URL;
  };

  return (
    <aside className="collection-sidebar">
      {/* Logo */}
      <div className="sidebar-logo">
        <Link href="/">
          <h1>COLLECTION</h1>
          <div className="sidebar-subtitle">Attrition OST</div>
        </Link>
      </div>

      {/* Navigation */}
      <nav className="sidebar-nav">
        {NAV_ITEMS.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={`sidebar-nav-link ${isActive(item.href) ? 'active' : ''}`}
          >
            <item.icon />
            <span>{item.label}</span>
          </Link>
        ))}
      </nav>

      <div className="sidebar-divider" />

      {/* Auth section */}
      <div className="sidebar-auth">
        {loading ? (
          <div className="skeleton skeleton-text" style={{ width: '100%', height: '32px' }} />
        ) : user ? (
          <>
            <div className="sidebar-user">
              <div className="sidebar-user-avatar">
                {user.avatarUrl ? (
                  <img src={user.avatarUrl} alt={user.username} />
                ) : (
                  user.username.charAt(0).toUpperCase()
                )}
              </div>
              <div className="sidebar-user-info">
                <div className="sidebar-user-name">{user.username}</div>
                <div className="sidebar-user-role">{user.role}</div>
              </div>
            </div>
            <button
              onClick={logout}
              className="sidebar-login-btn"
              style={{ marginTop: '8px' }}
            >
              <FiLogOut />
              Logout
            </button>
          </>
        ) : (
          <a href={LOGIN_URL} className="sidebar-login-btn">
            <FiLogIn />
            Login
          </a>
        )}

        <a href={getBackToGameUrl()} className="sidebar-back-link" target="_blank" rel="noopener noreferrer">
          <FiExternalLink />
          Back to Game
        </a>
      </div>
    </aside>
  );
}
