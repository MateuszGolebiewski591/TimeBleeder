using UnityEngine;
using Unity.Netcode;

public class AudioManager : MonoBehaviour
{
    [Header("Variables")]
    [SerializeField] private float pitchVariation = 0.1f;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource backgroundMusicSource;
    [SerializeField] private AudioSource OneShotSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField] private AudioClip countDownSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip placeBlock;
    [SerializeField] private AudioClip destroyBlock;
    [SerializeField] private AudioClip playerMove;
    [SerializeField] private AudioClip startFight;
    [SerializeField] private AudioClip switchTurn;
    [SerializeField] private AudioClip timeStop;

    public static AudioManager Instance {get; set;}

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }
    void Start()
    {
        backgroundMusicSource.clip = backgroundMusicClip;
        backgroundMusicSource.Play();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayClockSoundRPC() {
        OneShotSource.PlayOneShot(countDownSound);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayDeathRPC() {
        OneShotSource.PlayOneShot(deathSound);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayPlaceBlockRPC() {
        OneShotSource.PlayOneShot(placeBlock);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayDestroyBlockRPC() {
        OneShotSource.PlayOneShot(destroyBlock);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayPlayerMoveRPC() {
        OneShotSource.PlayOneShot(playerMove);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayStartGameRPC() {
        OneShotSource.PlayOneShot(startFight);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlaySwitchRPC() {
        OneShotSource.PlayOneShot(switchTurn);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayTimeStopRPC() {
        OneShotSource.PlayOneShot(timeStop);
    }
}
