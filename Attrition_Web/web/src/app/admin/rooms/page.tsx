'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import styles from './rooms-admin.module.css';
import { 
  Plus, 
  RefreshCw, 
  Eye, 
  Trash2, 
  Gamepad2, 
  Clock, 
  Search,
  Lock,
  Unlock
} from 'lucide-react';

interface GameRoom {
  roomId: string;
  roomCode: string;
  hostUserId: string;
  roomName: string;
  maxPlayers: number;
  status: string;
  isPrivate: boolean;
  createdAt: string;
  players: any[];
}

export default function AdminRooms() {
  const toast = useToast();
  
  const [rooms, setRooms] = useState<GameRoom[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');

  const fetchRooms = async () => {
    setLoading(true);
    try {
      const res = await api.get<GameRoom[]>('/admin/rooms');
      if (res.success && res.data) {
        setRooms(res.data);
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to fetch admin rooms list');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRooms();
  }, []);

  const handleTerminateRoom = async (code: string) => {
    if (!confirm(`Are you sure you want to terminate room ${code}?`)) return;

    try {
      const res = await api.post(`/rooms/${code}/end`);
      if (res.success) {
        toast.success(`Room ${code} terminated successfully.`);
        fetchRooms();
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to terminate room');
    }
  };

  const filteredRooms = rooms.filter(room => 
    room.roomName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    room.roomCode.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Manage Game Rooms</h1>
          <p className="text-sm" style={{ color: 'var(--text-tertiary)', marginTop: 'var(--space-1)' }}>Monitor active lobbies, inspect real-time player telemetry, and publish admin game commands.</p>
        </div>
        <button 
          onClick={fetchRooms} 
          className="btn btn-secondary btn-md flex items-center gap-2"
          disabled={loading}
        >
          <RefreshCw style={loading ? { animation: 'spin 0.8s linear infinite' } : {}} size={16} />
          Refresh list
        </button>
      </div>

      <div className="flex justify-between items-center mb-6 gap-4">
        {/* Search bar */}
        <div style={{ position: 'relative', flex: 1, maxWidth: 400 }}>
          <input 
            type="text" 
            placeholder="Search by room name or code..."
            className="input"
            style={{ paddingLeft: 'var(--space-10)' }}
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
          />
          <span style={{ position: 'absolute', left: 'var(--space-3)', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }}>
            <Search size={18} />
          </span>
        </div>
      </div>

      <div className={styles.tableCard}>
        {loading ? (
          <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: 'var(--space-20) 0' }}>
            <div className="skeleton" style={{ width: 32, height: 32, borderRadius: '50%' }} />
          </div>
        ) : filteredRooms.length === 0 ? (
          <div className="empty-state" style={{ padding: 'var(--space-10)' }}>
            <span className="empty-state-icon">🎮</span>
            <p>No game rooms matching search found.</p>
          </div>
        ) : (
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Code</th>
                <th>Room Name</th>
                <th>Type</th>
                <th>Players</th>
                <th>Status</th>
                <th>Created At</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredRooms.map(room => (
                <tr key={room.roomId}>
                  <td>
                    <strong style={{ color: 'var(--accent)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-sm)' }}>{room.roomCode}</strong>
                  </td>
                  <td>
                    <span style={{ fontWeight: 'var(--weight-semibold)', color: 'var(--text)' }}>{room.roomName}</span>
                  </td>
                  <td>
                    {room.isPrivate ? (
                      <span className={`${styles.badge} ${styles.badgePrivate}`}>
                        <Lock size={12} className="inline mr-1" /> Private
                      </span>
                    ) : (
                      <span className={`${styles.badge} ${styles.badgePublic}`}>
                        <Unlock size={12} className="inline mr-1" /> Public
                      </span>
                    )}
                  </td>
                  <td>
                    <span style={{ color: 'var(--text-secondary)', fontWeight: 'var(--weight-semibold)' }}>{room.players?.length || 0} / {room.maxPlayers}</span>
                  </td>
                  <td>
                    {room.status === 'waiting' && (
                      <span className={`${styles.badge} ${styles.badgeWaiting}`}>Waiting</span>
                    )}
                    {room.status === 'in_progress' && (
                      <span className={`${styles.badge} ${styles.badgeProgress}`}>In Progress</span>
                    )}
                    {room.status === 'ended' && (
                      <span className={`${styles.badge} ${styles.badgeEnded}`}>Ended</span>
                    )}
                  </td>
                  <td>
                    <span style={{ color: 'var(--text-tertiary)', fontSize: 'var(--text-xs)', display: 'flex', alignItems: 'center', gap: 'var(--space-1)' }}>
                      <Clock size={12} />
                      {new Date(room.createdAt).toLocaleString()}
                    </span>
                  </td>
                  <td>
                    <div className="flex items-center gap-2">
                      <Link 
                        href={`/admin/rooms/${room.roomCode}`}
                        className="btn btn-secondary btn-sm flex items-center gap-1 py-1"
                      >
                        <Eye size={14} /> Inspect
                      </Link>
                      {room.status !== 'ended' && (
                        <button 
                          onClick={() => handleTerminateRoom(room.roomCode)}
                          className="btn btn-danger btn-sm"
                        >
                          <Trash2 size={14} /> End
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
