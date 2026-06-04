import { useState } from "react";
import { motion } from "motion/react";
import { Eye, EyeOff, AlertCircle } from "lucide-react";

type Props = { onNavigate: (s: string) => void };

function GoogleIcon() {
  return (
    <svg viewBox="0 0 24 24" className="w-4 h-4" aria-hidden>
      <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4" />
      <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853" />
      <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l3.66-2.84z" fill="#FBBC05" />
      <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335" />
    </svg>
  );
}

export function LoginUI({ onNavigate }: Props) {
  const [showPass, setShowPass] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [hasError] = useState(true);

  const inputBase: React.CSSProperties = {
    background: "rgba(255,255,255,0.04)",
    border: "1px solid rgba(255,255,255,0.1)",
    borderRadius: 6,
    padding: "10px 14px",
    color: "#e8e4d8",
    fontSize: 13,
    width: "100%",
    outline: "none",
    fontFamily: "inherit",
    transition: "border-color 0.2s",
  };

  return (
    <div
      className="w-full h-full flex items-center justify-center"
      style={{ background: "radial-gradient(ellipse at center, #0d1018 0%, #07080d 100%)" }}
    >
      {/* Subtle bg pattern */}
      <div className="absolute inset-0 pointer-events-none" style={{ opacity: 0.03 }}>
        <svg className="w-full h-full">
          <defs>
            <pattern id="lp" width="40" height="40" patternUnits="userSpaceOnUse">
              <path d="M 40 0 L 0 0 0 40" fill="none" stroke="#fff" strokeWidth="0.5" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#lp)" />
        </svg>
      </div>

      <motion.div
        initial={{ opacity: 0, y: 20, scale: 0.97 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.35, ease: "easeOut" }}
        className="relative w-full max-w-sm rounded-xl overflow-hidden"
        style={{
          background: "rgba(10,12,20,0.88)",
          border: "1px solid rgba(201,168,76,0.22)",
          boxShadow: "0 0 80px rgba(0,0,0,0.8), 0 0 40px rgba(201,168,76,0.06), inset 0 0 40px rgba(0,0,0,0.4)",
          backdropFilter: "blur(20px)",
        }}
      >
        {/* Top accent line */}
        <div
          className="h-px w-full"
          style={{ background: "linear-gradient(90deg, transparent, rgba(201,168,76,0.6), transparent)" }}
        />

        <div className="p-8 flex flex-col gap-5">
          {/* Sigil */}
          <div className="flex flex-col items-center gap-2">
            <div
              className="w-10 h-10 rounded-full flex items-center justify-center"
              style={{ background: "rgba(201,168,76,0.1)", border: "1px solid rgba(201,168,76,0.3)" }}
            >
              <svg viewBox="0 0 20 20" className="w-5 h-5">
                <polygon points="10,2 12.9,7.6 19,8.5 14.5,12.9 15.6,19 10,16 4.4,19 5.5,12.9 1,8.5 7.1,7.6" fill="none" stroke="#c9a84c" strokeWidth="1.2" strokeLinejoin="round" />
              </svg>
            </div>
            <h2
              className="text-white tracking-widest uppercase"
              style={{ fontFamily: "'Cinzel', serif", fontSize: 18, fontWeight: 600 }}
            >
              Sign In
            </h2>
            <p className="text-white/30 text-center" style={{ fontSize: 11 }}>
              Enter the realm of Attrition
            </p>
          </div>

          {/* Error */}
          {hasError && (
            <div
              className="flex items-center gap-2 px-3 py-2 rounded"
              style={{ background: "rgba(180,30,30,0.12)", border: "1px solid rgba(180,30,30,0.3)" }}
            >
              <AlertCircle size={13} className="text-red-400 flex-shrink-0" />
              <span className="text-red-400" style={{ fontSize: 11 }}>Incorrect email or password.</span>
            </div>
          )}

          {/* Fields */}
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1.5">
              <label className="text-white/40 uppercase tracking-widest" style={{ fontSize: 9, fontFamily: "'Cinzel', serif" }}>
                Username / Email
              </label>
              <input
                type="text"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="your@email.com"
                style={{
                  ...inputBase,
                  borderColor: hasError ? "rgba(180,30,30,0.4)" : "rgba(255,255,255,0.1)",
                }}
                onFocus={(e) => (e.currentTarget.style.borderColor = "rgba(201,168,76,0.45)")}
                onBlur={(e) => (e.currentTarget.style.borderColor = hasError ? "rgba(180,30,30,0.4)" : "rgba(255,255,255,0.1)")}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-white/40 uppercase tracking-widest" style={{ fontSize: 9, fontFamily: "'Cinzel', serif" }}>
                Password
              </label>
              <div className="relative">
                <input
                  type={showPass ? "text" : "password"}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  style={{
                    ...inputBase,
                    paddingRight: 40,
                    borderColor: hasError ? "rgba(180,30,30,0.4)" : "rgba(255,255,255,0.1)",
                  }}
                  onFocus={(e) => (e.currentTarget.style.borderColor = "rgba(201,168,76,0.45)")}
                  onBlur={(e) => (e.currentTarget.style.borderColor = hasError ? "rgba(180,30,30,0.4)" : "rgba(255,255,255,0.1)")}
                />
                <button
                  type="button"
                  onClick={() => setShowPass((v) => !v)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-white/30 hover:text-white/60 transition-colors"
                  style={{ background: "none", border: "none", cursor: "pointer" }}
                >
                  {showPass ? <EyeOff size={14} /> : <Eye size={14} />}
                </button>
              </div>
            </div>
          </div>

          {/* Login button */}
          <motion.button
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
            onClick={() => onNavigate("save")}
            className="w-full py-3 rounded uppercase tracking-[0.2em]"
            style={{
              background: "linear-gradient(135deg, rgba(201,168,76,0.25), rgba(160,120,40,0.4))",
              border: "1px solid rgba(201,168,76,0.55)",
              boxShadow: "0 0 20px rgba(201,168,76,0.18)",
              fontFamily: "'Cinzel', serif",
              fontSize: 12,
              fontWeight: 600,
              color: "#e8d8a8",
              cursor: "pointer",
            }}
          >
            Login
          </motion.button>

          {/* Divider */}
          <div className="flex items-center gap-3">
            <div className="flex-1 h-px" style={{ background: "rgba(255,255,255,0.07)" }} />
            <span className="text-white/25 uppercase tracking-widest" style={{ fontSize: 9 }}>or</span>
            <div className="flex-1 h-px" style={{ background: "rgba(255,255,255,0.07)" }} />
          </div>

          {/* Google sign-in */}
          <motion.button
            whileHover={{ scale: 1.02, borderColor: "rgba(255,255,255,0.2)" }}
            whileTap={{ scale: 0.98 }}
            className="w-full py-2.5 rounded flex items-center justify-center gap-2.5"
            style={{
              background: "rgba(255,255,255,0.04)",
              border: "1px solid rgba(255,255,255,0.1)",
              cursor: "pointer",
              transition: "all 0.2s",
            }}
          >
            <GoogleIcon />
            <span className="text-white/55" style={{ fontSize: 12 }}>Sign in with Google</span>
          </motion.button>

          <p className="text-center text-white/20" style={{ fontSize: 10 }}>
            No account?{" "}
            <span className="text-[#c9a84c]/60 cursor-pointer hover:text-[#c9a84c]" style={{ transition: "color 0.2s" }}>
              Create one
            </span>
          </p>
        </div>

        {/* Bottom accent */}
        <div
          className="h-px w-full"
          style={{ background: "linear-gradient(90deg, transparent, rgba(201,168,76,0.2), transparent)" }}
        />
      </motion.div>
    </div>
  );
}
