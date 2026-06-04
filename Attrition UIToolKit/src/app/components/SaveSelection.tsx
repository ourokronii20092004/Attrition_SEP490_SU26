import { useState } from "react";
import { motion } from "motion/react";
import { Clock, MapPin, Plus, Trash2, ChevronRight } from "lucide-react";

type SaveSlot = {
  slot: number;
  name: string;
  level: number;
  location: string;
  playtime: string;
  deaths: number;
  filled: true;
} | { slot: number; filled: false };

const SAVES: SaveSlot[] = [
  { slot: 1, filled: true, name: "Kael the Undying", level: 42, location: "Ember Citadel — Inner Sanctum", playtime: "24:17", deaths: 188 },
  { slot: 2, filled: true, name: "Saria Ashborne", level: 18, location: "Fungal Caverns — Lower Depths", playtime: "08:44", deaths: 63 },
  { slot: 3, filled: false },
];

function AvatarSVG({ seed }: { seed: number }) {
  const hue = [260, 200, 140][seed % 3];
  return (
    <svg viewBox="0 0 72 72" className="w-full h-full">
      <defs>
        <radialGradient id={`ag${seed}`} cx="40%" cy="35%" r="60%">
          <stop offset="0%" stopColor={`hsl(${hue},30%,22%)`} />
          <stop offset="100%" stopColor={`hsl(${hue},20%,10%)`} />
        </radialGradient>
      </defs>
      <circle cx="36" cy="36" r="36" fill={`url(#ag${seed})`} />
      <circle cx="36" cy="26" r="12" fill={`hsl(${hue},18%,28%)`} />
      <ellipse cx="36" cy="62" rx="18" ry="16" fill={`hsl(${hue},18%,24%)`} />
      <path d="M22 44 Q36 36 50 44 L52 62 Q36 68 20 62 Z" fill={`hsl(${hue},20%,20%)`} />
    </svg>
  );
}

type Props = { onNavigate: (s: string) => void };

