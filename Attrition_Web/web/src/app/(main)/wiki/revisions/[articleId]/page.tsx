'use client';

import { useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import { formatDate } from '@/lib/utils';

interface WikiRevision {
  id: string;
  articleId: string;
  content: string;
  editedById: string;
  editedByName: string | null;
  changeNote: string | null;
  editedAt: string;
}

export default function ArticleRevisionsPage() {
  const params = useParams();
  const router = useRouter();
  const toast = useToast();
  const articleId = params?.articleId as string;

  const [revisions, setRevisions] = useState<WikiRevision[]>([]);
  const [loading, setLoading] = useState(true);
  
  // Diff selection states
  const [rev1Index, setRev1Index] = useState<number>(0);
  const [rev2Index, setRev2Index] = useState<number>(0);

  useEffect(() => {
    if (!articleId) return;

    const fetchRevisions = async () => {
      try {
        const res = await api.get<WikiRevision[]>(`/wiki/articles/${articleId}/revisions`);
        if (res.success && res.data) {
          setRevisions(res.data);
          if (res.data.length > 1) {
            setRev1Index(1); // Compare previous revision
            setRev2Index(0); // with the latest revision
          }
        }
      } catch (err: any) {
        toast.error(err.message || 'Failed to load revision history');
      } finally {
        setLoading(false);
      }
    };

    fetchRevisions();
  }, [articleId]);

  // A clean, line-by-line diff comparison helper
  const computeDiff = (oldText: string, newText: string) => {
    const oldLines = oldText ? oldText.split('\n') : [];
    const newLines = newText ? newText.split('\n') : [];
    
    const diffResult: { type: 'added' | 'removed' | 'unchanged'; text: string; lineNum?: number }[] = [];
    
    let oldIdx = 0;
    let newIdx = 0;

    while (oldIdx < oldLines.length || newIdx < newLines.length) {
      if (oldIdx < oldLines.length && newIdx < newLines.length) {
        if (oldLines[oldIdx] === newLines[newIdx]) {
          diffResult.push({ type: 'unchanged', text: oldLines[oldIdx], lineNum: newIdx + 1 });
          oldIdx++;
          newIdx++;
        } else {
          const oldLook = oldLines.slice(oldIdx, oldIdx + 5);
          const newLook = newLines.slice(newIdx, newIdx + 5);
          
          const foundInNew = oldLook.indexOf(newLines[newIdx]);
          const foundInOld = newLook.indexOf(oldLines[oldIdx]);

          if (foundInOld !== -1) {
            for (let i = 0; i < foundInOld; i++) {
              diffResult.push({ type: 'added', text: newLines[newIdx], lineNum: newIdx + 1 });
              newIdx++;
            }
          } else if (foundInNew !== -1) {
            for (let i = 0; i < foundInNew; i++) {
              diffResult.push({ type: 'removed', text: oldLines[oldIdx] });
              oldIdx++;
            }
          } else {
            diffResult.push({ type: 'removed', text: oldLines[oldIdx] });
            diffResult.push({ type: 'added', text: newLines[newIdx], lineNum: newIdx + 1 });
            oldIdx++;
            newIdx++;
          }
        }
      } else if (newIdx < newLines.length) {
        diffResult.push({ type: 'added', text: newLines[newIdx], lineNum: newIdx + 1 });
        newIdx++;
      } else if (oldIdx < oldLines.length) {
        diffResult.push({ type: 'removed', text: oldLines[oldIdx] });
        oldIdx++;
      }
    }

    return diffResult;
  };

  if (loading) {
    return (
      <div className="page container" style={{ display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
        <div className="skeleton" style={{ width: 32, height: 32, borderRadius: '50%' }} />
      </div>
    );
  }

  const hasHistory = revisions.length > 0;
  const diffLines = hasHistory && revisions.length > Math.max(rev1Index, rev2Index)
    ? computeDiff(revisions[rev1Index]?.content || '', revisions[rev2Index]?.content || '')
    : [];

  return (
    <div className="page container" style={{ maxWidth: '1100px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)', marginBottom: 'var(--space-6)' }}>
        <button onClick={() => router.back()} className="btn btn-secondary btn-sm btn-icon">
          ←
        </button>
        <span className="text-sm text-accent font-semibold" style={{ letterSpacing: '0.05em', textTransform: 'uppercase' }}>
          Wiki Article Revision Manager
        </span>
      </div>

      <div className="page-header">
        <h1>📜 Revision History &amp; Diffs</h1>
        <p>Audit previous modifications, compare differences between versions, and view contribution notes.</p>
      </div>

      {!hasHistory ? (
        <div className="card empty-state">
          <span className="empty-state-icon">🕐</span>
          <h3>No revision history recorded for this article.</h3>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-6)' }}>
          {/* Version selectors */}
          <div 
            className="card"
            style={{
              padding: 'var(--space-5)',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              gap: 'var(--space-4)',
              flexWrap: 'wrap'
            }}
          >
            {/* Version 1 selector (Older) */}
            <div className="input-group" style={{ flex: 1, minWidth: 220 }}>
              <label className="input-label">Compare Version (Older)</label>
              <select
                className="input"
                value={rev1Index}
                onChange={e => setRev1Index(Number(e.target.value))}
              >
                {revisions.map((rev, idx) => (
                  <option key={rev.id} value={idx}>
                    v{revisions.length - idx} - {rev.changeNote || 'Edit'} ({formatDate(rev.editedAt)})
                  </option>
                ))}
              </select>
            </div>

            <span className="text-accent" style={{ flexShrink: 0 }}>⇄</span>

            {/* Version 2 selector (Newer) */}
            <div className="input-group" style={{ flex: 1, minWidth: 220 }}>
              <label className="input-label">Against Version (Newer)</label>
              <select
                className="input"
                value={rev2Index}
                onChange={e => setRev2Index(Number(e.target.value))}
              >
                {revisions.map((rev, idx) => (
                  <option key={rev.id} value={idx}>
                    v{revisions.length - idx} - {rev.changeNote || 'Edit'} ({formatDate(rev.editedAt)})
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Diff Grid output view */}
          <div className="card" style={{ overflow: 'hidden' }}>
            <div
              style={{
                padding: 'var(--space-4)',
                borderBottom: '1px solid var(--border)',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                fontSize: 'var(--text-xs)',
                color: 'var(--text-tertiary)',
                textTransform: 'uppercase',
                letterSpacing: '0.05em',
                fontWeight: 'var(--weight-semibold)'
              }}
            >
              <span>📄 Side-by-side Audit Diff</span>
              <span style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-4)' }}>
                <span style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-1)' }}>
                  <span style={{ width: 10, height: 10, borderRadius: 'var(--radius-sm)', background: 'var(--success-bg)', border: '1px solid var(--success)', display: 'inline-block' }} />
                  Additions
                </span>
                <span style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-1)' }}>
                  <span style={{ width: 10, height: 10, borderRadius: 'var(--radius-sm)', background: 'var(--danger-bg)', border: '1px solid var(--danger)', display: 'inline-block' }} />
                  Deletions
                </span>
              </span>
            </div>

            <div 
              style={{
                fontFamily: 'var(--font-mono)',
                fontSize: 'var(--text-sm)',
                lineHeight: '1.6',
                maxHeight: '600px',
                overflowY: 'auto',
                padding: 'var(--space-4)',
                display: 'flex',
                flexDirection: 'column',
                gap: 1
              }}
            >
              {diffLines.map((line, idx) => (
                <div 
                  key={idx}
                  style={{
                    display: 'flex',
                    background: 
                      line.type === 'added' ? 'var(--success-bg)' : 
                      line.type === 'removed' ? 'var(--danger-bg)' : 
                      'transparent',
                    borderLeft: 
                      line.type === 'added' ? '3px solid var(--success)' : 
                      line.type === 'removed' ? '3px solid var(--danger)' : 
                      '3px solid transparent',
                    padding: '2px 8px',
                    borderRadius: 'var(--radius-sm)'
                  }}
                >
                  <span style={{ width: 40, color: 'var(--text-muted)', userSelect: 'none', flexShrink: 0 }}>
                    {line.lineNum || ''}
                  </span>
                  <span style={{ 
                    width: 15, 
                    color: line.type === 'added' ? 'var(--success)' : line.type === 'removed' ? 'var(--danger)' : 'var(--text-muted)', 
                    userSelect: 'none', 
                    flexShrink: 0 
                  }}>
                    {line.type === 'added' ? '+' : line.type === 'removed' ? '-' : ' '}
                  </span>
                  <span style={{ 
                    whiteSpace: 'pre-wrap', 
                    wordBreak: 'break-all', 
                    color: 
                      line.type === 'added' ? 'var(--success)' : 
                      line.type === 'removed' ? 'var(--danger)' : 
                      'var(--text-secondary)' 
                  }}>
                    {line.text}
                  </span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
