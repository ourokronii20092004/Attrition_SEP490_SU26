'use client';

import { useEffect, useState } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';

export default function RelayHandler() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const [status, setStatus] = useState<'processing' | 'success' | 'error'>('processing');
  const [message, setMessage] = useState('Processing authentication...');

  useEffect(() => {
    const token = searchParams.get('token');
    const refresh = searchParams.get('refresh');
    const returnUrl = searchParams.get('returnUrl') || '/';

    if (!token) {
      setStatus('error');
      setMessage('No authentication token provided.');
      return;
    }

    try {
      // Store tokens
      localStorage.setItem('attrition-token', token);
      if (refresh) {
        localStorage.setItem('attrition-refresh', refresh);
      }

      setStatus('success');
      setMessage('Authentication successful! Redirecting...');

      // Clean URL and redirect
      setTimeout(() => {
        router.replace(returnUrl);
      }, 500);
    } catch (err) {
      setStatus('error');
      setMessage('Failed to process authentication.');
    }
  }, [searchParams, router]);

  return (
    <div>
      {status === 'processing' && <div className="relay-spinner" />}
      {status === 'success' && (
        <div
          style={{
            width: '40px',
            height: '40px',
            margin: '0 auto var(--space-lg)',
            color: 'var(--gold)',
            fontSize: '40px',
            lineHeight: 1,
          }}
        >
          ✓
        </div>
      )}
      {status === 'error' && (
        <div
          style={{
            width: '40px',
            height: '40px',
            margin: '0 auto var(--space-lg)',
            color: 'var(--blood-bright)',
            fontSize: '40px',
            lineHeight: 1,
          }}
        >
          ✕
        </div>
      )}
      <p style={{ color: 'var(--text-secondary)', fontSize: '14px' }}>{message}</p>
    </div>
  );
}
