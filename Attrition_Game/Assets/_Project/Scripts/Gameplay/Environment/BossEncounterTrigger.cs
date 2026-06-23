using UnityEngine;
using Attrition.Gameplay.Enemy.SeveredFang;
using Attrition.Gameplay.Player;
using System.Collections.Generic;

namespace Attrition.Gameplay.Environment
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class BossEncounterTrigger : MonoBehaviour
    {
        public SeveredFangAI boss;
        
        private bool _isTriggered;
        private HashSet<PlayerController> _playersInTrigger = new HashSet<PlayerController>();

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isTriggered) return;

            if (other.CompareTag("Player"))
            {
                var player = other.GetComponentInParent<PlayerController>();
                if (player != null && !_playersInTrigger.Contains(player))
                {
                    _playersInTrigger.Add(player);
                    CheckTrigger();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_isTriggered) return;

            if (other.CompareTag("Player"))
            {
                var player = other.GetComponentInParent<PlayerController>();
                if (player != null && _playersInTrigger.Contains(player))
                {
                    _playersInTrigger.Remove(player);
                }
            }
        }

        private void CheckTrigger()
        {
            var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            int requiredPlayers = 1;

            if (Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop)
            {
                int activePlayers = 0;
                foreach (var p in allPlayers)
                {
                    if (p != null && !p.isDeadNetworked) activePlayers++;
                }
                requiredPlayers = Mathf.Max(1, activePlayers);
            }

            if (_playersInTrigger.Count >= requiredPlayers)
            {
                _isTriggered = true;
                if (boss != null)
                {
                    boss.StartIntroSequence();
                }
            }
        }
    }
}
