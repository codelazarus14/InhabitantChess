using InhabitantChess.BoardGame;
using InhabitantChess.Util;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    public class AudioEffects : MonoBehaviour
    {
        private InhabitantChess _instance;
        private BoardController _board;
        private PrisonerEffects _prisonerFX;
        private Dictionary<string, OWAudioSource> _audioSources;
        private List<OWAudioSource> _pieceSources;
        private List<AudioType> _furnitureNoises;
        private List<AudioType> _prisonerNoises;

        private void Start()
        {
            _instance = InhabitantChess.Instance;
            _board = _instance.BoardGame.GetComponentInChildren<BoardController>();

            OWAudioSource torchAudio = _instance.PrisonerSequence.TorchSocket.gameObject.AddComponent<OWAudioSource>();
            OWAudioSource lanternAudio = _instance.PrisonCell.FindChild("Props_PrisonCell/LowerCell/GhostLantern(Clone)/AudioSource_GhostLantern").GetComponent<OWAudioSource>();
            OWAudioSource playerAudio = Locator.GetPlayerAudioController()._oneShotExternalSource;
            OWAudioSource playerMusic = _instance.PrisonerSequence.PrisonerDirector._musicSource;
            _prisonerFX = _instance.PrisonerSequence.PrisonerDirector._prisonerEffects;

            _audioSources = new()
            {
                { nameof(torchAudio), torchAudio },
                { nameof(lanternAudio), lanternAudio },
                { nameof(playerAudio), playerAudio },
                { nameof(playerMusic), playerMusic }
            };

            _furnitureNoises = new()
            {
                AudioType.ModelShipImpact,
                AudioType.GearRotate_Heavy,
                AudioType.Door_SensorSliding_Loop,
                AudioType.Ghost_Footstep_Wood,
                AudioType.Prisoner_PickUpTorch,
                AudioType.NomaiDoorSlideBig_LP,
                AudioType.Sarcophagus_OpenFail,
            };

            _prisonerNoises = new()
            {
                AudioType.Ghost_Identify_Irritated,
                AudioType.Ghost_HuntFail
            };

            _instance.OnLeanForward += PlayLeanForward;
            _instance.OnLeanBackward += PlayLeanBack;
            _board.OnBoardInitialized += GetPieceSources;
            _board.OnPieceFinishedMoving += PlayPieceMoved;
            _instance.BoardGame.GetComponent<BoardGameController>().OnPieceRemoved += PlayPieceRemoved;
            _instance.PrisonerSequence.OnSpotlightTorch += PlayTorchSpotlight;
            _instance.PrisonerSequence.OnPrisonerCurious += PlayPrisonerCurious;
            _instance.PrisonerSequence.OnSetupGame += () => PlayFurnitureSounds(true);
            _instance.PrisonerSequence.OnCleanupGame += () => PlayFurnitureSounds(false);
        }

        private void OnDestroy()
        {
            _instance.OnLeanForward -= PlayLeanForward;
            _instance.OnLeanBackward -= PlayLeanBack;
            _board.OnBoardInitialized -= GetPieceSources;
            _board.OnPieceFinishedMoving -= PlayPieceMoved;
            _instance.BoardGame.GetComponent<BoardGameController>().OnPieceRemoved -= PlayPieceRemoved;
            _instance.PrisonerSequence.OnSpotlightTorch -= PlayTorchSpotlight;
            _instance.PrisonerSequence.OnPrisonerCurious -= PlayPrisonerCurious;
            _instance.PrisonerSequence.OnSetupGame -= () => PlayFurnitureSounds(true);
            _instance.PrisonerSequence.OnCleanupGame -= () => PlayFurnitureSounds(false);
        }

        private void GetPieceSources()
        {
            _pieceSources = new();
            foreach (var piece in _board.Pieces)
            {
                _pieceSources.Add(piece.g.AddComponent<OWAudioSource>());
            }
        }

        private void Play(OWAudioSource source, AudioType audio, float volume, float duration)
        {
            if (source != null)
            {
                if (source.isPlaying) source.Stop();
                source.AssignAudioLibraryClip(audio);
                source.SetLocalVolume(volume);
                source.Play();
                source.RandomizePlayhead();
                source.FadeOut(duration);
                Logger.Log($"Played audio {source._audioLibraryClip}");
            }
            else
            {
                Logger.LogError($"Couldn't find audio source {source}!");
            }
        }

        private void PlayOneShot(OWAudioSource source, AudioType audio)
        {
            if (source != null)
            {
                source.AssignAudioLibraryClip(audio);
                source.SetLocalVolume(1f);
                source.PlayOneShot(source._audioLibraryClip, 1f);
                Logger.Log($"Played oneshot {source._audioLibraryClip}");
            }
            else
            {
                Logger.LogError($"Couldn't find audio source {source}!");
            }
        }

        private void PlayTorchSpotlight()
        {
            PlayOneShot(_audioSources["torchAudio"], AudioType.ShipCockpitHeadlightsOn);
        }

        private void PlayFurnitureSounds(bool setup)
        {
            StartCoroutine(FurnitureChaosAudio(setup));
        }

        private IEnumerator FurnitureChaosAudio(bool setup)
        {
            List<AudioType> noises = new(_furnitureNoises);
            if (!setup) noises.Reverse();
            bool playedPrisonerNoise = false;

            foreach (AudioType type in noises)
            {
                PlayOneShot(_audioSources["playerAudio"], type);
                float randInterval = Random.Range(0.5f, 0.8f);
                yield return new WaitForSecondsRealtime(randInterval);

                if (!playedPrisonerNoise)
                {
                    playedPrisonerNoise = true;
                    int rIdx = (int)(randInterval * 10 % _prisonerNoises.Count);
                    PlayOneShot(_audioSources["playerAudio"], _prisonerNoises[rIdx]);
                }
            }
            PlayOneShot(_audioSources["lanternAudio"], AudioType.Artifact_Unconceal);
        }

        private void PlayPrisonerCurious()
        {
            _prisonerFX.PlayVoiceAudioNear(AudioType.Ghost_Identify_Curious);
        }

        private void PlayPieceMoved(int idx)
        {
            PlayOneShot(_pieceSources[idx], AudioType.MovementMetalFootstep);
        }

        private void PlayPieceRemoved(int idx)
        {
            PlayOneShot(_pieceSources[idx], AudioType.Artifact_Extinguish);
            GetPieceSources();
        }

        private void PlayLeanForward()
        {
            Play(_audioSources["playerAudio"], AudioType.TH_BridgeCreaking_LP, 0.8f, 2);
        }

        private void PlayLeanBack()
        {
            Play(_audioSources["playerAudio"], AudioType.TH_BridgeCreaking_LP, 0.8f, 2);
        }
    }
}
