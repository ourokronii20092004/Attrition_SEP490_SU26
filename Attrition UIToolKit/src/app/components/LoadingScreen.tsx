import { useState, useEffect } from "react";
import { motion } from "motion/react";

type Props = { onNavigate: (s: string) => void };

function SpinningRune() {
  return (
    <motion.div
      animate={{ rotate: 360 }}
      transition={{ duration: 8, repeat: Infinity, ease: "linear" }}
      className="relative"
      style={{ width: 40, height: 40 }}
    >
      <svg viewBox="0 0 40 40" className="w-full h-full">
        {/* Outer ring */}
        <circle cx="20" cy="20" r="18" fill="none" stroke="rgba(201,168,76,0.3)" strokeWidth="1" strokeDasharray="4 3" />
        {/* Inner rune shape */}
        <polygon
          points="20,4 23.5,14.5 34.5,14.5 25.7,21.3 29.2,31.8 20,25 10.8,31.8 14.3,21.3 5.5,14.5 16.5,14.5"
          fill="none"
          stroke="rgba(201,168,76,0.65)"
          strokeWidth="1"
          strokeLinejoin="round"
        />
        {/* Center */}
        <circle cx="20" cy="20" r="2.5" fill="rgba(201,168,76,0.7)" />
      </svg>
    </motion.div>
  );
}

function CountdownCircle({ seconds, total }: { seconds: number; total: number }) {
  const r = 22;
  const circ = 2 * Math.PI * r;
  const prog = seconds / total;
  return (
    <svg viewBox="0 0 56 56" className="w-14 h-14">
      <circle cx="28" cy="28" r={r} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="3" />
      <circle
        cx="28" cy="28" r={r}
        fill="none"
        stroke="#c9a84c"
        strokeWidth="3"
        strokeLinecap="round"
        strokeDasharray={circ}
        strokeDashoffset={circ * (1 - prog)}
        style={{
          transform: "rotate(-90deg)",
          transformOrigin: "28px 28px",
          transition: "stroke-dashoffset 0.5s linear",
          filter: "drop-shadow(0 0 4px rgba(201,168,76,0.6))",
        }}
      />
      <text
        x="28" y="32"
        textAnchor="middle"
        fill="#e8d8a8"
        fontSize="10"
        fontFamily="'Cinzel', serif"
      >
        {String(Math.floor(seconds / 60)).padStart(2, "0")}:{String(seconds % 60).padStart(2, "0")}
      </text>
    </svg>
  );
}

export function LoadingScreen({ onNavigate }: Props) {
  const TOTAL = 300;
  const [time, setTime] = useState(TOTAL);

  useEffect(() => {
    const id = setInterval(() => setTime((t) => Math.max(0, t - 1)), 1000);
    return () => clearInterval(id);
  }, []);

  const tips = [
    "Strike during the wind-up — patience is its own weapon.",
    "Flask of Embers restores both health and resolve.",
    "The fallen leave behind echoes. Retrieve them or lose them forever.",
    "Bosses grow tired. Make them chase their own shadow.",
  ];
  const [tip] = useState(() => tips[Math.floor(Math.random() * tips.length)]);

  return (
    <div className="w-full h-full relative flex items-center justify-center" style={{ background: "#000" }}>
      {/* Vignette */}
      <div
        className="absolute inset-0 pointer-events-none"
        style={{ background: "radial-gradient(ellipse at center, transparent 30%, rgba(0,0,0,0.7) 100%)" }}
      />

      {/* Center popup panel — MSG-G-14 */}
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4, delay: 0.3 }}
        className="relative w-full max-w-sm rounded-lg overflow-hidden"
        style={{
          background: "rgba(8,10,18,0.82)",
          border: "1px solid rgba(201,168,76,0.2)",
          boxShadow: "0 0 60px rgba(0,0,0,0.9), inset 0 0 30px rgba(0,0,0,0.5)",
          backdropFilter: "blur(12px)",
        }}
      >
        {/* Top accent */}
        <div className="h-px bg-gradient-to-r from-transparent via-[#c9a84c]/40 to-transparent" />

        <div className="p-6 flex flex-col items-center gap-4">
          {/* MSG ID */}
          <div
            className="absolute top-3 right-4 uppercase tracking-widest text-white/15"
            style={{ fontSize: 8, fontFamily: "'Cinzel', serif" }}
          >
            MSG-G-14
          </div>

          {/* Countdown */}
          <CountdownCircle seconds={time} total={TOTAL} />

          {/* Message */}
          <div className="text-center flex flex-col gap-2">
            <p className="text-white leading-relaxed" style={{ fontSize: 13 }}>
              Player2 lost connection.
            </p>
            <p className="text-[#c9a84c]/80" style={{ fontSize: 12 }}>
              Session paused ({String(Math.floor(time / 60)).padStart(2, "0")}:{String(time % 60).padStart(2, "0")} to reconnect)...
            </p>
            <p className="text-white/35 mt-1" style={{ fontSize: 11 }}>
              Waiting for Player2 to return to the session.
            </p>
          </div>

          {/* Actions */}
          <div className="flex gap-3">
            <button
              onClick={() => onNavigate("gameover")}
              className="px-4 py-2 rounded uppercase tracking-widest transition-all hover:bg-red-900/20"
              style={{
                background: "rgba(0,0,0,0.5)",
                border: "1px solid rgba(180,30,30,0.3)",
                fontSize: 10,
                color: "rgba(200,80,80,0.7)",
                cursor: "pointer",
                fontFamily: "'Cinzel', serif",
              }}
            >
              Abandon
            </button>
            <button
              className="px-4 py-2 rounded uppercase tracking-widest"
              style={{
                background: "rgba(201,168,76,0.12)",
                border: "1px solid rgba(201,168,76,0.3)",
                fontSize: 10,
                color: "#c9a84c",
                cursor: "pointer",
                fontFamily: "'Cinzel', serif",
              }}
            >
              Continue Waiting
            </button>
          </div>
        </div>

        <div className="h-px bg-gradient-to-r from-transparent via-[#c9a84c]/15 to-transparent" />
      </motion.div>

      {/* Bottom area: tip + spinning rune */}
      <div className="absolute bottom-6 left-0 right-0 flex items-end justify-between px-6">
        {/* Tip */}
        <div className="max-w-sm">
          <div
            className="uppercase tracking-widest text-[#c9a84c]/40 mb-1"
            style={{ fontSize: 8, fontFamily: "'Cinzel', serif" }}
          >
            Lore of the Ashen
          </div>
          <p className="text-white/25 leading-relaxed" style={{ fontSize: 11, fontStyle: "italic" }}>
            "{tip}"
          </p>
        </div>

        {/* Spinning rune */}
        <div className="flex flex-col items-center gap-1">
          <SpinningRune />
          <span className="text-white/20 uppercase tracking-widest" style={{ fontSize: 8 }}>Loading...</span>
        </div>
      </div>
    </div>
  );
}
