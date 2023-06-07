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
        private BoardGameController _gameController;
        private PrisonerEffects _prisonerFX;
        private Dictionary<string, OWAudioSource> _audioSources;
        private List<OWAudioSource> _pieceSources;
        private List<AudioType> _furnitureNoises;
        private List<AudioType> _prisonerNoises;
        private List<AudioType> _ambiences;
        private AudioType _currentAmbience;
        private float _initFadeOutTime, _initAmbienceTime, _ambienceInterval, _fadeDuration, _ambienceVolume = 0.05f, _creakVolume = 0.25f;
        private bool _playingAmbience;

        private void Start()
        {
            _instance = InhabitantChess.Instance;
            _board = _instance.BoardGame.GetComponentInChildren<BoardController>();
            _gameController = _instance.BoardGame.GetComponent<BoardGameController>();

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

            _ambiences = new()
            {
                AudioType.TH_Observatory,
                AudioType.SecretLibrary, // forbidden archives
                AudioType.Reel_3_Backdrop_A,
                AudioType.Reel_3_Backdrop_C,
                AudioType.Reel_3_Beat_B,
                AudioType.Reel_LibraryPath_Backdrop,
                AudioType.Reel_Secret_Beat_Tower_B
            };

            _instance.OnLeanForward += PlayLeanCreaking;
            _instance.OnLeanBackward += PlayLeanCreaking;
            _instance.OnSitDown += InitAmbience;
            _instance.OnStandUp += () => StopAmbience();
            _gameController.OnStartGame += InitAmbience;
            _gameController.OnStopGame += () => StopAmbience();
            _gameController.OnPieceRemoved += PlayPieceRemoved;
            _board.OnBoardInitialized += GetPieceSources;
            _board.OnPieceFinishedMoving += PlayPieceMoved;
            _instance.PrisonerSequence.OnSpotlightTorch += PlayTorchSpotlight;
            _instance.PrisonerSequence.OnPrisonerCurious += PlayPrisonerCurious;
            _instance.PrisonerSequence.OnSetupGame += () => PlayFurnitureSounds(true);
            _instance.PrisonerSequence.OnCleanupGame += () => PlayFurnitureSounds(false);

            enabled = false;
        }

        private void OnDestroy()
        {
            _instance.OnLeanForward -= PlayLeanCreaking;
            _instance.OnLeanBackward -= PlayLeanCreaking;
            _instance.OnSitDown -= InitAmbience;
            _instance.OnStandUp -= () => StopAmbience();
            _gameController.OnStartGame -= InitAmbience;
            _gameController.OnStopGame -= () => StopAmbience();
            _gameController.OnPieceRemoved -= PlayPieceRemoved;
            _board.OnBoardInitialized -= GetPieceSources;
            _board.OnPieceFinishedMoving -= PlayPieceMoved;
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

        private void PlayCreaking(OWAudioSource source, AudioType audio, float volume, float duration)
        {
            if (source != null)
            {
                if (source.isPlaying) source.Stop();
                source.AssignAudioLibraryClip(audio);
                source.SetLocalVolume(volume);
                source.Play();
                source.RandomizePlayhead();
                source.FadeOut(duration);
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

            // sequence of offscreen crashing and banging around
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

        private void PlayLeanCreaking()
        {
            PlayCreaking(_audioSources["playerAudio"], AudioType.TH_BridgeCreaking_LP, _creakVolume, 2);
        }

        private void Update()
        {
            if (!_playingAmbience && Time.time >= _initAmbienceTime)
            {
                StartNextAmbience();
            }
            else if (_playingAmbience && Time.time >= _initFadeOutTime)
            {
                StopAmbience(false);
            }
        }

        private void InitAmbience()
        {
            enabled = true;
            _ambienceInterval = 90;
            _fadeDuration = _ambienceInterval / 5;
            _initAmbienceTime = Time.time + _ambienceInterval;
        }

        private void StartNextAmbience()
        {
            OWAudioSource musicSource = _audioSources["playerMusic"];
            // avoid picking same track twice in a row
            int rIdx = Random.Range(0, _ambiences.Count - 1);
            _currentAmbience = _ambiences[rIdx];
            _ambiences[rIdx] = _ambiences[_ambiences.Count - 1];
            _ambiences[_ambiences.Count - 1] = _currentAmbience;

            _fadeDuration = Mathf.Min(20f, musicSource.clip.length / 4);
            musicSource.AssignAudioLibraryClip(_currentAmbience);
            musicSource.FadeIn(_fadeDuration, true, targetVolume: _ambienceVolume);
            // prepare for the next clip
            _ambienceInterval = 5 * _fadeDuration + 5 * Random.Range(0, _fadeDuration);
            float endTime = Time.time + musicSource.clip.length - musicSource.time;
            _initFadeOutTime = endTime - _fadeDuration;
            _initAmbienceTime = endTime + _ambienceInterval;

            _playingAmbience = true;
        }

        private void StopAmbience(bool endLoop = true)
        {
            OWAudioSource musicSource = _audioSources["playerMusic"];
            if (musicSource == null) return;

            float fadeTime = endLoop ? 5 : _fadeDuration;
            // replace other fades with new fade out
            if (musicSource._isLocalFading)
            {
                musicSource.Pause();
                musicSource.Play();
            }
            if (_playingAmbience)
            {
                musicSource.FadeOut(fadeTime);
            }

            _playingAmbience = false;
            enabled = !endLoop;
        }
    }
}
