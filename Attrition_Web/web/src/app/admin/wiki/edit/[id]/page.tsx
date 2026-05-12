'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import RichEditor from '@/components/RichEditor';
import Input from '@/components/Input';
import Button from '@/components/Button';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter } from 'next/navigation';
import { useToast } from '@/contexts/ToastContext';

export default function EditWikiArticle({ params }: { params: { id: string } }) {
  const { user, loading } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();

  const [categories, setCategories] = useState<any[]>([]);
  const [categoryId, setCategoryId] = useState('');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [status, setStatus] = useState('Published');
  const [changeNote, setChangeNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isFetching, setIsFetching] = useState(true);

  useEffect(() => {
    if (!loading && user?.role !== 'Admin') {
      router.push('/');
    }
  }, [user, loading, router]);

  useEffect(() => {
    const fetchData = async () => {
      const catsRes = await api.get('/api/wiki/categories');
      if (catsRes && catsRes.length > 0) setCategories(catsRes);

      // In a real app we need a way to get article by ID to populate the form,
      // but our endpoint gets it by slug. Assuming we add a way to get by ID or find the slug.
      // For this boilerplate, let's assume we can fetch by slug if we had it, or we just fail gracefully.
      // Wait, our API has `api/wiki/articles/{slug}`, but no `api/wiki/articles/{id}`.
      // We can search through articles to find the matching ID, or ideally add an endpoint.
      // For now, let's fetch all and find it.
      const artsRes = await api.get(`/api/wiki/articles?pageSize=1000`);
      if (artsRes.items) {
        const articleList = artsRes.items;
        const targetArticle = articleList.find((a: any) => a.id === params.id);
        
        if (targetArticle) {
          const detailRes = await api.get(`/api/wiki/articles/${targetArticle.slug}`);
          if (detailRes.success) {
            const data = detailRes.data;
            setTitle(data.title);
            setContent(data.content);
            setStatus(data.status);
            
            // Find category id from slug
            if (catsRes) {
              const cat = catsRes.find((c: any) => c.slug === data.categorySlug);
              if (cat) setCategoryId(cat.id.toString());
            }
          }
        }
      }
      setIsFetching(false);
    };

    if (user?.role === 'Admin') fetchData();
  }, [params.id, user]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !content.trim()) {
      showToast('Please fill in all fields', 'error');
      return;
    }

    setIsSubmitting(true);
    const res = await api.put(`/api/wiki/articles/${params.id}`, {
      title,
      content,
      status,
      changeNote: changeNote || 'Admin edit'
    });
    setIsSubmitting(false);

    if (res.success) {
      showToast('Article updated successfully', 'success');
      const catSlug = categories.find(c => c.id.toString() === categoryId)?.slug || 'general';
      // To get the new slug we would need to generate it or get it from API response. 
      // For simplicity, just redirect to category.
      router.push(`/wiki/${catSlug}`);
    } else {
      showToast(res.error || 'Failed to update article', 'error');
    }
  };

  if (loading || user?.role !== 'Admin') return null;

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Admin', href: '/admin' },
        { label: 'Edit Wiki Article' }
      ]} />

      <GlassCard style={{ maxWidth: '800px', margin: '0 auto' }}>
        <h1 style={{ marginBottom: 'var(--space-xl)' }}>Edit Wiki Article</h1>

        {isFetching ? (
          <div>Loading article data...</div>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
            <div className="input-group">
              <label>Category (Cannot be changed currently)</label>
              <select className="input" value={categoryId} disabled>
                {categories.map(cat => (
                  <option key={cat.id} value={cat.id}>{cat.name}</option>
                ))}
              </select>
            </div>

            <Input 
              label="Title" 
              value={title} 
              onChange={(e) => setTitle(e.target.value)} 
              required 
            />

            <div className="input-group">
              <label>Content</label>
              <RichEditor value={content} onChange={setContent} height={500} />
            </div>

            <div className="input-group">
              <label>Status</label>
              <select className="input" value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="Draft">Draft</option>
                <option value="Published">Published</option>
              </select>
            </div>

            <Input 
              label="Change Note" 
              value={changeNote} 
              onChange={(e) => setChangeNote(e.target.value)} 
              placeholder="e.g. Fixed typos in the boss strategy"
            />

            <div style={{ display: 'flex', gap: 'var(--space-md)', marginTop: 'var(--space-md)' }}>
              <Button type="submit" disabled={isSubmitting || !title.trim() || !content.trim()}>
                {isSubmitting ? 'Saving...' : 'Save Changes'}
              </Button>
              <Button type="button" variant="ghost" onClick={() => router.back()}>Cancel</Button>
            </div>
          </form>
        )}
      </GlassCard>
    </div>
  );
}