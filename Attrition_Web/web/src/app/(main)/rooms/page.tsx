'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { api, ApiResponse } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import styles from './rooms.module.css';
import { 
  Plus, 
  RefreshCw, 
  Play, 
  Users, 
  Lock, 
  Unlock, 
  User, 
  ShieldAlert,
  Gamepad2
} from 'lucide-react';

interface GameRoom {
  roomId: string;
  roomCode: string;
  hostUserId: string;
  roomName: string;
  maxPlayers: number;
  status: string;
  isPrivate: boolean;
  players: any[];
}

interface Character {
  characterId: string;
  name: string;
  class: string;
  level: number;
}

export default function RoomsBrowser() {
  const router = useRouter();
  const toast = useToast();

  const [rooms, setRooms] = useState<GameRoom[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [characters, setCharacters] = useState<Character[]>([]);
  const [selectedCharId, setSelectedCharId] = useState<string>('');

  // Form states
  const [newRoomName, setNewRoomName] = useState('');
  const [isPrivate, setIsPrivate] = useState(false);
  const [inviteCode, setInviteCode] = useState('');
  const [joining, setJoining] = useState(false);
  const [creating, setCreating] = useState(false);

  // Character creator states
  const [isCreatingChar, setIsCreatingChar] = useState(false);
  const [newCharName, setNewCharName] = useState('');
  const [newCharClass, setNewCharClass] = useState('Warrior');
  const [creatingChar, setCreatingChar] = useState(false);

  const fetchRooms = async (silent = false) => {
    if (!silent) setLoading(true);
    else setRefreshing(true);
    try {
      const res = await api.get<GameRoom[]>('/rooms');
      if (res.success && res.data) {
        setRooms(res.data);
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to fetch rooms');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  const fetchCharacters = async () => {
    try {
      const res = await api.get<Character[]>('/characters');
      if (res.success && res.data) {
        setCharacters(res.data);
        if (res.data.length > 0) {
          setSelectedCharId(res.data[0].characterId);
        }
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to load characters');
    }
  };

  useEffect(() => {
    fetchRooms();
    fetchCharacters();

    // Auto-refresh lobbies every 12 seconds
    const interval = setInterval(() => {
      fetchRooms(true);
    }, 12000);

    return () => clearInterval(interval);
  }, []);

  const handleCreateRoom = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCharId) {
      toast.error('You must select or create a character first!');
      return;
    }

    setCreating(true);
    try {
      // Step 1: Create room
      const res = await api.post<GameRoom>('/rooms', {
        roomName: newRoomName || undefined,
        isPrivate
      });

      if (res.success && res.data) {
        toast.success('Room created successfully!');
        
        // Step 2: Join room player details
        await api.post(`/rooms/${res.data.roomCode}/join`, {
          characterId: selectedCharId
        });

        router.push(`/rooms/${res.data.roomCode}`);
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to create room');
    } finally {
      setCreating(false);
    }
  };

  const handleJoinByCode = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inviteCode.trim()) {
      toast.error('Please enter an invite code');
      return;
    }
    if (!selectedCharId) {
      toast.error('You must select or create a character first!');
      return;
    }

    setJoining(true);
    try {
      const code = inviteCode.trim().toUpperCase();
      const res = await api.post(`/rooms/${code}/join`, {
        characterId: selectedCharId
      });

      if (res.success) {
        toast.success('Joined room successfully!');
        router.push(`/rooms/${code}`);
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to join room. Check code or character.');
    } finally {
      setJoining(false);
    }
  };

  const handleJoinLobby = async (code: string) => {
    if (!selectedCharId) {
      toast.error('You must select or create a character first!');
      return;
    }

    try {
      const res = await api.post(`/rooms/${code}/join`, {
        characterId: selectedCharId
      });

      if (res.success) {
        toast.success('Joined lobby!');
        router.push(`/rooms/${code}`);
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to join lobby');
    }
  };

  const handleCreateCharacter = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newCharName.trim()) {
      toast.error('Please enter a name for your character');
      return;
    }

    setCreatingChar(true);
    try {
      const res = await api.post<Character>('/characters', {
        name: newCharName,
        class: newCharClass
      });

      if (res.success && res.data) {
        toast.success(`Character ${res.data.name} created!`);
        setCharacters(prev => [...prev, res.data as Character]);
        setSelectedCharId(res.data.characterId);
        setIsCreatingChar(false);
        setNewCharName('');
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to create character');
    } finally {
      setCreatingChar(false);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.titleGlow}>Game Lobbies</h1>
        <button 
          onClick={() => fetchRooms(true)} 
          className="btn btn-secondary btn-sm flex items-center gap-2"
          disabled={refreshing || loading}
        >
          <RefreshCw style={refreshing ? { animation: 'spin 0.8s linear infinite' } : {}} size={16} />
          {refreshing ? 'Refreshing...' : 'Refresh'}
        </button>
      </div>

      <div className={styles.grid}>
        {/* Left Side: Lobby Browser list */}
        <div className={styles.glassCard}>
          <div className={styles.sectionTitle}>
            <Gamepad2 size={24} style={{ color: 'var(--accent)' }} />
            <h2>Active Game Lobbies</h2>
          </div>

          {loading ? (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: 'var(--space-20) 0' }}>
              <div className="skeleton" style={{ width: 32, height: 32, borderRadius: '50%' }} />
            </div>
          ) : rooms.length === 0 ? (
            <div className={styles.emptyState}>
              <Users size={48} style={{ margin: '0 auto var(--space-4)', opacity: 0.3, color: 'var(--accent)' }} />
              <p>No active public lobbies. Why not create one?</p>
            </div>
          ) : (
            <div className={styles.lobbyList}>
              {rooms.map(room => (
                <div key={room.roomId} className={styles.lobbyItem}>
                  <div className={styles.lobbyMeta}>
                    <div className="flex items-center gap-2">
                      <span className={styles.lobbyName}>{room.roomName}</span>
                      {room.isPrivate ? (
                        <Lock size={14} style={{ color: 'var(--danger)' }} />
                      ) : (
                        <Unlock size={14} style={{ color: 'var(--success)' }} />
                      )}
                    </div>
                    <span className={styles.lobbyHost}>Room Code: <strong style={{ color: 'var(--accent)' }}>{room.roomCode}</strong></span>
                  </div>
                  <div className="flex items-center gap-4">
                    <div className={styles.lobbyPlayers}>
                      <Users size={16} />
                      <span>{room.players?.length || 1} / {room.maxPlayers}</span>
                    </div>
                    <button 
                      onClick={() => handleJoinLobby(room.roomCode)}
                      className="btn btn-primary btn-sm flex items-center gap-1"
                      disabled={!selectedCharId}
                    >
                      <Play size={14} /> Join
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Right Side: Character & Room Join / Create Panels */}
        <div className="flex flex-col gap-6">
          {/* Character selection card */}
          <div className={styles.glassCard}>
            <div className={styles.sectionTitle}>
              <User size={20} style={{ color: 'var(--accent)' }} />
              <h2>Your Character</h2>
            </div>

            {isCreatingChar ? (
              <form onSubmit={handleCreateCharacter} className="flex flex-col gap-4">
                <div className={styles.formGroup}>
                  <label className={styles.formLabel}>Name</label>
                  <input 
                    type="text" 
                    className={styles.formInput} 
                    value={newCharName}
                    onChange={e => setNewCharName(e.target.value)}
                    placeholder="Enter name..."
                    required
                  />
                </div>
                <div className={styles.formGroup}>
                  <label className={styles.formLabel}>Class</label>
                  <select 
                    className={styles.formInput}
                    value={newCharClass}
                    onChange={e => setNewCharClass(e.target.value)}
                  >
                    <option value="Warrior">Warrior</option>
                    <option value="Mage">Mage</option>
                    <option value="Rogue">Rogue</option>
                  </select>
                </div>
                <div className="flex gap-2">
                  <button 
                    type="submit" 
                    className="btn btn-primary flex-1 btn-sm"
                    disabled={creatingChar}
                  >
                    {creatingChar ? 'Creating...' : 'Create'}
                  </button>
                  <button 
                    type="button" 
                    onClick={() => setIsCreatingChar(false)}
                    className="btn btn-secondary btn-sm"
                  >
                    Cancel
                  </button>
                </div>
              </form>
            ) : (
              <div className={styles.characterSelector}>
                {characters.length === 0 ? (
                  <div className="text-center py-4">
                    <p style={{ color: 'var(--text-muted)', marginBottom: 'var(--space-4)' }}>You have no characters yet.</p>
                    <button 
                      onClick={() => setIsCreatingChar(true)}
                      className="btn btn-primary btn-sm" style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-1)', margin: '0 auto' }}
                    >
                      <Plus size={16} /> Create Character
                    </button>
                  </div>
                ) : (
                  <>
                    <div className={styles.charGrid}>
                      {characters.map(char => (
                        <div 
                          key={char.characterId}
                          onClick={() => setSelectedCharId(char.characterId)}
                          className={`${styles.charItem} ${selectedCharId === char.characterId ? styles.charActive : ''}`}
                        >
                          <div className={styles.charAvatar}>
                            {char.name[0].toUpperCase()}
                          </div>
                          <div className={styles.charName}>{char.name}</div>
                          <div className={styles.charClass}>{char.class} (Lv. {char.level})</div>
                        </div>
                      ))}
                    </div>
                    <button 
                      onClick={() => setIsCreatingChar(true)}
                      className="btn btn-secondary btn-sm" style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-1)', margin: 'var(--space-2) auto 0' }}
                    >
                      <Plus size={14} /> Create Another
                    </button>
                  </>
                )}
              </div>
            )}
          </div>

          {/* Create room card */}
          <div className={styles.glassCard}>
            <div className={styles.sectionTitle}>
              <Plus size={20} style={{ color: 'var(--accent)' }} />
              <h2>Create A Room</h2>
            </div>
            <form onSubmit={handleCreateRoom}>
              <div className={styles.formGroup}>
                <label className={styles.formLabel}>Room Name</label>
                <input 
                  type="text" 
                  className={styles.formInput} 
                  placeholder="Leave blank for auto-name..."
                  value={newRoomName}
                  onChange={e => setNewRoomName(e.target.value)}
                />
              </div>
              <div className={styles.formRow}>
                <label className={styles.checkboxLabel}>
                  <input 
                    type="checkbox" 
                    checked={isPrivate} 
                    onChange={e => setIsPrivate(e.target.checked)}
                  />
                  <span>Private Lobby</span>
                </label>
              </div>
              <button 
                type="submit" 
                className="btn btn-primary" style={{ width: '100%' }}
                disabled={creating || !selectedCharId}
              >
                {creating ? 'Creating...' : 'Create & Host'}
              </button>
            </form>
          </div>

          {/* Join by code card */}
          <div className={styles.glassCard}>
            <div className={styles.sectionTitle}>
              <Lock size={20} style={{ color: 'var(--danger)' }} />
              <h2>Join Private Room</h2>
            </div>
            <form onSubmit={handleJoinByCode}>
              <div className={styles.formGroup}>
                <label className={styles.formLabel}>Invite Code</label>
                <input 
                  type="text" 
                  className={styles.formInput} 
                  placeholder="E.G. A1B2C3D4"
                  maxLength={8}
                  value={inviteCode}
                  onChange={e => setInviteCode(e.target.value)}
                />
              </div>
              <button 
                type="submit" 
                className="btn btn-primary" style={{ width: '100%' }}
                disabled={joining || !selectedCharId}
              >
                {joining ? 'Joining...' : 'Join Code'}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
}
