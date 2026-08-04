# Hợp đồng dữ liệu cho Frontend — Co-op Rooms

Tài liệu này tả **những gì frontend đọc được** từ backend về tiến trình co-op: endpoint, kiểu dữ
liệu, cách bung 3 blob JSON, và các cạm bẫy đã gặp thật.

Phần văn xuôi tiếng Việt, còn **tên field / kiểu / giá trị giữ nguyên tiếng Anh** vì đó là chuỗi
thật đi trên dây — đừng dịch chúng.

> Chiều ngược lại (Unity host **ghi** lên server) nằm ở [SAVE_PAYLOAD_FORMAT.md](SAVE_PAYLOAD_FORMAT.md).
> Đọc file đó nếu cần biết *vì sao* một field có giá trị như vậy. File này chỉ nói *đọc ra được gì*.

---

## 1. Mô hình dữ liệu — đọc trước khi code

Ba khái niệm dễ lẫn:

| Khái niệm | Là gì | Bảng |
|---|---|---|
| **character** | Nhân vật thuộc về một user. Tên, archetype. | `character.characters` |
| **room** (session) | Một chuyến đi co-op. Có `roomCode` để bạn bè vào. | `character.sessions` |
| **character-session** | Tiến trình của **một nhân vật trong một room**. | `character.character_session` |

Điểm quan trọng nhất: **tiến trình gắn với cặp (nhân vật, room)**, không gắn với nhân vật. Cùng
một nhân vật ở 2 room khác nhau sẽ có level, đồ, vị trí khác nhau. Đừng cache tiến trình theo
`characterId` — luôn theo `characterId + sessionId`.

Fog-of-war, boss đã hạ, checkpoint đã mở là **của cả room**, không của từng người: co-op 2 người
chung một bản đồ.

Unity host là **nguồn chân lý duy nhất**. Web chỉ đọc. Không có endpoint nào cho web ghi tiến trình.

---

## 2. Endpoint

Base URL lấy từ `API_BASE` trong [src/lib/config.ts](Attrition_Web/frontend/src/lib/config.ts).
Auth qua **cookie** (`credentials: "include"`), tự refresh trong
[src/lib/api/client.ts](Attrition_Web/frontend/src/lib/api/client.ts) — không tự gắn header
`Authorization`, cứ dùng `apiFetch`.

| Method | Path | Trả về | Đã bọc sẵn |
|---|---|---|---|
| `GET` | `/api/sessions` | `SessionSummaryDto[]` | `sessionsApi.getMine()` |
| `GET` | `/api/sessions/{id}` | `SessionDetailDto` | `sessionsApi.get(id)` |

Wrapper: [src/lib/api/sessions.ts](Attrition_Web/frontend/src/lib/api/sessions.ts). Dùng nó, đừng
gọi `fetch` trực tiếp — bạn sẽ mất phần refresh token và parse lỗi.

**Server tự giới hạn theo người đăng nhập.** Room không phải của bạn trả `403`, nên **không có**
tham số `ownerId` — đừng thêm vào.

Mọi response bọc trong envelope:

```ts
interface ApiResponse<T> { success: boolean; data: T; error: string | null }
```

`success: false` thì `error` là chuỗi hiển thị được. Mẫu đang dùng khắp repo:

```ts
const { data: rooms = [], isPending } = useQuery({
  queryKey: qk.sessions.mine(),
  queryFn: async () => { const r = await sessionsApi.getMine(); return r.success ? r.data ?? [] : []; },
});
```

**Một request là đủ cho cả trang chi tiết.** `GET /api/sessions/{id}` đã trả kèm mọi nhân vật,
world state và fog. Đừng thêm request phụ cho từng nhân vật.

---

## 3. Kiểu dữ liệu

Đã khai báo sẵn trong [src/lib/types.ts](Attrition_Web/frontend/src/lib/types.ts) (mục
`Co-op rooms`). Đừng khai lại — import từ đó, để khi backend đổi thì TypeScript báo lỗi ở một chỗ.

JSON là **camelCase** (backend dùng `record` PascalCase, ASP.NET tự chuyển).

### 3.1 `SessionSummaryDto` — danh sách room

| Field | Kiểu | Ý nghĩa |
|---|---|---|
| `id` | `string` (GUID) | Khoá của room, dùng cho URL chi tiết |
| `ownerId` | `string` (GUID) | User tạo room (= host) |
| `roomCode` | `string` | Mã bạn bè nhập để vào. **Cố định**, không đổi giữa các lần chơi |
| `name` | `string` | Tên do người chơi đặt |
| `isMultiplayer` | `boolean` | `false` = chơi đơn |
| `playTimeSeconds` | `number` | Tổng thời gian chơi, cộng dồn |
| `currentScene` | `string \| null` | Tên scene Unity lúc lưu cuối |
| `createdAt` / `updatedAt` / `lastPlayedAt` | `string` (ISO 8601 UTC) | Sắp danh sách theo `lastPlayedAt` giảm dần |
| `characterCount` | `number` | Số nhân vật trong room, để hiện `1/2 players` |

