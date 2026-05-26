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
      <div className="flex justify-center items-center h-screen bg-slate-950">
        <div className="spinner-border animate-spin inline-block w-8 h-8 border-4 rounded-full text-indigo-500" role="status"></div>
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
        <div className={styles.glassCard + ' flex flex-col md:flex-row justify-between items-start md:items-center gap-4'}>
          <div>
            <span className="text-sm font-semibold tracking-wider text-indigo-400 uppercase">Room Session Lobby</span>
            <h1 className="text-2xl md:text-3xl font-extrabold text-white mt-1">{room.roomName}</h1>
            <div className="flex items-center gap-4 mt-2 text-sm text-gray-400">
              <span className="flex items-center gap-1">
                <Users size={16} /> {room.players.length} / {room.maxPlayers} Players
              </span>
              <span className="flex items-center gap-1">
                <Clock size={16} /> Status: <strong className="text-indigo-300 capitalize">{room.status}</strong>
              </span>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <div className={styles.inviteCodeBox}>
              <span className="text-xs text-indigo-300 uppercase tracking-widest mr-1">Invite Code</span>
              <span className={styles.inviteCode}>{room.roomCode}</span>
              <button onClick={copyCode} className="text-indigo-400 hover:text-indigo-200 transition">
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
            <Users size={24} className="text-indigo-400" />
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

                  <div className={styles.charAvatar + ' mb-3'}>
                    {player.character?.name[0].toUpperCase() || 'P'}
                  </div>

                  <span className="font-bold text-lg text-white">
                    {player.user?.username} {isSelf && <span className="text-xs text-indigo-400 font-normal">(You)</span>}
                  </span>

                  <span className="text-xs text-indigo-300 font-semibold tracking-wider uppercase mt-1">
                    {player.character?.class} (Lv. {player.character?.level})
                  </span>

                  <span className="text-sm font-medium text-indigo-200 mt-1 italic">
                    "{player.character?.name}"
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
              <div key={`empty-${idx}`} className={`${styles.playerCard} border-dashed opacity-40`}>
                <div className="w-12 h-12 rounded-full border-2 border-dashed border-gray-600 flex items-center justify-center text-gray-500 mb-3">
                  +
                </div>
                <span className="font-semibold text-gray-500">Waiting for Player...</span>
                <span className="text-xs text-gray-600 uppercase tracking-widest mt-1">Empty Slot</span>
              </div>
            ))}
          </div>
        </div>

        {/* Lobby Footer Action buttons */}
        <div className={styles.glassCard + ' flex flex-col md:flex-row justify-between items-center gap-4 border-indigo-950 bg-slate-900/50'}>
          <div className="flex items-center gap-2">
            {allReady ? (
              <span className="flex items-center gap-2 text-emerald-400 font-semibold text-sm">
                <CheckCircle size={18} /> All players are ready!
              </span>
            ) : (
              <span className="flex items-center gap-2 text-amber-400 font-semibold text-sm">
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
                className="btn btn-primary bg-gradient-to-r from-fuchsia-600 to-indigo-600 hover:from-fuchsia-500 hover:to-indigo-500 text-white font-extrabold px-8 flex items-center gap-2 shadow-lg shadow-indigo-500/25"
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
