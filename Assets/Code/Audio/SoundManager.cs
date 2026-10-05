using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windy.Srpg.Game.Chapters;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Audio
{
    /// <summary>Persistent, two-dimensional chapter music and overlapping one-shot effects.</summary>
    public sealed class SoundManager : MonoBehaviour
    {
        private static SoundManager instance;

        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField] private SoundLibrary soundLibrary;

        private AudioSource introSource;
        private AudioSource loopSource;
        private AudioSource sfxSource;
        private ChapterData currentChapter;

        public static SoundManager Instance
        {
            get
            {
                if (instance == null && Application.isPlaying) Bootstrap();
                return instance;
            }
        }

        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                if (introSource != null) introSource.volume = musicVolume;
                if (loopSource != null) loopSource.volume = musicVolume;
            }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            new GameObject("Sound Manager").AddComponent<SoundManager>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            introSource = CreateSource("BGM Intro");
            loopSource = CreateSource("BGM Loop");
            sfxSource = CreateSource("SFX");
            introSource.volume = musicVolume;
            loopSource.volume = musicVolume;
            soundLibrary ??= Resources.Load<SoundLibrary>("SoundLibrary");
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            instance = null;
        }

        private AudioSource CreateSource(string sourceName)
        {
            GameObject child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == SceneManager.GetActiveScene()) SelectSceneMusic(scene);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next) => SelectSceneMusic(next);

        private void SelectSceneMusic(Scene scene)
        {
            ChapterData chapter = null;
            if (scene.IsValid() && scene.isLoaded)
            {
                GameObject[] roots = scene.GetRootGameObjects();
                CellGrid grid = roots.SelectMany(root => root.GetComponentsInChildren<CellGrid>(true))
                    .FirstOrDefault();
                ChapterData[] chapters = roots.SelectMany(root => root.GetComponentsInChildren<ChapterData>(true))
                    .ToArray();
                chapter = chapters.FirstOrDefault(data => data != null && (grid == null || data.gameObject != grid.gameObject))
                    ?? chapters.FirstOrDefault(data => data != null);
            }
            if (chapter != null && ReferenceEquals(chapter, currentChapter)) return;

            currentChapter = chapter;
            PlayMusic(chapter?.BgmIntro, chapter?.BgmLoop);
        }

        public void PlayMusic(AudioClip intro, AudioClip loop)
        {
            introSource.Stop();
            loopSource.Stop();
            introSource.clip = intro;
            loopSource.clip = loop;
            introSource.loop = false;
            loopSource.loop = true;

            if (intro == null)
            {
                if (loop != null) loopSource.Play();
                return;
            }

            double startTime = AudioSettings.dspTime + 0.05d;
            introSource.PlayScheduled(startTime);
            if (loop != null)
            {
                double introDuration = (double)intro.samples / intro.frequency;
                loopSource.PlayScheduled(startTime + introDuration);
            }
        }

        public void StopMusic() => PlayMusic(null, null);

        public void PlaySfx(SoundCue cue)
        {
            soundLibrary ??= Resources.Load<SoundLibrary>("SoundLibrary");
            if (soundLibrary != null && soundLibrary.TryGet(cue, out AudioClip clip, out float volume))
                PlaySfx(clip, volume);
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip != null) sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * sfxVolume);
        }

        public void SetSoundLibrary(SoundLibrary library) => soundLibrary = library;
    }
}
