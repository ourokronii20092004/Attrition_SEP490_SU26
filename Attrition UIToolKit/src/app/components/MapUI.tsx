import { useState } from "react";

type Room = {
  id: number; x: number; y: number; w: number; h: number;
  type: "normal" | "boss" | "checkpoint" | "start" | "secret";
  visited?: boolean; name?: string;
};

// Room grid — each unit = 28px, gap = 4px
const ROOMS: Room[] = [
  // Ember Citadel area (center-left)
  { id: 1, x: 4, y: 3, w: 3, h: 2, type: "checkpoint", visited: true, name: "Ember Gate" },
  { id: 2, x: 7, y: 3, w: 2, h: 2, type: "normal", visited: true },
  { id: 3, x: 9, y: 3, w: 2, h: 1, type: "normal", visited: true },
  { id: 4, x: 9, y: 4, w: 2, h: 2, type: "normal", visited: true },
  { id: 5, x: 7, y: 5, w: 2, h: 2, type: "normal", visited: true },
  { id: 6, x: 4, y: 5, w: 3, h: 1, type: "normal", visited: true },
  { id: 7, x: 4, y: 6, w: 2, h: 2, type: "normal", visited: true },
  { id: 8, x: 6, y: 6, w: 3, h: 1, type: "checkpoint", visited: true, name: "Ashen Gate" },
  { id: 9, x: 11, y: 2, w: 3, h: 3, type: "boss", visited: true, name: "Lord Cinder" },
  { id: 10, x: 11, y: 5, w: 2, h: 2, type: "normal", visited: true },
  { id: 11, x: 13, y: 5, w: 2, h: 3, type: "normal", visited: true },
  { id: 12, x: 11, y: 7, w: 2, h: 2, type: "checkpoint", visited: true, name: "Sunken Library" },
  // Fungal Caverns (lower left)
  { id: 13, x: 2, y: 8, w: 2, h: 2, type: "normal", visited: true },
  { id: 14, x: 4, y: 8, w: 3, h: 1, type: "normal", visited: true },
  { id: 15, x: 4, y: 9, w: 2, h: 2, type: "checkpoint", visited: true, name: "Fungal Caverns" },
  { id: 16, x: 6, y: 9, w: 2, h: 3, type: "normal", visited: true },
  { id: 17, x: 2, y: 10, w: 2, h: 2, type: "secret", visited: false },
  { id: 18, x: 8, y: 9, w: 3, h: 2, type: "boss", visited: true, name: "Mycelith Queen" },
  // Upper path (unvisited)
  { id: 19, x: 7, y: 1, w: 2, h: 2, type: "normal", visited: false },
  { id: 20, x: 9, y: 1, w: 2, h: 1, type: "normal", visited: false },
  { id: 21, x: 11, y: 0, w: 4, h: 2, type: "checkpoint", visited: false, name: "Iron Gate Keep" },
  // Right side
  { id: 22, x: 15, y: 4, w: 2, h: 2, type: "normal", visited: true },
  { id: 23, x: 15, y: 6, w: 3, h: 2, type: "normal", visited: false },
  // Player current room
  { id: 24, x: 9, y: 5, w: 2, h: 2, type: "start", visited: true, name: "Current Position" },
];

const UNIT = 28;
const GAP = 3;
const OFFSET_X = 20;
const OFFSET_Y = 14;

function roomRect(r: Room) {
  return {
    x: OFFSET_X + r.x * (UNIT + GAP),
    y: OFFSET_Y + r.y * (UNIT + GAP),
    w: r.w * UNIT + (r.w - 1) * GAP,
    h: r.h * UNIT + (r.h - 1) * GAP,
  };
}

type RoomFill = { fill: string; stroke: string; glow?: string };
function roomStyle(r: Room): RoomFill {
  if (!r.visited) return { fill: "rgba(30,34,45,0.6)", stroke: "rgba(80,90,110,0.35)" };
  switch (r.type) {
    case "boss": return { fill: "rgba(120,20,20,0.55)", stroke: "#aa2222", glow: "#cc222233" };
    case "checkpoint": return { fill: "rgba(20,60,40,0.55)", stroke: "#22aa55", glow: "#22aa5533" };
    case "start": return { fill: "rgba(160,120,20,0.4)", stroke: "#c9a84c", glow: "#c9a84c55" };
    case "secret": return { fill: "rgba(60,20,80,0.4)", stroke: "#7744aa66", glow: "#7744aa22" };
    default: return { fill: "rgba(25,30,42,0.8)", stroke: "rgba(80,90,120,0.5)" };
  }
}

