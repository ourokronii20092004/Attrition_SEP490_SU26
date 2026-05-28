'use client';

import { useEffect, useState, useCallback } from 'react';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import { formatDate, cn } from '@/lib/utils';
import styles from '../../admin.module.css';

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
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-6)' }}>
        <div>
          <h1 style={{ marginBottom: 'var(--space-2)' }}>Wiki Contribution Queue</h1>
          <p className="text-sm" style={{ color: 'var(--text-tertiary)', margin: 0 }}>
            Review, approve, or reject user-suggested edits to the official lore wiki guides.
          </p>
        </div>
        <button 
          onClick={fetchContributions} 
          className="btn btn-secondary btn-md"
          disabled={loading}
        >
          🔄 Refresh
        </button>
      </div>

      <div style={{ 
        display: 'grid',
        gridTemplateColumns: inspectingContent ? '1.2fr 1fr' : '1fr',
        gap: 'var(--space-6)',
        alignItems: 'start'
      }}>
        {/* Main List */}
        <div className={styles.adminTableWrapper}>
          <div className={styles.adminTableHeader}>
            <span className="text-sm" style={{ color: 'var(--text-tertiary)' }}>{contributions.length} Pending Requests</span>
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
                      <strong style={{ color: 'var(--text)' }}>{c.articleTitle}</strong>
                    </td>
                    <td>
                      <span className="text-accent font-semibold">{c.contributorName}</span>
                    </td>
                    <td>
                      <span className="text-sm" style={{ fontStyle: 'italic', color: 'var(--text-secondary)' }}>
                        &ldquo;{c.changeNote}&rdquo;
                      </span>
                    </td>
                    <td>
                      <span className="text-xs" style={{ color: 'var(--text-tertiary)' }}>{formatDate(c.submittedAt)}</span>
                    </td>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
                        <button 
                          onClick={() => setInspectingContent(c.suggestedContent)}
                          className="btn btn-secondary btn-sm"
                        >
                          Inspect
                        </button>
                        <button 
                          onClick={() => handleReview(c.id, 'Approved')}
                          disabled={reviewingId === c.id}
                          className="btn btn-sm btn-icon"
                          style={{ background: 'var(--success)', color: 'white', borderRadius: 'var(--radius-sm)' }}
                          title="Approve & Apply Edit"
                        >
                          ✓
                        </button>
                        <button 
                          onClick={() => handleReview(c.id, 'Rejected')}
                          disabled={reviewingId === c.id}
                          className="btn btn-danger btn-sm btn-icon"
                          title="Reject"
                        >
                          ✕
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: 'var(--space-12)' }}>
                    <div className="empty-state" style={{ padding: 0 }}>
                      <span className="empty-state-icon">✅</span>
                      <h3>All caught up!</h3>
                      <p>No pending contributions.</p>
                    </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Suggested Content Inspector */}
        {inspectingContent && (
          <div
            className="card"
            style={{
              padding: 'var(--space-6)',
              position: 'sticky',
              top: 'var(--space-5)',
              maxHeight: 'calc(100vh - 120px)',
              overflowY: 'auto'
            }}
          >
            <div style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              borderBottom: '1px solid var(--border)',
              paddingBottom: 'var(--space-3)',
              marginBottom: 'var(--space-4)'
            }}>
              <span className="text-sm font-semibold text-accent" style={{ letterSpacing: '0.05em', textTransform: 'uppercase' }}>
                📄 Inspecting Suggested Content
              </span>
              <button 
                onClick={() => setInspectingContent(null)}
                className="btn btn-ghost btn-sm btn-icon"
              >
                ✕
              </button>
            </div>
            <pre style={{
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-all',
              fontFamily: 'var(--font-mono)',
              fontSize: 'var(--text-sm)',
              lineHeight: '1.6',
              color: 'var(--text-secondary)',
              background: 'var(--bg-secondary)',
              padding: 'var(--space-4)',
              borderRadius: 'var(--radius-md)',
              border: '1px solid var(--border)'
            }}>
              {inspectingContent}
            </pre>
          </div>
        )}
      </div>
    </div>
  );
}
