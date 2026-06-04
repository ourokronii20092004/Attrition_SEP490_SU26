import { motion } from "motion/react";
import { RotateCcw } from "lucide-react";

type Props = { onNavigate: (s: string) => void };

export function GameOver({ onNavigate }: Props) {
  return (
    <div className="relative w-full h-full flex flex-col items-center justify-center overflow-hidden">
      {/* Dark base */}
      <div className="absolute inset-0" style={{ background: "#03010a" }} />

      {/* Blood vignette */}
      <div
        className="absolute inset-0"
        style={{
          background:
            "radial-gradient(ellipse at center, rgba(80,0,0,0.0) 10%, rgba(120,0,0,0.35) 50%, rgba(60,0,0,0.75) 80%, rgba(10,0,0,0.95) 100%)",
        }}
      />

      {/* Blurred atmospheric layer */}
      <div
        className="absolute inset-0 opacity-20"
        style={{
          background:
            "repeating-linear-gradient(0deg, transparent, transparent 3px, rgba(180,20,20,0.04) 3px, rgba(180,20,20,0.04) 4px)",
        }}
      />

      {/* Top blood drip effect */}
      <div
        className="absolute top-0 left-0 right-0 h-1"
        style={{
          background: "linear-gradient(90deg, transparent 5%, #8b0000 20%, #cc0000 50%, #8b0000 80%, transparent 95%)",
          boxShadow: "0 0 30px 4px rgba(180,0,0,0.4)",
        }}
      />

      {/* Content */}
      <div className="relative z-10 flex flex-col items-center gap-8">
        {/* Skull sigil */}
        <motion.div
          initial={{ scale: 0.5, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          transition={{ duration: 0.6, ease: [0.16, 1, 0.3, 1] }}
        >
          <svg viewBox="0 0 80 80" className="w-16 h-16" style={{ filter: "drop-shadow(0 0 12px rgba(180,0,0,0.7))" }}>
            <circle cx="40" cy="36" r="26" fill="none" stroke="rgba(180,0,0,0.6)" strokeWidth="1.5" />
            <circle cx="40" cy="36" r="20" fill="rgba(60,0,0,0.8)" stroke="rgba(150,0,0,0.4)" strokeWidth="1" />
            <text x="40" y="48" textAnchor="middle" fontSize="22" fill="rgba(180,0,0,0.9)">☠</text>
            {/* Outer ring dashes */}
            <circle cx="40" cy="36" r="30" fill="none" stroke="rgba(180,0,0,0.25)" strokeWidth="0.75" strokeDasharray="3 4" />
          </svg>
        </motion.div>

        {/* TEAM WIPED OUT — MSG-G-29 */}
        <div className="flex flex-col items-center gap-3">
          <motion.div
            className="uppercase tracking-widest text-white/20"
            style={{ fontSize: 10, fontFamily: "'Cinzel', serif" }}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.3 }}
          >
            MSG-G-29
          </motion.div>

          <motion.h1
            initial={{ opacity: 0, y: 20, scale: 0.9 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            transition={{ duration: 0.7, delay: 0.15, ease: [0.16, 1, 0.3, 1] }}
            style={{
              fontFamily: "'Cinzel', serif",
              fontSize: 52,
              fontWeight: 900,
              letterSpacing: "0.08em",
              lineHeight: 1,
              color: "#cc0000",
              textShadow: "0 0 40px rgba(200,0,0,0.9), 0 0 80px rgba(150,0,0,0.5), 0 2px 4px rgba(0,0,0,0.8)",
            }}
          >
            TEAM
            <br />
            WIPED OUT
          </motion.h1>
        </div>

        {/* Subtitle */}
        <motion.p
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 0.7 }}
          className="text-red-800/60 uppercase tracking-[0.3em]"
          style={{ fontSize: 11, fontFamily: "'Cinzel', serif" }}
        >
          Both Flames have been extinguished
        </motion.p>

        {/* Stats */}
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.9 }}
          className="flex gap-10"
        >
          {[
            { label: "Survived", value: "4:22" },
            { label: "Enemies Slain", value: "17" },
            { label: "Deaths", value: "2" },
          ].map(({ label, value }) => (
            <div key={label} className="flex flex-col items-center gap-1">
              <span
                className="font-mono text-red-900/70"
                style={{ fontSize: 20, textShadow: "0 0 10px rgba(180,0,0,0.4)" }}
              >
                {value}
              </span>
              <span className="uppercase tracking-widest text-white/20" style={{ fontSize: 9 }}>
                {label}
              </span>
            </div>
          ))}
        </motion.div>

        {/* Divider */}
        <div className="h-px w-40 bg-gradient-to-r from-transparent via-red-900/40 to-transparent" />

        {/* CTA */}
        <motion.button
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 1.0 }}
          whileHover={{ scale: 1.04 }}
          whileTap={{ scale: 0.96 }}
          onClick={() => onNavigate("hud")}
          className="flex items-center gap-3 px-10 py-3.5 rounded"
          style={{
            background: "rgba(255,255,255,0.06)",
            border: "1px solid rgba(255,255,255,0.15)",
            cursor: "pointer",
            transition: "all 0.2s",
          }}
          onMouseEnter={(e) => {
            e.currentTarget.style.background = "rgba(255,255,255,0.1)";
            e.currentTarget.style.borderColor = "rgba(255,255,255,0.3)";
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.background = "rgba(255,255,255,0.06)";
            e.currentTarget.style.borderColor = "rgba(255,255,255,0.15)";
          }}
        >
          <RotateCcw size={14} className="text-white/60" />
          <span
            className="text-white uppercase tracking-[0.2em]"
            style={{ fontFamily: "'Cinzel', serif", fontSize: 12, fontWeight: 600 }}
          >
            Return to Last Checkpoint
          </span>
        </motion.button>

        <motion.button
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 1.2 }}
          onClick={() => onNavigate("menu")}
          className="uppercase tracking-widest text-red-900/50 hover:text-red-800/70 transition-colors"
          style={{ fontSize: 10, fontFamily: "'Cinzel', serif", background: "none", border: "none", cursor: "pointer" }}
        >
          Quit to Main Menu
        </motion.button>
      </div>
    </div>
  );
}
