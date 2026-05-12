'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter } from 'next/navigation';
import Link from 'next/link';

export default function AdminDashboard() {
  const { user, loading } = useAuth();
  const router = useRouter();
  
  const [stats, setStats] = useState({
    users: 0,
    wikiArticles: 0,
    forumThreads: 0,
    pendingContributions: 0
  });
  
  useEffect(() => {
    if (!loading && user?.role !== 'Admin') {
      router.push('/');
    }
  }, [user, loading, router]);

  useEffect(() => {
    // In a real implementation we would fetch actual stats from a dedicated /api/admin/stats endpoint
    // For this boilerplate we'll just show some mock/partial data based on existing endpoints
    const fetchStats = async () => {
      if (user?.role !== 'Admin') return;
      
      try {
        const usersRes = await api.get('/api/admin/users?pageSize=1');
        const contRes = await api.get('/api/wiki/contributions?status=Pending');
        const artsRes = await api.get('/api/wiki/articles?pageSize=1');
        const threadsRes = await api.get('/api/forum/threads?pageSize=1');
        
        setStats({
          users: usersRes?.totalCount || 0,
          wikiArticles: artsRes?.totalCount || 0,
          forumThreads: threadsRes?.totalCount || 0,
          pendingContributions: contRes?.length || 0
        });
      } catch (e) {
        console.error(e);
      }
    };
    fetchStats();
  }, [user]);

  if (loading || user?.role !== 'Admin') return null;

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <h1 style={{ marginBottom: 'var(--space-xl)' }}>Admin Dashboard</h1>
      
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 'var(--space-lg)', marginBottom: 'var(--space-2xl)' }}>
        <GlassCard style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '2.5rem', fontWeight: 'bold', color: 'var(--accent)' }}>{stats.users}</div>
          <div style={{ color: 'var(--text-secondary)' }}>Total Users</div>
        </GlassCard>
        <GlassCard style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '2.5rem', fontWeight: 'bold', color: 'var(--accent)' }}>{stats.pendingContributions}</div>
          <div style={{ color: 'var(--text-secondary)' }}>Pending Contributions</div>
        </GlassCard>
        <GlassCard style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '2.5rem', fontWeight: 'bold', color: 'var(--accent)' }}>{stats.wikiArticles}</div>
          <div style={{ color: 'var(--text-secondary)' }}>Wiki Articles</div>
        </GlassCard>
        <GlassCard style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '2.5rem', fontWeight: 'bold', color: 'var(--accent)' }}>{stats.forumThreads}</div>
          <div style={{ color: 'var(--text-secondary)' }}>Forum Threads</div>
        </GlassCard>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 'var(--space-lg)' }}>
        <GlassCard>
          <h2 style={{ marginBottom: 'var(--space-md)' }}>Quick Links</h2>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-sm)' }}>
            <Link href="/admin/users" style={{ color: 'var(--text-primary)', padding: 'var(--space-sm)', background: 'var(--bg-tertiary)', borderRadius: 'var(--glass-radius-sm)' }}>
              Manage Users
            </Link>
            <Link href="/admin/wiki/new" style={{ color: 'var(--text-primary)', padding: 'var(--space-sm)', background: 'var(--bg-tertiary)', borderRadius: 'var(--glass-radius-sm)' }}>
              Create Wiki Article
            </Link>
          </div>
        </GlassCard>
      </div>
    </div>
  );
}