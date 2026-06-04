import { useState } from "react";
import { Crown, Wifi, ChevronRight, Users } from "lucide-react";
import { motion } from "motion/react";

type Props = { onNavigate: (screen: string) => void };

function PlayerCard({
  isHost, name, level, ready, onToggleReady,
}: {
  isHost: boolean; name: string | null; level: number | null; ready: boolean; onToggleReady?: () => void;
}) {
  const filled = name !== null;

  return (
    <div
      className="flex-1 rounded-lg flex flex-col items-center p-8 gap-6 relative overflow-hidden"
      style={{
        background: "rgba(10,12,18,0.88)",
        border: `1px solid ${isHost ? "rgba(201,168,76,0.35)" : "rgba(100,120,180,0.25)"}`,
        boxShadow: `inset 0 0 40px rgba(0,0,0,0.5), 0 0 ${isHost ? "20px rgba(201,168,76,0.08)" : "0"}`,
      }}
    >
      {/* Corner label */}
      <div
        className="absolute top-4 left-4 flex items-center gap-1.5 px-2.5 py-1 rounded"
        style={{
          background: isHost ? "rgba(201,168,76,0.15)" : "rgba(80,120,200,0.15)",
          border: `1px solid ${isHost ? "rgba(201,168,76,0.3)" : "rgba(80,120,200,0.3)"}`,
        }}
      >
        {isHost ? (
          <Crown size={11} className="text-[#c9a84c]" />
        ) : (
          <Users size={11} className="text-[#5588cc]" />
        )}
        <span
          className="uppercase tracking-widest"
          style={{ fontSize: 10, color: isHost ? "#c9a84c" : "#5588cc", fontFamily: "'Cinzel', serif" }}
        >
          {isHost ? "Host" : "Client"}
        </span>
      </div>

      {/* Avatar */}
      <div
        className="rounded-full flex items-center justify-center relative mt-4"
        style={{
          width: 120,
          height: 120,
          background: filled
            ? "linear-gradient(145deg, #1e1530, #0e0c18)"
            : "rgba(0,0,0,0.3)",
          border: `2px solid ${filled ? (isHost ? "rgba(201,168,76,0.55)" : "rgba(80,120,200,0.45)") : "rgba(255,255,255,0.06)"}`,
          boxShadow: filled ? `0 0 24px ${isHost ? "rgba(201,168,76,0.2)" : "rgba(80,120,200,0.15)"}` : "none",
        }}
      >
        {filled ? (
          <svg viewBox="0 0 120 120" className="w-full h-full">
            <circle cx="60" cy="40" r="20" fill="#3a2a4a" />
            <ellipse cx="60" cy="98" rx="32" ry="26" fill="#3a2a4a" />
            <path d="M42 68 Q60 58 78 68 L80 98 Q60 104 40 98 Z" fill="#2a2038" />
            {isHost && (
              <path d="M50 28 L55 22 L60 26 L65 22 L70 28 L65 25 L60 30 L55 25 Z" fill="#c9a84c" opacity="0.7" />
            )}
          </svg>
        ) : (
          <div className="flex flex-col items-center gap-2 text-white/20">
            <div className="w-10 h-10 rounded-full border-2 border-dashed border-white/10" />
            <span className="text-[10px] uppercase tracking-wider">Waiting...</span>
          </div>
        )}
      </div>

      {/* Info */}
      <div className="flex flex-col items-center gap-1 text-center">
        <span
          className="text-white tracking-wider"
          style={{ fontFamily: "'Cinzel', serif", fontSize: 18, fontWeight: 600 }}
        >
          {name ?? "—"}
        </span>
        {level !== null && (
          <span className="text-white/40 uppercase tracking-widest" style={{ fontSize: 11 }}>
            Level {level}
          </span>
        )}
      </div>

      {/* Status / Toggle */}
      {isHost ? (
        <div
          className="flex items-center gap-2 px-4 py-2 rounded"
          style={{
            background: "rgba(34,170,68,0.12)",
            border: "1px solid rgba(34,170,68,0.3)",
          }}
        >
          <div className="w-2 h-2 rounded-full bg-[#22aa44]" style={{ boxShadow: "0 0 6px #22aa44" }} />
          <span className="text-[#22aa44] uppercase tracking-widest" style={{ fontSize: 10 }}>
            Connected
          </span>
        </div>
      ) : (
        <button
          onClick={onToggleReady}
          className="flex items-center gap-2 px-5 py-2 rounded transition-all"
          style={{
            background: ready ? "rgba(34,170,68,0.15)" : "rgba(100,100,140,0.15)",
            border: `1px solid ${ready ? "rgba(34,170,68,0.4)" : "rgba(150,150,200,0.2)"}`,
            cursor: "pointer",
          }}
        >
          {ready ? (
            <>
              <div className="w-2 h-2 rounded-full bg-[#22aa44]" style={{ boxShadow: "0 0 6px #22aa44" }} />
              <span className="text-[#22aa44] uppercase tracking-widest" style={{ fontSize: 10, fontFamily: "'Cinzel', serif" }}>
                Ready
              </span>
            </>
          ) : (
            <>
              <div className="w-2 h-2 rounded-full bg-white/20" />
              <span className="text-white/40 uppercase tracking-widest" style={{ fontSize: 10, fontFamily: "'Cinzel', serif" }}>
                Not Ready
              </span>
            </>
          )}
        </button>
      )}
    </div>
  );
}

