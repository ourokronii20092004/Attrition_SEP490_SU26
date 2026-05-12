'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Button from '@/components/Button';
import Input from '@/components/Input';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter } from 'next/navigation';
import Link from 'next/link';

export default function Register() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const { register, user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (user) {
      router.push('/');
    }
  }, [user, router]);

  const getPasswordStrength = (pass: string) => {
    let score = 0;
    if (pass.length >= 8) score += 20;
    if (/[A-Z]/.test(pass)) score += 20;
    if (/[a-z]/.test(pass)) score += 20;
    if (/[0-9]/.test(pass)) score += 20;
    if (/[^a-zA-Z0-9]/.test(pass)) score += 20;
    return score;
  };

  const strength = getPasswordStrength(password);
  let strengthColor = 'var(--danger)';
  if (strength >= 60) strengthColor = 'var(--warning)';
  if (strength >= 100) strengthColor = 'var(--success)';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    const res = await register(username, password);
    if (!res.success) {
      // API typically returns validation errors in a specific format or generic message
      setError(res.error || 'Failed to register');
    }
  };

  if (loading) return null;

  return (
    <div className="container" style={{ padding: 'var(--space-3xl) 0', display: 'flex', justifyContent: 'center' }}>
      <GlassCard style={{ width: '100%', maxWidth: '400px' }}>
        <h1 style={{ marginBottom: 'var(--space-lg)', textAlign: 'center' }}>Create Account</h1>
        
        {error && (
          <div style={{ padding: '10px', background: 'rgba(220, 38, 38, 0.1)', border: '1px solid var(--danger)', borderRadius: 'var(--glass-radius-sm)', color: 'var(--danger)', marginBottom: 'var(--space-md)', fontSize: '14px' }}>
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
          <Input 
            label="Username" 
            value={username} 
            onChange={(e) => setUsername(e.target.value)} 
            required 
            minLength={3}
            maxLength={30}
            pattern="^[a-zA-Z0-9_]+$"
            title="Letters, numbers, and underscores only"
          />
          
          <div>
            <Input 
              label="Password" 
              type="password" 
              value={password} 
              onChange={(e) => setPassword(e.target.value)} 
              required 
            />
            {password.length > 0 && (
              <div style={{ marginTop: '4px' }}>
                <div style={{ height: '4px', width: '100%', background: 'var(--border)', borderRadius: '2px', overflow: 'hidden' }}>
                  <div style={{ height: '100%', width: `${strength}%`, background: strengthColor, transition: 'var(--transition-fast)' }} />
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px', marginTop: '8px', fontSize: '11px', color: 'var(--text-muted)' }}>
                  <span style={{ color: password.length >= 8 ? 'var(--success)' : 'inherit' }}>✓ 8+ chars</span>
                  <span style={{ color: /[A-Z]/.test(password) ? 'var(--success)' : 'inherit' }}>✓ Uppercase</span>
                  <span style={{ color: /[a-z]/.test(password) ? 'var(--success)' : 'inherit' }}>✓ Lowercase</span>
                  <span style={{ color: /[0-9]/.test(password) ? 'var(--success)' : 'inherit' }}>✓ Number</span>
                  <span style={{ color: /[^a-zA-Z0-9]/.test(password) ? 'var(--success)' : 'inherit' }}>✓ Special</span>
                </div>
              </div>
            )}
          </div>

          <Input 
            label="Confirm Password" 
            type="password" 
            value={confirmPassword} 
            onChange={(e) => setConfirmPassword(e.target.value)} 
            required 
          />
          
          <Button type="submit" style={{ marginTop: 'var(--space-sm)' }} disabled={strength < 100 || password !== confirmPassword}>Register</Button>
          
          <p style={{ textAlign: 'center', marginTop: 'var(--space-md)', fontSize: '14px', color: 'var(--text-secondary)' }}>
            Already have an account? <Link href="/auth/login" style={{ color: 'var(--accent)' }}>Login</Link>
          </p>
        </form>
      </GlassCard>
    </div>
  );
}