'use client';
import { Suspense, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useAuth } from '@/contexts/AuthContext';

function GoogleCallbackInner() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { googleLogin } = useAuth();

  useEffect(() => {
    const code = searchParams.get('code');
    if (code) {
      const redirectUri = `${window.location.origin}/auth/callback/google`;
      googleLogin(code, redirectUri).then((res) => {
        if (res.success) {
          router.push('/');
        } else {
          router.push(`/auth/login?error=${encodeURIComponent(res.error || 'Google login failed')}`);
        }
      });
    } else {
      router.push('/auth/login');
    }
  }, [searchParams, googleLogin, router]);

  return (
    <div className="loading-screen">
      <div className="spinner spinner-lg" />
      <h2>Authenticating...</h2>
      <p className="text-muted">Please wait while we connect you.</p>
    </div>
  );
}

export default function GoogleCallback() {
  return (
    <Suspense fallback={
      <div className="loading-screen">
        <div className="spinner spinner-lg" />
        <h2>Loading...</h2>
      </div>
    }>
      <GoogleCallbackInner />
    </Suspense>
  );
}