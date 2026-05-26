'use client';

import { useEffect, useState, useCallback } from 'react';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import { formatDate } from '@/lib/utils';
import styles from '../../admin.module.css';
import { 
  Check, 
  X, 
  RefreshCw, 
  FileText, 
  HelpCircle, 
  MessageSquare,
  ShieldCheck
} from 'lucide-react';

interface WikiContribution {
  id: string;
  articleId: string;
  articleTitle: string;
  contributorName: string;
  suggestedContent: string;
  changeNote: string;
  status: string;
  submittedAt: string;
}

export default function AdminWikiContributionsPage() {
  const toast = useToast();
  const [contributions, setContributions] = useState<WikiContribution[]>([]);
  const [loading, setLoading] = useState(true);
  const [reviewingId, setReviewingId] = useState<string | null>(null);
  const [inspectingContent, setInspectingContent] = useState<string | null>(null);

  const fetchContributions = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<WikiContribution[]>('/wiki/contributions?status=Pending');
      if (res.success && res.data) {
        setContributions(res.data);
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to fetch pending wiki contributions');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchContributions();
  }, [fetchContributions]);

  const handleReview = async (id: string, status: 'Approved' | 'Rejected') => {
    if (!confirm(`Are you sure you want to set this contribution as ${status}?`)) return;

    setReviewingId(id);
    try {
      const res = await api.post(`/wiki/contributions/${id}/review`, {
        status
      });

      if (res.success) {
        toast.success(`Contribution was successfully ${status}!`);
        setContributions(prev => prev.filter(c => c.id !== id));
        if (inspectingContent && inspectingContent === contributions.find(c => c.id === id)?.suggestedContent) {
          setInspectingContent(null);
        }
      }
    } catch (err: any) {
      toast.error(err.message || `Failed to ${status.toLowerCase()} contribution`);
    } finally {
      setReviewingId(null);
    }
  };

  return (
    <div style={{ color: '#f3f4f6' }}>
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-3xl font-extrabold text-white flex items-center gap-2">
            <ShieldCheck className="text-indigo-400" size={32} />
            Wiki Contribution Queue
          </h1>
          <p className="text-sm text-gray-400 mt-1">Review, approve, or reject user-suggested edits to the official lore wiki guides.</p>
        </div>
        <button 
          onClick={fetchContributions} 
          className="btn btn-secondary flex items-center gap-2"
          disabled={loading}
        >
          <RefreshCw className={loading ? 'animate-spin' : ''} size={16} />
          Refresh
        </button>
      </div>

      <div style={{ 
        display: 'grid',
        gridTemplateColumns: inspectingContent ? '1.2fr 1fr' : '1fr',
        gap: '24px',
        alignItems: 'start'
      }}>
        {/* Main List */}
        <div className={styles.adminTableWrapper} style={{ background: 'rgba(15, 15, 25, 0.7)', border: '1px solid rgba(255, 255, 255, 0.08)', borderRadius: '12px', padding: '20px' }}>
          <div className="flex justify-between items-center mb-4">
            <span className="text-sm text-gray-400">{contributions.length} Pending Requests</span>
          </div>

          <table className="table">
            <thead>
              <tr>
                <th>Guide Article</th>
                <th>Contributor</th>
                <th>Change Note</th>
                <th>Submitted</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                Array.from({ length: 3 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 5 }).map((_, j) => (
                      <td key={j}><div className="skeleton skeleton-text" /></td>
                    ))}
                  </tr>
                ))
              ) : contributions.length > 0 ? (
                contributions.map((c) => (
                  <tr key={c.id}>
                    <td>
                      <strong className="text-white block">{c.articleTitle}</strong>
                    </td>
                    <td>
                      <span className="text-indigo-300 font-semibold">{c.contributorName}</span>
                    </td>
                    <td>
                      <span className="text-gray-300 italic text-sm">"{c.changeNote}"</span>
                    </td>
                    <td>
                      <span className="text-xs text-gray-400">{formatDate(c.submittedAt)}</span>
                    </td>
                    <td>
                      <div className="flex items-center gap-2">
                        <button 
                          onClick={() => setInspectingContent(c.suggestedContent)}
                          className="btn btn-secondary btn-sm"
                          style={{ fontSize: '11px' }}
                        >
                          Inspect Content
                        </button>
                        <button 
                          onClick={() => handleReview(c.id, 'Approved')}
                          disabled={reviewingId === c.id}
                          className="btn btn-primary btn-sm bg-emerald-600 hover:bg-emerald-500 border-none p-1 flex items-center justify-center text-white"
                          title="Approve & Apply Edit"
                        >
                          <Check size={14} />
                        </button>
                        <button 
                          onClick={() => handleReview(c.id, 'Rejected')}
                          disabled={reviewingId === c.id}
                          className="btn btn-secondary btn-sm border-pink-900/30 text-pink-500 hover:bg-pink-950/20 p-1 flex items-center justify-center"
                          title="Reject"
                        >
                          <X size={14} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '40px 20px', color: '#9ca3af' }}>
                    <Check className="mx-auto mb-2 text-emerald-400 opacity-40" size={32} />
                    All caught up! No pending contributions.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Suggested Content Inspector */}
        {inspectingContent && (
          <div style={{
            background: '#0a0a14',
            border: '1px solid #1e1b4b',
            borderRadius: '12px',
            padding: '24px',
            position: 'sticky',
            top: '20px',
            maxHeight: 'calc(100vh - 120px)',
            overflowY: 'auto'
          }}>
            <div className="flex justify-between items-center border-b border-slate-800 pb-3 mb-4">
              <span className="text-sm font-semibold tracking-wider text-indigo-400 uppercase flex items-center gap-2">
                <FileText size={16} />
                Inspecting Suggested Content
              </span>
              <button 
                onClick={() => setInspectingContent(null)}
                className="text-gray-400 hover:text-white"
              >
                ✕
              </button>
            </div>
            <pre style={{
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-all',
              fontFamily: 'monospace',
              fontSize: '13px',
              lineHeight: '1.6',
              color: '#d1d5db',
              background: '#04040a',
              padding: '16px',
              borderRadius: '8px',
              border: '1px solid #111122'
            }}>
              {inspectingContent}
            </pre>
          </div>
        )}
      </div>
    </div>
  );
}
