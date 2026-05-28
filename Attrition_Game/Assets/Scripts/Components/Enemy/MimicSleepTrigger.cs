using UnityEngine;
using Attrition.Controllers;

/// <summary>
/// Gắn vào Child Object của Mimic enemy. Child cần có Collider2D với Is Trigger = true.
/// Khi Player chạm vào collider này, Mimic sẽ thức dậy (WakeUp).
/// Chỉ hoạt động khi EnemyAI có enableSleep = true và sleepWakeOnTouch = true.
/// LƯU Ý: Chỉ Host (StateAuthority) mới thay đổi được Networked state.
/// </summary>
public class MimicSleepTrigger : MonoBehaviour
{
    private EnemyAI aiComp;
    private EnemyController enemyController;

    private void Awake()
    {
        aiComp = GetComponentInParent<EnemyAI>();
        enemyController = GetComponentInParent<EnemyController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryWakeUp(other);
    }

    // Dùng OnTriggerStay2D làm fallback: nếu Player đã đứng trong trigger sẵn
    // thì OnTriggerEnter2D không fire → OnTriggerStay2D sẽ bắt được
    private void OnTriggerStay2D(Collider2D other)
    {
        TryWakeUp(other);
    }

    private void TryWakeUp(Collider2D other)
    {
        // Bỏ qua nếu quái đã chết
        if (enemyController != null && enemyController.IsDead) return;

        // Chỉ hoạt động khi enable sleep + wake on touch
        if (aiComp == null || !aiComp.enableSleep || !aiComp.sleepWakeOnTouch) return;

        // Chỉ thức dậy khi đang ngủ
        if (!aiComp.IsSleeping) return;

        // CHỈ Host (StateAuthority) mới được thay đổi Networked state
        if (!aiComp.HasStateAuthority) return;

        // Chỉ phản ứng với Player
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead) return;

        aiComp.WakeUpFromTouch();
    }
}
