using System.Collections;
using UnityEngine;

namespace Attrition.Gameplay
{

    public class DruidBossSkill : MonoBehaviour
    {
        [Header("VFX Settings")]
        [SerializeField] private GameObject earthVfxPrefab; // Kéo 'EarthWaveVFX_Prefab' vào đây
        [SerializeField] private Transform spawnOrigin;     // Vị trí xuất phát của sóng (Ví dụ: Đặt ngay chân Boss)

        [Header("Wave Parameters")]
        [SerializeField] private int numberOfGai = 5;       // Số lượng gai đất muốn mọc lan ra (Độ dài của sóng)
        [SerializeField] private float stepDistance = 1.5f;  // Khoảng cách giữa các gai đất với nhau
        [SerializeField] private float timeBetweenGai = 0.1f;// Tốc độ lan truyền của sóng (càng nhỏ lan càng nhanh)

        private SpriteRenderer spriteRenderer;

        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // ==========================================
        // HÀM CHÍNH ĐƯỢC GỌI TỪ ANIMATION EVENT
        // ==========================================
        // Bạn đặt Event này tại đúng Frame gậy của Boss Druid đập xuống đất trong Druid_Attack.anim
        public void TriggerEarthWave()
        {
            // Kích hoạt Coroutine để xử lý việc sinh ra chuỗi sóng lan truyền
            StartCoroutine(SpawnWaveRoutine());
        }

        private IEnumerator SpawnWaveRoutine()
        {
            // Xác định hướng di chuyển của sóng dựa trên hướng Boss đang nhìn
            // Nếu dùng flipX để quay mặt: flipX = true nghĩa là nhìn sang Trái (-1), ngược lại nhìn sang Phải (1)
            float attackDirection = spriteRenderer.flipX ? -1f : 1f;

            Vector3 currentSpawnPosition = spawnOrigin.position;

            for (int i = 0; i < numberOfGai; i++)
            {
                // 1. Tính toán vị trí kế tiếp cho gai đất
                // Công thức: Vị trí cũ + (Hướng nhìn * Khoảng cách bước * Số thứ tự gai)
                float offsetX = attackDirection * stepDistance;
                currentSpawnPosition = new Vector3(spawnOrigin.position.x + (offsetX * i), spawnOrigin.position.y, spawnOrigin.position.z);

                // 2. Sinh ra gai đất tại vị trí đã tính
                if (earthVfxPrefab != null)
                {
                    GameObject vfxInstance = Instantiate(earthVfxPrefab, currentSpawnPosition, Quaternion.identity);

                    // Nếu Boss quay trái, lật ngược Sprite của VFX lại cho đúng hướng sóng cuộn
                    if (attackDirection < 0)
                    {
                        vfxInstance.transform.localScale = new Vector3(-1f, 1f, 1f);
                    }

                    // Tự động xóa gai này sau khi nó chạy xong animation (ví dụ 1.2 giây)
                    Destroy(vfxInstance, 1.2f);
                }

                // 3. Đợi một khoảng thời gian ngắn trước khi mọc cái gai tiếp theo (Tạo hiệu ứng sóng)
                yield return new WaitForSeconds(timeBetweenGai);
            }
        }
    }
}