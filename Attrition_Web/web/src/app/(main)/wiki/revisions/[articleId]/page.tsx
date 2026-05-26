'use client';

import { useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import { formatDate } from '@/lib/utils';
import { 
  History, 
  ArrowLeft, 
  ArrowRightLeft,
  FileText,
  Clock,
  User
} from 'lucide-react';

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
          // Lookahead to see if it was an addition or deletion
          const oldLook = oldLines.slice(oldIdx, oldIdx + 5);
          const newLook = newLines.slice(newIdx, newIdx + 5);
          
          const foundInNew = oldLook.indexOf(newLines[newIdx]);
          const foundInOld = newLook.indexOf(oldLines[oldIdx]);

          if (foundInOld !== -1) {
            // Lines were added in new version
            for (let i = 0; i < foundInOld; i++) {
              diffResult.push({ type: 'added', text: newLines[newIdx], lineNum: newIdx + 1 });
              newIdx++;
            }
          } else if (foundInNew !== -1) {
            // Lines were removed from old version
            for (let i = 0; i < foundInNew; i++) {
              diffResult.push({ type: 'removed', text: oldLines[oldIdx] });
              oldIdx++;
            }
          } else {
            // Replacement: show removal then addition
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
      <div className="flex justify-center items-center py-20 bg-slate-950 min-h-screen">
        <div className="spinner-border animate-spin inline-block w-8 h-8 border-4 rounded-full text-indigo-500" role="status"></div>
      </div>
    );
  }

  const hasHistory = revisions.length > 0;
  const diffLines = hasHistory && revisions.length > Math.max(rev1Index, rev2Index)
    ? computeDiff(revisions[rev1Index]?.content || '', revisions[rev2Index]?.content || '')
    : [];

  return (
    <div className="container" style={{ maxWidth: '1100px', padding: '40px 20px', color: '#f3f4f6' }}>
      <div className="flex items-center gap-3 mb-6">
        <button onClick={() => router.back()} className="btn btn-secondary btn-sm p-2">
          <ArrowLeft size={16} />
        </button>
        <span className="text-sm font-semibold tracking-wider text-indigo-400 uppercase">Wiki Article Revision Manager</span>
      </div>

      <div className="flex justify-between items-start mb-8 gap-4 flex-col sm:flex-row">
        <div>
          <h1 className="text-3xl font-extrabold text-white flex items-center gap-2">
            <History className="text-indigo-400" size={32} />
            Revision History & Diffs
          </h1>
          <p className="text-sm text-gray-400 mt-1">Audit previous modifications, compare differences between versions, and view contribution notes.</p>
        </div>
      </div>

      {!hasHistory ? (
        <div className="text-center py-20 bg-slate-900/30 border border-slate-800 rounded-xl text-gray-500">
          <Clock size={48} className="mx-auto mb-3 opacity-30 text-indigo-400" />
          <p className="text-lg font-semibold">No revision history recorded for this article.</p>
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          {/* Version selectors */}
          <div 
            style={{
              background: 'rgba(15, 15, 25, 0.7)',
              backdropFilter: 'blur(12px)',
              border: '1px solid rgba(255, 255, 255, 0.08)',
              borderRadius: '12px',
              padding: '20px'
            }}
            className="flex flex-col md:flex-row justify-between items-center gap-4"
          >
            {/* Version 1 selector (Older) */}
            <div className="flex flex-col gap-1.5 w-full md:w-auto">
              <label className="text-xs font-semibold tracking-wider text-gray-400 uppercase">Compare Version (Older)</label>
              <select
                className="bg-slate-900 border border-slate-800 rounded-md py-2 px-3 text-white focus:outline-none focus:border-indigo-500 text-sm"
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

            <ArrowRightLeft className="text-indigo-400 rotate-90 md:rotate-0 flex-shrink-0" size={20} />

            {/* Version 2 selector (Newer) */}
            <div className="flex flex-col gap-1.5 w-full md:w-auto">
              <label className="text-xs font-semibold tracking-wider text-gray-400 uppercase">Against Version (Newer)</label>
              <select
                className="bg-slate-900 border border-slate-800 rounded-md py-2 px-3 text-white focus:outline-none focus:border-indigo-500 text-sm"
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
          <div 
            style={{
              background: '#04040a',
              border: '1px solid #111122',
              borderRadius: '12px',
              overflow: 'hidden',
              boxShadow: '0 8px 32px rgba(0,0,0,0.5)'
            }}
          >
            <div className="bg-slate-950 p-4 border-b border-slate-900 flex justify-between items-center text-xs text-gray-400 uppercase tracking-widest font-semibold">
              <span className="flex items-center gap-1.5"><FileText size={14} /> Side-by-side Audit Diff</span>
              <span className="flex items-center gap-4">
                <span className="flex items-center gap-1"><span className="w-2.5 h-2.5 rounded bg-emerald-500/20 border border-emerald-500/40 inline-block"></span> Additions</span>
                <span className="flex items-center gap-1"><span className="w-2.5 h-2.5 rounded bg-pink-500/20 border border-pink-500/40 inline-block"></span> Deletions</span>
              </span>
            </div>

            <div 
              style={{
                fontFamily: 'monospace',
                fontSize: '13px',
                lineHeight: '1.6',
                maxHeight: '600px',
                overflowY: 'auto',
                padding: '16px'
              }}
              className="flex flex-col gap-0.5"
            >
              {diffLines.map((line, idx) => (
                <div 
                  key={idx}
                  style={{
                    display: 'flex',
                    background: 
                      line.type === 'added' ? 'rgba(16, 185, 129, 0.12)' : 
                      line.type === 'removed' ? 'rgba(244, 63, 94, 0.12)' : 
                      'transparent',
                    borderLeft: 
                      line.type === 'added' ? '3px solid #10b981' : 
                      line.type === 'removed' ? '3px solid #f43f5e' : 
                      '3px solid transparent',
                    padding: '2px 8px'
                  }}
                >
                  <span style={{ width: '40px', color: '#4b5563', userSelect: 'none', flexShrink: 0 }}>
                    {line.lineNum || ''}
                  </span>
                  <span style={{ width: '15px', color: line.type === 'added' ? '#10b981' : line.type === 'removed' ? '#f43f5e' : '#4b5563', userSelect: 'none', flexShrink: 0 }}>
                    {line.type === 'added' ? '+' : line.type === 'removed' ? '-' : ' '}
                  </span>
                  <span style={{ 
                    whiteSpace: 'pre-wrap', 
                    wordBreak: 'break-all', 
                    color: 
                      line.type === 'added' ? '#a7f3d0' : 
                      line.type === 'removed' ? '#fecdd3' : 
                      '#9ca3af' 
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
