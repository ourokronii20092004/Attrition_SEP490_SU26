'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import SearchBar from '@/components/SearchBar';
import Pagination from '@/components/Pagination';
import Badge from '@/components/Badge';
import { api } from '@/lib/api';
import Link from 'next/link';
import Button from '@/components/Button';
import { useAuth } from '@/contexts/AuthContext';
import { FiLock, FiLogOut } from 'react-icons/fi';

export default function ForumCategory({ params }: { params: { category: string } }) {
  const [threads, setThreads] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const { user } = useAuth();

  const fetchThreads = async () => {
    setLoading(true);
    try {
      const res = await api.get(`/api/forum/threads?category=${params.category}&search=${search}&page=${page}`);
      if (res.items) {
        setThreads(res.items);
        setTotal(res.totalCount);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchThreads();
  }, [params.category, search, page]);

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Home', href: '/' }, 
        { label: 'Forum', href: '/forum' },
        { label: params.category.charAt(0).toUpperCase() + params.category.slice(1).replace('-', ' ') }
      ]} />
      
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-xl)', flexWrap: 'wrap', gap: 'var(--space-md)' }}>
        <h1 style={{ margin: 0 }}>
          {params.category.charAt(0).toUpperCase() + params.category.slice(1).replace('-', ' ')}
        </h1>
        <div style={{ display: 'flex', gap: 'var(--space-md)', alignItems: 'center' }}>
          <SearchBar value={search} onChange={(val) => { setSearch(val); setPage(1); }} placeholder="Search threads..." />
          {user && (
            <Link href={`/forum/new?category=${params.category}`}>
              <Button>New Thread</Button>
            </Link>
          )}
        </div>
      </div>

      {loading ? (
        <div style={{ textAlign: 'center', padding: 'var(--space-3xl)' }}>Loading...</div>
      ) : threads.length > 0 ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-sm)' }}>
          {threads.map((thread) => (
            <GlassCard key={thread.id} style={{ padding: 'var(--space-md)', transition: 'all var(--transition-base)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 'var(--space-md)' }}>
                <div style={{ flex: 1 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-sm)', marginBottom: 'var(--space-xs)' }}>
                    {thread.isPinned && <Badge variant="pinned">Pinned</Badge>}
                    {thread.isLocked && <FiLock color="var(--danger)" />}
                    <Link href={`/forum/${params.category}/${thread.id}`} style={{ textDecoration: 'none' }}>
                      <h3 style={{ margin: 0, color: 'var(--text-primary)' }}>{thread.title}</h3>
                    </Link>
                  </div>
                  <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>
                    Started by {thread.authorName} • {new Date(thread.createdAt).toLocaleDateString()}
                  </div>
                </div>
                
                <div style={{ display: 'flex', gap: 'var(--space-xl)', textAlign: 'right' }}>
                  <div>
                    <div style={{ fontSize: '1.1rem', fontWeight: 'bold', color: 'var(--text-primary)' }}>{thread.replyCount}</div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>REPLIES</div>
                  </div>
                  <div>
                    <div style={{ fontSize: '13px', color: 'var(--text-primary)', marginTop: '2px' }}>
                      {new Date(thread.lastReplyAt).toLocaleDateString()}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>LAST REPLY</div>
                  </div>
                </div>
              </div>
            </GlassCard>
          ))}
          <Pagination currentPage={page} totalPages={Math.ceil(total / 20)} onPageChange={setPage} />
        </div>
      ) : (
        <GlassCard style={{ textAlign: 'center', padding: 'var(--space-3xl)' }}>
          <p style={{ color: 'var(--text-secondary)' }}>No threads found in this category.</p>
        </GlassCard>
      )}
    </div>
  );
}