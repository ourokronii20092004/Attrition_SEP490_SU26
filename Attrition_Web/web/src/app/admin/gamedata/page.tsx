"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, cn } from "@/lib/utils";
import { Trash2, Edit, Plus, X, Upload, Download, RefreshCw } from "lucide-react";
import styles from "../admin.module.css";
import AssetPickerModal from "@/components/layout/AssetPickerModal";


// --- Interfaces ---
interface Item {
  itemId: number;
  name: string;
  itemType: string;
  rarity: string;
  stackLimit: number;
  description: string | null;
  iconKey: string | null;
}

interface Skill {
  skillId: number;
  name: string;
  skillType: string;
  damageType: string | null;
  manaCost: number;
  staminaCost: number;
  cooldownSec: number;
  baseDamage: number;
  description: string | null;
  iconKey: string | null;
  requiredLevel: number;
}

interface Level {
  levelNumber: number;
  expRequired: number;
  hpGrowth: number;
  adGrowth: number;
  apGrowth: number;
  defGrowth: number;
  resGrowth: number;
  speedGrowth: number;
}

interface SpawnPoint {
  id: string;
  enemyId: string;
  sceneName: string;
  positionX: number;
  positionY: number;
  positionZ: number;
  spawnIntervalSeconds: number;
  maxActiveCount: number;
}

interface GameConfig {
  key: string;
  value: string;
  description: string | null;
}

type TabType = "items" | "skills" | "levels" | "spawnpoints" | "configs";

