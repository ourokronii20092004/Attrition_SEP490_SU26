namespace Attrition.Persistence
{
    /// <summary>
    /// Cờ tạm dừng game cho chế độ SOLO. Fusion physics chạy theo Runner.DeltaTime,
    /// KHÔNG quan tâm Time.timeScale — nên đặt timeScale=0 không dừng được quái/đạn.
    /// Các hệ thống mô phỏng (EnemyController, PlayerController, projectile...) tự đọc cờ này
    /// trong FixedUpdateNetwork và return sớm + đóng băng velocity khi IsPaused.
    ///
    /// COOP không bao giờ set cờ này (online — dừng sẽ phá đồng bộ).
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused;
    }
}
