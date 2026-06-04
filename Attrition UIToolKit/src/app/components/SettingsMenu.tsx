import { useState } from "react";
import { Gamepad2, Monitor, Volume2, Keyboard, RotateCcw, Edit3, ChevronRight } from "lucide-react";

type SettingsTab = "Gameplay" | "Graphics" | "Audio" | "Controls";

const TABS: Array<{ id: SettingsTab; icon: React.ReactNode }> = [
  { id: "Gameplay", icon: <Gamepad2 size={15} /> },
  { id: "Graphics", icon: <Monitor size={15} /> },
  { id: "Audio", icon: <Volume2 size={15} /> },
  { id: "Controls", icon: <Keyboard size={15} /> },
];

const KEYBINDINGS: Array<{ action: string; key: string }> = [
  { action: "Jump", key: "Space" },
  { action: "Dash", key: "Shift" },
  { action: "Attack", key: "J" },
  { action: "Skill", key: "K" },
  { action: "Heal", key: "Q" },
  { action: "Mana Flask", key: "E" },
  { action: "Block", key: "L" },
  { action: "Map", key: "M" },
  { action: "Inventory", key: "I" },
  { action: "Interact", key: "F" },
];

const GAMEPLAY_SETTINGS = [
  { label: "Auto-Lock Target", type: "toggle", value: true },
  { label: "Show Damage Numbers", type: "toggle", value: true },
  { label: "Camera Shake", type: "toggle", value: false },
  { label: "Difficulty", type: "select", value: "Ashes of Despair" },
];

const GRAPHICS_SETTINGS = [
  { label: "Resolution", type: "select", value: "1920 × 1080" },
  { label: "Fullscreen Mode", type: "select", value: "Borderless Window" },
  { label: "VSync", type: "toggle", value: true },
  { label: "Frame Limit", type: "select", value: "144 FPS" },
  { label: "Shadow Quality", type: "select", value: "Ultra" },
  { label: "Post Processing", type: "toggle", value: true },
];

const AUDIO_SETTINGS = [
  { label: "Master Volume", type: "slider", value: 80 },
  { label: "Music Volume", type: "slider", value: 65 },
  { label: "SFX Volume", type: "slider", value: 100 },
  { label: "Ambient Volume", type: "slider", value: 70 },
  { label: "Voice Volume", type: "slider", value: 90 },
];

function Toggle({ value, onChange }: { value: boolean; onChange: () => void }) {
  return (
    <button
      onClick={onChange}
      className="relative rounded-full transition-all"
      style={{
        width: 40,
        height: 22,
        background: value ? "rgba(201,168,76,0.6)" : "rgba(60,60,80,0.8)",
        border: `1px solid ${value ? "rgba(201,168,76,0.8)" : "rgba(255,255,255,0.08)"}`,
        boxShadow: value ? "0 0 8px rgba(201,168,76,0.3)" : "none",
        transition: "all 0.2s",
        cursor: "pointer",
      }}
    >
      <div
        className="absolute rounded-full bg-white transition-all"
        style={{
          top: 2, width: 16, height: 16,
          left: value ? 20 : 2,
          transition: "left 0.2s",
          boxShadow: "0 1px 3px rgba(0,0,0,0.4)",
        }}
      />
    </button>
  );
}

function ControlsContent() {
  const [rebinding, setRebinding] = useState<string | null>(null);
  const [bindings, setBindings] = useState(
    Object.fromEntries(KEYBINDINGS.map((b) => [b.action, b.key]))
  );

  return (
    <div className="flex flex-col gap-4 flex-1">
      {/* Bindings list */}
      <div className="flex-1 flex flex-col gap-1 overflow-y-auto" style={{ scrollbarWidth: "none" }}>
        {KEYBINDINGS.map(({ action }) => (
          <div
            key={action}
            className="flex items-center justify-between py-2.5 px-4 rounded transition-all hover:bg-white/[0.03]"
            style={{ borderBottom: "1px solid rgba(255,255,255,0.04)" }}
          >
            <span className="text-white/70 uppercase tracking-wider" style={{ fontSize: 12 }}>
              {action}
            </span>
            <button
              onClick={() =>
                setRebinding((prev) => (prev === action ? null : action))
              }
              className="flex items-center gap-2 rounded px-3 py-1 transition-all"
              style={{
                background:
                  rebinding === action
                    ? "rgba(201,168,76,0.2)"
                    : "rgba(0,0,0,0.45)",
                border: `1px solid ${rebinding === action ? "rgba(201,168,76,0.5)" : "rgba(255,255,255,0.08)"}`,
                cursor: "pointer",
              }}
            >
              <span
                className="font-mono text-white/80 uppercase tracking-widest"
                style={{
                  fontSize: 11,
                  color: rebinding === action ? "#c9a84c" : "rgba(255,255,255,0.75)",
                }}
              >
                {rebinding === action ? "Press key…" : bindings[action]}
              </span>
            </button>
          </div>
        ))}
      </div>

      {/* Action buttons */}
      <div className="flex gap-3 pt-2" style={{ borderTop: "1px solid rgba(255,255,255,0.06)" }}>
        <button
          className="flex items-center gap-2 px-5 py-2.5 rounded transition-all hover:bg-white/5"
          style={{
            background: "rgba(0,0,0,0.4)",
            border: "1px solid rgba(255,255,255,0.1)",
            cursor: "pointer",
          }}
        >
          <Edit3 size={13} className="text-white/50" />
          <span className="text-white/60 uppercase tracking-wider" style={{ fontSize: 11, fontFamily: "'Cinzel', serif" }}>
            Rebind Keys
          </span>
        </button>
        <button
          onClick={() =>
            setBindings(Object.fromEntries(KEYBINDINGS.map((b) => [b.action, b.key])))
          }
          className="flex items-center gap-2 px-5 py-2.5 rounded transition-all hover:bg-white/5"
          style={{
            background: "rgba(0,0,0,0.4)",
            border: "1px solid rgba(255,255,255,0.1)",
            cursor: "pointer",
          }}
        >
          <RotateCcw size={13} className="text-white/50" />
          <span className="text-white/60 uppercase tracking-wider" style={{ fontSize: 11, fontFamily: "'Cinzel', serif" }}>
            Reset to Default
          </span>
        </button>
      </div>
    </div>
  );
}

