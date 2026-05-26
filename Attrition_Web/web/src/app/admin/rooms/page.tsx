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
          <p className="text-gray-400 mt-1">Monitor active lobbies, inspect real-time player telemetry, and publish admin game commands.</p>
        </div>
        <button 
          onClick={fetchRooms} 
          className="btn btn-secondary btn-md flex items-center gap-2"
          disabled={loading}
        >
          <RefreshCw className={loading ? 'animate-spin' : ''} size={16} />
          Refresh list
        </button>
      </div>

      <div className="flex justify-between items-center mb-6 gap-4">
        {/* Search bar */}
        <div className="relative flex-1 max-w-md">
          <input 
            type="text" 
            placeholder="Search by room name or code..."
            className="w-full bg-slate-900 border border-slate-800 rounded-md py-2 pl-10 pr-4 text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500"
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
          />
          <Search className="absolute left-3 top-2.5 text-gray-500" size={18} />
        </div>
      </div>

      <div className={styles.tableCard}>
        {loading ? (
          <div className="flex justify-center items-center py-20">
            <div className="spinner-border animate-spin inline-block w-8 h-8 border-4 rounded-full text-indigo-500" role="status"></div>
          </div>
        ) : filteredRooms.length === 0 ? (
          <div className="text-center py-10 text-gray-500">
            <Gamepad2 size={48} className="mx-auto mb-3 opacity-30" />
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
                    <strong className="text-indigo-400 font-mono text-sm">{room.roomCode}</strong>
                  </td>
                  <td>
                    <span className="font-semibold text-white">{room.roomName}</span>
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
                    <span className="text-gray-300 font-semibold">{room.players?.length || 0} / {room.maxPlayers}</span>
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
                    <span className="text-gray-400 text-xs flex items-center gap-1">
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
                          className="btn btn-secondary btn-sm text-pink-500 border-pink-900/30 hover:bg-pink-950/20 py-1"
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
