'use client';

import { useState, useEffect, useRef } from 'react';
import { useParams, useRouter } from 'next/navigation';
import * as signalR from '@microsoft/signalr';
import { api, getAccessToken } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import styles from '../rooms.module.css';
import { 
  Users, 
  Play, 
  XCircle, 
  CheckCircle, 
  Clock, 
  Copy, 
  CornerDownLeft, 
  Gamepad2, 
  Crown 
} from 'lucide-react';

interface RoomPlayer {
  userId: string;
  user: {
    username: string;
    email: string;
  };
  characterId: string;
  character?: {
    name: string;
    class: string;
    level: number;
  };
  playerRole: number; // 0 = guest, 1 = host
  isReady: boolean;
}

interface GameRoom {
  roomId: string;
  roomCode: string;
  hostUserId: string;
  roomName: string;
  maxPlayers: number;
  status: string;
  isPrivate: boolean;
  players: RoomPlayer[];
}

export default function RoomLobby() {
  const params = useParams();
  const router = useRouter();
  const toast = useToast();
  const code = params?.code as string;

  const [room, setRoom] = useState<GameRoom | null>(null);
  const [loading, setLoading] = useState(true);
  const [currentUser, setCurrentUser] = useState<any>(null);
  const [readyLoading, setReadyLoading] = useState(false);
  const [startLoading, setStartLoading] = useState(false);
  
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const fetchRoomData = async () => {
    try {
      const res = await api.get<GameRoom>(`/rooms/${code}`);
      if (res.success && res.data) {
        setRoom(res.data);
      } else {
        toast.error('Room not found');
        router.push('/rooms');
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to load room details');
      router.push('/rooms');
    } finally {
      setLoading(false);
    }
  };

  const fetchCurrentUser = async () => {
    try {
      const res = await api.get<any>('/users/me');
      if (res.success) {
        setCurrentUser(res.data);
      }
    } catch (err) {
      console.error('Failed to load current user context', err);
    }
  };

  useEffect(() => {
    if (!code) return;

    fetchRoomData();
    fetchCurrentUser();

    // Setup SignalR Connection
    const token = getAccessToken();
    const hubUrl = `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/gamehub`;
    
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token || ''
      })
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    const startHub = async () => {
      try {
        await connection.start();
        console.log('SignalR connected to GameHub successfully.');
        
        // Join group room code
        await connection.invoke('JoinRoom', code);

        // Set listeners
        connection.on('OnPlayerJoined', (userId: string) => {
          console.log(`Player joined: ${userId}`);
          fetchRoomData();
        });

        connection.on('OnPlayerLeft', (userId: string) => {
          console.log(`Player left: ${userId}`);
          fetchRoomData();
        });

        connection.on('OnPlayerAction', (connId: string, payload: any) => {
          console.log('Realtime player action', connId, payload);
          fetchRoomData();
        });
      } catch (err) {
        console.error('SignalR GameHub connection error', err);
      }
    };

    startHub();

    return () => {
      if (connectionRef.current) {
        connectionRef.current.invoke('LeaveRoom', code)
          .then(() => connectionRef.current?.stop())
          .catch(err => console.error('Error during Hub disconnect', err));
      }
    };
  }, [code]);

  const copyCode = () => {
    navigator.clipboard.writeText(code);
    toast.success('Invite code copied to clipboard!');
  };

  const handleToggleReady = async () => {
    setReadyLoading(true);
    try {
      const res = await api.post(`/rooms/${code}/ready`);
      if (res.success) {
        await fetchRoomData();
        // Notify others via SignalR
        if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
          await connectionRef.current.invoke('SendInput', code, { action: 'toggle_ready' });
        }
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to toggle ready status');
    } finally {
      setReadyLoading(false);
    }
  };

  const handleLeaveRoom = async () => {
    try {
      await api.post(`/rooms/${code}/leave`);
      router.push('/rooms');
    } catch (err: any) {
      toast.error(err.message || 'Error leaving room');
    }
  };

  const handleStartGame = async () => {
    setStartLoading(true);
    try {
      const res = await api.post(`/rooms/${code}/start`);
      if (res.success) {
        toast.success('Starting game sessions...');
        await fetchRoomData();
        if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
          await connectionRef.current.invoke('SendInput', code, { action: 'start_game' });
        }
      }
    } catch (err: any) {
      toast.error(err.message || 'Failed to start game');
    } finally {
      setStartLoading(false);
    }
  };

  if (loading) {
    return (
      <div className={styles.container} style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '80vh' }}>
        <div className="skeleton" style={{ width: 32, height: 32, borderRadius: '50%' }} />
      </div>
    );
  }

  if (!room) return null;

  const isHost = currentUser && room.hostUserId === currentUser.userId;
  const activePlayer = room.players.find(p => p.userId === currentUser?.userId);
  const allReady = room.players.every(p => p.isReady);

  return (
    <div className={styles.container}>
      <div className={styles.lobbyContainer}>
        {/* Lobby Top bar */}
        <div className={styles.glassCard} style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: 'var(--space-4)' }}>
          <div>
            <span className="text-sm font-semibold" style={{ letterSpacing: '0.05em', color: 'var(--accent)', textTransform: 'uppercase' }}>Room Session Lobby</span>
            <h1 style={{ fontSize: 'var(--text-3xl)', fontWeight: 'var(--weight-extrabold)', color: 'var(--text)', marginTop: 'var(--space-1)' }}>{room.roomName}</h1>
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-4)', marginTop: 'var(--space-2)', fontSize: 'var(--text-sm)', color: 'var(--text-tertiary)' }}>
              <span className="flex items-center gap-1">
                <Users size={16} /> {room.players.length} / {room.maxPlayers} Players
              </span>
              <span className="flex items-center gap-1">
                <Clock size={16} /> Status: <strong style={{ color: 'var(--accent)', textTransform: 'capitalize' }}>{room.status}</strong>
              </span>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <div className={styles.inviteCodeBox}>
              <span className="text-xs" style={{ color: 'var(--accent)', textTransform: 'uppercase', letterSpacing: '0.1em', marginRight: 'var(--space-1)' }}>Invite Code</span>
              <span className={styles.inviteCode}>{room.roomCode}</span>
              <button onClick={copyCode} className="btn btn-ghost btn-sm btn-icon">
                <Copy size={16} />
              </button>
            </div>
            <button 
              onClick={handleLeaveRoom} 
              className="btn btn-secondary flex items-center gap-1"
            >
              <CornerDownLeft size={16} /> Leave
            </button>
          </div>
        </div>

        {/* Players grid */}
        <div>
          <div className={styles.sectionTitle}>
            <Users size={24} style={{ color: 'var(--accent)' }} />
            <h2>Lobby Players</h2>
          </div>

          <div className={styles.playerGrid}>
            {room.players.map(player => {
              const isPlayerHost = player.playerRole === 1;
              const isSelf = currentUser && player.userId === currentUser.userId;

              return (
                <div 
                  key={player.userId} 
                  className={`${styles.playerCard} ${isSelf ? styles.playerCardActive : ''}`}
                >
                  {isPlayerHost && (
                    <span className={styles.hostBadge}>
                      <Crown size={12} className="inline mr-1" /> Host
                    </span>
                  )}

                  <div className={styles.charAvatar} style={{ marginBottom: 'var(--space-3)' }}>
                    {player.character?.name[0].toUpperCase() || 'P'}
                  </div>

                  <span style={{ fontWeight: 'var(--weight-bold)', fontSize: 'var(--text-lg)', color: 'var(--text)' }}>
                    {player.user?.username} {isSelf && <span style={{ fontSize: 'var(--text-xs)', color: 'var(--accent)', fontWeight: 'var(--weight-normal)' }}>(You)</span>}
                  </span>

                  <span style={{ fontSize: 'var(--text-xs)', color: 'var(--accent)', fontWeight: 'var(--weight-semibold)', letterSpacing: '0.05em', textTransform: 'uppercase', marginTop: 'var(--space-1)' }}>
                    {player.character?.class} (Lv. {player.character?.level})
                  </span>

                  <span style={{ fontSize: 'var(--text-sm)', fontWeight: 'var(--weight-medium)', color: 'var(--text-secondary)', marginTop: 'var(--space-1)', fontStyle: 'italic' }}>
                    &ldquo;{player.character?.name}&rdquo;
                  </span>

                  <div className={styles.readyIndicator}>
                    <span className={`${styles.readyBadge} ${player.isReady ? styles.readyBadgeTrue : styles.readyBadgeFalse}`}>
                      {player.isReady ? 'Ready' : 'Not Ready'}
                    </span>
                  </div>
                </div>
              );
            })}

            {/* Empty slots placeholders */}
            {Array.from({ length: Math.max(0, room.maxPlayers - room.players.length) }).map((_, idx) => (
              <div key={`empty-${idx}`} className={styles.playerCard} style={{ borderStyle: 'dashed', opacity: 0.4 }}>
                <div style={{ width: 48, height: 48, borderRadius: '50%', border: '2px dashed var(--border-hover)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-muted)', marginBottom: 'var(--space-3)' }}>
                  +
                </div>
                <span style={{ fontWeight: 'var(--weight-semibold)', color: 'var(--text-muted)' }}>Waiting for Player...</span>
                <span style={{ fontSize: 'var(--text-xs)', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.1em', marginTop: 'var(--space-1)' }}>Empty Slot</span>
              </div>
            ))}
          </div>
        </div>

        {/* Lobby Footer Action buttons */}
        <div className={styles.glassCard} style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: 'var(--space-4)' }}>
          <div className="flex items-center gap-2">
            {allReady ? (
              <span style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', color: 'var(--success)', fontWeight: 'var(--weight-semibold)', fontSize: 'var(--text-sm)' }}>
                <CheckCircle size={18} /> All players are ready!
              </span>
            ) : (
              <span style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', color: 'var(--warning)', fontWeight: 'var(--weight-semibold)', fontSize: 'var(--text-sm)' }}>
                <Clock size={18} /> Waiting for all players to set Ready...
              </span>
            )}
          </div>

          <div className={styles.lobbyActions}>
            <button
              onClick={handleToggleReady}
              disabled={readyLoading}
              className={`btn ${activePlayer?.isReady ? 'btn-secondary' : 'btn-primary'} flex items-center gap-2`}
            >
              {activePlayer?.isReady ? <XCircle size={16} /> : <CheckCircle size={16} />}
              {activePlayer?.isReady ? 'Set Not Ready' : 'Set Ready'}
            </button>

            {isHost && (
              <button
                onClick={handleStartGame}
                disabled={startLoading || !allReady || room.status !== 'waiting'}
                className="btn btn-primary btn-lg"
              >
                <Play size={16} />
                {startLoading ? 'Launching...' : 'Start Game'}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
