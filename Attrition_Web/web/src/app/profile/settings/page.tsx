'use client';
import { useState, useRef } from 'react';
import GlassCard from '@/components/GlassCard';
import Button from '@/components/Button';
import Input from '@/components/Input';
import Avatar from '@/components/Avatar';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';
import { api } from '@/lib/api';

export default function Settings() {
  const { user, refreshUser } = useAuth();
  const { showToast } = useToast();
  
  const [bio, setBio] = useState(user?.bio || '');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isUploading, setIsUploading] = useState(false);
  
  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!user) return null;

  const handleUpdateBio = async (e: React.FormEvent) => {
    e.preventDefault();
    const res = await api.put('/api/users/profile', { bio });
    if (res.success) {
      await refreshUser();
      showToast('Profile updated successfully', 'success');
    } else {
      showToast(res.error || 'Failed to update profile', 'error');
    }
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (newPassword !== confirmPassword) {
      showToast('New passwords do not match', 'error');
      return;
    }

    const res = await api.post('/api/auth/change-password', { currentPassword, newPassword });
    if (res.success) {
      showToast('Password changed successfully', 'success');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      await refreshUser();
    } else {
      showToast(res.error || 'Failed to change password', 'error');
    }
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 5 * 1024 * 1024) {
      showToast('File must be smaller than 5MB', 'error');
      return;
    }

    setIsUploading(true);
    try {
      const res = await api.upload('/api/users/avatar', file);
      if (res.success) {
        await refreshUser();
        showToast('Avatar updated successfully', 'success');
      } else {
        showToast(res.error || 'Failed to upload avatar', 'error');
      }
    } catch (err) {
      showToast('An error occurred during upload', 'error');
    } finally {
      setIsUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0', maxWidth: '800px' }}>
      <h1 style={{ marginBottom: 'var(--space-xl)' }}>Profile Settings</h1>
      
      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-xl)' }}>
        <GlassCard>
          <h2 style={{ marginBottom: 'var(--space-md)' }}>Avatar</h2>
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-lg)' }}>
            <Avatar username={user.username} src={user.avatarUrl} size="xl" />
            <div>
              <input 
                type="file" 
                accept="image/jpeg,image/png,image/gif,image/webp" 
                style={{ display: 'none' }} 
                ref={fileInputRef}
                onChange={handleFileChange}
              />
              <Button 
                onClick={() => fileInputRef.current?.click()} 
                disabled={isUploading}
              >
                {isUploading ? 'Uploading...' : 'Change Avatar'}
              </Button>
              <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: 'var(--space-xs)' }}>
                Max size: 5MB. Formats: JPG, PNG, GIF, WEBP.
              </p>
            </div>
          </div>
        </GlassCard>

        <GlassCard>
          <h2 style={{ marginBottom: 'var(--space-md)' }}>Public Profile</h2>
          <form onSubmit={handleUpdateBio} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
            <Input 
              label="Bio" 
              multiline 
              value={bio} 
              onChange={(e) => setBio(e.target.value)} 
              placeholder="Tell others about yourself..." 
              maxLength={500}
            />
            <Button type="submit" style={{ alignSelf: 'flex-start' }}>Save Profile</Button>
          </form>
        </GlassCard>

        <GlassCard>
          <h2 style={{ marginBottom: 'var(--space-md)' }}>Change Password</h2>
          <form onSubmit={handleChangePassword} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)', maxWidth: '400px' }}>
            <Input 
              label="Current Password" 
              type="password" 
              value={currentPassword} 
              onChange={(e) => setCurrentPassword(e.target.value)} 
              required 
            />
            <Input 
              label="New Password" 
              type="password" 
              value={newPassword} 
              onChange={(e) => setNewPassword(e.target.value)} 
              required 
            />
            <Input 
              label="Confirm New Password" 
              type="password" 
              value={confirmPassword} 
              onChange={(e) => setConfirmPassword(e.target.value)} 
              required 
            />
            <Button type="submit" variant="danger" style={{ alignSelf: 'flex-start', marginTop: 'var(--space-sm)' }}>
              Update Password
            </Button>
          </form>
        </GlassCard>
      </div>
    </div>
  );
}