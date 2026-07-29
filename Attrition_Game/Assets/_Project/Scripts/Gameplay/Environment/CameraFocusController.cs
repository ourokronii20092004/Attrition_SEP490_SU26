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
    ///
    /// Cutscene (lia QUA NHIỀU điểm) dùng bộ 3 hàm: Begin() → Point(pos) nhiều lần → End().
    /// Lý do tách: FocusOn tự trả camera về player sau mỗi lần gọi, nên gọi liên tiếp sẽ giật
    /// (player → điểm 1 → player → điểm 2) và Confiner2D bị tắt/bật lặp.
    /// </summary>
    public class CameraFocusController : MonoBehaviour
    {
        private static CameraFocusController _instance;

        private CinemachineCamera _cam;
        private Transform _focusTarget;     // target tạm để camera follow tới vị trí cần ngắm
        private Coroutine _running;

        // Trạng thái phiên Begin/End: nhớ confiner để trả lại đúng như trước.
        private CinemachineConfiner2D _heldConfiner;
        private bool _heldConfinerWas;
        private bool _holding;

        public static void FocusOn(Vector3 worldPos, float holdSeconds = 1.6f)
        {
            EnsureInstance();
            _instance.DoFocus(worldPos, holdSeconds);
        }

        /// <summary>Bắt đầu giữ camera (cutscene). Camera rời player tới khi End() được gọi.</summary>
        public static void Begin()
        {
            EnsureInstance();
            _instance.DoBegin();
        }

        /// <summary>Lia camera tới điểm mới. Chỉ có tác dụng giữa Begin() và End().</summary>
        public static void Point(Vector3 worldPos)
        {
            if (_instance == null || !_instance._holding) return;
            var cam = _instance._cam;
            if (cam == null) return;
            _instance._focusTarget.position = new Vector3(worldPos.x, worldPos.y, cam.transform.position.z);
        }

        /// <summary>
        /// Khoảng cách camera → điểm đang ngắm (để chờ camera lia tới).
        /// Trả 0 khi không giữ camera (không có Cinemachine trong scene) — coi như "đã tới", để
        /// cutscene chạy tiếp ngay thay vì chờ hết timeout ở TỪNG nhịp.
        /// </summary>
        public static float DistanceToPoint()
        {
            if (_instance == null || !_instance._holding || _instance._cam == null) return 0f;
            return Vector2.Distance(_instance._cam.transform.position, _instance._focusTarget.position);
        }

        /// <summary>Trả camera về player local + phục hồi Confiner2D. An toàn khi gọi nhiều lần.</summary>
        public static void End()
        {
            if (_instance == null || !_instance._holding) return;
            _instance.DoEnd();
        }

        private void DoBegin()
        {
            _cam = FindAnyObjectByType<CinemachineCamera>();
            if (_cam == null) return;

            // FocusOn đang chạy → dừng, tránh nó tự trả camera về player giữa cutscene.
            if (_running != null) { StopCoroutine(_running); _running = null; }

            _heldConfiner = _cam.GetComponent<CinemachineConfiner2D>();
            _heldConfinerWas = _heldConfiner != null && _heldConfiner.enabled;
            if (_heldConfiner != null) _heldConfiner.enabled = false;

            // Bắt đầu từ chính vị trí camera hiện tại → không giật ở beat đầu.
            _focusTarget.position = _cam.transform.position;
            _cam.Follow = _focusTarget;
            _holding = true;
        }

        private void DoEnd()
        {
            _holding = false;
            if (_cam != null)
            {
                var player = FindLocalPlayer();
                if (player != null) _cam.Follow = player;
            }
            if (_heldConfiner != null) _heldConfiner.enabled = _heldConfinerWas;
            _heldConfiner = null;
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
            // Cutscene đang giữ camera → bỏ qua. Nếu không, FocusRoutine sẽ trả camera về player
            // giữa cutscene (vd cửa mở trong lúc đang chiếu) và cắt ngang cảnh.
            if (_holding) return;

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
