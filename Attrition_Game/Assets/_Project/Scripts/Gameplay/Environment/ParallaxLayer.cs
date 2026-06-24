using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Điều khiển parallax scrolling cho một lớp background.
    /// Gắn vào mỗi SpriteRenderer con trong ParallaxBackground.
    /// 
    /// parallaxFactor: 0 = đứng yên (rất xa), 1 = di chuyển cùng camera (rất gần).
    /// Sprite tự động lặp lại (tile) theo chiều ngang khi camera di chuyển.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Parallax Settings")]
        [Tooltip("0 = đứng yên (xa nhất), 1 = di chuyển cùng camera (gần nhất).")]
        [Range(0f, 1f)]
        public float parallaxFactor = 0.5f;

        [Tooltip("Bật lặp lại (tile) theo chiều ngang.")]
        public bool infiniteHorizontal = true;

        [Tooltip("Bám theo chiều dọc của Camera để không bao giờ lộ viền ngoài map.")]
        public bool followCameraY = true;

        private Transform _cam;
        private float _startPosX;
        private float _startPosY;
        private float _spriteWidth;

        private void Start()
        {
            if (Camera.main != null)
            {
                _cam = Camera.main.transform;
            }
            else
            {
                var fallbackCam = FindAnyObjectByType<Camera>();
                if (fallbackCam != null) _cam = fallbackCam.transform;
            }
            
            if (_cam == null)
            {
                Debug.LogWarning("[ParallaxLayer] Không tìm thấy Camera nào trong scene để follow!");
            }

            _startPosX = transform.position.x;
            _startPosY = transform.position.y;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                _spriteWidth = sr.sprite.bounds.size.x * transform.lossyScale.x;
            }
        }

        private void LateUpdate()
        {
            // Camera có thể chưa tồn tại lúc Start (Cinemachine/player spawn muộn qua mạng) → thử lấy lại.
            if (_cam == null)
            {
                var c = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
                if (c != null) _cam = c.transform;
                if (_cam == null) return;
            }

            // Parallax offset
            // parallaxFactor: 0 = xa nhất (cùng tốc độ camera), 1 = gần nhất (như mặt đất)
            float distFromStart = _cam.position.x * (1f - parallaxFactor);
            float remainderX = _cam.position.x * parallaxFactor;

            float targetY = followCameraY ? _cam.position.y + _startPosY : transform.position.y;

            transform.position = new Vector3(
                _startPosX + distFromStart,
                targetY,
                transform.position.z
            );

            // Infinite tiling: khi camera đi quá 1 sprite width, dịch startPos
            if (infiniteHorizontal && _spriteWidth > 0.01f)
            {
                if (remainderX > _startPosX + _spriteWidth)
                    _startPosX += _spriteWidth;
                else if (remainderX < _startPosX - _spriteWidth)
                    _startPosX -= _spriteWidth;
            }
        }
    }
}
