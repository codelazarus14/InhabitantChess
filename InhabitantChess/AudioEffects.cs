using System.Collections.Generic;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    public class AudioEffects : MonoBehaviour
    {
        private Dictionary<string, OWAudioSource> _audioSources;

        private void Start()
        {
            InhabitantChess instance = InhabitantChess.Instance;
            OWAudioSource torchAudio = instance.PrisonerSequence.TorchSocket.gameObject.AddComponent<OWAudioSource>();
            _audioSources = new()
            {
                { nameof(torchAudio), torchAudio }
            };
        }
        private void PlayOneshot(string sourceName, AudioType audio)
        {
            var source = _audioSources[sourceName];
            if (source != null)
            {
                source.AssignAudioLibraryClip(audio);
                source.PlayOneShot(source._audioLibraryClip, 1f);
                Logger.Log($"Played oneshot {source._audioLibraryClip}");
            } 
            else
            {
                Logger.LogError($"Couldn't find audio source {sourceName}!");
            }
        }

        public void PlayTorchSpotlight()
        {
            PlayOneshot("torchAudio", AudioType.ShipCockpitHeadlightsOn);
        }
    }
}
