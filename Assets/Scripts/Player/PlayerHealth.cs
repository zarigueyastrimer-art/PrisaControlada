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
        public event Action<int> OnHealed;

        [Header("Audio - Hit")]
        [SerializeField] private AudioSource hitYouAudioSource;
        [SerializeField] private AudioClip hitYouClip;
        [SerializeField, Range(0f, 1f)] private float hitYouVolume = 1f;

        [Header("Audio - Poca Vida (<= 50)")]
        [SerializeField] private AudioSource pocaVidaAudioSource;
        [SerializeField] private AudioClip pocaVidaClip;
        [SerializeField, Range(0f, 1f)] private float pocaVidaVolume = 1f;

        [Header("Audio - Muy Poca Vida (<= 25)")]
        [SerializeField] private AudioSource muyPocaVidaAudioSource;
        [SerializeField] private AudioClip muyPocaVidaClip;
        [SerializeField, Range(0f, 1f)] private float muyPocaVidaVolume = 1f;

        private bool _warnedAt50;
        private bool _warnedAt25;

        private void Awake()
        {
            CurrentHealth = maxHealth;

            EnsureAudioSource(ref hitYouAudioSource, false);
            EnsureAudioSource(ref pocaVidaAudioSource, true);
            EnsureAudioSource(ref muyPocaVidaAudioSource, true);
        }

        private void EnsureAudioSource(ref AudioSource source, bool loop)
        {
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
        }

        private void OnEnable()
        {
            OnDamaged += OnHealthDamaged;
            OnHealed += OnHealthHealed;
        }

        private void OnDisable()
        {
            OnDamaged -= OnHealthDamaged;
            OnHealed -= OnHealthHealed;
        }

        private void OnHealthDamaged(int currentHealth)
        {
            PlayHitYou();
            UpdateLowHealthAudio(currentHealth);
        }

        private void OnHealthHealed(int currentHealth)
        {
            UpdateLowHealthAudio(currentHealth);
        }

        private void PlayHitYou()
        {
            if (hitYouAudioSource == null || hitYouClip == null) return;
            hitYouAudioSource.clip = hitYouClip;
            hitYouAudioSource.loop = false;
            hitYouAudioSource.volume = hitYouVolume;
            hitYouAudioSource.PlayOneShot(hitYouClip, hitYouVolume);
        }

        private void UpdateLowHealthAudio(int currentHealth)
        {
            bool playingPoca = pocaVidaAudioSource != null && pocaVidaAudioSource.isPlaying;
            bool playingMuyPoca = muyPocaVidaAudioSource != null && muyPocaVidaAudioSource.isPlaying;

            if (currentHealth <= 25)
            {
                if (playingPoca) pocaVidaAudioSource.Stop();
                if (!playingMuyPoca && muyPocaVidaClip != null)
                {
                    muyPocaVidaAudioSource.clip = muyPocaVidaClip;
                    muyPocaVidaAudioSource.loop = true;
                    muyPocaVidaAudioSource.volume = muyPocaVidaVolume;
                    muyPocaVidaAudioSource.Play();
                }
            }
            else if (currentHealth <= 50)
            {
                if (playingMuyPoca) muyPocaVidaAudioSource.Stop();
                if (!playingPoca && pocaVidaClip != null)
                {
                    pocaVidaAudioSource.clip = pocaVidaClip;
                    pocaVidaAudioSource.loop = true;
                    pocaVidaAudioSource.volume = pocaVidaVolume;
                    pocaVidaAudioSource.Play();
                }
            }
            else
            {
                if (playingPoca) pocaVidaAudioSource.Stop();
                if (playingMuyPoca) muyPocaVidaAudioSource.Stop();
            }
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
            OnHealed?.Invoke(CurrentHealth);
            Debug.Log($"[PlayerHealth] HP {CurrentHealth}/{maxHealth} (+{amount})");
        }

        private void Die()
        {
            Debug.Log("[PlayerHealth] JUGADOR MUERTO (HP 0).");
            OnDied?.Invoke();
        }
    }
}
