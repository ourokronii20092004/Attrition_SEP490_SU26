import { useState } from "react";
import { Flame } from "lucide-react";
import { motion } from "motion/react";

function Bar({ value, color, glow, h }: { value: number; color: string; glow: string; h: number }) {
  return (
    <div
      className="relative w-full rounded-sm overflow-hidden"
      style={{ height: h, background: "rgba(0,0,0,0.55)", border: "1px solid rgba(255,255,255,0.05)" }}
    >
      <div
        className="h-full rounded-sm"
        style={{
          width: `${value}%`,
          background: `linear-gradient(90deg, ${color}88, ${color})`,
          boxShadow: `0 0 8px ${glow}, 0 0 2px ${glow}`,
          transition: "width 0.4s ease",
        }}
      />
    </div>
  );
}

function FlaskIcon({ color }: { color: string }) {
  return (
    <svg viewBox="0 0 24 24" className="w-7 h-7" fill="none" stroke={color} strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M9 3h6" /><path d="M9 3v5l-4 6a2 2 0 001.7 3h10.6A2 2 0 0019 14l-4-6V3" /><path d="M8 14s1 1 4 1 4-1 4-1" />
    </svg>
  );
}

export function GameHUD() {
  const [hp] = useState(78);
  const [mp] = useState(100);
  const [sp] = useState(55);
  const [bossHp] = useState(43);
  const [healthFlasks] = useState(3);
  const [manaFlasks] = useState(2);

  const slotBase =
    "relative flex items-center justify-center rounded-full bg-black/70 border-2 flex-shrink-0";

  return (
    <div
      className="relative w-full h-full"
      style={{ background: "radial-gradient(ellipse at 30% 60%, #1a0a0a 0%, #0d1117 40%, #080a14 100%)" }}
    >
      {/* Grid bg */}
      <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
        <svg className="w-full h-full opacity-[0.04]">
          <defs>
            <pattern id="grid" width="60" height="60" patternUnits="userSpaceOnUse">
              <path d="M 60 0 L 0 0 0 60" fill="none" stroke="#ffffff" strokeWidth="0.5" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#grid)" />
        </svg>
        <span className="absolute uppercase tracking-[0.5em] text-white/10" style={{ fontSize: 11, fontFamily: "'Cinzel', serif" }}>
          Game World
        </span>
      </div>

      {/* ── TOP-CENTER: Boss HP Bar ── */}
      <div className="absolute top-4 left-1/2 -translate-x-1/2 flex flex-col items-center gap-1.5 w-80">
        <div
          className="uppercase tracking-[0.25em] text-white/60"
          style={{ fontFamily: "'Cinzel', serif", fontSize: 11 }}
        >
          Lord Cinder
        </div>
        <div className="w-full">
          <div
            className="w-full rounded-sm overflow-hidden relative"
            style={{ height: 10, background: "rgba(0,0,0,0.65)", border: "1px solid rgba(180,30,30,0.35)" }}
          >
            {/* Boss bar fill */}
            <motion.div
              className="h-full rounded-sm"
              initial={{ width: "100%" }}
              animate={{ width: `${bossHp}%` }}
              transition={{ duration: 1.2, ease: "easeOut" }}
              style={{
                background: "linear-gradient(90deg, #7a0000, #cc1111)",
                boxShadow: "0 0 10px rgba(180,0,0,0.7), 0 0 3px rgba(220,50,50,0.9)",
              }}
            />
            {/* Phase divider at 50% */}
            <div
              className="absolute top-0 bottom-0 w-px"
              style={{ left: "50%", background: "rgba(255,255,255,0.2)" }}
            />
          </div>
        </div>
        <div className="flex items-center gap-2">
          {[1, 2].map((phase) => (
            <div
              key={phase}
              className="w-1.5 h-1.5 rounded-full"
              style={{
                background: phase === 1 ? "#cc1111" : "rgba(255,255,255,0.15)",
                boxShadow: phase === 1 ? "0 0 4px #cc1111" : "none",
              }}
            />
          ))}
          <span className="font-mono text-white/30" style={{ fontSize: 9 }}>{bossHp}/100</span>
        </div>
      </div>

      {/* ── TOP-LEFT: Avatar + Bars ── */}
      <div className="absolute top-5 left-5 flex items-center gap-3">
        <div
          className={`${slotBase} w-16 h-16`}
          style={{
            borderColor: "rgba(201,168,76,0.65)",
            boxShadow: "0 0 14px rgba(201,168,76,0.28), inset 0 0 8px rgba(0,0,0,0.6)",
            background: "linear-gradient(145deg, #1e1430 0%, #0e0c14 100%)",
          }}
        >
          <svg viewBox="0 0 64 64" className="w-full h-full">
            <circle cx="32" cy="22" r="11" fill="#3a2a4a" />
            <ellipse cx="32" cy="54" rx="17" ry="14" fill="#3a2a4a" />
          </svg>
        </div>
        <div className="flex flex-col gap-[6px] w-44">
          <div className="flex items-center gap-2">
            <span className="w-5 text-right font-mono text-[10px] text-[#cc4444]">HP</span>
            <div className="flex-1"><Bar value={hp} color="#cc2929" glow="rgba(204,41,41,0.8)" h={8} /></div>
            <span className="font-mono text-[10px] text-white/40 w-10">{hp}/100</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="w-5 text-right font-mono text-[10px] text-[#4488cc]">MP</span>
            <div className="flex-1"><Bar value={mp} color="#2255cc" glow="rgba(34,85,204,0.8)" h={6} /></div>
            <span className="font-mono text-[10px] text-white/40 w-10">{mp}/100</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="w-5 text-right font-mono text-[10px] text-[#44aa55]">SP</span>
            <div className="flex-1"><Bar value={sp} color="#22aa44" glow="rgba(34,170,68,0.8)" h={5} /></div>
            <span className="font-mono text-[10px] text-white/40 w-10">{sp}/100</span>
          </div>
        </div>
      </div>

      {/* ── BOTTOM-LEFT: Flasks + Skill ── */}
      <div className="absolute bottom-8 left-5 flex items-end gap-3">
        {/* Health Flask */}
        <div className="relative">
          <div
            className={`${slotBase} w-14 h-14`}
            style={{ borderColor: "rgba(204,41,41,0.55)", boxShadow: "0 0 10px rgba(204,41,41,0.2), inset 0 0 8px rgba(0,0,0,0.6)" }}
          >
            <FlaskIcon color="#cc4444" />
          </div>
          <div className="absolute -top-1 -right-1 w-5 h-5 rounded-full flex items-center justify-center border border-black/60" style={{ background: "#8b1a1a", fontSize: 10, color: "#fff" }}>
            {healthFlasks}
          </div>
          <div className="text-center mt-1 uppercase tracking-wider text-white/30" style={{ fontSize: 9 }}>[Q]</div>
        </div>

        {/* Active Skill */}
        <div className="relative flex flex-col items-center -mb-1">
          <div
            className={`${slotBase}`}
            style={{ width: 70, height: 70, borderColor: "rgba(201,168,76,0.75)", boxShadow: "0 0 18px rgba(201,168,76,0.35), inset 0 0 10px rgba(0,0,0,0.5)" }}
          >
            <Flame className="w-8 h-8 text-orange-400" />
          </div>
          <div className="mt-1 px-2 py-[2px] rounded uppercase tracking-widest text-[#c9a84c]" style={{ fontSize: 9, background: "rgba(0,0,0,0.7)", border: "1px solid rgba(201,168,76,0.25)" }}>
            Fire [J]
          </div>
        </div>

        {/* Mana Flask */}
        <div className="relative">
          <div
            className={`${slotBase} w-14 h-14`}
            style={{ borderColor: "rgba(34,85,204,0.55)", boxShadow: "0 0 10px rgba(34,85,204,0.2), inset 0 0 8px rgba(0,0,0,0.6)" }}
          >
            <FlaskIcon color="#4488cc" />
          </div>
          <div className="absolute -top-1 -right-1 w-5 h-5 rounded-full flex items-center justify-center border border-black/60" style={{ background: "#1a3a8b", fontSize: 10, color: "#fff" }}>
            {manaFlasks}
          </div>
          <div className="text-center mt-1 uppercase tracking-wider text-white/30" style={{ fontSize: 9 }}>[E]</div>
        </div>
      </div>

      {/* ── BOTTOM-RIGHT: Minimap ── */}
      <div className="absolute bottom-8 right-5 flex flex-col items-center gap-1">
        <div
          className="relative rounded-full overflow-hidden"
          style={{
            width: 136, height: 136,
            background: "rgba(8,10,18,0.88)",
            border: "2px solid rgba(201,168,76,0.38)",
            boxShadow: "0 0 24px rgba(0,0,0,0.9), 0 0 10px rgba(201,168,76,0.18), inset 0 0 14px rgba(0,0,0,0.6)",
          }}
        >
          <div className="absolute inset-0 rounded-full" style={{ border: "1px solid rgba(201,168,76,0.15)" }} />
          <svg viewBox="0 0 136 136" className="w-full h-full">
            <rect x="18" y="60" width="100" height="50" fill="#161a24" rx="2" />
            <rect x="28" y="28" width="32" height="55" fill="#111520" rx="2" />
            <rect x="74" y="38" width="28" height="48" fill="#111520" rx="2" />
            <rect x="50" y="80" width="36" height="20" fill="#1a2030" rx="1" />
            <path d="M28 88 Q68 75 108 88" stroke="#24304a" strokeWidth="5" fill="none" />
            <circle cx="68" cy="68" r="68" fill="url(#fog2)" />
            <defs>
              <radialGradient id="fog2" cx="50%" cy="50%" r="50%">
                <stop offset="60%" stopColor="transparent" />
                <stop offset="100%" stopColor="rgba(8,10,18,0.85)" />
              </radialGradient>
            </defs>
            <circle cx="38" cy="52" r="2.5" fill="#cc2929" />
            <circle cx="88" cy="76" r="2.5" fill="#cc2929" />
            <circle cx="55" cy="90" r="2.5" fill="#cc2929" />
            <rect x="92" y="46" width="7" height="6" fill="#c9a84c" rx="1" opacity="0.75" />
            <circle cx="68" cy="70" r="3.5" fill="#c9a84c" />
            <circle cx="68" cy="70" r="6" fill="none" stroke="#c9a84c" strokeWidth="1" opacity="0.5" />
          </svg>
        </div>
        <span className="uppercase tracking-[0.3em] text-[#c9a84c]/50" style={{ fontSize: 9, fontFamily: "'Cinzel', serif" }}>
          Map [M]
        </span>
      </div>
    </div>
  );
}
