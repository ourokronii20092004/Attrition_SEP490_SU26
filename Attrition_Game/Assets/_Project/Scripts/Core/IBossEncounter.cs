namespace Attrition.Core
{
    /// <summary>
    /// Hợp đồng chung cho AI boss có "encounter" (chờ trigger → vào trận → reset sau khi team wipe).
    ///
    /// VÌ SAO CẦN: trước đây `BossEncounterTrigger.boss` và `BossGateController.bossAI` khai kiểu CỨNG là
    /// `SeveredFangAI`, còn `BossController.EncounterActive` + `PlayerController.ResetLivingBossesAfterWipe`
    /// phải `GetComponent` từng loại AI rồi if-else. Với 2 boss đã là 2 nhánh; thêm Elf/DemonKin/ArchDemon
    /// sẽ thành 5 nhánh Ở MỖI CHỖ, và boss mới KHÔNG gán được vào ô Inspector của trigger/gate.
    ///
    /// Đặt ở Attrition.Core vì Gameplay (AI boss) và Environment (gate/trigger) đều tham chiếu được Core,
    /// còn Core thì không phụ thuộc ai — tránh vòng lặp assembly.
    ///
    /// Unity Inspector: dùng `[SerializeReference]` KHÔNG cần thiết ở đây — gate/trigger giữ tham chiếu
    /// dạng `UnityEngine.Object` rồi cast sang interface này (xem BossEncounterTrigger.boss).
    /// </summary>
    public interface IBossEncounter
    {
        /// <summary>Đã vào trận chưa (player đã kích hoạt). Dùng để ẩn/hiện thanh máu boss.</summary>
        bool EncounterStarted { get; }

        /// <summary>Boss còn đang đứng chờ trigger? Gate đọc để biết có nên khoá cửa vào hay chưa.</summary>
        bool IsWaitingForTrigger { get; }

        /// <summary>Player đã vào phòng → bắt đầu intro + state machine. Chỉ host gọi.</summary>
        void StartIntroSequence();

        /// <summary>Cả team chết mà boss còn sống → trả boss về trạng thái chờ trigger. Chỉ host gọi.</summary>
        void ResetEncounter();
    }
}
