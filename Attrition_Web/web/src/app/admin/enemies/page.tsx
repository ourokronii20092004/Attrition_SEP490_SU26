"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, cn } from "@/lib/utils";
import { Trash2, Edit, Plus, X, ShieldAlert } from "lucide-react";
import styles from "../admin.module.css";

interface LootEntry {
  enemyId: string;
  itemId: number;
  dropChance: number;
  minQty: number;
  maxQty: number;
  itemName?: string; // resolved in client
}

interface Enemy {
  enemyId: string;
  name: string;
  tier: string;
  spawnBiome: string | null;
  hp: number;
  ad: number;
  ap: number;
  def: number;
  res: number;
  attackSpeed: number;
  isRanged: boolean;
  expReward: number;
  goldReward: number;
  lootTable: LootEntry[];
}

interface Item {
  itemId: number;
  name: string;
  itemType: string;
}

export default function AdminEnemiesPage() {
  const toast = useToast();
  const [enemies, setEnemies] = useState<Enemy[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [loading, setLoading] = useState(true);

  // Edit / Form state
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  
  const [enemyId, setEnemyId] = useState("");
  const [name, setName] = useState("");
  const [tier, setTier] = useState("common");
  const [spawnBiome, setSpawnBiome] = useState("");
  const [hp, setHp] = useState(100);
  const [ad, setAd] = useState(10);
  const [ap, setAp] = useState(0);
  const [def, setDef] = useState(5);
  const [res, setRes] = useState(0);
  const [attackSpeed, setAttackSpeed] = useState(1.0);
  const [isRanged, setIsRanged] = useState(false);
  const [expReward, setExpReward] = useState(10);
  const [goldReward, setGoldReward] = useState(5);
  const [lootTable, setLootTable] = useState<LootEntry[]>([]);

  // Sub-editor item states
  const [selectedItemId, setSelectedItemId] = useState<number>(0);
  const [dropChance, setDropChance] = useState<number>(0.1);
  const [minQty, setMinQty] = useState<number>(1);
  const [maxQty, setMaxQty] = useState<number>(1);

  const fetchEnemies = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<Enemy[]>("/game/data/enemies");
      if (res.success && res.data) {
        setEnemies(res.data);
      }
    } catch {
      toast.error("Failed to load enemies");
    } finally {
      setLoading(false);
    }
  }, [toast]);

  const fetchItems = useCallback(async () => {
    try {
      const res = await api.get<Item[]>("/game/data/items");
      if (res.success && res.data) {
        setItems(res.data);
        if (res.data.length > 0) setSelectedItemId(res.data[0].itemId);
      }
    } catch {
      toast.error("Failed to load items");
    }
  }, [toast]);

  useEffect(() => {
    fetchEnemies();
    fetchItems();
  }, [fetchEnemies, fetchItems]);

  const handleOpenCreate = () => {
    setEditingId(null);
    setEnemyId("");
    setName("");
    setTier("common");
    setSpawnBiome("");
    setHp(100);
    setAd(10);
    setAp(0);
    setDef(5);
    setRes(0);
    setAttackSpeed(1.0);
    setIsRanged(false);
    setExpReward(10);
    setGoldReward(5);
    setLootTable([]);
    setFormOpen(true);
  };

  const handleOpenEdit = (enemy: Enemy) => {
    setEditingId(enemy.enemyId);
    setEnemyId(enemy.enemyId);
    setName(enemy.name);
    setTier(enemy.tier);
    setSpawnBiome(enemy.spawnBiome || "");
    setHp(enemy.hp);
    setAd(enemy.ad);
    setAp(enemy.ap);
    setDef(enemy.def);
    setRes(enemy.res);
    setAttackSpeed(enemy.attackSpeed);
    setIsRanged(enemy.isRanged);
    setExpReward(enemy.expReward);
    setGoldReward(enemy.goldReward);
    setLootTable(enemy.lootTable);
    setFormOpen(true);
  };

  const handleAddLoot = () => {
    if (selectedItemId === 0) return;
    if (lootTable.some(le => le.itemId === selectedItemId)) {
      toast.error("Item already exists in loot table");
      return;
    }
    const itemObj = items.find(i => i.itemId === selectedItemId);
    const newLoot: LootEntry = {
      enemyId: enemyId || "TEMP",
      itemId: selectedItemId,
      dropChance,
      minQty,
      maxQty,
      itemName: itemObj?.name
    };
    setLootTable([...lootTable, newLoot]);
  };

  const handleRemoveLoot = (itemId: number) => {
    setLootTable(lootTable.filter(le => le.itemId !== itemId));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!enemyId || !name) {
      toast.error("ID and Name are required");
      return;
    }

    const payload: Enemy = {
      enemyId,
      name,
      tier,
      spawnBiome: spawnBiome || null,
      hp,
      ad,
      ap,
      def,
      res,
      attackSpeed,
      isRanged,
      expReward,
      goldReward,
      lootTable: lootTable.map(le => ({
        enemyId,
        itemId: le.itemId,
        dropChance: le.dropChance,
        minQty: le.minQty,
        maxQty: le.maxQty
      }))
    };

    try {
      if (editingId) {
        const res = await api.put<Enemy>(`/game/data/enemies/${editingId}`, payload);
        if (res.success) {
          toast.success("Enemy updated successfully");
          setFormOpen(false);
          fetchEnemies();
        }
      } else {
        const res = await api.post<Enemy>("/game/data/enemies", payload);
        if (res.success) {
          toast.success("Enemy created successfully");
          setFormOpen(false);
          fetchEnemies();
        }
      }
    } catch {
      toast.error("Failed to save enemy");
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm(`Are you sure you want to delete enemy "${id}"?`)) return;
    try {
      const res = await api.delete(`/game/data/enemies/${id}`);
      if (res.success) {
        toast.success("Enemy deleted successfully");
        fetchEnemies();
      }
    } catch {
      toast.error("Failed to delete enemy");
    }
  };

  const getItemName = (itemId: number) => {
    const item = items.find(i => i.itemId === itemId);
    return item ? item.name : `Item #${itemId}`;
  };

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-6)" }}>
        <h1>Enemy Database</h1>
        <button className="btn btn-primary btn-sm" style={{ display: "flex", alignItems: "center", gap: 6 }} onClick={handleOpenCreate}>
          <Plus size={14} /> Add Enemy
        </button>
      </div>

      <div className={styles.adminTableWrapper}>
        <table className="table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Name</th>
              <th>Tier</th>
              <th>Biome</th>
              <th>HP</th>
              <th>AD/AP</th>
              <th>DEF/RES</th>
              <th>EXP/Gold</th>
              <th>Loot Table</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <tr key={i}>
                  {Array.from({ length: 10 }).map((_, j) => (
                    <td key={j}><div className="skeleton skeleton-text" /></td>
                  ))}
                </tr>
              ))
            ) : enemies.length > 0 ? (
              enemies.map((enemy) => (
                <tr key={enemy.enemyId}>
                  <td><code>{enemy.enemyId}</code></td>
                  <td><strong>{enemy.name}</strong></td>
                  <td>
                    <span className={cn(
                      "badge",
                      enemy.tier.toLowerCase() === "boss" ? "badge-danger" : enemy.tier.toLowerCase() === "elite" ? "badge-warning" : "badge-default"
                    )}>
                      {enemy.tier.toUpperCase()}
                    </span>
                  </td>
                  <td><span className="text-muted">{enemy.spawnBiome || "—"}</span></td>
                  <td>{enemy.hp}</td>
                  <td>{enemy.ad} / {enemy.ap}</td>
                  <td>{enemy.def} / {enemy.res}</td>
                  <td>{enemy.expReward} / {enemy.goldReward}</td>
                  <td>
                    <span style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>
                      {enemy.lootTable?.length || 0} entries
                    </span>
                  </td>
                  <td>
                    <div style={{ display: "flex", gap: 4 }}>
                      <button className="btn btn-secondary btn-sm" style={{ padding: "var(--space-2)" }} onClick={() => handleOpenEdit(enemy)} title="Edit">
                        <Edit size={14} />
                      </button>
                      <button className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)", padding: "var(--space-2)" }} onClick={() => handleDelete(enemy.enemyId)} title="Delete">
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={10} style={{ textAlign: "center", padding: "var(--space-8)" }}>
                  No enemies found
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Add/Edit Modal */}
      {formOpen && (
        <div style={{
          position: "fixed", top: 0, left: 0, right: 0, bottom: 0,
          background: "rgba(0,0,0,0.6)", zIndex: 9999, display: "flex",
          alignItems: "center", justifyContent: "center", backdropFilter: "blur(4px)"
        }}>
          <div className={styles.settingsCard} style={{ maxWidth: 800, width: "95%", maxHeight: "90vh", overflowY: "auto", display: "flex", flexDirection: "column", gap: "var(--space-5)" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: "1px solid var(--border)", paddingBottom: "var(--space-3)" }}>
              <h3>{editingId ? "Edit Enemy Profile" : "Register New Enemy"}</h3>
              <button style={{ background: "none", border: "none", cursor: "pointer", color: "var(--text-muted)" }} onClick={() => setFormOpen(false)}>
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "var(--space-5)" }}>
              {/* Form columns */}
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "var(--space-4)" }}>
                <div className="input-group">
                  <label className="input-label">Enemy ID (Machine Key)</label>
                  <input
                    type="text"
                    className="input"
                    value={enemyId}
                    onChange={(e) => setEnemyId(e.target.value)}
                    placeholder="e.g. shadow_stalker"
                    disabled={!!editingId}
                    required
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Display Name</label>
                  <input
                    type="text"
                    className="input"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="e.g. Shadow Stalker"
                    required
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Tier</label>
                  <select className="input" value={tier} onChange={(e) => setTier(e.target.value)}>
                    <option value="common">Common</option>
                    <option value="elite">Elite</option>
                    <option value="boss">Boss</option>
                  </select>
                </div>
                <div className="input-group">
                  <label className="input-label">Spawn Biome</label>
                  <input
                    type="text"
                    className="input"
                    value={spawnBiome}
                    onChange={(e) => setSpawnBiome(e.target.value)}
                    placeholder="e.g. Forest, Dungeon"
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Health Points (HP)</label>
                  <input
                    type="number"
                    className="input"
                    value={hp}
                    onChange={(e) => setHp(parseInt(e.target.value) || 0)}
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Attack Damage (AD)</label>
                  <input
                    type="number"
                    className="input"
                    value={ad}
                    onChange={(e) => setAd(parseInt(e.target.value) || 0)}
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Ability Power (AP)</label>
                  <input
                    type="number"
                    className="input"
                    value={ap}
                    onChange={(e) => setAp(parseInt(e.target.value) || 0)}
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Armor Defense (DEF)</label>
                  <input
                    type="number"
                    className="input"
                    value={def}
                    onChange={(e) => setDef(parseInt(e.target.value) || 0)}
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Magic Resistance (RES)</label>
                  <input
                    type="number"
                    className="input"
                    value={res}
                    onChange={(e) => setRes(parseInt(e.target.value) || 0)}
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Attack Speed multiplier</label>
                  <input
                    type="number"
                    step="0.1"
                    className="input"
                    value={attackSpeed}
                    onChange={(e) => setAttackSpeed(parseFloat(e.target.value) || 1.0)}
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Exp Reward</label>
                  <input
                    type="number"
                    className="input"
                    value={expReward}
                    onChange={(e) => setExpReward(parseInt(e.target.value) || 0)}
                  />
                </div>
                <div className="input-group">
                  <label className="input-label">Gold Reward</label>
                  <input
                    type="number"
                    className="input"
                    value={goldReward}
                    onChange={(e) => setGoldReward(parseInt(e.target.value) || 0)}
                  />
                </div>
              </div>

              <div className="input-group" style={{ flexDirection: "row", gap: "var(--space-2)", alignItems: "center" }}>
                <input
                  type="checkbox"
                  id="isRangedCheckbox"
                  checked={isRanged}
                  onChange={(e) => setIsRanged(e.target.checked)}
                />
                <label htmlFor="isRangedCheckbox" style={{ fontSize: "var(--text-sm)", cursor: "pointer" }}>Is Ranged Attacker</label>
              </div>

              {/* Loot Table Sub-Editor */}
              <div style={{ borderTop: "1px solid var(--border)", paddingTop: "var(--space-4)" }}>
                <h4 style={{ marginBottom: "var(--space-3)" }}>Loot Table Editor</h4>
                
                {/* Loot Entry creator */}
                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: "var(--space-3)", marginBottom: "var(--space-4)", background: "var(--bg-secondary)", padding: "var(--space-3)", borderRadius: "var(--radius-md)", border: "1px solid var(--border)" }}>
                  <div className="input-group">
                    <label className="input-label">Item</label>
                    <select
                      className="input"
                      value={selectedItemId}
                      onChange={(e) => setSelectedItemId(parseInt(e.target.value) || 0)}
                    >
                      {items.map(item => (
                        <option key={item.itemId} value={item.itemId}>
                          {item.name} ({item.itemType})
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="input-group">
                    <label className="input-label">Drop Chance (0-1)</label>
                    <input
                      type="number"
                      step="0.01"
                      min="0"
                      max="1"
                      className="input"
                      value={dropChance}
                      onChange={(e) => setDropChance(parseFloat(e.target.value) || 0.0)}
                    />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Min Qty</label>
                    <input
                      type="number"
                      min="1"
                      className="input"
                      value={minQty}
                      onChange={(e) => setMinQty(parseInt(e.target.value) || 1)}
                    />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Max Qty</label>
                    <input
                      type="number"
                      min="1"
                      className="input"
                      value={maxQty}
                      onChange={(e) => setMaxQty(parseInt(e.target.value) || 1)}
                    />
                  </div>
                  <div style={{ display: "flex", alignItems: "flex-end" }}>
                    <button type="button" className="btn btn-secondary btn-sm" style={{ width: "100%", height: 36, display: "flex", alignItems: "center", justifyContent: "center", gap: 4 }} onClick={handleAddLoot}>
                      <Plus size={14} /> Add Entry
                    </button>
                  </div>
                </div>

                {/* Loot Entry List */}
                <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-2)" }}>
                  {lootTable.length === 0 ? (
                    <p style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>This enemy currently has no items in their loot table.</p>
                  ) : (
                    lootTable.map(le => (
                      <div key={le.itemId} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "var(--space-2) var(--space-3)", background: "var(--bg-secondary)", borderRadius: "var(--radius-sm)", border: "1px solid var(--border)", fontSize: "var(--text-sm)" }}>
                        <span>
                          <strong>{le.itemName || getItemName(le.itemId)}</strong> · Chance: <strong>{(le.dropChance * 100).toFixed(1)}%</strong> · Qty: <strong>{le.minQty}-{le.maxQty}</strong>
                        </span>
                        <button type="button" className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)", borderColor: "transparent", background: "none", padding: "var(--space-1)" }} onClick={() => handleRemoveLoot(le.itemId)}>
                          <Trash2 size={14} />
                        </button>
                      </div>
                    ))
                  )}
                </div>
              </div>

              {/* Action Buttons */}
              <div style={{ display: "flex", gap: "var(--space-3)", justifyContent: "flex-end", borderTop: "1px solid var(--border)", paddingTop: "var(--space-4)", marginTop: "var(--space-2)" }}>
                <button type="button" className="btn btn-secondary btn-md" onClick={() => setFormOpen(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary btn-md">
                  {editingId ? "Save Changes" : "Create Enemy"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
