using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum SfxId
{
    // Player
    PlayerShoot,
    PlayerFall,
    PlayerHeal,
    PlayerShieldOn,
    PlayerHurt,
    PlayerRespawn,
    PlayerWalkLoop,

    // Boss
    BossSpawn,
    BossShoot,
    BossPhaseChange,
    BossDeath,
    CrystalHit
}

[System.Serializable]
public class SfxSetting
{
    public SfxId id;
    public AudioClip sfx;

    [Range(0f, 3f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    [Range(0f, 0.5f)] public float randomPitch = 0f;

    [Header("3D Setting")]
    public bool use3D = true;
    [Range(0f, 1f)] public float spatialBlend = 1f;
    [Tooltip("Logarithmic,近距離大聲，遠距離快速變. Linear, 從 Min Distance 到 Max Distance 直線變小 ")]
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    [Tooltip("在這個距離內，幾乎維持最大音量")]
    public float minDistance = 1.5f;
    [Tooltip("超過這個距離後，音量通常非常小")]
    public float maxDistance = 30f;

    [Header("Optional Mixer Group")]
    public AudioMixerGroup outputGroup;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Global Sources")]
    [SerializeField] private AudioSource bgmSource;     // 2D BGM
    [SerializeField] private AudioSource uiSfxSource;   // 2D UI

    [Header("Default Volumes")]
    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float uiSfxVolume = 1f;

    [Header("Sfx Settings")]
    [SerializeField] private SfxSetting[] sfxSettings;

    [SerializeField] private bool enableAudioDebugLog = true;

    // loop can play on target object
    private readonly Dictionary<Transform, AudioSource> attachedLoopSources = new Dictionary<Transform, AudioSource>();
    private readonly Dictionary<SfxId, SfxSetting> sfxMap = new Dictionary<SfxId, SfxSetting>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // DontDestroyOnLoad(gameObject);

        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
            bgmSource.spatialBlend = 0f; // BGM 2D
        }

        if (uiSfxSource != null)
        {
            uiSfxSource.volume = uiSfxVolume;
            uiSfxSource.spatialBlend = 0f; // UI 2D
        }

        BuildSfxMap();
    }

    private void BuildSfxMap()
    {
        sfxMap.Clear();
        if (sfxSettings == null) return;

        foreach (var s in sfxSettings)
        {
            if (s == null) continue;
            if (!sfxMap.ContainsKey(s.id))
                sfxMap.Add(s.id, s);
            else
                sfxMap[s.id] = s;
        }
    }

   private bool TryGetSfxSetting(SfxId id, out SfxSetting setting)
    {
        if (sfxMap.Count == 0)
        {
            BuildSfxMap();
            AudioLog($"BuildSfxMap called, count={sfxMap.Count}");
        }

        bool ok = sfxMap.TryGetValue(id, out setting);
        if (!ok)
        {
            AudioLog($"SfxId not found in map: {id}");
            return false;
        }

        if (setting == null)
        {
            AudioLog($"SfxSetting is null: {id}");
            return false;
        }

        if (setting.sfx == null)
        {
            AudioLog($"AudioClip is null for id: {id}");
            return false;
        }

        AudioLog($"SfxSetting OK: {id}, clip={setting.sfx.name}, vol={setting.volume}");
        return true;
    }

    // 2D play (UI or no spatial positioning)
   public void PlaySfx2D(SfxId id, float volumeScale = 1f)
    {
        AudioLog($"PlaySfx2D called: id={id}, volumeScale={volumeScale}");

        if (uiSfxSource == null)
        {
            AudioLog("uiSfxSource is NULL");
            return;
        }

        if (!TryGetSfxSetting(id, out var s)) return;

        float pitch = s.pitch + Random.Range(-s.randomPitch, s.randomPitch);
        uiSfxSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        uiSfxSource.outputAudioMixerGroup = s.outputGroup;
        uiSfxSource.PlayOneShot(s.sfx, Mathf.Clamp01(s.volume * volumeScale));

        AudioLog($"PlaySfx2D playing: {s.sfx.name}");
    }

