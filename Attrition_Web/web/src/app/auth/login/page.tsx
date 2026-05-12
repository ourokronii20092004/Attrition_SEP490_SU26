'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Button from '@/components/Button';
import Input from '@/components/Input';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter } from 'next/navigation';
import Link from 'next/link';

export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const { login, user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (user && !user.mustChangePassword) {
      router.push('/');
    }
  }, [user, router]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    const res = await login(username, password);
    if (!res.success) {
      setError(res.error || 'Failed to login');
    }
  };

  if (loading) return null;

  return (
    <div className="container" style={{ padding: 'var(--space-3xl) 0', display: 'flex', justifyContent: 'center' }}>
      <GlassCard style={{ width: '100%', maxWidth: '400px' }}>
        <h1 style={{ marginBottom: 'var(--space-lg)', textAlign: 'center' }}>Welcome Back</h1>
        
        {error && (
          <div style={{ padding: '10px', background: 'rgba(220, 38, 38, 0.1)', border: '1px solid var(--danger)', borderRadius: 'var(--glass-radius-sm)', color: 'var(--danger)', marginBottom: 'var(--space-md)', fontSize: '14px' }}>
            {error}
          </div>
        )}

        {user?.mustChangePassword ? (
          <div>
            <p style={{ color: 'var(--warning)', marginBottom: 'var(--space-md)', textAlign: 'center' }}>
              You must change your password before continuing.
            </p>
            <Link href="/profile/settings" style={{ display: 'block', textAlign: 'center' }}>
              <Button style={{ width: '100%' }}>Go to Settings</Button>
            </Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
            <Input 
              label="Username" 
              value={username} 
              onChange={(e) => setUsername(e.target.value)} 
              required 
            />
            <Input 
              label="Password" 
              type="password" 
              value={password} 
              onChange={(e) => setPassword(e.target.value)} 
              required 
            />
            <Button type="submit" style={{ marginTop: 'var(--space-sm)' }}>Login</Button>
            
            <p style={{ textAlign: 'center', marginTop: 'var(--space-md)', fontSize: '14px', color: 'var(--text-secondary)' }}>
              Don't have an account? <Link href="/auth/register" style={{ color: 'var(--accent)' }}>Register</Link>
            </p>
          </form>
        )}
      </GlassCard>
    </div>
  );
}