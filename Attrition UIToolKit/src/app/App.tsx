import { useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { GameHUD } from "./components/GameHUD";
import { CharacterInventory } from "./components/CharacterInventory";
import { MainMenu } from "./components/MainMenu";
import { CoopLobby } from "./components/CoopLobby";
import { SettingsMenu } from "./components/SettingsMenu";
import { SaveSelection } from "./components/SaveSelection";
import { LoginUI } from "./components/LoginUI";
import { MatchmakingUI } from "./components/MatchmakingUI";
import { FastTravel } from "./components/FastTravel";
import { MapUI } from "./components/MapUI";
import { LoadingScreen } from "./components/LoadingScreen";
import { GameOver } from "./components/GameOver";

type Screen =
  | "menu" | "login" | "save"
  | "hud" | "character"
  | "matchmaking" | "coop"
  | "fasttravel" | "map"
  | "loading" | "gameover" | "settings";

const SCREENS: Array<{ id: Screen; label: string }> = [
  { id: "menu", label: "Menu" },
  { id: "login", label: "Login" },
  { id: "save", label: "Save" },
  { id: "hud", label: "HUD" },
  { id: "character", label: "Inventory" },
  { id: "matchmaking", label: "Matchmaking" },
  { id: "coop", label: "Co-op" },
  { id: "fasttravel", label: "Fast Travel" },
  { id: "map", label: "Map" },
  { id: "loading", label: "Loading" },
  { id: "gameover", label: "Game Over" },
  { id: "settings", label: "Settings" },
];

export default function App() {
  const [screen, setScreen] = useState<Screen>("menu");
  const nav = (s: string) => setScreen(s as Screen);

  function renderScreen(s: Screen) {
    switch (s) {
      case "menu":        return <MainMenu onNavigate={nav} />;
      case "login":       return <LoginUI onNavigate={nav} />;
      case "save":        return <SaveSelection onNavigate={nav} />;
      case "hud":         return <GameHUD />;
      case "character":   return <CharacterInventory />;
      case "matchmaking": return <MatchmakingUI onNavigate={nav} />;
      case "coop":        return <CoopLobby onNavigate={nav} />;
      case "fasttravel":  return <FastTravel onNavigate={nav} />;
      case "map":         return <MapUI />;
      case "loading":     return <LoadingScreen onNavigate={nav} />;
      case "gameover":    return <GameOver onNavigate={nav} />;
      case "settings":    return <SettingsMenu />;
    }
  }

  return (
    <div
      className="relative w-full h-screen overflow-hidden"
      style={{ background: "#07080d", fontFamily: "'Inter', system-ui, sans-serif" }}
    >
      {/* ── Navigator ── */}
      <div
        className="absolute top-0 left-0 right-0 z-50 flex items-stretch overflow-x-auto"
        style={{
          background: "rgba(3,4,8,0.97)",
          borderBottom: "1px solid rgba(201,168,76,0.13)",
          scrollbarWidth: "none",
          height: 34,
        }}
      >
        {/* Wordmark */}
        <div
          className="flex items-center gap-2 px-4 flex-shrink-0"
          style={{ borderRight: "1px solid rgba(201,168,76,0.1)" }}
        >
          <div
            className="w-1.5 h-1.5 rotate-45 flex-shrink-0"
            style={{ background: "#c9a84c", boxShadow: "0 0 5px rgba(201,168,76,0.9)" }}
          />
          <span
            className="uppercase tracking-[0.22em] text-[#c9a84c]/55 whitespace-nowrap"
            style={{ fontSize: 8, fontFamily: "'Cinzel', serif" }}
          >
            Attrition UI
          </span>
        </div>

        {/* Screen tabs */}
        <div className="flex flex-1 overflow-x-auto" style={{ scrollbarWidth: "none" }}>
          {SCREENS.map(({ id, label }) => {
            const active = screen === id;
            return (
              <button
                key={id}
                onClick={() => setScreen(id)}
                className="relative px-4 flex-shrink-0 flex items-center"
                style={{
                  background: "none",
                  border: "none",
                  borderRight: "1px solid rgba(201,168,76,0.06)",
                  cursor: "pointer",
                }}
              >
                <span
                  className="uppercase tracking-wider whitespace-nowrap"
                  style={{
                    fontFamily: "'Cinzel', serif",
                    fontSize: 9,
                    color: active ? "#e8d8a8" : "rgba(140,130,110,0.5)",
                    transition: "color 0.15s",
                  }}
                >
                  {label}
                </span>
                {active && (
                  <span
                    className="absolute bottom-0 left-0 right-0 h-px"
                    style={{
                      background: "linear-gradient(90deg, transparent, #c9a84c, transparent)",
                      boxShadow: "0 0 4px rgba(201,168,76,0.7)",
                    }}
                  />
                )}
              </button>
            );
          })}
        </div>
      </div>

      {/* ── Screen content with transitions ── */}
      <div className="h-full" style={{ paddingTop: 34 }}>
        <AnimatePresence mode="wait">
          <motion.div
            key={screen}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            transition={{ duration: 0.22, ease: "easeOut" }}
            className="w-full h-full"
          >
            {renderScreen(screen)}
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}
