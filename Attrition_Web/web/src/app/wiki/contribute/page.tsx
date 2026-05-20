'use client';
import { useState, useEffect } from 'react';
import Breadcrumb from '@/components/Breadcrumb';
import RichEditor from '@/components/RichEditor';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import { useRouter } from 'next/navigation';

export default function WikiContributePage() {
  const { user, loading } = useAuth();
  const { showToast } = useToast();
  const router = useRouter();

  const [categories, setCategories] = useState<any[]>([]);
  const [articleSlug, setArticleSlug] = useState('');
  const [content, setContent] = useState('');
  const [changeNote, setChangeNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [articles, setArticles] = useState<any[]>([]);

  useEffect(() => {
    if (!loading && !user) {
      router.push('/auth/login');
    }
  }, [user, loading, router]);

  useEffect(() => {
    const fetchData = async () => {
      const catsRes = await api.get('/api/wiki/categories');
      if (Array.isArray(catsRes)) setCategories(catsRes);

      const artsRes = await api.get('/api/wiki/articles?pageSize=200');
      if (artsRes.items) setArticles(artsRes.items);
    };
    fetchData();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!articleSlug || !content.trim()) {
      showToast('Please select an article and add your contribution', 'error');
      return;
    }

    setIsSubmitting(true);
    const res = await api.post('/api/wiki/contributions', {
      articleSlug,
      content,
      changeNote: changeNote || 'Community contribution',
    });
    setIsSubmitting(false);

    if (res.success) {
      showToast('Contribution submitted for review!', 'success');
      router.push('/wiki');
    } else {
      showToast(res.error || 'Failed to submit contribution', 'error');
    }
  };

  if (loading || !user) return null;

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Wiki', href: '/wiki' },
        { label: 'Contribute' },
      ]} />

      <div className="glass-card-static" style={{ maxWidth: '800px', margin: '0 auto' }}>
        <h1 className="mb-md">✏️ Contribute to the Wiki</h1>
        <p className="text-muted mb-xl">
          Submit an edit to an existing article. Your contribution will be reviewed by an admin before being published.
        </p>

        <form onSubmit={handleSubmit} className="auth-form">
          <div className="input-group">
            <label>Select Article</label>
            <select
              className="input"
              value={articleSlug}
              onChange={(e) => setArticleSlug(e.target.value)}
              required
            >
              <option value="" disabled>Choose an article to edit...</option>
              {articles.map((article) => (
                <option key={article.id} value={article.slug}>
                  {article.title}
                </option>
              ))}
            </select>
          </div>

          <div className="input-group">
            <label>Your Changes</label>
            <RichEditor value={content} onChange={setContent} height={400} />
          </div>

          <div className="input-group">
            <label>Change Note</label>
            <input
              className="input"
              type="text"
              value={changeNote}
              onChange={(e) => setChangeNote(e.target.value)}
              placeholder="Describe what you changed..."
            />
          </div>

          <div style={{ display: 'flex', gap: 'var(--space-md)', marginTop: 'var(--space-md)' }}>
            <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
              {isSubmitting ? 'Submitting...' : '📤 Submit Contribution'}
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => router.back()}>
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}