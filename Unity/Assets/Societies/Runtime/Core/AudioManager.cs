using UnityEngine;

namespace Societies.Runtime.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource _audioSource;

        private void Awake()
        {
            Instance = this;
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        public void PlaySound(string soundName)
        {
            // MVP: just log for now
            Debug.Log("Sound: " + soundName);
        }

        public void PlayFootstep()
        {
            PlaySound("footstep");
        }

        public void PlayMine()
        {
            PlaySound("mine");
        }

        public void PlayPlace()
        {
            PlaySound("place");
        }

        public void PlayCraft()
        {
            PlaySound("craft");
        }
    }
}
