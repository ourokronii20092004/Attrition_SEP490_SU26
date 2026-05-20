'use client';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';

export default function ProfileSettingsPage() {
  const { user, loading, refreshUser } = useAuth();
  const { showToast } = useToast();
  const router = useRouter();

  const [bio, setBio] = useState('');
  const [avatarFile, setAvatarFile] = useState<File | null>(null);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!loading && !user) {
      router.push('/auth/login');
    }
    if (user) {
      setBio(user.bio || '');
    }
  }, [user, loading, router]);

  const handleUpdateProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);

    const res = await api.put('/api/users/me', { bio });
    if (res.success) {
      showToast('Profile updated!', 'success');
      await refreshUser();
    } else {
      showToast(res.error || 'Failed to update profile', 'error');
    }

    setIsSaving(false);
  };

  const handleAvatarUpload = async () => {
    if (!avatarFile) return;
    const res = await api.upload('/api/users/me/avatar', avatarFile);
    if (res.success) {
      showToast('Avatar updated!', 'success');
      await refreshUser();
      setAvatarFile(null);
    } else {
      showToast(res.error || 'Failed to upload avatar', 'error');
    }
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentPassword || !newPassword) {
      showToast('Please fill in both password fields', 'error');
      return;
    }

    setIsSaving(true);
    const res = await api.post('/api/auth/change-password', {
      currentPassword,
      newPassword,
    });
    setIsSaving(false);

    if (res.success) {
      showToast('Password changed!', 'success');
      setCurrentPassword('');
      setNewPassword('');
    } else {
      showToast(res.error || 'Failed to change password', 'error');
    }
  };

  if (loading || !user) return null;

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Profile', href: `/profile/${user.username}` },
        { label: 'Settings' },
      ]} />

      <div style={{ maxWidth: '600px', margin: '0 auto' }}>
        <h1 className="mb-xl">⚙ Profile Settings</h1>

        {/* Update Profile */}
        <div className="glass-card-static mb-xl">
          <h3 className="mb-lg">Update Profile</h3>
          <form onSubmit={handleUpdateProfile} className="auth-form">
            <div className="input-group">
              <label>Bio</label>
              <textarea
                className="input"
                value={bio}
                onChange={(e) => setBio(e.target.value)}
                placeholder="Tell us about yourself..."
                rows={3}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={isSaving}>
              {isSaving ? 'Saving...' : 'Save Changes'}
            </button>
          </form>
        </div>

        {/* Avatar Upload */}
        <div className="glass-card-static mb-xl">
          <h3 className="mb-lg">Avatar</h3>
          <div className="input-group">
            <label>Upload new avatar</label>
            <input
              type="file"
              accept="image/*"
              className="input"
              onChange={(e) => setAvatarFile(e.target.files?.[0] || null)}
            />
          </div>
          <button
            className="btn btn-primary mt-md"
            onClick={handleAvatarUpload}
            disabled={!avatarFile}
          >
            Upload Avatar
          </button>
        </div>

        {/* Change Password */}
        <div className="glass-card-static">
          <h3 className="mb-lg">Change Password</h3>
          <form onSubmit={handleChangePassword} className="auth-form">
            <div className="input-group">
              <label>Current Password</label>
              <input
                className="input"
                type="password"
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                placeholder="Current password"
              />
            </div>
            <div className="input-group">
              <label>New Password</label>
              <input
                className="input"
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="New password"
                minLength={6}
              />
            </div>
            <button type="submit" className="btn btn-danger" disabled={isSaving}>
              Change Password
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}