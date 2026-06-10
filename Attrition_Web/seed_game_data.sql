-- ============================================================
-- Attrition — Seed dữ liệu quái (enemy.enemies + enemy.enemy_loot)
-- Khớp 1:1 với EnemyStatsSO trong game (Assets/_Project/Scripts/Data/Enemies).
-- tier: Normal (tier 0) / Elite (tier 1) / Boss.
-- Chạy: psql -d <db> -f seed_game_data.sql   (hoặc nạp qua docker exec).
-- Idempotent: ON CONFLICT cập nhật lại, chạy nhiều lần không nhân đôi.
-- ============================================================

INSERT INTO enemy.enemies
  ("EnemyId","Name","Tier","SpawnBiome","Hp","Ad","Ap","Def","Res","AttackSpeed","IsRanged","ExpReward","GoldReward","Lore","CreatedAt","UpdatedAt")
VALUES
  ('axe_demon',       'Axe Demon',        'Normal', 'The Darkest Path', 40, 10, 0,  4,  0, 1, false, 10, 5,  'A corrupted brute wielding a rusted axe.',            now(), now()),
  ('bat',             'Bat',              'Normal', 'The Darkest Path', 40, 10, 0,  0,  0, 1, false, 10, 3,  'A swarming cave dweller.',                            now(), now()),
  ('flying_demon',    'Flying Demon',     'Normal', 'The Darkest Path', 20, 10, 0,  0,  0, 1, false, 10, 4,  'A winged fiend that strikes from above.',             now(), now()),
  ('huntress',        'Huntress',         'Normal', 'Forest',           50, 10, 0,  0,  0, 1, false, 10, 6,  'A fallen ranger of the old wood.',                    now(), now()),
  ('huntress_bow',    'Huntress (Bow)',   'Normal', 'Forest',           40, 10, 0,  0,  0, 1, true,  10, 6,  'Looses spectral arrows from afar.',                   now(), now()),
  ('mimic',           'Mimic',            'Normal', 'Forest',           40, 10, 0,  0,  0, 1, false, 10, 8,  'A chest that hungers.',                               now(), now()),
  ('mushroom',        'Mushroom',         'Normal', 'Forest',           40, 10, 0,  0,  0, 1, false, 10, 4,  'Releases toxic spores when disturbed.',               now(), now()),
  ('rat',             'Rat',              'Normal', 'The Darkest Path', 20, 10, 0,  0,  0, 1, false, 10, 2,  'Vermin of the depths.',                               now(), now()),
  ('red_bat',         'Red Bat',          'Normal', 'The Darkest Path', 40, 10, 0,  0,  0, 1, false, 10, 3,  'A fiercer, blood-red cousin of the bat.',             now(), now()),
  ('skeleton_sword',  'Skeleton Swordsman','Normal','The Darkest Path', 30, 10, 0,  0,  0, 1, false, 10, 4,  'Risen bones clutching a chipped blade.',              now(), now()),
  ('slime',           'Slime',            'Normal', 'Forest',           30, 10, 0, 15,  0, 1, false, 10, 3,  'Gelatinous and stubbornly resilient.',                now(), now()),
  ('slime2',          'Slime (Greater)',  'Normal', 'Forest',           30, 10, 0, 15,  0, 1, false, 10, 3,  'A denser slime with a tougher hide.',                 now(), now()),
  ('the_dark',        'The Dark',         'Normal', 'The Darkest Path', 80, 10, 0,  5,  5, 1, false, 10, 10, 'A shadow given malevolent form.',                     now(), now()),
  ('summon_of_undead','Undead Summon',    'Normal', 'Crypt',            10,  0, 0,  0,  0, 1, false, 0,  0,  'A fragile thrall conjured by the Undead.',            now(), now()),
  -- ELITE (tier 1): có poise, EXP cao hơn
  ('crab',            'Armored Crab',     'Elite',  'Forest',          120, 10,10, 20, 10, 1, false, 50, 25, 'Its shell turns aside all but the heaviest blows.',   now(), now()),
  ('cultist',         'Cultist',          'Elite',  'Crypt',           120, 18, 0,  5,  0, 1, false, 50, 25, 'Chants forbidden rites in the dark.',                 now(), now()),
  ('frogger',         'Frogger',          'Elite',  'Forest',          120, 18, 0,  5,  0, 1, false, 50, 25, 'A bloated amphibian horror.',                         now(), now()),
  ('Gollux',          'Gollux',           'Elite',  'The Darkest Path',120, 18, 0, 20,  0, 1, false, 50, 30, 'A hulking guardian of iron and stone.',               now(), now()),
  ('nightborne',      'Nightborne',       'Elite',  'Crypt',           120, 18, 0,  5,  0, 1, false, 50, 28, 'Born of shadow, swift and merciless.',                now(), now()),
  ('undead',          'Undead',           'Elite',  'Crypt',           120, 18, 0,  5,  0, 1, false, 50, 25, 'Raises lesser thralls to overwhelm prey.',            now(), now())
ON CONFLICT ("EnemyId") DO UPDATE SET
  "Name" = EXCLUDED."Name", "Tier" = EXCLUDED."Tier", "SpawnBiome" = EXCLUDED."SpawnBiome",
  "Hp" = EXCLUDED."Hp", "Ad" = EXCLUDED."Ad", "Ap" = EXCLUDED."Ap",
  "Def" = EXCLUDED."Def", "Res" = EXCLUDED."Res", "AttackSpeed" = EXCLUDED."AttackSpeed",
  "IsRanged" = EXCLUDED."IsRanged", "ExpReward" = EXCLUDED."ExpReward",
  "GoldReward" = EXCLUDED."GoldReward", "Lore" = EXCLUDED."Lore", "UpdatedAt" = now();

-- ── Loot: elite rơi trang bị (khớp itemId trong ItemDatabase của game) ──
INSERT INTO enemy.enemy_loot
  ("Id","ItemName","Rarity","IconKey","DropChance","MinQty","MaxQty","EnemyId")
VALUES
  (1, 'Leather Helm',  'Common',   'leather_helm',  0.25, 1, 1, 'crab'),
  (2, 'Bronze Armor',  'Uncommon', 'bronze_chest',  0.15, 1, 1, 'cultist'),
  (3, 'Iron Boots',    'Uncommon', 'iron_boots',    0.15, 1, 1, 'frogger'),
  (4, 'Iron Armor',    'Rare',     'iron_chest',    0.10, 1, 1, 'Gollux'),
  (5, 'Gilded Helm',   'Rare',     'gold_helm',     0.08, 1, 1, 'nightborne'),
  (6, 'Vigor Charm',   'Uncommon', 'acc_stamina_charm', 0.20, 1, 1, 'undead')
ON CONFLICT ("Id") DO UPDATE SET
  "ItemName" = EXCLUDED."ItemName", "Rarity" = EXCLUDED."Rarity", "IconKey" = EXCLUDED."IconKey",
  "DropChance" = EXCLUDED."DropChance", "MinQty" = EXCLUDED."MinQty",
  "MaxQty" = EXCLUDED."MaxQty", "EnemyId" = EXCLUDED."EnemyId";
