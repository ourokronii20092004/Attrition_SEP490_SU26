import { useState } from "react";
import { Flame, Crown, Shield, Footprints, Circle, Gem, Shirt } from "lucide-react";

function Bar({ value, color, glow, label }: { value: number; color: string; glow: string; label: string }) {
  return (
    <div className="flex items-center gap-2">
      <span className="font-mono text-[10px] w-5 text-right" style={{ color }}>{label}</span>
      <div
        className="flex-1 rounded-sm overflow-hidden"
        style={{ height: 6, background: "rgba(0,0,0,0.5)", border: "1px solid rgba(255,255,255,0.04)" }}
      >
        <div
          className="h-full rounded-sm"
          style={{
            width: `${value}%`,
            background: `linear-gradient(90deg, ${color}77, ${color})`,
            boxShadow: `0 0 6px ${glow}`,
          }}
        />
      </div>
      <span className="font-mono text-[10px] text-white/30 w-10">{value}/100</span>
    </div>
  );
}

function Slot({
  label, icon, filled, size = 56, gold,
}: {
  label?: string; icon?: React.ReactNode; filled?: boolean; size?: number; gold?: boolean;
}) {
  return (
    <div className="flex flex-col items-center gap-1">
      <div
        className="rounded-full flex items-center justify-center relative flex-shrink-0 cursor-pointer transition-all hover:scale-105"
        style={{
          width: size,
          height: size,
          background: filled ? "linear-gradient(145deg, #1e1830, #12101a)" : "rgba(0,0,0,0.45)",
          border: `2px solid ${gold ? "rgba(201,168,76,0.55)" : "rgba(255,255,255,0.08)"}`,
          boxShadow: filled
            ? `inset 0 0 8px rgba(0,0,0,0.6), 0 0 ${gold ? "12px rgba(201,168,76,0.25)" : "6px rgba(0,0,0,0.4)"}`
            : "inset 0 0 8px rgba(0,0,0,0.5)",
        }}
      >
        {icon && <span className="text-white/60">{icon}</span>}
        {!icon && !filled && (
          <div className="w-3 h-3 rounded-full border border-white/10" />
        )}
      </div>
      {label && (
        <span className="text-[9px] text-white/30 uppercase tracking-wider text-center" style={{ maxWidth: size }}>
          {label}
        </span>
      )}
    </div>
  );
}

const INVENTORY_ITEMS: Array<{ label: string; color: string } | null> = [
  { label: "Iron Sword", color: "#888aaa" },
  { label: "Fire Staff", color: "#cc4422" },
  { label: "Steel Shield", color: "#7788aa" },
  { label: "Chain Mail", color: "#5a6070" },
  null,
  { label: "Speed Ring", color: "#44aacc" },
  { label: "HP Flask", color: "#cc2929" },
  { label: "MP Flask", color: "#2255cc" },
  null,
  { label: "Shadow Gem", color: "#8844cc" },
  { label: "Iron Boots", color: "#6677aa" },
  null,
  { label: "Arcane Orb", color: "#aa44cc" },
  { label: "Power Rune", color: "#c9a84c" },
  null,
  null,
  { label: "Dark Cloak", color: "#445566" },
  null,
  null,
  null,
];

const TABS = ["Equipment", "Accessory", "Skill"] as const;
type Tab = (typeof TABS)[number];

