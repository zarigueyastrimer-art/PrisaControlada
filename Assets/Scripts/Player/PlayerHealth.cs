using System;
using UnityEngine;

namespace PrisaControlada.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 100;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsDead => CurrentHealth <= 0;

        public event Action<int> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnDamaged?.Invoke(CurrentHealth);
            Debug.Log($"[PlayerHealth] HP {CurrentHealth}/{maxHealth} (-{amount})");

            if (IsDead)
                Die();
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            Debug.Log($"[PlayerHealth] HP {CurrentHealth}/{maxHealth} (+{amount})");
        }

        private void Die()
        {
            Debug.Log("[PlayerHealth] JUGADOR MUERTO (HP 0). Solo debug: seguís moviéndote.");
            OnDied?.Invoke();
        }
    }
}