import { useState } from "react";
import { motion } from "motion/react";
import { Zap, MapPin } from "lucide-react";

type Checkpoint = {
  id: number;
  name: string;
  region: string;
  discovered: boolean;
  current?: boolean;
  previewColor: string;
  accent: string;
};

const CHECKPOINTS: Checkpoint[] = [
  { id: 1, name: "Ember Citadel", region: "Inner Sanctum", discovered: true, current: true, previewColor: "#1a0c0a", accent: "#cc4422" },
  { id: 2, name: "Ashen Gate", region: "Outer Walls", discovered: true, previewColor: "#120e0a", accent: "#aa6622" },
  { id: 3, name: "Fungal Caverns", region: "Lower Depths", discovered: true, previewColor: "#0a120a", accent: "#44aa44" },
  { id: 4, name: "Sunken Library", region: "Submerged Archive", discovered: true, previewColor: "#080a18", accent: "#4466cc" },
  { id: 5, name: "Ruined Aqueduct", region: "Eastern Span", discovered: true, previewColor: "#0e0e12", accent: "#7788aa" },
  { id: 6, name: "Iron Gate Keep", region: "Northern Hold", discovered: false, previewColor: "#0a0a0a", accent: "#666666" },
  { id: 7, name: "The Ashfields", region: "Desolation Flats", discovered: true, previewColor: "#14100a", accent: "#cc8833" },
];

function MapPreview({ cp }: { cp: Checkpoint }) {
  return (
    <div
      className="w-full h-full rounded-lg overflow-hidden relative"
      style={{ background: cp.previewColor, border: `1px solid ${cp.accent}33` }}
    >
      {/* Stylized SVG map snippet */}
      <svg viewBox="0 0 280 200" className="w-full h-full">
        <defs>
          <radialGradient id={`pg${cp.id}`} cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor={cp.accent} stopOpacity="0.12" />
            <stop offset="100%" stopColor={cp.previewColor} stopOpacity="1" />
          </radialGradient>
        </defs>
        <rect width="280" height="200" fill={`url(#pg${cp.id})`} />

        {/* Terrain outlines */}
        <g stroke={`${cp.accent}33`} strokeWidth="1" fill="none">
          <rect x="20" y="30" width="80" height="60" rx="2" />
          <rect x="110" y="20" width="60" height="40" rx="2" />
          <rect x="180" y="50" width="80" height="80" rx="2" />
          <rect x="40" y="110" width="120" height="60" rx="2" />
          <rect x="170" y="140" width="90" height="40" rx="2" />
        </g>

        {/* Paths */}
        <path d="M100 60 L110 40 M170 40 L180 60 M160 110 L170 140" stroke={`${cp.accent}44`} strokeWidth="2" strokeDasharray="4 3" />

        {/* Checkpoint beacon */}
        <g>
          <circle cx="100" cy="110" r="6" fill={cp.accent} opacity="0.9" />
          <circle cx="100" cy="110" r="10" fill="none" stroke={cp.accent} strokeWidth="1.5" opacity="0.5" />
          <circle cx="100" cy="110" r="16" fill="none" stroke={cp.accent} strokeWidth="0.75" opacity="0.25" />
          <line x1="100" y1="80" x2="100" y2="96" stroke={cp.accent} strokeWidth="1.5" opacity="0.7" />
        </g>

        {/* Region label */}
        <text x="140" y="180" fill={`${cp.accent}55`} fontSize="8" textAnchor="middle" fontFamily="'Cinzel', serif" letterSpacing="3">
          {cp.region.toUpperCase()}
        </text>
      </svg>

      {/* Overlay info */}
      <div
        className="absolute bottom-0 left-0 right-0 p-4"
        style={{ background: `linear-gradient(to top, ${cp.previewColor}ee, transparent)` }}
      >
        <div
          className="text-white tracking-wider uppercase"
          style={{ fontFamily: "'Cinzel', serif", fontSize: 13, fontWeight: 600 }}
        >
          {cp.name}
        </div>
        <div className="text-white/40 uppercase tracking-widest" style={{ fontSize: 9 }}>
          {cp.region}
        </div>
      </div>
    </div>
  );
}

type Props = { onNavigate: (s: string) => void };