export default function AdminGameDataPage() {
  const toast = useToast();
  const [activeTab, setActiveTab] = useState<TabType>("items");
  const [loading, setLoading] = useState(true);
  const [importing, setImporting] = useState(false);
  const [pickerTarget, setPickerTarget] = useState<string | null>(null);


  // --- Lists ---
  const [items, setItems] = useState<Item[]>([]);
  const [skills, setSkills] = useState<Skill[]>([]);
  const [levels, setLevels] = useState<Level[]>([]);
  const [spawnpoints, setSpawnpoints] = useState<SpawnPoint[]>([]);
  const [configs, setConfigs] = useState<GameConfig[]>([]);

  // --- Form Modal state ---
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<any>(null);

  // Field states (Dynamic based on active tab)
  const [fields, setFields] = useState<any>({});

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      if (activeTab === "items") {
        const res = await api.get<Item[]>("/game/data/items");
        if (res.success && res.data) setItems(res.data);
      } else if (activeTab === "skills") {
        const res = await api.get<Skill[]>("/game/data/skills");
        if (res.success && res.data) setSkills(res.data);
      } else if (activeTab === "levels") {
        const res = await api.get<Level[]>("/game/data/levels");
        if (res.success && res.data) setLevels(res.data);
      } else if (activeTab === "spawnpoints") {
        const res = await api.get<SpawnPoint[]>("/game/data/spawnpoints");
        if (res.success && res.data) setSpawnpoints(res.data);
      } else if (activeTab === "configs") {
        const res = await api.get<GameConfig[]>("/game/data/configs");
        if (res.success && res.data) setConfigs(res.data);
      }
    } catch {
      toast.error(`Failed to load ${activeTab}`);
    } finally {
      setLoading(false);
    }
  }, [activeTab, toast]);

  useEffect(() => {
    fetchData();
  }, [activeTab, fetchData]);

  // --- Export / Import ---
  const handleExport = async () => {
    try {
      const res = await api.get<any>("/game/data/export");
      if (res.success && res.data) {
        const blob = new Blob([JSON.stringify(res.data, null, 2)], { type: "application/json" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `attrition_gamedata_${new Date().toISOString().split("T")[0]}.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        toast.success("Game database backup exported!");
      }
    } catch {
      toast.error("Failed to export game database");
    }
  };

  const handleImport = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    
    if (!confirm("Are you sure you want to import this game database? This will merge and update existing configurations.")) return;

    setImporting(true);
    const reader = new FileReader();
    reader.onload = async (event) => {
      try {
        const json = JSON.parse(event.target?.result as string);
        const res = await api.post("/game/data/import", json);
        if (res.success) {
          toast.success("Game database imported successfully!");
          fetchData();
        } else {
          toast.error(res.error || "Failed to import database");
        }
      } catch {
        toast.error("Invalid database backup file format");
      } finally {
        setImporting(false);
      }
    };
    reader.readAsText(file);
    e.target.value = ""; // reset picker
  };

  // --- Delete actions ---
  const handleDelete = async (id: any) => {
    if (!confirm(`Are you sure you want to delete this configuration?`)) return;
    try {
      let endpoint = "";
      if (activeTab === "items") endpoint = `/game/data/items/${id}`;
      else if (activeTab === "skills") endpoint = `/game/data/skills/${id}`;
      else if (activeTab === "levels") endpoint = `/game/data/levels/${id}`;
      else if (activeTab === "spawnpoints") endpoint = `/game/data/spawnpoints/${id}`;
      else if (activeTab === "configs") endpoint = `/game/data/configs/${id}`;

      const res = await api.delete(endpoint);
      if (res.success) {
        toast.success("Configuration deleted successfully");
        fetchData();
      }
    } catch {
      toast.error("Failed to delete configuration");
    }
  };

  // --- Form Modal actions ---
  const handleOpenCreate = () => {
    setEditingId(null);
    if (activeTab === "items") {
      setFields({ itemId: 0, name: "", itemType: "consumable", rarity: "common", stackLimit: 99, description: "", iconKey: "" });
    } else if (activeTab === "skills") {
      setFields({ skillId: 0, name: "", skillType: "magic", damageType: "fire", manaCost: 10, staminaCost: 0, cooldownSec: 5, baseDamage: 25, description: "", iconKey: "", requiredLevel: 1 });
    } else if (activeTab === "levels") {
      setFields({ levelNumber: 1, expRequired: 100, hpGrowth: 10, adGrowth: 1, apGrowth: 1, defGrowth: 1, resGrowth: 1, speedGrowth: 0 });
    } else if (activeTab === "spawnpoints") {
      setFields({ id: "", enemyId: "", sceneName: "", positionX: 0, positionY: 0, positionZ: 0, spawnIntervalSeconds: 30, maxActiveCount: 1 });
    } else if (activeTab === "configs") {
      setFields({ key: "", value: "", description: "" });
    }
    setFormOpen(true);
  };

  const handleOpenEdit = (item: any) => {
    if (activeTab === "items") {
      setEditingId(item.itemId);
      setFields({ ...item });
    } else if (activeTab === "skills") {
      setEditingId(item.skillId);
      setFields({ ...item });
    } else if (activeTab === "levels") {
      setEditingId(item.levelNumber);
      setFields({ ...item });
    } else if (activeTab === "spawnpoints") {
      setEditingId(item.id);
      setFields({ ...item });
    } else if (activeTab === "configs") {
      setEditingId(item.key);
      setFields({ ...item });
    }
    setFormOpen(true);
  };

  const handleFieldChange = (key: string, val: any) => {
    setFields({ ...fields, [key]: val });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      let endpoint = "";
      if (activeTab === "items") endpoint = "/game/data/items";
      else if (activeTab === "skills") endpoint = "/game/data/skills";
      else if (activeTab === "levels") endpoint = "/game/data/levels";
      else if (activeTab === "spawnpoints") endpoint = "/game/data/spawnpoints";
      else if (activeTab === "configs") endpoint = "/game/data/configs";

      if (editingId !== null) {
        const res = await api.put(`${endpoint}/${editingId}`, fields);
        if (res.success) {
          toast.success("Configuration updated successfully");
          setFormOpen(false);
          fetchData();
        }
      } else {
        const res = await api.post(endpoint, fields);
        if (res.success) {
          toast.success("Configuration created successfully");
          setFormOpen(false);
          fetchData();
        }
      }
    } catch {
      toast.error("Failed to save configuration");
    }
  };

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-6)" }}>
        <h1>Game Subsystems</h1>
        
        <div style={{ display: "flex", gap: "var(--space-2)" }}>
          <button className="btn btn-secondary btn-sm" style={{ display: "flex", alignItems: "center", gap: 6 }} onClick={handleExport}>
            <Download size={14} /> Export Backup
          </button>
          
          <label className="btn btn-secondary btn-sm" style={{ display: "inline-flex", alignItems: "center", gap: 6, cursor: "pointer", margin: 0 }}>
            <Upload size={14} /> {importing ? "Importing..." : "Import Backup"}
            <input type="file" accept=".json" onChange={handleImport} style={{ display: "none" }} disabled={importing} />
          </label>
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: "flex", borderBottom: "1px solid var(--border)", marginBottom: "var(--space-6)", gap: "var(--space-1)" }}>
        {(["items", "skills", "levels", "spawnpoints", "configs"] as TabType[]).map((tab) => (
          <button
            key={tab}
            className={cn(
              "btn btn-sm",
              activeTab === tab ? "btn-primary" : "btn-secondary"
            )}
            onClick={() => setActiveTab(tab)}
            style={{ borderRadius: "var(--radius-md) var(--radius-md) 0 0", borderBottom: "none" }}
          >
            {tab === "spawnpoints" ? "Spawn Points" : tab === "configs" ? "Game Configs" : tab.charAt(0).toUpperCase() + tab.slice(1)}
          </button>
        ))}
      </div>

      {/* Actions & List */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-4)" }}>
        <span className="text-sm text-muted">
          {activeTab === "items" ? `${items.length} items` : activeTab === "skills" ? `${skills.length} skills` : activeTab === "levels" ? `${levels.length} levels` : activeTab === "spawnpoints" ? `${spawnpoints.length} spawn points` : `${configs.length} configurations`}
        </span>
        
        <button className="btn btn-primary btn-sm" style={{ display: "flex", alignItems: "center", gap: 4 }} onClick={handleOpenCreate}>
          <Plus size={14} /> Add Config
        </button>
      </div>

      <div className={styles.adminTableWrapper}>
        <table className="table">
          <thead>
            {activeTab === "items" && (
              <tr>
                <th>ID</th>
                <th>Name</th>
                <th>Type</th>
                <th>Rarity</th>
                <th>Stack Limit</th>
                <th>Description</th>
                <th>Actions</th>
              </tr>
            )}
            {activeTab === "skills" && (
              <tr>
                <th>ID</th>
                <th>Name</th>
                <th>Type</th>
                <th>Dmg Type</th>
                <th>Mana/Stam</th>
                <th>CD</th>
                <th>Base Damage</th>
                <th>Level</th>
                <th>Actions</th>
              </tr>
            )}
            {activeTab === "levels" && (
              <tr>
                <th>Level</th>
                <th>Exp Required</th>
                <th>HP growth</th>
                <th>AD/AP growth</th>
                <th>DEF/RES growth</th>
                <th>Actions</th>
              </tr>
            )}
            {activeTab === "spawnpoints" && (
              <tr>
                <th>ID</th>
                <th>Enemy</th>
                <th>Scene</th>
                <th>Coordinates (X, Y, Z)</th>
                <th>Interval</th>
                <th>Max Spawn</th>
                <th>Actions</th>
              </tr>
            )}
            {activeTab === "configs" && (
              <tr>
                <th>Configuration Key</th>
                <th>Value</th>
                <th>Description</th>
                <th>Actions</th>
              </tr>
            )}
          </thead>
          <tbody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <tr key={i}>
                  {Array.from({ length: activeTab === "skills" ? 9 : activeTab === "items" ? 7 : 6 }).map((_, j) => (
                    <td key={j}><div className="skeleton skeleton-text" /></td>
                  ))}
                </tr>
              ))
            ) : (
              <>
                {activeTab === "items" && items.map(item => (
                  <tr key={item.itemId}>
                    <td><code>#{item.itemId}</code></td>
                    <td><strong>{item.name}</strong></td>
                    <td><span className="badge badge-default">{item.itemType}</span></td>
                    <td><span className="badge badge-default" style={{ textTransform: "capitalize" }}>{item.rarity}</span></td>
                    <td>{item.stackLimit}</td>
                    <td style={{ maxWidth: 200, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{item.description || "—"}</td>
                    <td>
                      <div style={{ display: "flex", gap: 4 }}>
                        <button className="btn btn-secondary btn-sm" style={{ padding: "var(--space-2)" }} onClick={() => handleOpenEdit(item)}><Edit size={14} /></button>
                        <button className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)", padding: "var(--space-2)" }} onClick={() => handleDelete(item.itemId)}><Trash2 size={14} /></button>
                      </div>
                    </td>
                  </tr>
                ))}

                {activeTab === "skills" && skills.map(skill => (
                  <tr key={skill.skillId}>
                    <td><code>#{skill.skillId}</code></td>
                    <td><strong>{skill.name}</strong></td>
                    <td><span className="badge badge-default">{skill.skillType}</span></td>
                    <td><span className="badge badge-default">{skill.damageType || "—"}</span></td>
                    <td>{skill.manaCost} / {skill.staminaCost}</td>
                    <td>{skill.cooldownSec}s</td>
                    <td>{skill.baseDamage}</td>
                    <td>Lvl {skill.requiredLevel}</td>
                    <td>
                      <div style={{ display: "flex", gap: 4 }}>
                        <button className="btn btn-secondary btn-sm" style={{ padding: "var(--space-2)" }} onClick={() => handleOpenEdit(skill)}><Edit size={14} /></button>
                        <button className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)", padding: "var(--space-2)" }} onClick={() => handleDelete(skill.skillId)}><Trash2 size={14} /></button>
                      </div>
                    </td>
                  </tr>
                ))}

                {activeTab === "levels" && levels.map(lvl => (
                  <tr key={lvl.levelNumber}>
                    <td><strong>Level {lvl.levelNumber}</strong></td>
                    <td>{lvl.expRequired.toLocaleString()} exp</td>
                    <td>+{lvl.hpGrowth} HP</td>
                    <td>+{lvl.adGrowth} AD / +{lvl.apGrowth} AP</td>
                    <td>+{lvl.defGrowth} DEF / +{lvl.resGrowth} RES</td>
                    <td>
                      <div style={{ display: "flex", gap: 4 }}>
                        <button className="btn btn-secondary btn-sm" style={{ padding: "var(--space-2)" }} onClick={() => handleOpenEdit(lvl)}><Edit size={14} /></button>
                        <button className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)", padding: "var(--space-2)" }} onClick={() => handleDelete(lvl.levelNumber)}><Trash2 size={14} /></button>
                      </div>
                    </td>
                  </tr>
                ))}

                {activeTab === "spawnpoints" && spawnpoints.map(sp => (
                  <tr key={sp.id}>
                    <td><code style={{ fontSize: 10 }}>{sp.id.substring(0, 8)}</code></td>
                    <td><strong>{sp.enemyId}</strong></td>
                    <td><code>{sp.sceneName}</code></td>
                    <td>({sp.positionX.toFixed(1)}, {sp.positionY.toFixed(1)}, {sp.positionZ.toFixed(1)})</td>
                    <td>{sp.spawnIntervalSeconds}s</td>
                    <td>{sp.maxActiveCount} max</td>
                    <td>
                      <div style={{ display: "flex", gap: 4 }}>
                        <button className="btn btn-secondary btn-sm" style={{ padding: "var(--space-2)" }} onClick={() => handleOpenEdit(sp)}><Edit size={14} /></button>
                        <button className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)", padding: "var(--space-2)" }} onClick={() => handleDelete(sp.id)}><Trash2 size={14} /></button>
                      </div>
                    </td>
                  </tr>
                ))}

                {activeTab === "configs" && configs.map(cfg => (
                  <tr key={cfg.key}>
                    <td><strong><code>{cfg.key}</code></strong></td>
                    <td><code>{cfg.value}</code></td>
                    <td style={{ maxWidth: 300, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{cfg.description || "—"}</td>
                    <td>
                      <div style={{ display: "flex", gap: 4 }}>
                        <button className="btn btn-secondary btn-sm" style={{ padding: "var(--space-2)" }} onClick={() => handleOpenEdit(cfg)}><Edit size={14} /></button>
                        <button className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)", padding: "var(--space-2)" }} onClick={() => handleDelete(cfg.key)}><Trash2 size={14} /></button>
                      </div>
                    </td>
                  </tr>
                ))}
              </>
            )}
          </tbody>
        </table>
      </div>

      {/* Config Form Modal */}
      {formOpen && (
        <div style={{
          position: "fixed", top: 0, left: 0, right: 0, bottom: 0,
          background: "rgba(0,0,0,0.6)", zIndex: 9999, display: "flex",
          alignItems: "center", justifyContent: "center", backdropFilter: "blur(4px)"
        }}>
          <div className={styles.settingsCard} style={{ maxWidth: 600, width: "95%", maxHeight: "90vh", overflowY: "auto", display: "flex", flexDirection: "column", gap: "var(--space-5)" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: "1px solid var(--border)", paddingBottom: "var(--space-3)" }}>
              <h3>{editingId !== null ? "Edit Configuration" : "Add New Configuration"}</h3>
              <button style={{ background: "none", border: "none", cursor: "pointer", color: "var(--text-muted)" }} onClick={() => setFormOpen(false)}>
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
              {activeTab === "items" && (
                <>
                  <div className="input-group">
                    <label className="input-label">Item ID</label>
                    <input type="number" className="input" value={fields.itemId} onChange={(e) => handleFieldChange("itemId", parseInt(e.target.value) || 0)} disabled={editingId !== null} required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Item Name</label>
                    <input type="text" className="input" value={fields.name} onChange={(e) => handleFieldChange("name", e.target.value)} required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Type</label>
                    <select className="input" value={fields.itemType} onChange={(e) => handleFieldChange("itemType", e.target.value)}>
                      <option value="consumable">Consumable</option>
                      <option value="gear">Gear/Equipment</option>
                      <option value="material">Crafting Material</option>
                      <option value="quest">Quest Item</option>
                    </select>
                  </div>
                  <div className="input-group">
                    <label className="input-label">Rarity</label>
                    <select className="input" value={fields.rarity} onChange={(e) => handleFieldChange("rarity", e.target.value)}>
                      <option value="common">Common</option>
                      <option value="uncommon">Uncommon</option>
                      <option value="rare">Rare</option>
                      <option value="legendary">Legendary</option>
                    </select>
                  </div>
                  <div className="input-group">
                    <label className="input-label">Stack Limit</label>
                    <input type="number" className="input" value={fields.stackLimit} onChange={(e) => handleFieldChange("stackLimit", parseInt(e.target.value) || 1)} />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Icon / Sprite Path</label>
                    <div style={{ display: "flex", gap: 8 }}>
                      <input type="text" className="input" value={fields.iconKey || ""} onChange={(e) => handleFieldChange("iconKey", e.target.value)} placeholder="/uploads/assets/icon.png" style={{ flex: 1 }} />
                      <button type="button" className="btn btn-secondary btn-sm" onClick={() => setPickerTarget("iconKey")} style={{ whiteSpace: "nowrap", height: "42px" }}>
                        Pick Asset
                      </button>
                    </div>
                  </div>
                  <div className="input-group">
                    <label className="input-label">Description</label>
                    <textarea className="input" value={fields.description || ""} onChange={(e) => handleFieldChange("description", e.target.value)} rows={3} />
                  </div>
                </>
              )}

              {activeTab === "skills" && (
                <>
                  <div className="input-group">
                    <label className="input-label">Skill ID</label>
                    <input type="number" className="input" value={fields.skillId} onChange={(e) => handleFieldChange("skillId", parseInt(e.target.value) || 0)} disabled={editingId !== null} required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Skill Name</label>
                    <input type="text" className="input" value={fields.name} onChange={(e) => handleFieldChange("name", e.target.value)} required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Skill Type</label>
                    <input type="text" className="input" value={fields.skillType} onChange={(e) => handleFieldChange("skillType", e.target.value)} placeholder="magic / physical / passive" />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Mana Cost</label>
                    <input type="number" className="input" value={fields.manaCost} onChange={(e) => handleFieldChange("manaCost", parseInt(e.target.value) || 0)} />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Cooldown Seconds</label>
                    <input type="number" step="0.1" className="input" value={fields.cooldownSec} onChange={(e) => handleFieldChange("cooldownSec", parseFloat(e.target.value) || 0)} />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Base Damage</label>
                    <input type="number" className="input" value={fields.baseDamage} onChange={(e) => handleFieldChange("baseDamage", parseInt(e.target.value) || 0)} />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Required Level</label>
                    <input type="number" className="input" value={fields.requiredLevel} onChange={(e) => handleFieldChange("requiredLevel", parseInt(e.target.value) || 1)} />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Icon / Sprite Path</label>
                    <div style={{ display: "flex", gap: 8 }}>
                      <input type="text" className="input" value={fields.iconKey || ""} onChange={(e) => handleFieldChange("iconKey", e.target.value)} placeholder="/uploads/assets/skill.png" style={{ flex: 1 }} />
                      <button type="button" className="btn btn-secondary btn-sm" onClick={() => setPickerTarget("iconKey")} style={{ whiteSpace: "nowrap", height: "42px" }}>
                        Pick Asset
                      </button>
                    </div>
                  </div>
                  <div className="input-group">
                    <label className="input-label">Description</label>
                    <textarea className="input" value={fields.description || ""} onChange={(e) => handleFieldChange("description", e.target.value)} rows={3} />
                  </div>
                </>
              )}

              {activeTab === "levels" && (
                <>
                  <div className="input-group">
                    <label className="input-label">Level Number</label>
                    <input type="number" className="input" value={fields.levelNumber} onChange={(e) => handleFieldChange("levelNumber", parseInt(e.target.value) || 1)} disabled={editingId !== null} required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Exp Required</label>
                    <input type="number" className="input" value={fields.expRequired} onChange={(e) => handleFieldChange("expRequired", parseInt(e.target.value) || 0)} required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">HP Growth</label>
                    <input type="number" className="input" value={fields.hpGrowth} onChange={(e) => handleFieldChange("hpGrowth", parseInt(e.target.value) || 0)} />
                  </div>
                  <div className="input-group">
                    <label className="input-label">AD / AP Growth</label>
                    <div style={{ display: "flex", gap: 8 }}>
                      <input type="number" className="input" placeholder="AD" value={fields.adGrowth} onChange={(e) => handleFieldChange("adGrowth", parseInt(e.target.value) || 0)} />
                      <input type="number" className="input" placeholder="AP" value={fields.apGrowth} onChange={(e) => handleFieldChange("apGrowth", parseInt(e.target.value) || 0)} />
                    </div>
                  </div>
                </>
              )}

              {activeTab === "spawnpoints" && (
                <>
                  <div className="input-group">
                    <label className="input-label">Enemy Key ID</label>
                    <input type="text" className="input" value={fields.enemyId} onChange={(e) => handleFieldChange("enemyId", e.target.value)} placeholder="e.g. shadow_stalker" required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Scene/Map Name</label>
                    <input type="text" className="input" value={fields.sceneName} onChange={(e) => handleFieldChange("sceneName", e.target.value)} placeholder="e.g. level_forest_1" required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Coordinates (X, Y, Z)</label>
                    <div style={{ display: "flex", gap: 8 }}>
                      <input type="number" step="0.1" className="input" placeholder="X" value={fields.positionX} onChange={(e) => handleFieldChange("positionX", parseFloat(e.target.value) || 0)} />
                      <input type="number" step="0.1" className="input" placeholder="Y" value={fields.positionY} onChange={(e) => handleFieldChange("positionY", parseFloat(e.target.value) || 0)} />
                      <input type="number" step="0.1" className="input" placeholder="Z" value={fields.positionZ} onChange={(e) => handleFieldChange("positionZ", parseFloat(e.target.value) || 0)} />
                    </div>
                  </div>
                  <div className="input-group">
                    <label className="input-label">Spawn Cooldown (Seconds)</label>
                    <input type="number" className="input" value={fields.spawnIntervalSeconds} onChange={(e) => handleFieldChange("spawnIntervalSeconds", parseInt(e.target.value) || 30)} />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Max Active Count</label>
                    <input type="number" className="input" value={fields.maxActiveCount} onChange={(e) => handleFieldChange("maxActiveCount", parseInt(e.target.value) || 1)} />
                  </div>
                </>
              )}

              {activeTab === "configs" && (
                <>
                  <div className="input-group">
                    <label className="input-label">Configuration Key</label>
                    <input type="text" className="input" value={fields.key} onChange={(e) => handleFieldChange("key", e.target.value)} placeholder="e.g. global_exp_multiplier" disabled={editingId !== null} required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Value</label>
                    <input type="text" className="input" value={fields.value} onChange={(e) => handleFieldChange("value", e.target.value)} placeholder="e.g. 1.2" required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Description / Purpose</label>
                    <textarea className="input" value={fields.description || ""} onChange={(e) => handleFieldChange("description", e.target.value)} rows={3} />
                  </div>
                </>
              )}

              <div style={{ display: "flex", gap: "var(--space-3)", justifyContent: "flex-end", borderTop: "1px solid var(--border)", paddingTop: "var(--space-4)", marginTop: "var(--space-2)" }}>
                <button type="button" className="btn btn-secondary btn-md" onClick={() => setFormOpen(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary btn-md">
                  {editingId !== null ? "Save Changes" : "Create Config"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
      <AssetPickerModal
        isOpen={pickerTarget !== null}
        onClose={() => setPickerTarget(null)}
        onSelect={(url) => {
          if (pickerTarget) {
            handleFieldChange(pickerTarget, url);
          }
        }}
        allowedTypes={["image", "sprite"]}
      />
    </div>
  );
}

