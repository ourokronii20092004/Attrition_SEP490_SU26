'use client';
import { useState, useEffect } from 'react';
import Link from 'next/link';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';

export default function AdminDashboard() {
  const { user } = useAuth();
  const [stats, setStats] = useState({
    users: 0,
    wikiArticles: 0,
    forumThreads: 0,
    pendingContributions: 0,
  });
  const [recentUsers, setRecentUsers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchStats = async () => {
      if (user?.role !== 'Admin') return;

      try {
        const [usersRes, artsRes, threadsRes, contRes] = await Promise.all([
          api.get('/api/admin/users?pageSize=5'),
          api.get('/api/wiki/articles?pageSize=1'),
          api.get('/api/forum/threads?pageSize=1'),
          api.get('/api/wiki/contributions?status=Pending'),
        ]);

        setStats({
          users: usersRes?.totalCount || 0,
          wikiArticles: artsRes?.totalCount || 0,
          forumThreads: threadsRes?.totalCount || 0,
          pendingContributions: Array.isArray(contRes) ? contRes.length : 0,
        });

        if (usersRes?.items) {
          setRecentUsers(usersRes.items.slice(0, 5));
        }
      } catch (e) {
        console.error('Failed to fetch stats:', e);
      }
      setLoading(false);
    };
    fetchStats();
  }, [user]);

  return (
    <div>
      <div className="admin-page-header">
        <h1>📊 Dashboard</h1>
        <span className="text-muted">Welcome back, {user?.username}</span>
      </div>

      {/* Stats Cards */}
      <div className="stats-grid mb-2xl">
        <div className="stat-card">
          <div className="stat-value">{loading ? '—' : stats.users}</div>
          <div className="stat-label">Total Users</div>
        </div>
        <div className="stat-card stat-soul">
          <div className="stat-value">{loading ? '—' : stats.wikiArticles}</div>
          <div className="stat-label">Wiki Articles</div>
        </div>
        <div className="stat-card stat-gold">
          <div className="stat-value">{loading ? '—' : stats.forumThreads}</div>
          <div className="stat-label">Forum Threads</div>
        </div>
        <div className="stat-card stat-blood">
          <div className="stat-value">{loading ? '—' : stats.pendingContributions}</div>
          <div className="stat-label">Pending Contributions</div>
        </div>
      </div>

      {/* Quick Links */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 'var(--space-lg)' }}>
        <div className="glass-card-static">
          <h3 className="mb-md">Quick Actions</h3>
          <div className="flex-col gap-sm">
            <Link href="/admin/wiki/new" className="btn btn-ghost" style={{ justifyContent: 'flex-start' }}>
              ✏️ Create Wiki Article
            </Link>
            <Link href="/admin/users" className="btn btn-ghost" style={{ justifyContent: 'flex-start' }}>
              👥 Manage Users
            </Link>
            <Link href="/admin/forum" className="btn btn-ghost" style={{ justifyContent: 'flex-start' }}>
              💬 Manage Forum
            </Link>
            <Link href="/admin/music" className="btn btn-ghost" style={{ justifyContent: 'flex-start' }}>
              🎵 Manage Music
            </Link>
          </div>
        </div>

        <div className="glass-card-static">
          <h3 className="mb-md">Recent Users</h3>
          {recentUsers.length > 0 ? (
            <div className="flex-col gap-sm">
              {recentUsers.map((u: any) => (
                <div key={u.id} className="flex-between">
                  <span>{u.username}</span>
                  <span className="badge badge-user">{u.role}</span>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-muted" style={{ fontSize: '14px' }}>No recent users</p>
          )}
        </div>
      </div>
    </div>
  );
}