export function SaveSelection({ onNavigate }: Props) {
  const [selected, setSelected] = useState<number>(0);

  return (
    <div
      className="w-full h-full flex flex-col items-center justify-center gap-6 p-8"
      style={{ background: "radial-gradient(ellipse at center, #0d1018 0%, #07080d 100%)" }}
    >
      {/* Title */}
      <div className="flex flex-col items-center gap-1 mb-2">
        <h1
          className="uppercase tracking-[0.25em] text-white"
          style={{ fontFamily: "'Cinzel', serif", fontSize: 22, fontWeight: 700 }}
        >
          Select Save Data
        </h1>
        <div className="h-px w-48 bg-gradient-to-r from-transparent via-[#c9a84c]/50 to-transparent" />
      </div>

      {/* Save slots */}
      <div className="w-full max-w-xl flex flex-col gap-3">
        {SAVES.map((save, i) => {
          const isSelected = selected === i;
          return (
            <motion.button
              key={save.slot}
              onClick={() => setSelected(i)}
              whileHover={{ scale: 1.01 }}
              whileTap={{ scale: 0.99 }}
              className="w-full text-left rounded-lg overflow-hidden relative"
              style={{
                background: isSelected
                  ? "rgba(201,168,76,0.08)"
                  : "rgba(10,12,20,0.85)",
                border: `1px solid ${isSelected ? "rgba(201,168,76,0.45)" : "rgba(255,255,255,0.07)"}`,
                boxShadow: isSelected ? "0 0 20px rgba(201,168,76,0.1), inset 0 0 30px rgba(0,0,0,0.3)" : "inset 0 0 30px rgba(0,0,0,0.3)",
                transition: "border-color 0.2s, box-shadow 0.2s",
                cursor: "pointer",
              }}
            >
              {/* Active indicator */}
              {isSelected && (
                <div
                  className="absolute left-0 top-0 bottom-0 w-0.5"
                  style={{ background: "linear-gradient(to bottom, transparent, #c9a84c, transparent)" }}
                />
              )}

              {save.filled ? (
                <div className="flex items-center gap-5 p-4">
                  {/* Avatar */}
                  <div
                    className="w-16 h-16 rounded-full flex-shrink-0 overflow-hidden"
                    style={{
                      border: `2px solid ${isSelected ? "rgba(201,168,76,0.55)" : "rgba(255,255,255,0.1)"}`,
                      boxShadow: isSelected ? "0 0 12px rgba(201,168,76,0.2)" : "none",
                    }}
                  >
                    <AvatarSVG seed={i} />
                  </div>

                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-baseline justify-between">
                      <span
                        className="text-white tracking-wider truncate"
                        style={{ fontFamily: "'Cinzel', serif", fontSize: 15, fontWeight: 600 }}
                      >
                        {save.name}
                      </span>
                      <span
                        className="text-[#c9a84c] ml-3 flex-shrink-0 uppercase tracking-widest"
                        style={{ fontSize: 10, fontFamily: "'Cinzel', serif" }}
                      >
                        Lv. {save.level}
                      </span>
                    </div>
                    <div className="flex items-center gap-1 mt-1">
                      <MapPin size={10} className="text-white/30 flex-shrink-0" />
                      <span className="text-white/40 truncate" style={{ fontSize: 11 }}>{save.location}</span>
                    </div>
                    <div className="flex items-center gap-4 mt-2">
                      <div className="flex items-center gap-1">
                        <Clock size={9} className="text-white/25" />
                        <span className="font-mono text-white/35" style={{ fontSize: 10 }}>{save.playtime}</span>
                      </div>
                      <span className="text-white/20" style={{ fontSize: 10 }}>
                        {save.deaths} deaths
                      </span>
                    </div>
                  </div>

                  {/* Slot number + actions */}
                  <div className="flex flex-col items-center gap-2 flex-shrink-0">
                    <span className="font-mono text-white/15" style={{ fontSize: 10 }}>SLOT {save.slot}</span>
                    {isSelected && (
                      <button
                        onClick={(e) => e.stopPropagation()}
                        className="p-1.5 rounded transition-colors hover:bg-red-900/30"
                        style={{ border: "1px solid rgba(180,40,40,0.2)" }}
                      >
                        <Trash2 size={11} className="text-red-500/50" />
                      </button>
                    )}
                  </div>
                </div>
              ) : (
                /* Empty slot */
                <div className="flex items-center justify-center gap-3 p-4 h-20">
                  <div
                    className="w-10 h-10 rounded-full border-2 border-dashed flex items-center justify-center"
                    style={{ borderColor: "rgba(255,255,255,0.08)" }}
                  >
                    <Plus size={14} className="text-white/20" />
                  </div>
                  <span className="text-white/25 uppercase tracking-widest" style={{ fontSize: 11, fontFamily: "'Cinzel', serif" }}>
                    Empty Slot {save.slot}
                  </span>
                </div>
              )}
            </motion.button>
          );
        })}
      </div>

      {/* Actions */}
      <div className="flex items-center gap-4">
        <motion.button
          whileHover={{ scale: 1.03 }}
          whileTap={{ scale: 0.97 }}
          onClick={() => onNavigate("menu")}
          className="flex items-center gap-2 px-6 py-2.5 rounded uppercase tracking-widest transition-colors"
          style={{
            background: "rgba(0,0,0,0.5)",
            border: "1px solid rgba(255,255,255,0.08)",
            fontFamily: "'Cinzel', serif",
            fontSize: 11,
            color: "rgba(255,255,255,0.35)",
            cursor: "pointer",
          }}
        >
          ← Back
        </motion.button>

        <motion.button
          whileHover={{ scale: 1.03 }}
          whileTap={{ scale: 0.97 }}
          onClick={() => onNavigate("hud")}
          className="flex items-center gap-3 px-8 py-2.5 rounded uppercase tracking-widest"
          style={{
            background: "linear-gradient(135deg, rgba(201,168,76,0.22), rgba(160,120,40,0.32))",
            border: "1px solid rgba(201,168,76,0.5)",
            boxShadow: "0 0 20px rgba(201,168,76,0.15)",
            fontFamily: "'Cinzel', serif",
            fontSize: 11,
            color: "#e8d8a8",
            cursor: "pointer",
          }}
        >
          <Plus size={13} />
          Create New Character
        </motion.button>

        {SAVES[selected]?.filled && (
          <motion.button
            whileHover={{ scale: 1.03 }}
            whileTap={{ scale: 0.97 }}
            onClick={() => onNavigate("hud")}
            className="flex items-center gap-2 px-6 py-2.5 rounded uppercase tracking-widest"
            style={{
              background: "rgba(201,168,76,0.15)",
              border: "1px solid rgba(201,168,76,0.35)",
              fontFamily: "'Cinzel', serif",
              fontSize: 11,
              color: "#c9a84c",
              cursor: "pointer",
            }}
          >
            Continue <ChevronRight size={13} />
          </motion.button>
        )}
      </div>
    </div>
  );
}
