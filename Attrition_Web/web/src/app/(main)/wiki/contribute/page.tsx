'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import styles from '../wiki.module.css';
import { cn } from '@/lib/utils';

interface Article {
  id: string;
  title: string;
  slug: string;
}

export default function ContributeWiki() {
  const router = useRouter();
  const toast = useToast();

  const [articles, setArticles] = useState<Article[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  // Form states
  const [selectedArticleId, setSelectedArticleId] = useState('');
  const [changeNote, setChangeNote] = useState('');
  const [suggestedContent, setSuggestedContent] = useState('');
  const [activeTab, setActiveTab] = useState<'edit' | 'preview'>('edit');

  useEffect(() => {
    const fetchArticles = async () => {
      try {
        const res = await api.get<any>('/wiki/articles');
        // Extract list
        let list: Article[] = [];
        if (res.success && res.data && Array.isArray(res.data.items)) {
          list = res.data.items;
        } else if (res.success && Array.isArray(res.data)) {
          list = res.data;
        }
        setArticles(list);
        if (list.length > 0) {
          setSelectedArticleId(list[0].id);
        }
      } catch (err: any) {
        toast.error(err.message || 'Failed to load wiki articles list');
      } finally {
        setLoading(false);
      }
    };
    fetchArticles();
  }, []);

  const handleSubmitSuggestion = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedArticleId) {
      toast.error('Please select an article to contribute to');
      return;
    }
    if (!suggestedContent.trim()) {
      toast.error('Suggested content cannot be empty');
      return;
    }
    if (!changeNote.trim()) {
      toast.error('Please specify a brief change note for reviewers');
      return;
    }

    setSubmitting(true);
    try {
      const res = await api.post(`/wiki/articles/${selectedArticleId}/suggest`, {
        suggestedContent,
        changeNote
      });

      if (res.success) {
        toast.success('Edit suggestion submitted successfully! Reviewers will look at it shortly.');
        router.push('/wiki');
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to submit suggestion');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="page container" style={{ display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
        <div className="skeleton" style={{ width: 32, height: 32, borderRadius: '50%' }} />
      </div>
    );
  }

  return (
    <div className="page container" style={{ maxWidth: '900px' }}>
      <div className="page-header">
        <h1>Contribute to Wiki</h1>
        <p>Suggest revisions or improvements to existing wiki guides. Submissions enter the review moderation queue.</p>
      </div>

      <div className="card" style={{ padding: 'var(--space-6)' }}>
        <form onSubmit={handleSubmitSuggestion} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-6)' }}>
          {/* Target Article */}
          <div className="input-group">
            <label className="input-label">Select Target Wiki Article</label>
            <select
              className="input"
              value={selectedArticleId}
              onChange={e => setSelectedArticleId(e.target.value)}
              required
            >
              {articles.map(art => (
                <option key={art.id} value={art.id}>{art.title}</option>
              ))}
            </select>
          </div>

          {/* Change Note */}
          <div className="input-group">
            <label className="input-label">Change Note / Description</label>
            <input
              type="text"
              className="input"
              placeholder="e.g. Added item location specs, corrected boss drop chances..."
              value={changeNote}
              onChange={e => setChangeNote(e.target.value)}
              required
            />
          </div>

          {/* Editor Header: Tabs */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-3)' }}>
              <span className="input-label">Suggested Content (Markdown)</span>
              <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
                <button
                  type="button"
                  onClick={() => setActiveTab('edit')}
                  className={cn('btn btn-sm', activeTab === 'edit' ? 'btn-primary' : 'btn-secondary')}
                >
                  ✏️ Write
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab('preview')}
                  className={cn('btn btn-sm', activeTab === 'preview' ? 'btn-primary' : 'btn-secondary')}
                >
                  👁 Preview
                </button>
              </div>
            </div>

            {/* Editor Input / Preview Body */}
            {activeTab === 'edit' ? (
              <textarea
                className="input"
                rows={15}
                placeholder={"# Guide Title\n\nUse markdown formatting to contribute.\n\n- Item lists\n- Drop chances\n- Strategies..."}
                value={suggestedContent}
                onChange={e => setSuggestedContent(e.target.value)}
                style={{ resize: 'vertical', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-sm)' }}
                required
              />
            ) : (
              <div
                className="card"
                style={{
                  padding: 'var(--space-6)',
                  minHeight: 300,
                  maxHeight: 500,
                  overflowY: 'auto',
                  background: 'var(--bg-secondary)'
                }}
              >
                {suggestedContent.trim() ? (
                  <div className={styles.articleBody}>
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>
                      {suggestedContent}
                    </ReactMarkdown>
                  </div>
                ) : (
                  <div className="empty-state">
                    <span className="empty-state-icon">📝</span>
                    <h3>Nothing to preview</h3>
                    <p>Type markdown in the Write tab to see preview here.</p>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Actions */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-3)' }}>
            <button
              type="button"
              onClick={() => router.push('/wiki')}
              className="btn btn-secondary btn-md"
            >
              Cancel
            </button>
            <button
              type="submit"
              className={cn("btn btn-primary btn-md", submitting && "btn-loading")}
              disabled={submitting}
            >
              {submitting ? 'Submitting...' : '📩 Submit Suggestion'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