`characterCount` là **đếm sẵn ở server**, không phải `characters.length` — summary không trả mảng
nhân vật. Muốn chi tiết thì phải gọi endpoint chi tiết.

### 3.2 `SessionDetailDto` — chi tiết room

Mọi field của summary, **trừ** `characterCount`, **cộng thêm**:

| Field | Kiểu | Ý nghĩa |
|---|---|---|
| `characters` | `CharacterSessionDto[]` | Tiến trình từng nhân vật |
| `worldStates` | `WorldStateDto[]` | Cờ tiến trình cả room (xem §4) |
| `fogJson` | `string \| null` | Ô bản đồ đã mở, JSON array (xem §5.3) |

### 3.3 `CharacterSessionDto` — tiến trình một nhân vật trong room

| Field | Kiểu | Ý nghĩa | Ghi chú |
|---|---|---|---|
| `characterId` | `string` (GUID) | | Khoá cùng với `sessionId` |
| `sessionId` | `string` (GUID) | | |
| `playerRole` | `number` | `0` = host, `1` = người vào sau | Sắp host lên trước |
| `name` | `string \| null` | Tên nhân vật | **Join** từ bảng `characters`. `null` = nhân vật đã bị xoá — phải fallback, đừng render `null` |
| `archetype` | `string \| null` | Lớp nhân vật | Cùng cảnh báo `null` như trên |
| `currentLevel` | `number` | | Nền tính điểm chưa dùng (§6) |
| `currentExp` | `number` | Exp trong level hiện tại | |
| `allocatedPointsJson` | `string \| null` | Điểm **tự cộng** | Mảng 7 số (§5.2). Khác với `ad`/`ap`/… bên dưới |
| `maxHp` / `currentHp` | `number` | | `currentHp` là lúc lưu, không phải lúc này |
| `maxMana` / `currentMana` | `number` | | |
| `maxStamina` | `number` | | **Không có** `currentStamina` — stamina hồi nhanh, không lưu |
| `potionMaxFlasks` | `number` | Số bình HP tối đa | Nâng ở chỗ nghỉ |
| `potionMaxManaFlasks` | `number` | Số bình mana tối đa | |
| `healthCharges` | `number` | Bình HP **còn lại** lúc lưu | Hiện dạng `healthCharges / potionMaxFlasks` |
| `manaCharges` | `number` | Bình mana **còn lại** | `manaCharges / potionMaxManaFlasks` |
| `attackSpeed` | `number` (float) | | |
| `ad` / `ap` / `def` / `res` | `number` | Chỉ số **cuối** đã gộp base + điểm cộng + đồ | Cả 4 bằng `0` = save từ bản game cũ → **ẩn đi**, đừng hiện `0` (§6.1) |
| `posX` / `posY` / `posZ` | `number` (float) | Vị trí lúc lưu | `posZ` gần như luôn `0` (game 2D) |
| `lastRestPointId` | `string \| null` | Chỗ nghỉ dùng cuối | Id thô, không phải tên đẹp |
| `inventoryJson` | `string \| null` | Túi đồ + đồ đang mặc | Blob (§5.1) |
| `equipmentJson` | `string \| null` | **Luôn `null`** | Unity không bao giờ gửi. Đồ đang mặc nằm trong `inventoryJson`. Đừng làm UI cho field này |
| `deathCount` | `number` | Số lần chết **trong room này** | Không phải tổng cả đời. Không reset khi hồi sinh |
| `updatedAt` | `string` (ISO) | Lần lưu cuối của nhân vật này | Có thể khác `session.updatedAt` |

`isAlive` **có** trong payload Unity gửi lên nhưng **không** trả về — đừng chờ nó.

### 3.4 `WorldStateDto`

| Field | Kiểu | Ý nghĩa |
|---|---|---|
| `eventId` | `string` | Khoá có tiền tố (§4) |
| `stateValue` | `number` (short) | Cờ trạng thái. `> 0` = đã xảy ra |
| `progress` | `number` | Bộ đếm, chỉ quest dùng |
| `updatedAt` | `string` (ISO) | |

---

## 4. `worldStates` — ba loại trong một mảng

Đây là chỗ dễ sai nhất. Backend nhồi 3 loại tiến trình khác nhau vào **một bảng**, phân biệt bằng
tiền tố `eventId`:

| Tiền tố | Loại | `stateValue` | `progress` |
|---|---|---|---|
| `q:` | Quest | Trạng thái quest | Bộ đếm |
| `cp:` | Checkpoint đã mở | `1` | không dùng |
| *(không có)* | Boss đã hạ | `1` | không dùng |

