using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour {
    public static SoundManager Instance { get; private set; }

    [Header("BGM 기본 설정")]
    [SerializeField] float masterBgmVolume = 1.0f;
    [SerializeField] bool useCrossfade = true;
    [SerializeField] float crossfadeSeconds = 0.8f;

    [Header("씬별 BGM 매핑 (선택)")]
    [SerializeField] List<SceneBgmEntry> sceneBgms = new List<SceneBgmEntry>();

    AudioSource _bgmSourceA;
    AudioSource _bgmSourceB;
    bool _usingA = true;

    Dictionary<string, AudioClip> _map;

    [System.Serializable]
    public class SceneBgmEntry {
        public string sceneName;      // 예: "Title", "GameScene"
        public AudioClip clip;        // 해당 씬의 BGM
        [Range(0f, 1f)] public float volume = 1f;
    }

    // 게임 시작 시 자동 생성(씬에 프리팹을 놓지 않아도 됨)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() {
        if (Instance != null) return;
        var go = new GameObject("SoundManager");
        go.AddComponent<SoundManager>();
    }

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 두 개의 오디오소스로 크로스페이드 지원
        _bgmSourceA = gameObject.AddComponent<AudioSource>();
        _bgmSourceB = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { _bgmSourceA, _bgmSourceB }) {
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
        }

        // 인스펙터 매핑을 딕셔너리로
        _map = new Dictionary<string, AudioClip>();
        foreach (var e in sceneBgms) {
            if (!string.IsNullOrEmpty(e.sceneName) && e.clip != null)
                _map[e.sceneName] = e.clip;
        }

        // 현재 씬 즉시 재생
        PlayBgmForScene(SceneManager.GetActiveScene().name, instant: true);

        // 씬 로드 시마다 BGM 변경
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() {
        if (Instance == this) {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        PlayBgmForScene(scene.name);
    }

    /// <summary>
    /// 씬 이름에 맞는 BGM 재생(인스펙터 매핑 → Resources/BGM/씬이름 순으로 탐색)
    /// </summary>
    public void PlayBgmForScene(string sceneName, bool instant = false) {
        AudioClip clip = null;
        float volume = 1f;

        if (_map != null && _map.TryGetValue(sceneName, out var mapped)) {
            clip = mapped;
            // 인스펙터에서 개별 볼륨도 지원하고 싶다면 아래 검색
            var entry = sceneBgms.Find(e => e.sceneName == sceneName);
            if (entry != null) volume = Mathf.Clamp01(entry.volume);
        } else {
            // Resources/BGM/<SceneName> 자동 로드
            clip = Resources.Load<AudioClip>($"BGM/{sceneName}");
        }

        if (clip == null) {
            // 해당 씬에 지정된 BGM이 없으면 정지
            StopBgm();
            return;
        }

        PlayBgm(clip, volume, instant);
    }

    /// <summary>
    /// 지정된 클립 재생(크로스페이드 지원)
    /// </summary>
    public void PlayBgm(AudioClip clip, float volume = 1f, bool instant = false) {
        var current = _usingA ? _bgmSourceA : _bgmSourceB;
        var next = _usingA ? _bgmSourceB : _bgmSourceA;

        // 이미 같은 클립이면 스킵
        if (current.isPlaying && current.clip == clip) return;

        next.clip = clip;
        next.volume = 0f;
        next.pitch = 1f;
        next.loop = true;
        next.Play();

        float targetVol = Mathf.Clamp01(volume) * Mathf.Clamp01(masterBgmVolume);

        StopAllCoroutines();

        if (instant || !useCrossfade || crossfadeSeconds <= 0.01f) {
            current.Stop();
            next.volume = targetVol;
        } else {
            StartCoroutine(CrossfadeRoutine(current, next, targetVol, crossfadeSeconds));
        }

        _usingA = !_usingA;
    }

    public void StopBgm(bool fadeOut = true) {
        var current = _usingA ? _bgmSourceA : _bgmSourceB;
        if (!current.isPlaying) return;

        StopAllCoroutines();
        if (!fadeOut || crossfadeSeconds <= 0.01f) {
            current.Stop();
            current.volume = 0f;
        } else {
            StartCoroutine(FadeOutAndStop(current, crossfadeSeconds));
        }
    }

    IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, float toVol, float seconds) {
        float t = 0f;
        float startFrom = from.isPlaying ? from.volume : 0f;
        while (t < seconds) {
            t += Time.unscaledDeltaTime;
            float k = t / seconds;
            if (from.isPlaying) from.volume = Mathf.Lerp(startFrom, 0f, k);
            to.volume = Mathf.Lerp(0f, toVol, k);
            yield return null;
        }
        if (from.isPlaying) { from.Stop(); from.volume = 0f; }
        to.volume = toVol;
    }

    IEnumerator FadeOutAndStop(AudioSource src, float seconds) {
        float t = 0f;
        float start = src.volume;
        while (t < seconds) {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        src.Stop();
        src.volume = 0f;
    }

    // 전역 볼륨 제어 (옵션)
    public void SetMasterBgmVolume(float v) {
        masterBgmVolume = Mathf.Clamp01(v);
        var current = _usingA ? _bgmSourceA : _bgmSourceB;
        // 현재 트랙 볼륨을 비율로 재적용
        if (current.isPlaying) current.volume = masterBgmVolume;
    }
}