export function CoopLobby({ onNavigate }: Props) {
  const [clientReady, setClientReady] = useState(false);

  return (
    <div
      className="w-full h-full flex flex-col items-center justify-between p-8"
      style={{ background: "radial-gradient(ellipse at center top, #0f1018 0%, #07080d 100%)" }}
    >
      {/* Header */}
      <div className="flex flex-col items-center gap-2">
        <div className="flex items-center gap-4">
          <div className="h-px w-16 bg-gradient-to-r from-transparent to-[#c9a84c]/50" />
          <h1
            className="text-white tracking-widest uppercase"
            style={{ fontFamily: "'Cinzel', serif", fontSize: 26, fontWeight: 700 }}
          >
            Co-op Room
          </h1>
          <div className="h-px w-16 bg-gradient-to-l from-transparent to-[#c9a84c]/50" />
        </div>
        <div className="flex items-center gap-2">
          <Wifi size={11} className="text-[#22aa44]" />
          <span className="text-[#22aa44]/70 uppercase tracking-widest" style={{ fontSize: 10 }}>
            Room ID: 8F4A-D91C
          </span>
        </div>
      </div>

      {/* Player cards */}
      <div className="flex gap-6 w-full max-w-2xl">
        <PlayerCard isHost name="Artorias" level={42} ready />
        <div
          className="flex items-center justify-center"
          style={{ color: "rgba(201,168,76,0.4)", fontSize: 22, fontFamily: "'Cinzel', serif" }}
        >
          vs
        </div>
        <PlayerCard
          isHost={false}
          name="Eileen"
          level={38}
          ready={clientReady}
          onToggleReady={() => setClientReady((v) => !v)}
        />
      </div>

      {/* Bottom */}
      <div className="w-full max-w-2xl flex items-center justify-between">
        <button
          onClick={() => onNavigate("menu")}
          className="uppercase tracking-widest text-white/30 hover:text-white/60 transition-colors"
          style={{ fontSize: 11, fontFamily: "'Cinzel', serif", background: "none", border: "none", cursor: "pointer" }}
        >
          ← Back
        </button>

        {/* Start Journey */}
        <motion.button
          whileHover={{ scale: 1.03 }}
          whileTap={{ scale: 0.97 }}
          className="flex items-center gap-3 px-10 py-4 rounded relative overflow-hidden"
          style={{
            background: clientReady
              ? "linear-gradient(135deg, rgba(201,168,76,0.25), rgba(160,120,40,0.35))"
              : "rgba(30,30,40,0.7)",
            border: `1px solid ${clientReady ? "rgba(201,168,76,0.55)" : "rgba(255,255,255,0.08)"}`,
            boxShadow: clientReady ? "0 0 24px rgba(201,168,76,0.2)" : "none",
            cursor: clientReady ? "pointer" : "not-allowed",
            transition: "all 0.3s",
          }}
        >
          <span
            className="uppercase tracking-[0.2em]"
            style={{
              fontFamily: "'Cinzel', serif",
              fontSize: 14,
              fontWeight: 600,
              color: clientReady ? "#e8d8a8" : "rgba(255,255,255,0.3)",
            }}
          >
            Start Journey
          </span>
          <ChevronRight
            size={16}
            style={{ color: clientReady ? "#c9a84c" : "rgba(255,255,255,0.2)" }}
          />
        </motion.button>

        <div className="flex items-center gap-1.5">
          <Wifi size={11} className="text-[#22aa44]" />
          <span className="font-mono text-[#22aa44]/60" style={{ fontSize: 10 }}>Ping: 24ms</span>
        </div>
      </div>
    </div>
  );
}
