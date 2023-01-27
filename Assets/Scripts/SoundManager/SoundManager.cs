// ISTA 425 / INFO 525 Algorithms for Games
//
// Sample code file

using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public enum SoundType
    {
        Cast,
        Death,
        Fall,
        Hack,
        Jump,
        Move,
        Raven,
        Fire,
        Boom,
        Night,
        None
    }

    // metadata on the properties of each sound effect
    [System.Serializable]
    public class SoundEffect
    {
        [Tooltip("Type of sound effect")]
        public SoundType soundType;

        [Tooltip("Audio clip to be used for this effect")]
        public AudioClip audioClip;

        [Tooltip("Minimum time between sound instances (sec)")]
        public float length  = 0.0f;

        [Tooltip("Starting volume of sound effect")]
        public float volume  = 1.0f;
        [Tooltip("Starting pitch of sound effect")]
        public float pitch   = 1.0f;

        [Tooltip("Are multiple instances allowed?")]
        public bool multiple = true;
    }

    // metadata about the last active instance of the sound effect
    private class SoundHistory
    {
        public SoundType   soundType;
        public AudioSource lastSource;
        public float       lastPlay;

        // used by the audio fader system
        public float       fadeTime;
        public float       startVolume;

        public SoundHistory ()
        {
            Reset();
        }

        public void Reset ()
        {
            soundType   = SoundType.None;
            lastSource  = null;
            lastPlay    = -1.0f;
            fadeTime    = -1.0f;
            startVolume = -1.0f;
        }
    }

    private class AudioPoolData
    {
        public GameObject  audioObject = null;
        public AudioSource audioSource = null;
        
        // constructor method
        public AudioPoolData(int id)
        {
            audioObject = new GameObject("SoundSource" + id);
            audioSource = audioObject.AddComponent<AudioSource>();
        }
    }

    [Tooltip("Maximum number of simultaneous audio sources")]
    public int numSoundSources = 8;

    [Tooltip("Array of sound effects assigned to sound types")]
    public SoundEffect[] soundEffectArray;

    private static SoundManager inst = null;
    
    private Dictionary<SoundType, SoundEffect>  soundEffectDictionary;
    private Dictionary<SoundType, SoundHistory> soundEffectHistory;

    // the index of the last source used in the sound pool
    private int lastSource = 0;

    private AudioPoolData[] soundPool;
    private AudioSource     defSource;

    // sounds scheduled for fade out
    private List<SoundHistory> fadeList;

    public static SoundManager Instance
    {
        get
        {
            if (inst == null)
            {
                inst = Instantiate(Resources.Load<SoundManager>("Managers/SoundManager"));
                inst.Initialize();
            }
            return inst;
        }
    }

    private void Initialize()
    {
        // initialize the sound effect dictionary
        soundEffectDictionary = new Dictionary<SoundType, SoundEffect> ();
        soundEffectHistory    = new Dictionary<SoundType, SoundHistory>();

        foreach (SoundEffect effect in soundEffectArray)
        {
            soundEffectDictionary [effect.soundType] = effect;
            soundEffectHistory    [effect.soundType] = new SoundHistory ();
        }

        // the default audio settings.
        defSource = new AudioSource();

        // initialize the audio source pool
        soundPool = new AudioPoolData[numSoundSources];
        for (int i = 0; i < numSoundSources; i++)
        {
            soundPool[i] = new AudioPoolData(i);
        }

        fadeList = new List<SoundHistory>();

        //Debug.Log("Initializing");
    }

    private bool getNextSource(out int nextSource)
    {
        bool empty = false;
        nextSource = lastSource;

        // cycle through the pool until we find the next 
        // available slot, or return false
        for (int i = 0; i < numSoundSources && !empty; i++)
        {
            nextSource = (lastSource + i) % numSoundSources;
            if (!soundPool[nextSource].audioSource.isPlaying)
                empty = true;
        }
        return empty;
    }

    private void ResetAudioSource (ref AudioSource source, Vector3 position, bool is3D)
    {
        // source is non-positional when vector is at infinity.
        if (is3D)
        {
            source.transform.position = position;

            // spatial audio properties
            source.maxDistance  = 100f;
            source.spatialBlend = 1f;
            source.rolloffMode  = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
        }
        else // 2D
        {
            source.transform.position = Vector3.zero;
          
            // default audio properties
            source.maxDistance  = 500.0f;
            source.spatialBlend = 0.0f;
            source.rolloffMode  = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 1.0f;
        }
    }

    private SoundEffect GetSoundEffect(SoundType soundType)
    {
        SoundEffect effect = null;
        if (soundEffectDictionary.ContainsKey(soundType))
            effect = soundEffectDictionary[soundType];

        if (effect == null)
            Debug.LogError("Sound " + soundType + " not found.");

        return effect;
    }

    private void UpdateFaders ()
    {
        List<SoundHistory> purgeList = new List<SoundHistory> ();

        bool remove;
        foreach (SoundHistory hist in fadeList)
        {
            remove = false;

            // verify the sound is still playing
            if (IsPlaying(hist.soundType))
            {
                hist.lastSource.volume -= hist.startVolume * Time.deltaTime / hist.fadeTime;

                if (hist.lastSource.volume <= 0.0f)
                {
                    StopSound(hist.soundType);
                    remove = true;
                }
            }
            else
                remove = true;

            if (remove)
                purgeList.Add (hist);
        }

        // now purge all obsolete histories from list (faded/stopped sounds)
        foreach (SoundHistory hist in purgeList)
            fadeList.Remove(hist);

        purgeList.Clear();
    }

    public bool IsPlaying (SoundType sound)
    {
        bool playing = false;

        if (soundEffectHistory.ContainsKey(sound))
        {
            // retrive the last audio source playing this sound.
            AudioSource source = soundEffectHistory[sound].lastSource;

            if (source != null && source.isPlaying)
            {
                // verify this audio source is still playing the
                // clip that we want to be stopped (not already
                // reassigned to something else
                if (source.clip == soundEffectDictionary[sound].audioClip)
                    playing = true;
            }
        }
        return playing;
    }

    public bool CanPlay(SoundType sound)
    {
        bool canPlay = true;

        if (IsPlaying(sound))
        {
            bool allowMult = soundEffectDictionary[sound].multiple;
            float reqTime = soundEffectDictionary[sound].length;

            if (!allowMult && reqTime > 0.0f)
            {
                float lastTimePlayed = soundEffectHistory[sound].lastPlay;

                // If enough time has elapsed since the last
                // time of play, set this new value. Otherwise
                // deny the play request.
                // Note: last = -1 is the init state of dict
                if (lastTimePlayed != -1.0 && lastTimePlayed + reqTime > Time.time)
                    canPlay = false;
            }
        }

        return canPlay;
    }

    public void PlaySound (SoundType sound, bool loop = false)
    {
        PlaySound(sound, Vector3.positiveInfinity, loop, false);
    }

    public void PlaySound (SoundType sound, Vector3 position, bool loop = false, bool is3D = true)
    {
        int nextSource;
        if (CanPlay (sound) && getNextSource(out nextSource))
        {
            SoundEffect effect = GetSoundEffect(sound);
            if (effect != null)
            {
                AudioClip clip = effect.audioClip;
                if (clip != null)
                {
                    AudioSource poolSource = soundPool[nextSource].audioSource;

                    // reset 2D/3D audio parameters
                    ResetAudioSource(ref poolSource, position, is3D);

                    // setup source parameters
                    poolSource.volume = effect.volume;
                    poolSource.pitch  = effect.pitch;
                    poolSource.loop   = loop;
                    poolSource.clip   = clip;

                    // reset the audio history
                    soundEffectHistory[sound].Reset();

                    // stup the history metadata
                    soundEffectHistory[sound].soundType   = sound;
                    soundEffectHistory[sound].lastPlay    = Time.time;
                    soundEffectHistory[sound].lastSource  = poolSource;

                    if (loop || !effect.multiple)
                        poolSource.Play();
                    else
                        poolSource.PlayOneShot(clip);

                    lastSource = nextSource;

                    //Debug.Log("Audio pool object ID " + nextSource + " at " + poolSource.transform.position);
                }
            }
        }
    }
    
    // Note Time is desired fade time, given in seconds.
    public void FadeOut(SoundType sound, float time)
    {
        if (soundEffectHistory.ContainsKey(sound))
        {
            SoundHistory hist = soundEffectHistory[sound];

            if (!fadeList.Contains(hist))
            {
                hist.fadeTime    = time;
                hist.startVolume = hist.lastSource.volume;
                fadeList.Add(hist);
            }
        }
    }

    public void StopSound(SoundType sound)
    {
        if (soundEffectHistory.ContainsKey (sound))
        {
            // retrive the last audio source playing this sound.
            AudioSource source = soundEffectHistory[sound].lastSource;

            // Note: PlayOneShot cannot be 'stopped'
            if (IsPlaying(sound) && !soundEffectDictionary[sound].multiple)
            {
                source.Stop();

                // play is complete: reset history metadata
                soundEffectHistory[sound].Reset();
            }
            //else
            //    Debug.Log("Note: Sound not playing or attempting to stop PlayOneShot");
        }
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        UpdateFaders();
    }
}

