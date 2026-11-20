using UnityEngine;

namespace SlotGame.Audio
{
    /// <summary>
    /// Tiny sound facade: one AudioSource for one-shots, one for the looping reel whirr.
    /// Every method is null-safe so the game still runs if a clip is not assigned.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioClip spinLoop;
        [SerializeField] private AudioClip reelStop;
        [SerializeField] private AudioClip lever;
        [SerializeField] private AudioClip click;
        [SerializeField] private AudioClip win;
        [SerializeField] private AudioClip jackpot;
        [SerializeField] private AudioClip bonus;
        [SerializeField] private AudioClip gameOver;

        private AudioSource oneShotSource;
        private AudioSource loopSource;

        public bool IsMuted { get; private set; }

        private void Awake()
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;

            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            loopSource.volume = 0.5f;
        }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            AudioListener.volume = muted ? 0f : 1f;
        }

        public void PlayLever() => Play(lever);
        public void PlayClick() => Play(click);
        public void PlayReelStop() => Play(reelStop);
        public void PlayWin() => Play(win);
        public void PlayJackpot() => Play(jackpot);
        public void PlayBonus() => Play(bonus);
        public void PlayGameOver() => Play(gameOver);

        public void StartSpinLoop()
        {
            if (spinLoop == null) return;
            loopSource.clip = spinLoop;
            loopSource.Play();
        }

        public void StopSpinLoop()
        {
            if (loopSource.isPlaying) loopSource.Stop();
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) oneShotSource.PlayOneShot(clip);
        }
    }
}
