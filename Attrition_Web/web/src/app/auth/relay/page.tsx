'use client';
import { Suspense, useEffect } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';

function RelayInner() {
  const searchParams = useSearchParams();
  const router = useRouter();

  useEffect(() => {
    const token = searchParams.get('token');
    const refresh = searchParams.get('refresh');
    const returnUrl = searchParams.get('returnUrl') || '/';

    if (token) {
      localStorage.setItem('attrition-token', token);
    }
    if (refresh) {
      localStorage.setItem('attrition-refresh', refresh);
    }

    router.push(returnUrl);
  }, [searchParams, router]);

  return (
    <div className="loading-screen">
      <div className="spinner spinner-lg" />
      <h2>Authenticating...</h2>
      <p className="text-muted">Relaying your session...</p>
    </div>
  );
}

export default function RelayPage() {
  return (
    <Suspense fallback={
      <div className="loading-screen">
        <div className="spinner spinner-lg" />
        <h2>Loading...</h2>
      </div>
    }>
      <RelayInner />
    </Suspense>
  );
}