function GenericSettings({ items }: { items: typeof GAMEPLAY_SETTINGS }) {
  const [vals, setVals] = useState(items.map((i) => i.value));
  return (
    <div className="flex-1 flex flex-col gap-3 overflow-y-auto" style={{ scrollbarWidth: "none" }}>
      {items.map((item, i) => (
        <div
          key={item.label}
          className="flex items-center justify-between py-3 px-4 rounded"
          style={{ borderBottom: "1px solid rgba(255,255,255,0.04)" }}
        >
          <span className="text-white/70 uppercase tracking-wider" style={{ fontSize: 12 }}>
            {item.label}
          </span>
          {item.type === "toggle" && (
            <Toggle
              value={vals[i] as boolean}
              onChange={() =>
                setVals((v) => v.map((x, j) => (j === i ? !x : x)))
              }
            />
          )}
          {item.type === "select" && (
            <div
              className="flex items-center gap-2 px-3 py-1 rounded"
              style={{ background: "rgba(0,0,0,0.45)", border: "1px solid rgba(255,255,255,0.08)" }}
            >
              <span className="text-white/60 font-mono" style={{ fontSize: 11 }}>
                {vals[i] as string}
              </span>
              <ChevronRight size={11} className="text-white/30" />
            </div>
          )}
          {item.type === "slider" && (
            <div className="flex items-center gap-3">
              <div
                className="rounded-full overflow-hidden"
                style={{ width: 100, height: 4, background: "rgba(0,0,0,0.5)" }}
              >
                <div
                  className="h-full rounded-full"
                  style={{
                    width: `${vals[i]}%`,
                    background: "linear-gradient(90deg, #c9a84c88, #c9a84c)",
                    boxShadow: "0 0 4px rgba(201,168,76,0.5)",
                  }}
                />
              </div>
              <span className="font-mono text-white/40 w-8 text-right" style={{ fontSize: 10 }}>
                {vals[i]}%
              </span>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

export function SettingsMenu() {
  const [activeTab, setActiveTab] = useState<SettingsTab>("Controls");

  const contentMap: Record<SettingsTab, React.ReactNode> = {
    Controls: <ControlsContent />,
    Gameplay: <GenericSettings items={GAMEPLAY_SETTINGS} />,
    Graphics: <GenericSettings items={GRAPHICS_SETTINGS} />,
    Audio: <GenericSettings items={AUDIO_SETTINGS} />,
  };

  return (
    <div
      className="w-full h-full flex items-center justify-center p-8"
      style={{ background: "radial-gradient(ellipse at center, #0d1018 0%, #07080d 100%)" }}
    >
      <div
        className="w-full max-w-3xl h-full max-h-[600px] rounded-lg overflow-hidden flex"
        style={{
          background: "rgba(8,10,16,0.96)",
          border: "1px solid rgba(201,168,76,0.22)",
          boxShadow: "0 0 60px rgba(0,0,0,0.8), inset 0 0 40px rgba(0,0,0,0.4)",
        }}
      >
        {/* Left tab rail */}
        <div
          className="flex flex-col py-6 flex-shrink-0"
          style={{ width: 180, background: "rgba(0,0,0,0.4)", borderRight: "1px solid rgba(201,168,76,0.12)" }}
        >
          <h2
            className="text-white uppercase tracking-widest px-6 mb-6"
            style={{ fontFamily: "'Cinzel', serif", fontSize: 12, fontWeight: 600 }}
          >
            Settings
          </h2>
          {TABS.map(({ id, icon }) => (
            <button
              key={id}
              onClick={() => setActiveTab(id)}
              className="flex items-center gap-3 px-6 py-3 text-left transition-all relative"
              style={{
                background: activeTab === id ? "rgba(201,168,76,0.1)" : "transparent",
                borderRight: activeTab === id ? "2px solid #c9a84c" : "2px solid transparent",
                cursor: "pointer",
              }}
            >
              <span style={{ color: activeTab === id ? "#c9a84c" : "rgba(255,255,255,0.3)" }}>
                {icon}
              </span>
              <span
                className="uppercase tracking-wider"
                style={{
                  fontFamily: "'Cinzel', serif",
                  fontSize: 12,
                  color: activeTab === id ? "#e8d8a8" : "rgba(255,255,255,0.4)",
                }}
              >
                {id}
              </span>
            </button>
          ))}
        </div>

        {/* Right content */}
        <div className="flex-1 flex flex-col p-6 overflow-hidden">
          <div className="flex items-center justify-between mb-5">
            <h3
              className="text-white uppercase tracking-widest"
              style={{ fontFamily: "'Cinzel', serif", fontSize: 14, fontWeight: 600 }}
            >
              {activeTab}
            </h3>
            <div
              className="text-[10px] uppercase tracking-widest text-white/20"
              style={{ fontFamily: "'Cinzel', serif" }}
            >
              PC · Keyboard & Mouse
            </div>
          </div>

          <div
            className="h-px w-full mb-5"
            style={{ background: "linear-gradient(90deg, rgba(201,168,76,0.3), transparent)" }}
          />

          {contentMap[activeTab]}
        </div>
      </div>
    </div>
  );
}