    // 3D once: play at point (no follow)
    public void PlaySfxAtPoint(SfxId id, Vector3 worldPos, float volumeScale = 1f)
    {
        AudioLog($"PlaySfxAtPoint called: id={id}, pos={worldPos}");

        if (!TryGetSfxSetting(id, out var s)) return;

        GameObject go = new GameObject($"SFX_{id}");
        go.transform.position = worldPos;

        AudioSource src = go.AddComponent<AudioSource>();
        ConfigureSourceFromSetting(src, s, volumeScale, force3D: true, forceLoop: false);
        src.clip = s.sfx;
        src.Play();

        AudioLog($"PlaySfxAtPoint playing: {s.sfx.name}, src={go.name}");
        Destroy(go, s.sfx.length + 0.2f);
    }

    // 3D follow: play once on target object
    public void PlaySfxAttachedOnce(SfxId id, Transform target, float volumeScale = 1f)
    {
        AudioLog($"PlaySfxAttachedOnce called: id={id}, target={(target ? target.name : "NULL")}");

        if (target == null) return;
        if (!TryGetSfxSetting(id, out var s)) return;

        GameObject go = new GameObject($"SFX_{id}_Attached");
        go.transform.SetParent(target, false);
        go.transform.localPosition = Vector3.zero;

        AudioSource src = go.AddComponent<AudioSource>();
        ConfigureSourceFromSetting(src, s, volumeScale, force3D: true, forceLoop: false);
        src.clip = s.sfx;
        src.Play();

        AudioLog($"PlaySfxAttachedOnce playing: {s.sfx.name}, parent={target.name}");
        Destroy(go, s.sfx.length + 0.2f);
    }

    // 3D continuous loop (e.g. walking/engine) on target object
    public void StartLoopOnTarget(SfxId id, Transform target, float volumeScale = 1f)
    {
        if (target == null) return;
        if (!TryGetSfxSetting(id, out var s)) return;

        if (attachedLoopSources.TryGetValue(target, out var existing) && existing != null)
        {
            if (!existing.isPlaying || existing.clip != s.sfx)
            {
                ConfigureSourceFromSetting(existing, s, volumeScale, force3D: true, forceLoop: true);
                existing.clip = s.sfx;
                existing.Play();
            }
            return;
        }

        GameObject go = new GameObject($"Loop_{id}");
        go.transform.SetParent(target, false);
        go.transform.localPosition = Vector3.zero;

        AudioSource src = go.AddComponent<AudioSource>();
        ConfigureSourceFromSetting(src, s, volumeScale, force3D: true, forceLoop: true);
        src.clip = s.sfx;
        src.Play();

        attachedLoopSources[target] = src;
    }

    public void StopLoopOnTarget(Transform target)
    {
        if (target == null) return;
        if (!attachedLoopSources.TryGetValue(target, out var src)) return;

        if (src != null)
        {
            src.Stop();
            Destroy(src.gameObject);
        }

        attachedLoopSources.Remove(target);
    }

    public void PlayBgm(AudioClip clip, bool loop = true, float volume = -1f)
    {
        if (bgmSource == null || clip == null) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = (volume >= 0f) ? Mathf.Clamp01(volume) : bgmVolume;
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    private static void ConfigureSourceFromSetting(
        AudioSource src,
        SfxSetting s,
        float volumeScale,
        bool force3D,
        bool forceLoop)
    {
        src.playOnAwake = false;
        src.loop = forceLoop;
        src.volume = Mathf.Clamp01(s.volume * volumeScale);

        float pitch = s.pitch + Random.Range(-s.randomPitch, s.randomPitch);
        src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);

        src.outputAudioMixerGroup = s.outputGroup;
        src.rolloffMode = s.rolloffMode;
        src.minDistance = Mathf.Max(0.01f, s.minDistance);
        src.maxDistance = Mathf.Max(src.minDistance, s.maxDistance);

        src.spatialBlend = force3D ? 1f : s.spatialBlend;
    }

    private void AudioLog(string msg)
    {
        if (!enableAudioDebugLog) return;
        Debug.Log($"[AudioManager] {msg}");
    }
}