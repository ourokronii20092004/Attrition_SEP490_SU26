'use client';
import { useEffect } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/contexts/AuthContext';

const SIDEBAR_LINKS = [
  { href: '/admin', label: 'Dashboard', icon: '📊' },
  { section: 'Users' },
  { href: '/admin/users', label: 'Manage Users', icon: '👥' },
  { section: 'Wiki' },
  { href: '/admin/wiki', label: 'Wiki Articles', icon: '📖' },
  { href: '/admin/wiki/categories', label: 'Wiki Categories', icon: '🏷️' },
  { href: '/admin/wiki/new', label: 'New Article', icon: '✏️' },
  { section: 'Forum' },
  { href: '/admin/forum', label: 'Forum Threads', icon: '💬' },
  { href: '/admin/forum/posts', label: 'Forum Posts', icon: '📝' },
  { section: 'Music' },
  { href: '/admin/music', label: 'Music Albums', icon: '🎵' },
  { href: '/admin/music/tracks', label: 'Music Tracks', icon: '🎶' },
];

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!loading && user?.role !== 'Admin') {
      router.push('/');
    }
  }, [user, loading, router]);

  if (loading) {
    return (
      <div className="loading-screen">
        <div className="spinner spinner-lg" />
        <p className="text-muted">Loading admin panel...</p>
      </div>
    );
  }

  if (user?.role !== 'Admin') return null;

  return (
    <div className="admin-layout">
      <aside className="admin-sidebar">
        <div className="admin-sidebar-header">
          <h3>⚔ Admin Panel</h3>
        </div>

        <div className="admin-sidebar-section">
          {SIDEBAR_LINKS.map((item, i) => {
            if ('section' in item) {
              return (
                <div key={`section-${i}`} className="admin-sidebar-label">
                  {item.section}
                </div>
              );
            }

            const isActive = item.href === '/admin'
              ? pathname === '/admin'
              : pathname.startsWith(item.href!) && item.href !== '/admin';

            return (
              <Link
                key={item.href}
                href={item.href!}
                className={`admin-sidebar-link ${isActive ? 'active' : ''}`}
              >
                <span className="sidebar-icon">{item.icon}</span>
                {item.label}
              </Link>
            );
          })}
        </div>
      </aside>

      <div className="admin-content">
        {children}
      </div>
    </div>
  );
}
