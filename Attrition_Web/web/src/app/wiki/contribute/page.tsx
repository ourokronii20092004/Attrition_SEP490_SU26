'use client';
import { useState, useEffect, Suspense } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import RichEditor from '@/components/RichEditor';
import Button from '@/components/Button';
import Input from '@/components/Input';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter, useSearchParams } from 'next/navigation';
import { useToast } from '@/contexts/ToastContext';

export default function ContributePage() {
  return (
    <Suspense fallback={<div className="container" style={{ padding: 'var(--space-2xl) 0' }}>Loading...</div>}>
      <Contribute />
    </Suspense>
  );
}

function Contribute() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const { showToast } = useToast();
  
  const articleId = searchParams.get('articleId');
  const title = searchParams.get('title');

  const [articles, setArticles] = useState<any[]>([]);
  const [selectedArticleId, setSelectedArticleId] = useState(articleId || '');
  const [content, setContent] = useState('');
  const [changeNote, setChangeNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!loading && !user) {
      router.push('/auth/login');
    }
  }, [user, loading, router]);

  useEffect(() => {
    const fetchArticles = async () => {
      // Just fetch a bunch of articles to populate the dropdown, though in a real app this might need search/pagination
      const res = await api.get('/api/wiki/articles?pageSize=100');
      if (res.items) {
        setArticles(res.items);
      }
    };
    fetchArticles();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedArticleId) {
      showToast('Please select an article', 'error');
      return;
    }

    setIsSubmitting(true);
    const res = await api.post(`/api/wiki/articles/${selectedArticleId}/suggest`, {
      suggestedContent: content,
      changeNote
    });

    setIsSubmitting(false);

    if (res.success) {
      showToast('Suggestion submitted for review!', 'success');
      router.push('/wiki');
    } else {
      showToast(res.error || 'Failed to submit suggestion', 'error');
    }
  };

  if (loading || !user) return null;

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Wiki', href: '/wiki' },
        { label: 'Contribute' }
      ]} />

      <GlassCard style={{ maxWidth: '800px', margin: '0 auto' }}>
        <h1 style={{ marginBottom: 'var(--space-md)' }}>Suggest an Edit</h1>
        <p style={{ color: 'var(--text-secondary)', marginBottom: 'var(--space-xl)' }}>
          Your contribution will be reviewed by a moderator before it becomes public.
        </p>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
          <div className="input-group">
            <label>Article</label>
            <select 
              className="input" 
              value={selectedArticleId} 
              onChange={(e) => setSelectedArticleId(e.target.value)}
              required
            >
              <option value="" disabled>Select an article to edit...</option>
              {title && articleId && !articles.find(a => a.id === articleId) && (
                <option value={articleId}>{title}</option>
              )}
              {articles.map(a => (
                <option key={a.id} value={a.id}>{a.title}</option>
              ))}
            </select>
          </div>

          <div className="input-group">
            <label>Suggested Content</label>
            <RichEditor value={content} onChange={setContent} height={400} />
          </div>

          <Input 
            label="Change Note (Optional)" 
            value={changeNote} 
            onChange={(e) => setChangeNote(e.target.value)} 
            placeholder="Briefly describe what you changed and why" 
          />

          <Button type="submit" disabled={isSubmitting || !content || !selectedArticleId} style={{ alignSelf: 'flex-start', marginTop: 'var(--space-sm)' }}>
            {isSubmitting ? 'Submitting...' : 'Submit for Review'}
          </Button>
        </form>
      </GlassCard>
    </div>
  );
}