export function FastTravel({ onNavigate }: Props) {
  const [selected, setSelected] = useState(0);
  const selectedCp = CHECKPOINTS[selected];

  return (
    <div
      className="w-full h-full flex flex-col"
      style={{ background: "radial-gradient(ellipse at center, #0d1018 0%, #07080d 100%)" }}
    >
      {/* Header */}
      <div
        className="flex items-center justify-between px-6 py-4"
        style={{ borderBottom: "1px solid rgba(201,168,76,0.15)" }}
      >
        <div className="flex items-center gap-3">
          <Zap size={15} className="text-[#c9a84c]/60" />
          <h1
            className="text-white uppercase tracking-widest"
            style={{ fontFamily: "'Cinzel', serif", fontSize: 14, fontWeight: 600 }}
          >
            Fast Travel
          </h1>
        </div>
        <span className="text-white/30 uppercase tracking-widest" style={{ fontSize: 10 }}>
          {CHECKPOINTS.filter((c) => c.discovered).length} / {CHECKPOINTS.length} Discovered
        </span>
      </div>

      {/* Body */}
      <div className="flex flex-1 overflow-hidden">
        {/* Left: Checkpoint list */}
        <div
          className="flex flex-col overflow-y-auto w-72 flex-shrink-0"
          style={{ borderRight: "1px solid rgba(201,168,76,0.1)", scrollbarWidth: "none" }}
        >
          <div className="p-3 flex flex-col gap-1">
            {CHECKPOINTS.map((cp, i) => {
              const isSelected = selected === i;
              return (
                <button
                  key={cp.id}
                  onClick={() => cp.discovered && setSelected(i)}
                  className="w-full text-left rounded px-4 py-3 flex items-center gap-3 relative transition-all"
                  style={{
                    background: isSelected ? "rgba(201,168,76,0.1)" : "transparent",
                    border: `1px solid ${isSelected ? `${cp.accent}44` : "transparent"}`,
                    opacity: cp.discovered ? 1 : 0.4,
                    cursor: cp.discovered ? "pointer" : "not-allowed",
                  }}
                >
                  {/* Active indicator */}
                  {isSelected && (
                    <div
                      className="absolute left-0 top-2 bottom-2 w-0.5 rounded-full"
                      style={{ background: cp.accent }}
                    />
                  )}

                  {/* Beacon icon */}
                  <div
                    className="w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0"
                    style={{
                      background: isSelected ? `${cp.accent}22` : "rgba(0,0,0,0.4)",
                      border: `1px solid ${isSelected ? `${cp.accent}55` : "rgba(255,255,255,0.06)"}`,
                    }}
                  >
                    {cp.current ? (
                      <div className="w-2 h-2 rounded-full" style={{ background: cp.accent, boxShadow: `0 0 4px ${cp.accent}` }} />
                    ) : (
                      <MapPin size={11} style={{ color: isSelected ? cp.accent : "rgba(255,255,255,0.25)" }} />
                    )}
                  </div>

                  <div className="flex flex-col gap-0.5 min-w-0">
                    <span
                      className="truncate"
                      style={{
                        fontFamily: "'Cinzel', serif",
                        fontSize: 12,
                        fontWeight: isSelected ? 600 : 400,
                        color: isSelected ? "#e8d8a8" : "rgba(255,255,255,0.55)",
                      }}
                    >
                      {cp.name}
                    </span>
                    <span className="text-white/30 truncate uppercase tracking-wider" style={{ fontSize: 9 }}>
                      {cp.discovered ? cp.region : "???"}
                    </span>
                  </div>

                  {cp.current && (
                    <span
                      className="ml-auto uppercase tracking-widest flex-shrink-0"
                      style={{ fontSize: 8, color: cp.accent, fontFamily: "'Cinzel', serif" }}
                    >
                      ◉
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        </div>

        {/* Right: Map preview */}
        <div className="flex-1 p-5">
          <motion.div
            key={selected}
            initial={{ opacity: 0, scale: 0.97 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ duration: 0.2 }}
            className="w-full h-full"
          >
            <MapPreview cp={selectedCp} />
          </motion.div>
        </div>
      </div>

      {/* Bottom: Teleport */}
      <div
        className="flex items-center justify-between px-6 py-4"
        style={{ borderTop: "1px solid rgba(201,168,76,0.12)" }}
      >
        <button
          onClick={() => onNavigate("menu")}
          className="uppercase tracking-widest text-white/25 hover:text-white/50 transition-colors"
          style={{ fontSize: 10, fontFamily: "'Cinzel', serif", background: "none", border: "none", cursor: "pointer" }}
        >
          ← Close
        </button>

        <motion.button
          whileHover={{ scale: 1.03 }}
          whileTap={{ scale: 0.97 }}
          disabled={selectedCp.current}
          className="flex items-center gap-3 px-10 py-3 rounded uppercase tracking-widest"
          style={{
            background: selectedCp.current
              ? "rgba(30,30,40,0.6)"
              : `linear-gradient(135deg, ${selectedCp.accent}22, ${selectedCp.accent}38)`,
            border: `1px solid ${selectedCp.current ? "rgba(255,255,255,0.06)" : `${selectedCp.accent}55`}`,
            boxShadow: selectedCp.current ? "none" : `0 0 20px ${selectedCp.accent}18`,
            fontFamily: "'Cinzel', serif",
            fontSize: 12,
            fontWeight: 600,
            color: selectedCp.current ? "rgba(255,255,255,0.2)" : "#e8d8a8",
            cursor: selectedCp.current ? "not-allowed" : "pointer",
          }}
        >
          <Zap size={14} style={{ color: selectedCp.current ? "rgba(255,255,255,0.15)" : selectedCp.accent }} />
          {selectedCp.current ? "Current Location" : `Teleport to ${selectedCp.name}`}
        </motion.button>

        <div className="w-24" />
      </div>
    </div>
  );
}