export function CharacterInventory() {
  const [activeTab, setActiveTab] = useState<Tab>("Equipment");

  const panelStyle: React.CSSProperties = {
    background: "rgba(10,12,18,0.92)",
    border: "1px solid rgba(201,168,76,0.22)",
    boxShadow: "inset 0 0 40px rgba(0,0,0,0.5)",
  };

  return (
    <div
      className="w-full h-full flex gap-4 p-5 overflow-hidden"
      style={{ background: "radial-gradient(ellipse at center, #0d1018 0%, #07080d 100%)" }}
    >
      {/* ── LEFT PANEL: Character ── */}
      <div className="flex-1 rounded-lg p-6 flex flex-col" style={panelStyle}>
        {/* Header */}
        <div className="mb-5 flex items-baseline justify-between">
          <h2
            className="text-white tracking-widest uppercase"
            style={{ fontFamily: "'Cinzel', serif", fontSize: 15, fontWeight: 600 }}
          >
            Character
          </h2>
          <span className="text-[#c9a84c]/60 tracking-wider uppercase" style={{ fontSize: 10 }}>
            Lv. 42 · Artorias
          </span>
        </div>

        {/* Equipment area */}
        <div className="flex items-center justify-center gap-6 flex-1">
          {/* Left equipment column */}
          <div className="flex flex-col gap-4">
            <Slot label="Helmet" icon={<Crown size={20} />} filled />
            <Slot label="Armor" icon={<Shirt size={20} />} filled />
            <Slot label="Pants" icon={<Shield size={18} />} filled />
            <Slot label="Boots" icon={<Footprints size={18} />} filled />
          </div>

          {/* Center avatar */}
          <div className="flex flex-col items-center gap-4">
            <div
              className="rounded-full flex items-center justify-center relative"
              style={{
                width: 140,
                height: 140,
                background: "linear-gradient(145deg, #1e1530, #0e0c18)",
                border: "2px solid rgba(201,168,76,0.55)",
                boxShadow: "0 0 32px rgba(201,168,76,0.22), inset 0 0 20px rgba(0,0,0,0.7)",
              }}
            >
              {/* Glow ring */}
              <div
                className="absolute rounded-full"
                style={{
                  inset: 4,
                  border: "1px solid rgba(201,168,76,0.12)",
                  borderRadius: "50%",
                }}
              />
              <svg viewBox="0 0 140 140" className="w-full h-full">
                <circle cx="70" cy="46" r="24" fill="#3a2a4a" />
                <ellipse cx="70" cy="115" rx="36" ry="30" fill="#3a2a4a" />
                {/* Armor glint */}
                <path d="M50 80 Q70 68 90 80 L92 115 Q70 120 48 115 Z" fill="#2a2038" />
                <path d="M68 70 Q70 66 72 70 L74 82 Q70 84 66 82 Z" fill="#c9a84c" opacity="0.4" />
              </svg>
            </div>

            {/* Status bars */}
            <div className="w-48 flex flex-col gap-2">
              <Bar value={78} color="#cc2929" glow="rgba(204,41,41,0.6)" label="HP" />
              <Bar value={100} color="#2255cc" glow="rgba(34,85,204,0.6)" label="MP" />
              <Bar value={55} color="#22aa44" glow="rgba(34,170,68,0.6)" label="SP" />
            </div>

            {/* Stats */}
            <div
              className="w-48 rounded grid grid-cols-2 gap-x-4 gap-y-1 p-3"
              style={{ background: "rgba(0,0,0,0.4)", border: "1px solid rgba(255,255,255,0.05)" }}
            >
              {[
                { k: "Def", v: 48 },
                { k: "Res", v: 32 },
                { k: "Ad", v: 71 },
                { k: "Ap", v: 55 },
              ].map(({ k, v }) => (
                <div key={k} className="flex items-center justify-between">
                  <span className="text-white/40 uppercase tracking-wider" style={{ fontSize: 10 }}>{k}</span>
                  <span className="font-mono text-white/80" style={{ fontSize: 12 }}>{v}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Right equipment column */}
          <div className="flex flex-col gap-4">
            <Slot label="Ring" icon={<Circle size={14} />} filled />
            <Slot label="Amulet" icon={<Gem size={18} />} filled />
            <Slot
              label="Skill"
              icon={<Flame size={20} className="text-orange-400" />}
              filled
              gold
              size={64}
            />
          </div>
        </div>
      </div>

      {/* ── RIGHT PANEL: Inventory ── */}
      <div className="w-72 rounded-lg p-5 flex flex-col" style={panelStyle}>
        {/* Header */}
        <h2
          className="text-white tracking-widest uppercase mb-4"
          style={{ fontFamily: "'Cinzel', serif", fontSize: 15, fontWeight: 600 }}
        >
          Inventory
        </h2>

        {/* Tabs */}
        <div
          className="flex rounded overflow-hidden mb-5"
          style={{ border: "1px solid rgba(201,168,76,0.2)" }}
        >
          {TABS.map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className="flex-1 py-2 transition-all"
              style={{
                fontSize: 11,
                fontFamily: "'Cinzel', serif",
                letterSpacing: "0.05em",
                background:
                  activeTab === tab
                    ? "rgba(201,168,76,0.18)"
                    : "rgba(0,0,0,0.3)",
                color: activeTab === tab ? "#e8d8a8" : "rgba(180,165,140,0.55)",
                borderRight: tab !== "Skill" ? "1px solid rgba(201,168,76,0.15)" : "none",
              }}
            >
              {tab}
            </button>
          ))}
        </div>

        {/* 4×5 grid */}
        <div className="grid grid-cols-4 gap-2 flex-1">
          {INVENTORY_ITEMS.map((item, i) => (
            <div key={i} className="flex flex-col items-center gap-1">
              <div
                className="rounded-full flex items-center justify-center cursor-pointer relative overflow-hidden transition-all hover:scale-105"
                style={{
                  width: 48,
                  height: 48,
                  background: item
                    ? `radial-gradient(circle at 35% 35%, ${item.color}44, rgba(10,10,18,0.9))`
                    : "rgba(0,0,0,0.35)",
                  border: item
                    ? `2px solid ${item.color}55`
                    : "2px solid rgba(255,255,255,0.06)",
                  boxShadow: item ? `0 0 8px ${item.color}25` : "none",
                }}
              >
                {item && (
                  <div
                    className="w-5 h-5 rounded-full"
                    style={{ background: item.color, opacity: 0.8 }}
                  />
                )}
                {!item && <div className="w-2.5 h-2.5 rounded-full border border-white/8" />}
              </div>
              {item && (
                <span
                  className="text-white/30 text-center leading-tight"
                  style={{ fontSize: 8, maxWidth: 48 }}
                >
                  {item.label}
                </span>
              )}
            </div>
          ))}
        </div>

        {/* Weight bar */}
        <div className="mt-4 pt-4" style={{ borderTop: "1px solid rgba(255,255,255,0.06)" }}>
          <div className="flex justify-between mb-1">
            <span className="text-white/30 uppercase tracking-wider" style={{ fontSize: 9 }}>Weight</span>
            <span className="font-mono text-white/40" style={{ fontSize: 9 }}>14 / 40</span>
          </div>
          <div
            className="w-full rounded-sm overflow-hidden"
            style={{ height: 4, background: "rgba(0,0,0,0.5)" }}
          >
            <div
              className="h-full rounded-sm"
              style={{
                width: "35%",
                background: "linear-gradient(90deg, #c9a84c88, #c9a84c)",
                boxShadow: "0 0 6px rgba(201,168,76,0.6)",
              }}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
