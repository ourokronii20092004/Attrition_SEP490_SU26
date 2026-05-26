'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import styles from '../wiki.module.css';
import { 
  FileEdit, 
  Eye, 
  Send, 
  HelpCircle, 
  BookOpen, 
  PenTool 
} from 'lucide-react';

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
      <div className="flex justify-center items-center py-20 bg-slate-950 min-h-screen">
        <div className="spinner-border animate-spin inline-block w-8 h-8 border-4 rounded-full text-indigo-500" role="status"></div>
      </div>
    );
  }

  return (
    <div className="container" style={{ maxWidth: '900px', padding: '40px 20px', color: '#f3f4f6' }}>
      <div className="flex items-center gap-3 mb-6">
        <PenTool size={36} className="text-indigo-400" />
        <div>
          <h1 className="text-3xl font-extrabold text-white">Contribute Guide</h1>
          <p className="text-sm text-gray-400">Suggest revisions or improvements to existing wiki guides. Submissions enter the review moderation queue.</p>
        </div>
      </div>

      <div 
        style={{
          background: 'rgba(15, 15, 25, 0.7)',
          backdropFilter: 'blur(12px)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: '16px',
          padding: '30px'
        }}
      >
        <form onSubmit={handleSubmitSuggestion} className="flex flex-col gap-6">
          {/* Target Article */}
          <div className="flex flex-col gap-2">
            <label className="text-sm font-semibold tracking-wider text-gray-400 uppercase">Select Target Wiki Article</label>
            <select
              className="bg-slate-900 border border-slate-800 rounded-md py-3 px-4 text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500"
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
          <div className="flex flex-col gap-2">
            <label className="text-sm font-semibold tracking-wider text-gray-400 uppercase">Change Note / Description</label>
            <input
              type="text"
              className="bg-slate-900 border border-slate-800 rounded-md py-3 px-4 text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500"
              placeholder="e.g. Added item location specs, corrected boss drop chances..."
              value={changeNote}
              onChange={e => setChangeNote(e.target.value)}
              required
            />
          </div>

          {/* Editor Header: Tabs */}
          <div>
            <div className="flex justify-between items-center border-b border-slate-800 pb-2">
              <label className="text-sm font-semibold tracking-wider text-gray-400 uppercase">Suggested Content (Markdown)</label>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => setActiveTab('edit')}
                  className={`btn btn-sm ${activeTab === 'edit' ? 'btn-primary' : 'btn-secondary'} flex items-center gap-1`}
                >
                  <FileEdit size={14} /> Write
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab('preview')}
                  className={`btn btn-sm ${activeTab === 'preview' ? 'btn-primary' : 'btn-secondary'} flex items-center gap-1`}
                >
                  <Eye size={14} /> Preview
                </button>
              </div>
            </div>

            {/* Editor Input / Preview Body */}
            <div className="mt-4">
              {activeTab === 'edit' ? (
                <textarea
                  className="w-full bg-slate-900 border border-slate-800 rounded-md p-4 text-white placeholder-gray-600 focus:outline-none focus:border-indigo-500 font-mono text-sm"
                  rows={15}
                  placeholder="# Guide Title&#10;&#10;Use markdown formatting to contribute.&#10;&#10;- Item lists&#10;- Drop chances&#10;- Strategies..."
                  value={suggestedContent}
                  onChange={e => setSuggestedContent(e.target.value)}
                  style={{ resize: 'vertical' }}
                  required
                />
              ) : (
                <div 
                  className="bg-slate-900/50 border border-slate-850 p-6 rounded-md min-h-[300px] overflow-y-auto max-h-[500px]"
                  style={{ color: '#d1d5db', lineHeight: 1.7 }}
                >
                  {suggestedContent.trim() ? (
                    <div className={styles.articleBody}>
                      <ReactMarkdown remarkPlugins={[remarkGfm]}>
                        {suggestedContent}
                      </ReactMarkdown>
                    </div>
                  ) : (
                    <div className="flex flex-col items-center justify-center py-20 text-gray-500 gap-2">
                      <HelpCircle size={32} className="opacity-30" />
                      <p>Type markdown in the Write tab to see preview here.</p>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Actions */}
          <div className="flex justify-end gap-3 mt-4">
            <button
              type="button"
              onClick={() => router.push('/wiki')}
              className="btn btn-secondary"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 px-8 flex items-center gap-2"
              disabled={submitting}
            >
              <Send size={16} /> {submitting ? 'Submitting...' : 'Submit Suggestion'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