**Đừng tự parse.** Dùng helper có sẵn
[src/lib/world-state.ts](Attrition_Web/frontend/src/lib/world-state.ts):

```ts
import { splitWorldStates } from "@/lib/world-state";
const { quests, checkpoints, bosses } = splitWorldStates(room.worldStates);
```

Ba cái bẫy helper này đã xử lý, và là lý do đừng viết lại:

1. **Boss id có thể bắt đầu bằng chữ `q`.** `queen_moth` không phải quest. Phải khớp `"q:"` đủ hai
   ký tự, không phải `startsWith("q")`.
2. **`stateValue` phải kiểm `> 0`.** Có dòng tồn tại với `stateValue: 0` (đã ghi rồi lại hạ cờ).
   Coi nó là "đã hạ boss" thì hiện sai.
3. **Save từ bản game cũ không có tiền tố**, nên lần đọc đầu chúng lọt vào `bosses`. Tự hết sau
   lần lưu tiếp theo. Nếu thấy boss id lạ trên môi trường cũ thì đây là nguyên nhân, không phải bug
   frontend.

Có test ở [src/lib/world-state.test.ts](Attrition_Web/frontend/src/lib/world-state.test.ts). Sửa
helper thì chạy lại `npx vitest run`.

---

## 5. Ba blob JSON

Cả ba là **chuỗi chứa JSON**, không phải object lồng. Phải `JSON.parse`, và **luôn bọc try/catch** —
blob do client ghi, có thể hỏng, và một `throw` sẽ làm trắng cả trang.

### 5.1 `inventoryJson`

```jsonc
{
  "equipmentSlots":  [ { "itemId": "iron_sword", "amount": 1 }, {}, ... ],  // 40 ô
  "accessorySlots":  [ ... ],                                              // 10 ô
  "materialSlots":   [ ... ],                                              // 14 ô
  "equippedHead":      { "itemId": "leather_cap", "amount": 1 },
  "equippedChest":     { ... },
  "equippedLegs":      { ... },
  "equippedBoots":     { ... },
  "equippedSkill":     { ... },
  "equippedAccessory": { ... }
}
```

**Vị trí ô mã hoá bằng index trong mảng.** Mảng ghi **mọi ô, kể cả ô trống**, nên `equipmentSlots[7]`
đúng là ô số 8 trong game. Hệ quả trực tiếp:

> **Không được `.filter()` bỏ ô trống trước khi render.** Lọc là lệch toàn bộ lưới — mọi món đồ
> nhảy sang chỗ khác. Đây là bug đã có thật ở bản trước.

Ô trống là `{}` hoặc `{ "itemId": "" }` — kiểm cả hai (helper `isFilled` đã làm).

Sức chứa `40 / 10 / 14` khớp `[Capacity(n)]` bên Unity. Mảng ngắn hơn (save cũ) thì phần đuôi để
trống; dài hơn thì lấy `Math.max` để không mất đồ.

`itemId` tra vào catalog `ItemSO` của game. Web **không có** sprite atlas, nên hiện id + số lượng.

Đã có component: [src/components/inventory-view.tsx](Attrition_Web/frontend/src/components/inventory-view.tsx)
— dùng `<InventoryView json={char.inventoryJson} />`, đã lo lưới, ô trống, và blob hỏng.

### 5.2 `allocatedPointsJson`

Mảng 7 số, **thứ tự là hợp đồng** (khớp enum `StatType` bên Unity):

```json
[hp, mana, stamina, ad, ap, def, res]
```

Nhãn hiển thị ở `STAT_LABELS` trong [world-state.ts](Attrition_Web/frontend/src/lib/world-state.ts).
Đây **chỉ là điểm người chơi tự cộng**, không phải chỉ số cuối — chỉ số cuối nằm ở `ad`/`ap`/`def`/
`res` (§6.1). Dùng `parseAllocated(json)` — trả `[]` khi thiếu hoặc hỏng.

### 5.3 `fogJson`

Mảng chuỗi khoá `"scene:cellX:cellY"`:

```json
["Elf Valley -Map 3:12:-4", "Elf Valley -Map 3:13:-4"]
```

**Tên scene có thể chứa dấu hai chấm**, nên phải tách **từ phải sang**: hai đoạn cuối là toạ độ,
phần còn lại là tên scene. `parseFog(fogJson)` trả `Map<scene, số ô>` và đã xử lý đúng.

Đủ để hiện "đã khám phá bao nhiêu ô ở mỗi map". Vẽ minimap thật thì cần biên độ map — chưa có
trong payload, đừng cố đoán.

---

## 6. Tính ra, không lưu

