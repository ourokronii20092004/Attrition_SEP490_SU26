'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import MarkdownRenderer from '@/components/MarkdownRenderer';
import Button from '@/components/Button';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import Link from 'next/link';

export default function WikiArticle({ params }: { params: { category: string, slug: string } }) {
  const [article, setArticle] = useState<any>(null);
  const [revisions, setRevisions] = useState<any[]>([]);
  const [showHistory, setShowHistory] = useState(false);
  const [loading, setLoading] = useState(true);
  const { user } = useAuth();

  useEffect(() => {
    const fetchArticle = async () => {
      try {
        const res = await api.get(`/api/wiki/articles/${params.slug}`);
        if (res.success) {
          setArticle(res.data);
        }
      } catch (e) {
        console.error(e);
      } finally {
        setLoading(false);
      }
    };
    fetchArticle();
  }, [params.slug]);

  const loadRevisions = async () => {
    if (revisions.length === 0 && article) {
      const res = await api.get(`/api/wiki/articles/${article.id}/revisions`);
      setRevisions(res || []);
    }
    setShowHistory(!showHistory);
  };

  if (loading) return <div className="container" style={{ padding: 'var(--space-3xl)', textAlign: 'center' }}>Loading...</div>;
  if (!article) return <div className="container" style={{ padding: 'var(--space-3xl)', textAlign: 'center' }}>Article not found</div>;

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Wiki', href: '/wiki' },
        { label: params.category.charAt(0).toUpperCase() + params.category.slice(1).replace('-', ' '), href: `/wiki/${params.category}` },
        { label: article.title }
      ]} />

      <div style={{ display: 'flex', gap: 'var(--space-xl)', alignItems: 'flex-start' }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <GlassCard style={{ padding: 'var(--space-xl)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 'var(--space-md)' }}>
              <h1 style={{ margin: 0, fontSize: '2.5rem' }}>{article.title}</h1>
              
              <div style={{ display: 'flex', gap: 'var(--space-sm)' }}>
                {user && (
                  <Link href={`/wiki/contribute?articleId=${article.id}&title=${encodeURIComponent(article.title)}`}>
                    <Button variant="secondary">Suggest Edit</Button>
                  </Link>
                )}
                {user?.role === 'Admin' && (
                  <>
                    <Link href={`/admin/wiki/edit/${article.id}`}>
                      <Button>Edit (Admin)</Button>
                    </Link>
                    <Button variant="danger" onClick={async () => {
                      if (!confirm('Are you sure you want to delete this article?')) return;
                      const res = await api.delete(`/api/wiki/articles/${article.id}`);
                      if (res.success) {
                        window.location.href = `/wiki/${params.category}`;
                      } else {
                        alert(res.error || 'Failed to delete article');
                      }
                    }}>
                      Delete
                    </Button>
                  </>
                )}
              </div>
            </div>

            <div style={{ display: 'flex', gap: 'var(--space-lg)', fontSize: '14px', color: 'var(--text-muted)', marginBottom: 'var(--space-xl)', paddingBottom: 'var(--space-md)', borderBottom: '1px solid var(--border)' }}>
              <span>Created by {article.authorName || 'Unknown'}</span>
              {article.lastEditorName && <span>Last edited by {article.lastEditorName}</span>}
              <span>Updated: {new Date(article.updatedAt).toLocaleDateString()}</span>
            </div>

            <MarkdownRenderer content={article.content} />
          </GlassCard>

          <div style={{ marginTop: 'var(--space-xl)' }}>
            <Button variant="ghost" onClick={loadRevisions}>
              {showHistory ? 'Hide Revision History' : 'View Revision History'}
            </Button>
            
            {showHistory && (
              <div style={{ marginTop: 'var(--space-md)', display: 'flex', flexDirection: 'column', gap: 'var(--space-sm)' }}>
                {revisions.map((rev) => (
                  <GlassCard key={rev.id} style={{ padding: 'var(--space-md)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ fontWeight: 500, color: 'var(--text-primary)' }}>{new Date(rev.editedAt).toLocaleString()}</span>
                      <span style={{ color: 'var(--text-muted)', fontSize: '14px' }}>{rev.changeNote || 'No notes'}</span>
                    </div>
                  </GlassCard>
                ))}
                {revisions.length === 0 && <p style={{ color: 'var(--text-muted)' }}>No revisions found.</p>}
              </div>
            )}
          </div>
        </div>

        {/* Desktop Sidebar for TOC could go here, omitting for simplicity/brevity unless specifically needed */}
      </div>
    </div>
  );
}