const LEGEND = [
  { color: "#c9a84c", glow: "#c9a84c", label: "Current Position", symbol: "◉" },
  { color: "#22aa55", glow: "#22aa55", label: "Checkpoint", symbol: "✦" },
  { color: "#cc2222", glow: "#cc2222", label: "Boss Room", symbol: "☠" },
  { color: "#7744aa", glow: "#7744aa", label: "Secret Area", symbol: "◈" },
  { color: "#3a4060", glow: "none", label: "Undiscovered", symbol: "▪" },
];

export function MapUI() {
  const [hoveredRoom, setHoveredRoom] = useState<number | null>(null);

  const svgW = 580;
  const svgH = 420;

  return (
    <div
      className="w-full h-full flex flex-col"
      style={{ background: "#06070c" }}
    >
      {/* Header */}
      <div
        className="flex items-center justify-between px-5 py-3 flex-shrink-0"
        style={{ borderBottom: "1px solid rgba(201,168,76,0.12)" }}
      >
        <div className="flex flex-col gap-0.5">
          <h1
            className="text-white uppercase tracking-[0.3em]"
            style={{ fontFamily: "'Cinzel', serif", fontSize: 14, fontWeight: 700 }}
          >
            Ember Citadel
          </h1>
          <span className="text-white/30 uppercase tracking-widest" style={{ fontSize: 9 }}>
            Attrition: A Spark in the Ashes · World Map
          </span>
        </div>
        <div className="flex items-center gap-3">
          <div className="w-2 h-2 rounded-full bg-[#c9a84c]" style={{ boxShadow: "0 0 6px #c9a84c" }} />
          <span className="text-white/40 uppercase tracking-widest" style={{ fontSize: 9 }}>
            Ember Citadel — Inner Sanctum
          </span>
        </div>
      </div>

      {/* Map area */}
      <div className="flex-1 relative overflow-hidden flex items-center justify-center">
        {/* Background grid */}
        <div className="absolute inset-0">
          <svg className="w-full h-full opacity-[0.04]">
            <defs>
              <pattern id="mapgrid" width="32" height="32" patternUnits="userSpaceOnUse">
                <path d="M 32 0 L 0 0 0 32" fill="none" stroke="rgba(80,120,180,1)" strokeWidth="0.5" />
              </pattern>
            </defs>
            <rect width="100%" height="100%" fill="url(#mapgrid)" />
          </svg>
        </div>

        {/* Rooms SVG */}
        <svg
          viewBox={`0 0 ${svgW} ${svgH}`}
          className="relative z-10"
          style={{ width: "100%", height: "100%", maxWidth: 700, maxHeight: 520 }}
        >
          <defs>
            {/* Glow filters */}
            <filter id="glow-gold">
              <feGaussianBlur stdDeviation="3" result="blur" />
              <feComposite in="SourceGraphic" in2="blur" operator="over" />
            </filter>
            <filter id="glow-red">
              <feGaussianBlur stdDeviation="2.5" result="blur" />
              <feComposite in="SourceGraphic" in2="blur" operator="over" />
            </filter>
            <filter id="glow-green">
              <feGaussianBlur stdDeviation="2" result="blur" />
              <feComposite in="SourceGraphic" in2="blur" operator="over" />
            </filter>
          </defs>

          {/* Rooms */}
          {ROOMS.map((r) => {
            const { x, y, w, h } = roomRect(r);
            const style = roomStyle(r);
            const isHovered = hoveredRoom === r.id;
            return (
              <g key={r.id}
                onMouseEnter={() => setHoveredRoom(r.id)}
                onMouseLeave={() => setHoveredRoom(null)}
                style={{ cursor: "pointer" }}
              >
                {/* Glow behind */}
                {style.glow && (
                  <rect x={x - 2} y={y - 2} width={w + 4} height={h + 4} rx={2}
                    fill={style.glow} filter="url(#glow-green)" opacity={isHovered ? 0.7 : 0.4} />
                )}
                {/* Room rect */}
                <rect x={x} y={y} width={w} height={h} rx={2}
                  fill={style.fill}
                  stroke={style.stroke}
                  strokeWidth={isHovered ? 1.5 : 1}
                  opacity={r.visited ? 1 : 0.6}
                />

                {/* Room type icons */}
                {r.type === "boss" && r.visited && (
                  <text x={x + w / 2} y={y + h / 2 + 4} textAnchor="middle"
                    fill="#cc3333" fontSize="11" fontFamily="serif" opacity="0.85">☠</text>
                )}
                {r.type === "checkpoint" && r.visited && (
                  <g>
                    <circle cx={x + w / 2} cy={y + h / 2} r={3} fill="#22aa55"
                      style={{ filter: "drop-shadow(0 0 3px #22aa55)" }} />
                  </g>
                )}
                {r.type === "start" && (
                  <g>
                    <circle cx={x + w / 2} cy={y + h / 2} r={4} fill="#c9a84c"
                      style={{ filter: "drop-shadow(0 0 5px #c9a84c)" }} />
                    <circle cx={x + w / 2} cy={y + h / 2} r={7} fill="none"
                      stroke="#c9a84c" strokeWidth="1" opacity="0.5" />
                  </g>
                )}
                {r.type === "secret" && !r.visited && (
                  <text x={x + w / 2} y={y + h / 2 + 3} textAnchor="middle"
                    fill="#7744aa" fontSize="8" opacity="0.5">?</text>
                )}

                {/* Hover name tooltip */}
                {isHovered && r.name && (
                  <g>
                    <rect x={x + w / 2 - 44} y={y - 20} width={88} height={16} rx={3}
                      fill="rgba(5,6,10,0.92)" stroke="rgba(201,168,76,0.35)" strokeWidth="0.75" />
                    <text x={x + w / 2} y={y - 8} textAnchor="middle"
                      fill="#e8d8a8" fontSize="8" fontFamily="'Cinzel', serif" letterSpacing="1">
                      {r.name}
                    </text>
                  </g>
                )}
              </g>
            );
          })}

          {/* Connecting corridors — thin lines between adjacent rooms */}
          <g stroke="rgba(80,100,140,0.35)" strokeWidth="2">
            {/* Horizontal connectors */}
            {[
              [1,2],[2,3],[5,6],[6,7],[8,9],[10,11],[13,14],[14,15],[15,16],[16,18],
            ].map(([a,b],i) => {
              const ra = ROOMS.find(r => r.id === a)!;
              const rb = ROOMS.find(r => r.id === b)!;
              if (!ra || !rb) return null;
              const ra_ = roomRect(ra);
              const rb_ = roomRect(rb);
              const y1 = ra_.y + ra_.h / 2;
              const y2 = rb_.y + rb_.h / 2;
              return <line key={i} x1={ra_.x + ra_.w} y1={y1} x2={rb_.x} y2={y2} />;
            })}
          </g>
        </svg>

        {/* Legend — bottom right */}
        <div
          className="absolute bottom-4 right-4 rounded p-3 flex flex-col gap-2"
          style={{
            background: "rgba(5,6,12,0.88)",
            border: "1px solid rgba(201,168,76,0.18)",
            backdropFilter: "blur(6px)",
          }}
        >
          <div className="uppercase tracking-widest text-white/30 mb-1" style={{ fontSize: 8, fontFamily: "'Cinzel', serif" }}>
            Legend
          </div>
          {LEGEND.map((l) => (
            <div key={l.label} className="flex items-center gap-2">
              <span style={{ color: l.color, fontSize: 11, lineHeight: 1, textShadow: l.glow !== "none" ? `0 0 6px ${l.glow}` : "none" }}>
                {l.symbol}
              </span>
              <span className="text-white/45 uppercase tracking-wider" style={{ fontSize: 9 }}>{l.label}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
