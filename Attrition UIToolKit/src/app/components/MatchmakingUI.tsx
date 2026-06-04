import { useState } from "react";
import { motion } from "motion/react";
import { Globe, Key, ChevronRight, Users, Wifi } from "lucide-react";

type Props = { onNavigate: (s: string) => void };

export function MatchmakingUI({ onNavigate }: Props) {
  const [roomCode, setRoomCode] = useState("");
  const [hoveredPanel, setHoveredPanel] = useState<"host" | "join" | null>(null);
  const [joinError, setJoinError] = useState(false);

  const handleJoin = () => {
    if (roomCode.trim().length < 4) { setJoinError(true); return; }
    onNavigate("coop");
  };

  const panelBase: React.CSSProperties = {
    background: "rgba(10,12,20,0.85)",
    border: "1px solid rgba(255,255,255,0.07)",
    boxShadow: "inset 0 0 40px rgba(0,0,0,0.4)",
    transition: "border-color 0.25s, box-shadow 0.25s",
  };

  return (
    <div
      className="w-full h-full flex flex-col items-center justify-center gap-8 p-8"
      style={{ background: "radial-gradient(ellipse at center, #0d1018 0%, #07080d 100%)" }}
    >
      {/* Header */}
      <div className="flex flex-col items-center gap-2">
        <div className="flex items-center gap-3">
          <Users size={16} className="text-[#c9a84c]/60" />
          <h1
            className="text-white uppercase tracking-[0.25em]"
            style={{ fontFamily: "'Cinzel', serif", fontSize: 22, fontWeight: 700 }}
          >
            Multiplayer
          </h1>
        </div>
        <div className="h-px w-40 bg-gradient-to-r from-transparent via-[#c9a84c]/40 to-transparent" />
        <p className="text-white/30 uppercase tracking-widest" style={{ fontSize: 10 }}>
          Attrition: A Spark in the Ashes
        </p>
      </div>

      {/* Panels */}
      <div className="w-full max-w-2xl flex gap-5">
        {/* Host Panel */}
        <motion.div
          className="flex-1 rounded-lg flex flex-col items-center justify-center gap-6 p-8 relative cursor-pointer"
          style={{
            ...panelBase,
            borderColor: hoveredPanel === "host" ? "rgba(201,168,76,0.45)" : "rgba(255,255,255,0.07)",
            boxShadow: hoveredPanel === "host"
              ? "inset 0 0 40px rgba(0,0,0,0.4), 0 0 30px rgba(201,168,76,0.08)"
              : panelBase.boxShadow,
          }}
          onMouseEnter={() => setHoveredPanel("host")}
          onMouseLeave={() => setHoveredPanel(null)}
          onClick={() => onNavigate("coop")}
        >
          {/* Icon */}
          <div
            className="w-16 h-16 rounded-full flex items-center justify-center"
            style={{
              background: hoveredPanel === "host" ? "rgba(201,168,76,0.12)" : "rgba(255,255,255,0.04)",
              border: `2px solid ${hoveredPanel === "host" ? "rgba(201,168,76,0.45)" : "rgba(255,255,255,0.08)"}`,
              boxShadow: hoveredPanel === "host" ? "0 0 20px rgba(201,168,76,0.15)" : "none",
              transition: "all 0.25s",
            }}
          >
            <Globe size={26} style={{ color: hoveredPanel === "host" ? "#c9a84c" : "rgba(255,255,255,0.35)" }} />
          </div>

          <div className="text-center">
            <h2
              className="text-white uppercase tracking-widest mb-2"
              style={{ fontFamily: "'Cinzel', serif", fontSize: 16, fontWeight: 600 }}
            >
              Host Game
            </h2>
            <p className="text-white/40 leading-relaxed" style={{ fontSize: 12 }}>
              Load your world and invite a friend.
              <br />
              Share your room code to begin.
            </p>
          </div>

          {/* CTA */}
          <motion.div
            className="flex items-center gap-2 px-6 py-2.5 rounded"
            animate={{
              background: hoveredPanel === "host" ? "rgba(201,168,76,0.18)" : "rgba(0,0,0,0.4)",
              borderColor: hoveredPanel === "host" ? "rgba(201,168,76,0.5)" : "rgba(255,255,255,0.08)",
            }}
            style={{ border: "1px solid", transition: "all 0.25s" }}
          >
            <span
              className="uppercase tracking-widest"
              style={{
                fontFamily: "'Cinzel', serif",
                fontSize: 11,
                color: hoveredPanel === "host" ? "#e8d8a8" : "rgba(255,255,255,0.4)",
                transition: "color 0.25s",
              }}
            >
              Create Room
            </span>
            <ChevronRight size={13} style={{ color: hoveredPanel === "host" ? "#c9a84c" : "rgba(255,255,255,0.2)" }} />
          </motion.div>
        </motion.div>

        {/* Divider */}
        <div className="flex flex-col items-center justify-center gap-2 flex-shrink-0">
          <div className="w-px flex-1 bg-gradient-to-b from-transparent via-[#c9a84c]/20 to-transparent" />
          <span className="text-white/20 uppercase tracking-widest" style={{ fontFamily: "'Cinzel', serif", fontSize: 11 }}>or</span>
          <div className="w-px flex-1 bg-gradient-to-b from-transparent via-[#c9a84c]/20 to-transparent" />
        </div>

        {/* Join Panel */}
        <motion.div
          className="flex-1 rounded-lg flex flex-col items-center justify-center gap-6 p-8 relative"
          style={{
            ...panelBase,
            borderColor: hoveredPanel === "join" ? "rgba(80,130,220,0.45)" : "rgba(255,255,255,0.07)",
            boxShadow: hoveredPanel === "join"
              ? "inset 0 0 40px rgba(0,0,0,0.4), 0 0 30px rgba(80,130,220,0.06)"
              : panelBase.boxShadow,
          }}
          onMouseEnter={() => setHoveredPanel("join")}
          onMouseLeave={() => setHoveredPanel(null)}
        >
          {/* Icon */}
          <div
            className="w-16 h-16 rounded-full flex items-center justify-center"
            style={{
              background: hoveredPanel === "join" ? "rgba(80,130,220,0.12)" : "rgba(255,255,255,0.04)",
              border: `2px solid ${hoveredPanel === "join" ? "rgba(80,130,220,0.45)" : "rgba(255,255,255,0.08)"}`,
              boxShadow: hoveredPanel === "join" ? "0 0 20px rgba(80,130,220,0.12)" : "none",
              transition: "all 0.25s",
            }}
          >
            <Key size={26} style={{ color: hoveredPanel === "join" ? "#5588cc" : "rgba(255,255,255,0.35)" }} />
          </div>

          <div className="text-center">
            <h2
              className="text-white uppercase tracking-widest mb-2"
              style={{ fontFamily: "'Cinzel', serif", fontSize: 16, fontWeight: 600 }}
            >
              Join Game
            </h2>
            <p className="text-white/40 leading-relaxed" style={{ fontSize: 12 }}>
              Enter a room code shared by<br />
              your companion to connect.
            </p>
          </div>

          {/* Code input + button */}
          <div className="w-full flex flex-col gap-2">
            <input
              value={roomCode}
              onChange={(e) => { setRoomCode(e.target.value.toUpperCase()); setJoinError(false); }}
              placeholder="Enter Room Code..."
              maxLength={8}
              className="w-full text-center rounded font-mono uppercase tracking-[0.3em]"
              style={{
                background: "rgba(0,0,0,0.5)",
                border: `1px solid ${joinError ? "rgba(180,30,30,0.5)" : "rgba(80,130,220,0.25)"}`,
                padding: "10px 14px",
                color: "#e8d8a8",
                fontSize: 14,
                outline: "none",
              }}
              onFocus={(e) => (e.currentTarget.style.borderColor = "rgba(80,130,220,0.55)")}
              onBlur={(e) => (e.currentTarget.style.borderColor = joinError ? "rgba(180,30,30,0.5)" : "rgba(80,130,220,0.25)")}
            />
            {joinError && (
              <p className="text-red-400 text-center" style={{ fontSize: 10 }}>Invalid room code. Please try again.</p>
            )}
            <motion.button
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
              onClick={handleJoin}
              className="w-full py-2.5 rounded uppercase tracking-widest flex items-center justify-center gap-2"
              style={{
                background: "rgba(80,130,220,0.15)",
                border: "1px solid rgba(80,130,220,0.35)",
                fontFamily: "'Cinzel', serif",
                fontSize: 11,
                color: "#88aadd",
                cursor: "pointer",
              }}
            >
              <Wifi size={13} /> Join
            </motion.button>
          </div>
        </motion.div>
      </div>

      <button
        onClick={() => onNavigate("menu")}
        className="uppercase tracking-widest text-white/25 hover:text-white/50 transition-colors"
        style={{ fontSize: 10, fontFamily: "'Cinzel', serif", background: "none", border: "none", cursor: "pointer" }}
      >
        ← Back to Menu
      </button>
    </div>
  );
}
