import { useState } from "react";
import { motion } from "motion/react";

const BG_URL =
  "https://images.unsplash.com/photo-1709586733081-af013389dea2?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&w=1920&q=80";

const MENU_ITEMS = [
  { label: "Solo Mode", action: "save" },
  { label: "Co-op Mode", action: "matchmaking" },
  { label: "Settings", action: "settings" },
  { label: "Quit", action: null },
];

type Props = { onNavigate: (screen: string) => void };

export function MainMenu({ onNavigate }: Props) {
  const [hovered, setHovered] = useState<number | null>(null);

  return (
    <div className="relative w-full h-full overflow-hidden flex">
      {/* Background image */}
      <img
        src={BG_URL}
        alt="Dark foggy castle ruins"
        className="absolute inset-0 w-full h-full object-cover object-center"
        style={{ filter: "blur(4px) brightness(0.28) saturate(0.5)" }}
      />

      {/* Overlays */}
      <div
        className="absolute inset-0"
        style={{
          background:
            "linear-gradient(to right, rgba(3,2,8,0.98) 0%, rgba(5,4,12,0.8) 45%, rgba(5,4,12,0.1) 100%)",
        }}
      />
      <div
        className="absolute inset-0"
        style={{ background: "linear-gradient(to top, rgba(3,2,8,0.95) 0%, transparent 60%)" }}
      />

      {/* Fog strips */}
      {[18, 35, 52].map((pct, i) => (
        <div
          key={i}
          className="absolute left-0 right-0"
          style={{
            top: `${pct}%`,
            height: 100,
            background: `rgba(200,210,230,${0.025 + i * 0.012})`,
            filter: "blur(28px)",
          }}
        />
      ))}

      {/* Center-aligned content */}
      <div className="relative z-10 flex flex-col items-center justify-center h-full w-full gap-0">
        {/* Title block */}
        <div className="flex flex-col items-center mb-10">
          {/* Ornamental top */}
          <div className="flex items-center gap-3 mb-4">
            <div className="h-px w-20 bg-gradient-to-r from-transparent to-[#c9a84c]/50" />
            <div className="w-1.5 h-1.5 rotate-45 bg-[#c9a84c]/60" />
            <div className="h-px w-20 bg-gradient-to-l from-transparent to-[#c9a84c]/50" />
          </div>

          <h1
            className="text-white text-center leading-[1.05]"
            style={{
              fontFamily: "'Cinzel', serif",
              fontSize: 44,
              fontWeight: 900,
              letterSpacing: "0.12em",
              textShadow: "0 0 50px rgba(201,168,76,0.4), 0 2px 10px rgba(0,0,0,0.9)",
            }}
          >
            ATTRITION
          </h1>
          <div
            className="text-[#c9a84c]/60 text-center mt-1"
            style={{
              fontFamily: "'Cinzel', serif",
              fontSize: 13,
              letterSpacing: "0.22em",
              textShadow: "0 0 20px rgba(201,168,76,0.3)",
            }}
          >
            A Spark in the Ashes
          </div>

          {/* Ornamental bottom */}
          <div className="flex items-center gap-3 mt-4">
            <div className="h-px w-14 bg-gradient-to-r from-transparent to-[#c9a84c]/30" />
            <div className="w-px h-3 bg-[#c9a84c]/30" />
            <div className="h-px w-14 bg-gradient-to-l from-transparent to-[#c9a84c]/30" />
          </div>
        </div>

        {/* Menu buttons — vertical center list */}
        <nav className="flex flex-col items-center gap-1 w-64">
          {MENU_ITEMS.map((item, i) => {
            const isHovered = hovered === i;
            return (
              <button
                key={item.label}
                onMouseEnter={() => setHovered(i)}
                onMouseLeave={() => setHovered(null)}
                onClick={() => item.action && onNavigate(item.action)}
                className="group relative w-full text-center py-3 outline-none"
                style={{ background: "none", border: "none" }}
              >
                {/* Hover bg */}
                <motion.div
                  className="absolute inset-0 rounded"
                  animate={{ opacity: isHovered ? 1 : 0 }}
                  transition={{ duration: 0.15 }}
                  style={{ background: "rgba(255,255,255,0.03)" }}
                />

                <span
                  className="relative inline-block"
                  style={{
                    fontFamily: "'Cinzel', serif",
                    fontSize: 15,
                    fontWeight: isHovered ? 600 : 400,
                    letterSpacing: "0.18em",
                    color: isHovered ? "#ffffff" : "rgba(200,196,188,0.65)",
                    textShadow: isHovered ? "0 0 20px rgba(255,255,255,0.3), 0 0 8px rgba(200,220,255,0.2)" : "none",
                    transition: "color 0.2s, text-shadow 0.2s",
                  }}
                >
                  {item.label}

                  {/* Silver glowing underline */}
                  <motion.span
                    className="absolute -bottom-0.5 left-0 h-px block"
                    animate={{
                      width: isHovered ? "100%" : "0%",
                      opacity: isHovered ? 1 : 0,
                    }}
                    transition={{ duration: 0.22 }}
                    style={{
                      background: "linear-gradient(90deg, transparent, rgba(220,230,255,0.8), transparent)",
                      boxShadow: "0 0 8px rgba(180,200,255,0.7)",
                    }}
                  />
                </span>

                {/* Particle sparkles on hover */}
                {isHovered && (
                  <span className="absolute right-0 top-1/2 -translate-y-1/2 flex gap-1 pointer-events-none">
                    {[0, 1, 2].map((j) => (
                      <motion.span
                        key={j}
                        className="block w-0.5 h-0.5 rounded-full bg-white"
                        initial={{ opacity: 0, x: 0 }}
                        animate={{ opacity: [0, 0.8, 0], x: 6 + j * 5 }}
                        transition={{ duration: 0.7, delay: j * 0.08, repeat: Infinity }}
                      />
                    ))}
                  </span>
                )}
              </button>
            );
          })}
        </nav>

        {/* Footer */}
        <div className="absolute bottom-5 text-center text-white/15 uppercase tracking-widest" style={{ fontSize: 9 }}>
          v1.4.2 &nbsp;·&nbsp; © 2026 Attrition Studios
        </div>
      </div>

      {/* Right silhouette art */}
      <div className="absolute right-0 bottom-0 w-2/5 h-full pointer-events-none opacity-40">
        <svg viewBox="0 0 400 600" className="w-full h-full" preserveAspectRatio="xMaxYMax meet">
          <g fill="#05040c">
            <rect x="50" y="200" width="60" height="400" />
            <rect x="40" y="180" width="80" height="28" />
            <rect x="40" y="175" width="10" height="14" /><rect x="60" y="175" width="10" height="14" />
            <rect x="80" y="175" width="10" height="14" /><rect x="100" y="175" width="10" height="14" />
            <rect x="160" y="280" width="50" height="320" />
            <rect x="150" y="258" width="70" height="30" />
            <rect x="150" y="252" width="12" height="14" /><rect x="168" y="252" width="12" height="14" />
            <rect x="186" y="252" width="12" height="14" /><rect x="204" y="252" width="12" height="14" />
            <rect x="250" y="310" width="140" height="290" />
            <rect x="236" y="284" width="168" height="36" />
            <rect x="236" y="276" width="16" height="18" /><rect x="262" y="276" width="16" height="18" />
            <rect x="288" y="276" width="16" height="18" /><rect x="314" y="276" width="16" height="18" />
            <rect x="340" y="276" width="16" height="18" /><rect x="366" y="276" width="16" height="18" />
          </g>
          <rect x="0" y="480" width="400" height="120" fill="url(#mf2)" />
          <defs>
            <linearGradient id="mf2" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="rgba(5,4,12,0)" />
              <stop offset="100%" stopColor="rgba(3,2,8,0.9)" />
            </linearGradient>
          </defs>
        </svg>
      </div>
    </div>
  );
}
