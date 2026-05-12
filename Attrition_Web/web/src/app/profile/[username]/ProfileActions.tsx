'use client';
import { useAuth } from '@/contexts/AuthContext';
import Button from '@/components/Button';
import Link from 'next/link';

export default function ProfileActions({ profileUsername }: { profileUsername: string }) {
  const { user } = useAuth();

  if (!user || user.username !== profileUsername) {
    return null;
  }

  return (
    <div style={{ marginTop: 'var(--space-md)' }}>
      <Link href="/profile/settings" style={{ textDecoration: 'none' }}>
        <Button variant="secondary">Edit Profile</Button>
      </Link>
    </div>
  );
}
