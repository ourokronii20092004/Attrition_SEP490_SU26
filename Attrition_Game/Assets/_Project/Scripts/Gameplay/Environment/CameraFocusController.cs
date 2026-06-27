using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Tạm thời di chuyển camera (Cinemachine) tới NGẮM một vị trí (vd cửa vừa mở), giữ vài giây,
    /// rồi TRẢ LẠI follow player local. Local thuần — mỗi máy tự ngắm camera của mình.
    ///
    /// Dùng: CameraFocusController.FocusOn(worldPos, holdSeconds). Tự tạo instance khi cần.
    /// </summary>
    public class CameraFocusController : MonoBehaviour
    {
        private static CameraFocusController _instance;

        private CinemachineCamera _cam;
        private Transform _focusTarget;     // target tạm để camera follow tới vị trí cần ngắm
        private Coroutine _running;

        public static void FocusOn(Vector3 worldPos, float holdSeconds = 1.6f)
        {
            EnsureInstance();
            _instance.DoFocus(worldPos, holdSeconds);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("CameraFocusController");
            _instance = go.AddComponent<CameraFocusController>();
            var ft = new GameObject("CameraFocusTarget");
            _instance._focusTarget = ft.transform;
        }

        private void DoFocus(Vector3 worldPos, float hold)
        {
            _cam = FindAnyObjectByType<CinemachineCamera>();
            if (_cam == null) return;
            if (_running != null) StopCoroutine(_running);
            _focusTarget.position = new Vector3(worldPos.x, worldPos.y, _cam.transform.position.z);
            _running = StartCoroutine(FocusRoutine(hold));
        }

        private IEnumerator FocusRoutine(float hold)
        {
            // Confiner2D (từ CameraZoomZone/CameraBoundsZone) giới hạn camera trong bounds room → có thể
            // CHẶN camera lia tới cửa. Tạm TẮT trong lúc focus, bật lại khi xong.
            var confiner = _cam.GetComponent<CinemachineConfiner2D>();
            bool confinerWas = confiner != null && confiner.enabled;
            if (confiner != null) confiner.enabled = false;

            // Ngắm cửa: chuyển Follow sang target tạm. Cinemachine tự lerp mượt tới đó.
            _cam.Follow = _focusTarget;

            // Chờ camera tới gần vị trí ngắm (tối đa 2.5s) rồi giữ thêm hold giây cho người chơi thấy.
            float t = 0f;
            while (t < 2.5f)
            {
                float d = Vector2.Distance(_cam.transform.position, _focusTarget.position);
                if (d < 0.6f) break;
                t += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(hold);

            // Trả camera về player local + bật lại confiner.
            var player = FindLocalPlayer();
            if (player != null) _cam.Follow = player;
            if (confiner != null) confiner.enabled = confinerWas;
            _running = null;
        }

        private Transform FindLocalPlayer()
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc != null && pc.HasInputAuthority) return pc.transform;
            return null;
        }
    }
}