Vài giá trị UI cần nhưng **không** có trong payload — phải tự tính:

| Giá trị | Công thức |
|---|---|
| Điểm chưa dùng | `(level - 1) * 5 - Σ allocatedPoints` → `unspentPoints(level, allocated)`, đã clamp `≥ 0` |
| Số người trong phòng | `characters.length` (chi tiết) hoặc `characterCount` (danh sách) |
| Thời gian chơi dạng đọc được | `formatPlaytime(playTimeSeconds)` trong [src/lib/format-duration.ts](Attrition_Web/frontend/src/lib/format-duration.ts) |
| "x phút trước" | Component `<RelativeTime />` |

`5` là `statPointsPerLevel` từ `LevelingConfig.asset` bên game. Game đổi thì `POINTS_PER_LEVEL`
trong `world-state.ts` phải đổi theo — không có cách nào phát hiện tự động.

### 6.1 `ad` / `ap` / `def` / `res` — ngoại lệ, và cách xử lý

Bốn field này là chỉ số **cuối cùng**: `base (theo class) + điểm tự cộng + đồ đang mặc`, đã cộng
xong bên game.

Chúng **không** tính ra được từ `allocatedPointsJson`. Web thiếu hai mảnh: chỉ số nền của class, và
bảng chỉ số của từng món đồ. Nên game gửi thẳng kết quả.

Hai điều cần nhớ khi làm UI:

1. **Cả bốn bằng `0` nghĩa là "không biết", không phải "bằng không".** Đó là dòng lưu bởi bản game
   cũ hơn field này. Nhân vật thật không bao giờ có `AD = 0`. Phải **ẩn** cả cụm:

   ```ts
   const hasCombatStats = c.ad > 0 || c.ap > 0 || c.def > 0 || c.res > 0;
   ```

   Tự hết sau lần lưu tiếp theo của room đó.

2. **Đây là ảnh chụp, không phải nguồn.** Game không bao giờ đọc ngược 4 field này — `PlayerStats`
   tự tính lại khi spawn. Nên giá trị cũ chỉ làm web hiện sai, **không** hỏng được nhân vật.
   `allocatedPointsJson` thì ngược lại: nó *là* nguồn, game đọc lại thật.

Hệ quả: nếu số trên web trông lạ, so với `allocatedPointsJson` — đó mới là dữ liệu chuẩn.

---

## 7. Không tồn tại — đừng làm UI

Đã kiểm, những field này **sẽ không bao giờ có dữ liệu**:

| Field | Lý do |
|---|---|
| `equipmentJson` | Luôn `null`. Đồ đang mặc nằm trong `inventoryJson` |
| `gold` | Game **không có** hệ thống tiền. Không có field này |
| `currentStamina` | Hồi quá nhanh, không đáng lưu. Chỉ có `maxStamina` |
| `slotIndex` | Vị trí ô mã hoá bằng index mảng (§5.1) |
| `isAlive` | Có ở chiều ghi, không trả về |
| Toạ độ boss / checkpoint | Chỉ có id chuỗi, không có vị trí |

---

## 8. Tham chiếu nhanh cho UI

Trang [/rooms](Attrition_Web/frontend/src/app/rooms/page.tsx) và
[/rooms/[id]](Attrition_Web/frontend/src/app/rooms/[id]/page.tsx) đã làm đúng những mục dưới — copy
mẫu ở đó, đừng dựng lại từ đầu.

Trang chi tiết đang hiện: tổ đội (host trước, sort theo `playerRole`), level/exp, HP/mana/stamina,
AD/AP/DEF/RES, bình thuốc còn lại, số lần chết, điểm đã cộng + điểm chưa dùng, túi đồ đúng vị trí,
boss đã hạ, chỗ nghỉ đã mở, fog theo từng map, quest.

Component dùng lại được: `PageShell`, `PageTitle`, `Card`, `EmptyState`, `SkeletonList`,
`Pagination` + `useClientPagination`, `InventoryView`, `SnapshotTimeline`, `RelativeTime`.

### Checklist khi thêm UI mới

- [ ] `name` / `archetype` có thể `null` — có fallback chưa?
- [ ] Blob đã bọc `try/catch` (hoặc dùng helper) chưa?
- [ ] Ô đồ **chưa** bị `.filter()` bỏ ô trống chứ?
- [ ] `worldStates` tách qua `splitWorldStates`, không tự `startsWith`?
- [ ] Đã kiểm `stateValue > 0`, không chỉ kiểm dòng có tồn tại?
- [ ] `ad`/`ap`/`def`/`res` all-zero đã ẩn, không hiện `0`?
- [ ] Điểm chưa dùng là tính ra, không đọc từ field?
- [ ] Vẫn **một** request cho trang chi tiết?
- [ ] Import type từ `lib/types.ts`, không khai lại?
