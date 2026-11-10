using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BepJam
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }
        private const float MusicVolume = 0.55f;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> missing = new HashSet<string>();
        private readonly List<AudioSource> loops = new List<AudioSource>();
        private readonly Dictionary<AudioSource, Coroutine> loopFades = new Dictionary<AudioSource, Coroutine>();
        private readonly HashSet<AudioSource> stopping = new HashSet<AudioSource>();
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly AudioSource[] shots = new AudioSource[12];
        private bool musicOn;
        private bool sfxOn;
        private bool ducked;
        private int nextShot;
        private int active;
        private string musicName;
        private Coroutine transition;
        private Coroutine gainFade;
        private float musicGain;
        private readonly float[] weights = new float[2];

        public static void Ensure()
        {
            if (I != null) return;
            new GameObject("AudioManager").AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            musicOn = PlayerPrefs.GetInt("bj_music", 1) != 0;
            sfxOn = PlayerPrefs.GetInt("bj_sfx", 1) != 0;
            musicGain = musicOn ? MusicVolume : 0f;
            for (int i = 0; i < music.Length; i++) music[i] = NewSource(true);
            for (int i = 0; i < shots.Length; i++) shots[i] = NewSource(false);
        }

        private AudioSource NewSource(bool loop)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        private AudioClip Clip(string name)
        {
            string key = name ?? "<null>";
            if (clips.TryGetValue(key, out AudioClip cached)) return cached;
            AudioClip clip = string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>("Audio/" + name);
            clips[key] = clip;
            if (clip == null && missing.Add(key)) Debug.LogWarning("BepJam audio clip missing: " + key, this);
            return clip;
        }

        public bool MusicOn
        {
            get => musicOn;
            set
            {
                if (musicOn == value) return;
                musicOn = value;
                PlayerPrefs.SetInt("bj_music", value ? 1 : 0);
                PlayerPrefs.Save();
                FadeGain();
            }
        }

        public bool SfxOn
        {
            get => sfxOn;
            set
            {
                if (sfxOn == value) return;
                sfxOn = value;
                PlayerPrefs.SetInt("bj_sfx", value ? 1 : 0);
                PlayerPrefs.Save();
                foreach (AudioSource src in shots) src.mute = !value;
                foreach (AudioSource src in loops) src.mute = !value;
            }
        }

        public void PlayMusic(string name, float fade = 0.8f)
        {
            if (musicName == name && music[active].isPlaying) return;
            AudioClip clip = Clip(name);
            if (clip == null) return;
            if (transition != null) StopCoroutine(transition);
            // Keep the louder voice when a new request interrupts a crossfade.
            int keep = weights[0] >= weights[1] ? 0 : 1;
            active = 1 - keep;
            music[active].Stop();
            weights[active] = 0f;
            music[active].volume = 0f;
            music[active].clip = clip;
            music[active].Play();
            musicName = name;
            transition = StartCoroutine(Transition(active, fade));
        }

        public void StopMusic(float fade = 0.5f)
        {
            musicName = null;
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Transition(-1, fade));
        }

        private IEnumerator Transition(int target, float seconds)
        {
            float a = weights[0], b = weights[1];
            float elapsed = 0f;
            do
            {
                elapsed += Time.unscaledDeltaTime;
                float t = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
                weights[0] = Mathf.Lerp(a, target == 0 ? 1f : 0f, t);
                weights[1] = Mathf.Lerp(b, target == 1 ? 1f : 0f, t);
                ApplyMusic();
                if (t >= 1f) break;
                yield return null;
            } while (true);
            for (int i = 0; i < 2; i++) if (i != target) music[i].Stop();
            transition = null;
        }

        private void ApplyMusic()
        {
            for (int i = 0; i < 2; i++) music[i].volume = weights[i] * musicGain;
        }

        public void DuckMusic(bool duck)
        {
            if (ducked == duck) return;
            ducked = duck;
            FadeGain();
        }

        private void FadeGain()
        {
            if (gainFade != null) StopCoroutine(gainFade);
            gainFade = StartCoroutine(ChangeGain());
        }

        private IEnumerator ChangeGain()
        {
            float start = musicGain;
            float target = musicOn ? MusicVolume * (ducked ? 0.35f : 1f) : 0f;
            float elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.unscaledDeltaTime;
                musicGain = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / 0.3f));
                ApplyMusic();
                yield return null;
            }
            gainFade = null;
        }

        public AudioSource StartLoop(string name, float volume = 0.6f)
        {
            AudioClip clip = Clip(name);
            if (clip == null) return null;
            AudioSource src = null;
            foreach (AudioSource candidate in loops)
                if (!candidate.isPlaying && !stopping.Contains(candidate)) { src = candidate; break; }
            if (src == null) { src = NewSource(true); loops.Add(src); }
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = 1f;
            src.mute = !sfxOn;
            src.Play();
            return src;
        }

        public void StopLoop(AudioSource src, float fade = 0.25f)
        {
            if (src == null || !loops.Contains(src)) return;
            if (loopFades.TryGetValue(src, out Coroutine routine)) StopCoroutine(routine);
            loopFades.Remove(src);
            if (fade <= 0f) { src.Stop(); src.clip = null; stopping.Remove(src); return; }
            stopping.Add(src);
            loopFades[src] = StartCoroutine(FadeLoop(src, fade));
        }

        private IEnumerator FadeLoop(AudioSource src, float seconds)
        {
            float start = src.volume;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                src.volume = start * (1f - Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            src.Stop();
            src.clip = null;
            stopping.Remove(src);
            loopFades.Remove(src);
        }

        internal void PlayShot(string name, float volume, float pitch)
        {
            if (!sfxOn) return;
            AudioClip clip = Clip(name);
            if (clip == null) return;
            AudioSource src = shots[nextShot];
            nextShot = (nextShot + 1) % shots.Length;
            src.Stop();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            src.mute = false;
            src.Play();
        }

        private void OnDestroy()
        {
            if (I == this) I = null;
        }
    }

    public static class Sfx
    {
        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            AudioManager.Ensure();
            AudioManager.I.PlayShot(name, volume, pitch);
        }

        public static void PlayVaried(string name, float volume = 1f, float pitchSpread = 0.08f)
        {
            float spread = Mathf.Abs(pitchSpread);
            Play(name, volume, 1f + Random.Range(-spread, spread));
        }
    }
}
