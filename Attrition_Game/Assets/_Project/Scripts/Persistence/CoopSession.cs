namespace Attrition.Persistence
{
    /// <summary>
    /// Trạng thái phiên coop dùng chung (không qua mạng — chỉ cờ cục bộ để UI/sim đọc).
    /// Host set khi client rời/đang chờ tải; GameUIController hiện overlay "Waiting" + GamePause.
    ///
    /// Luồng:
    ///   - Client rời giữa trận → host set WaitingForPlayer=true → pause + hiện Waiting.
    ///   - Client vào lại / cả hai đã tải xong → set false → resume.
    /// Coop rule (đã chốt): host KHÔNG drop về solo, mà CHỜ client quay lại (BR-32: giữ slot 5 phút).
    /// </summary>
    public static class CoopSession
    {
        /// <summary>True khi đang chờ 1 người chơi (client rời, hoặc chưa tải xong). Host điều khiển.</summary>
        public static bool WaitingForPlayer;

        /// <summary>Thông điệp hiện trên overlay Waiting.</summary>
        public static string WaitingMessage = "WAITING FOR PLAYER...";

        public static void BeginWaiting(string message = null)
        {
            WaitingForPlayer = true;
            if (!string.IsNullOrEmpty(message)) WaitingMessage = message;
            GamePause.IsPaused = true;
        }

        public static void EndWaiting()
        {
            WaitingForPlayer = false;
            GamePause.IsPaused = false;
        }

        /// <summary>Reset khi rời trận / về menu.</summary>
        public static void Reset()
        {
            WaitingForPlayer = false;
            WaitingMessage = "WAITING FOR PLAYER...";
        }
    }
}
