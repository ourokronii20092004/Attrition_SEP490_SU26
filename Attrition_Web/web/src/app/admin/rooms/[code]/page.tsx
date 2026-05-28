'use client';

import { useState, useEffect, useRef } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import styles from '../rooms-admin.module.css';
import { 
  Gamepad2, 
  Terminal as TermIcon, 
  Map, 
  Send, 
  ArrowLeft,
  Users,
  Compass,
  Zap,
  Play
} from 'lucide-react';

interface TelemetryPlayer {
  username: string;
  x: number;
  y: number;
  hp: number;
  maxHp: number;
  characterClass: string;
}

interface TelemetryState {
  players: TelemetryPlayer[];
  scene: string;
  monstersAlive: number;
  secondsElapsed: number;
}

interface LogEntry {
  timestamp: string;
  message: string;
  type: 'log' | 'cmd' | 'err';
}

export default function AdminRoomInspector() {
  const params = useParams();
  const router = useRouter();
  const toast = useToast();
  const code = params?.code as string;

  const [loading, setLoading] = useState(true);
  const [telemetry, setTelemetry] = useState<TelemetryState | null>(null);
  
  // Console Command states
  const [commandType, setCommandType] = useState('MESSAGE');
  const [commandPayload, setCommandPayload] = useState('');
  const [sendingCmd, setSendingCmd] = useState(false);
  const [terminalLogs, setTerminalLogs] = useState<LogEntry[]>([]);

  // Simulation mode
  const [isSimulating, setIsSimulating] = useState(false);
  const simIntervalRef = useRef<NodeJS.Timeout | null>(null);

  const addLog = (message: string, type: 'log' | 'cmd' | 'err' = 'log') => {
    const entry: LogEntry = {
      timestamp: new Date().toLocaleTimeString(),
      message,
      type
    };
    setTerminalLogs(prev => [entry, ...prev].slice(0, 100)); // Cap logs at 100 entries
  };

  const fetchTelemetryState = async () => {
    try {
      const res = await api.get<string>(`/rooms/${code}/state`);
      if (res.success && res.data) {
        const parsed: TelemetryState = JSON.parse(res.data);
        setTelemetry(parsed);
      }
    } catch (err: any) {
      // It's normal to not have a state in Redis if game hasn't started yet
      console.log('No active Redis session state yet.', err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!code) return;

    fetchTelemetryState();
    
    // Poll telemetry state from Redis every 3 seconds
    const interval = setInterval(() => {
      if (!isSimulating) {
        fetchTelemetryState();
      }
    }, 3000);

    // Seed initial logs
    addLog(`Connected to room session telemetry channel for ${code}.`, 'log');

    return () => {
      clearInterval(interval);
      if (simIntervalRef.current) clearInterval(simIntervalRef.current);
    };
  }, [code, isSimulating]);

  const handleSendCommand = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!commandPayload.trim() && commandType !== 'TOGGLE_GODMODE') {
      toast.error('Please enter a command payload');
      return;
    }

    setSendingCmd(true);
    try {
      const payload = commandPayload.trim() || 'true';
      addLog(`Dispatched command: ${commandType} payload: ${payload}`, 'cmd');

      const res = await api.put(`/rooms/${code}/commands`, {
        command: commandType,
        payload
      });

      if (res.success) {
        toast.success('Command published!');
        setCommandPayload('');
      }
    } catch (err: any) {
      addLog(`Command dispatch failed: ${err.message}`, 'err');
      toast.error(err.message || 'Failed to send command');
    } finally {
      setSendingCmd(false);
    }
  };

  // Start a local coordinate movement simulation to demonstrate the minimap functionality
  const toggleSimulation = async () => {
    if (isSimulating) {
      setIsSimulating(false);
      if (simIntervalRef.current) clearInterval(simIntervalRef.current);
      addLog('Lobby coordinate telemetry simulation terminated.', 'log');
      return;
    }

    setIsSimulating(true);
    addLog('Seeding mock coordinates. Telemetry simulation online...', 'log');

    // Initial state
    let mockState: TelemetryState = {
      players: [
        { username: 'GamerGod_99', x: 250, y: 250, hp: 100, maxHp: 100, characterClass: 'Warrior' },
        { username: 'Luminance_Mage', x: 180, y: 200, hp: 72, maxHp: 80, characterClass: 'Mage' },
        { username: 'ShadowBlade', x: 300, y: 150, hp: 90, maxHp: 90, characterClass: 'Rogue' }
      ],
      scene: 'Forgotten_Ruins_Area_2',
      monstersAlive: 8,
      secondsElapsed: 120
    };

    setTelemetry(mockState);

    // Every 1.5 seconds, drift positions and update Redis
    simIntervalRef.current = setInterval(async () => {
      mockState = {
        ...mockState,
        secondsElapsed: mockState.secondsElapsed + 1,
        players: mockState.players.map(p => {
          // Drifts position by random amount [-15, +15] scaled to boundary [10, 490]
          const dx = Math.floor(Math.random() * 31) - 15;
          const dy = Math.floor(Math.random() * 31) - 15;
          return {
            ...p,
            x: Math.max(10, Math.min(490, p.x + dx)),
            y: Math.max(10, Math.min(490, p.y + dy)),
            hp: Math.max(0, Math.min(p.maxHp, p.hp + (Math.random() > 0.85 ? -5 : 0)))
          };
        })
      };

      setTelemetry(mockState);
      
      // Update state in Redis to simulate Photon Client activity
      try {
        await api.put(`/rooms/${code}/state`, JSON.stringify(mockState));
      } catch (e) {
        console.error('Failed to sync simulation state', e);
      }
    }, 1500);
  };

  return (
    <div className={styles.container}>
      <div className="flex items-center gap-3 mb-6">
        <button onClick={() => router.push('/admin/rooms')} className="btn btn-secondary btn-sm p-2">
          <ArrowLeft size={16} />
        </button>
        <span className="text-sm font-semibold" style={{ letterSpacing: '0.05em', color: 'var(--accent)', textTransform: 'uppercase' }}>Lobby Session Telemetry</span>
      </div>

      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Session Inspector: {code}</h1>
          <p className="text-sm" style={{ color: 'var(--text-tertiary)', marginTop: 'var(--space-1)' }}>Real-time Redis telemetry, game command dispatch pipelines, and visual coordinate rendering.</p>
        </div>

        <button 
          onClick={toggleSimulation}
          className={`btn ${isSimulating ? 'btn-danger' : 'btn-primary'}`}
          style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}
        >
          <Play size={16} />
          {isSimulating ? 'Stop Telemetry Simulation' : 'Launch Mock Simulator'}
        </button>
      </div>

      <div className={styles.inspectorGrid}>
        {/* Left Column: Stats & Command dispatch console */}
        <div className="flex flex-col gap-6">
          <div className={styles.panel}>
            <div className={styles.panelTitle}>
              <Users size={18} style={{ display: 'inline', marginRight: 'var(--space-2)', color: 'var(--accent)' }} />
              Room Players
            </div>

            {telemetry ? (
              <div className="flex flex-col gap-4">
                <div className="card" style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-4)', fontSize: 'var(--text-sm)', padding: 'var(--space-3)' }}>
                  <div>Scene: <strong style={{ color: 'var(--text)' }}>{telemetry.scene}</strong></div>
                  <div>Monsters: <strong style={{ color: 'var(--text)' }}>{telemetry.monstersAlive}</strong></div>
                  <div>Elapsed: <strong style={{ color: 'var(--text)' }}>{telemetry.secondsElapsed}s</strong></div>
                </div>

                <div className="flex flex-col gap-3">
                  {telemetry.players.map((p, idx) => (
                    <div key={idx} className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: 'var(--space-3)' }}>
                      <div>
                        <span style={{ fontWeight: 'var(--weight-bold)', color: 'var(--text)', display: 'block' }}>{p.username}</span>
                        <span style={{ fontSize: 'var(--text-xs)', color: 'var(--accent)', textTransform: 'uppercase', letterSpacing: '0.1em' }}>{p.characterClass}</span>
                      </div>
                      <div style={{ textAlign: 'right' }}>
                        <span style={{ fontSize: 'var(--text-xs)', color: 'var(--text-tertiary)', display: 'block' }}>Position: ({Math.round(p.x)}, {Math.round(p.y)})</span>
                        <span style={{ fontSize: 'var(--text-xs)', color: 'var(--success)', fontWeight: 'var(--weight-semibold)' }}>HP: {p.hp}/{p.maxHp}</span>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <div className="empty-state" style={{ padding: 'var(--space-10)' }}>
                <span className="empty-state-icon">🧭</span>
                <p>Waiting for Photon active session state...</p>
                <p className="text-xs" style={{ color: 'var(--text-muted)', marginTop: 'var(--space-1)' }}>Activate the Telemetry Simulator on the right to start plotting mock values.</p>
              </div>
            )}
          </div>

          <div className={styles.panel}>
            <div className={styles.panelTitle}>
              <Zap size={18} style={{ display: 'inline', marginRight: 'var(--space-2)', color: 'var(--accent)' }} />
              Developer Command Dispatcher
            </div>
            
            <form onSubmit={handleSendCommand} className={styles.consoleForm}>
              <select 
                className={styles.consoleSelect}
                value={commandType}
                onChange={e => setCommandType(e.target.value)}
              >
                <option value="MESSAGE">BROADCAST MESSAGE</option>
                <option value="KICK_PLAYER">KICK PLAYER</option>
                <option value="ADD_XP">GRANT EXPERIENCE (XP)</option>
                <option value="SPAWN_MONSTER">SPAWN MONSTERS</option>
                <option value="TOGGLE_GODMODE">TOGGLE GOD MODE</option>
              </select>
              
              {commandType !== 'TOGGLE_GODMODE' && (
                <input 
                  type="text" 
                  className={styles.consoleInput + ' flex-1'}
                  placeholder="Enter payload/values..."
                  value={commandPayload}
                  onChange={e => setCommandPayload(e.target.value)}
                  required
                />
              )}

              <button 
                type="submit" 
                className="btn btn-primary p-3 flex items-center justify-center gap-1"
                disabled={sendingCmd}
              >
                <Send size={16} /> Send
              </button>
            </form>
          </div>

          <div className={styles.panel}>
            <div className={styles.panelTitle}>
              <TermIcon size={18} style={{ display: 'inline', marginRight: 'var(--space-2)', color: 'var(--success)' }} />
              Telemetry Log Terminal
            </div>
            <div className={styles.terminal}>
              {terminalLogs.map((log, idx) => (
                <div key={idx} className={
                  log.type === 'cmd' ? styles.terminalCmd : 
                  log.type === 'err' ? styles.terminalErr : 
                  styles.terminalLog
                }>
                  <span>[{log.timestamp}]</span> {log.message}
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Right Column: Radar Sweep 2D Map */}
        <div className={styles.panel}>
          <div className={styles.panelTitle}>
              <Map size={18} style={{ display: 'inline', marginRight: 'var(--space-2)', color: 'var(--accent)' }} />
            2D Arena Map Plot
          </div>

          <div className={styles.minimapWrapper}>
            <div className={styles.minimapGrid}>
              {telemetry?.players.map((p, idx) => {
                // Scale coordinate [0, 500] to percentage [0, 100]
                const left = `${(p.x / 500) * 100}%`;
                const top = `${(p.y / 500) * 100}%`;

                return (
                  <div 
                    key={idx} 
                    className={styles.playerDot}
                    style={{ left, top }}
                  >
                    <span className={styles.playerDotLabel}>
                      {p.username} ({Math.round(p.x)}, {Math.round(p.y)})
                    </span>
                  </div>
                );
              })}
            </div>

            <div style={{ textAlign: 'center', fontSize: 'var(--text-xs)', color: 'var(--text-muted)', marginTop: 'var(--space-2)' }}>
              <Compass size={14} style={{ display: 'inline', marginRight: 'var(--space-1)' }} /> Grid coordinates represent active [500x500] gameplay bounds.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
