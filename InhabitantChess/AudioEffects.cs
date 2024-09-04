using InhabitantChess.BoardGame;
using InhabitantChess.Util;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InhabitantChess
{
    public class AudioEffects : MonoBehaviour
    {
        public delegate void FurnitureAudioEvent(bool isSetup);
        public FurnitureAudioEvent OnFurnitureAudioFinished;

        private static AudioType[] s_furnitureNoises =
        [
            AudioType.ModelShipImpact,
            AudioType.GearRotate_Heavy,
            AudioType.Door_SensorSliding_Loop,
            AudioType.Ghost_Footstep_Wood,
            AudioType.Prisoner_PickUpTorch,
            AudioType.NomaiDoorSlideBig_LP,
            AudioType.Sarcophagus_OpenFail
        ];

        private static AudioType[] s_prisonerNoises =
        [
            AudioType.Ghost_Identify_Irritated,
            AudioType.Ghost_HuntFail
        ];

        private static AudioType[] s_ambiences =
        [
            AudioType.TH_Observatory,
            AudioType.SecretLibrary, // forbidden archives
            AudioType.Reel_3_Backdrop_A,
            AudioType.Reel_3_Backdrop_C,
            AudioType.Reel_3_Beat_B,
            AudioType.Reel_LibraryPath_Backdrop,
            AudioType.Reel_Secret_Beat_Tower_B
        ];
        private AudioType _currentAmbience;

        private struct ICAudioSources
        {
            public OWAudioSource playerAudio;
            public OWAudioSource playerMusic;
            public OWAudioSource torchAudio;
            public OWAudioSource lanternAudio;
        }
        private ICAudioSources _audioSources;

        private OWAudioSource[] _pieceSources;
        private ChessGame _game;
        private BoardGameController _gameController;
        private BoardController _board;
        private PrisonerEffects _prisonerFX;

        private const float AmbienceVolume = 0.05f, CreakVolume = 0.25f;
        private float _initFadeOutTime, _initAmbienceTime, _ambienceInterval, _fadeDuration;
        private bool _playingAmbience;

        private InhabitantChess InhabitantChess => InhabitantChess.Instance;
        private PrisonerSequence PrisonerSequence => InhabitantChess.Instance.PrisonerSequence;

        private void Start()
        {
            _audioSources = new ICAudioSources { };

            InitBoardGameSFX();
            if (PrisonerSequence != null)
                InitPrisonerSequenceSFX();

            enabled = false;
        }

        private void InitBoardGameSFX()
        {
            _game = GetComponent<ChessGame>();
            _gameController = GetComponent<BoardGameController>();
            _board = GetComponentInChildren<BoardController>();
            _audioSources.playerAudio = Locator.GetPlayerAudioController()._oneShotExternalSource;

            _game.OnLeanForward += PlayLeanCreaking;
            _game.OnLeanBackward += PlayLeanCreaking;
            _gameController.OnStopGame += PlayGameOver;
            _gameController.OnPieceRemoved += PlayPieceRemoved;
            _board.OnBoardReset += GetPieceSources;
            _board.OnPieceFinishedMoving += PlayPieceMoved;
        }

        private void InitPrisonerSequenceSFX()
        {
            _prisonerFX = PrisonerSequence.PrisonerDirector._prisonerEffects;
            _audioSources.torchAudio = PrisonerSequence.TorchSocket.gameObject.AddComponent<OWAudioSource>();
            _audioSources.lanternAudio = PrisonerSequence.gameObject.FindChild("Props_PrisonCell/LowerCell/GhostLantern(Clone)/AudioSource_GhostLantern").GetComponent<OWAudioSource>();
            _audioSources.playerMusic = PrisonerSequence.PrisonerDirector._musicSource;

            _game.OnSitDown += OnSitDown;
            _game.OnStoodUp += OnStoodUp;
            PrisonerSequence.OnSpotlightTorch += PlayTorchSpotlight;
            PrisonerSequence.OnPrisonerCurious += PlayPrisonerCurious;
            PrisonerSequence.OnSetupGame += OnSetupGame;
            PrisonerSequence.OnCleanupGame += OnCleanupGame;
            OnFurnitureAudioFinished += PrisonerSequence.OnFurnitureAudioFinished;
        }

        private void OnDestroy()
        {
            _game.OnLeanForward -= PlayLeanCreaking;
            _game.OnLeanBackward -= PlayLeanCreaking;
            _gameController.OnStopGame -= PlayGameOver;
            _gameController.OnPieceRemoved -= PlayPieceRemoved;
            _board.OnBoardReset -= GetPieceSources;
            _board.OnPieceFinishedMoving -= PlayPieceMoved;

            if (PrisonerSequence != null)
            {
                _game.OnSitDown -= OnSitDown;
                _game.OnStoodUp -= OnStoodUp;
                PrisonerSequence.OnSpotlightTorch -= PlayTorchSpotlight;
                PrisonerSequence.OnPrisonerCurious -= PlayPrisonerCurious;
                PrisonerSequence.OnSetupGame -= OnSetupGame;
                PrisonerSequence.OnCleanupGame -= OnCleanupGame;
            }
        }

        private void GetPieceSources()
        {
            _pieceSources = new OWAudioSource[_board.Pieces.Count];
            for (int i = 0; i < _board.Pieces.Count; i++)
                _pieceSources[i] = _board.Pieces[i].gameObject.AddComponent<OWAudioSource>();
        }

        private void PlayCreaking(OWAudioSource source, AudioType audio, float volume, float duration)
        {
            if (source != null && !source.isPlaying)
            {
                source.AssignAudioLibraryClip(audio);
                source.SetLocalVolume(volume);
                source.Play();
                source.RandomizePlayhead();
                source.FadeOut(duration);
            }
            else if (source == null)
            {
                Util.Logger.LogError($"Couldn't find audio source {source}!");
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
                Util.Logger.LogError($"Couldn't find audio source {source}!");
            }
        }

        private void PlayTorchSpotlight()
        {
            PlayOneShot(_audioSources.torchAudio, AudioType.ShipCockpitHeadlightsOn);
        }

        private void PlayFurnitureSounds(bool isSetup, bool shortcutSkip = false)
        {
            StartCoroutine(FurnitureChaosAudio(isSetup, shortcutSkip));
        }

        private IEnumerator FurnitureChaosAudio(bool isSetup, bool shortcutSkip)
        {
            if (!shortcutSkip)
            {
                List<AudioType> noises = new(s_furnitureNoises);
                if (!isSetup) noises.Reverse();
                bool playedPrisonerNoise = false;

                // sequence of offscreen crashing and banging around
                foreach (AudioType type in noises)
                {
                    PlayOneShot(_audioSources.playerAudio, type);
                    float randInterval = Random.Range(0.5f, 0.8f);
                    yield return new WaitForSecondsRealtime(randInterval);

                    if (!playedPrisonerNoise)
                    {
                        playedPrisonerNoise = true;
                        int rIdx = (int)(randInterval * 10 % s_prisonerNoises.Length);
                        PlayOneShot(_audioSources.playerAudio, s_prisonerNoises[rIdx]);
                    }
                }
                PlayOneShot(_audioSources.lanternAudio, AudioType.Artifact_Unconceal);
            }
            OnFurnitureAudioFinished?.Invoke(isSetup);
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
            PlayCreaking(_audioSources.playerAudio, AudioType.TH_BridgeCreaking_LP, CreakVolume, 2);
        }

        private void Update()
        {
            if (InhabitantChess.CurrentGame == null) return;

            if (!_playingAmbience && Time.time >= _initAmbienceTime)
            {
                StartNextAmbience();
            }
            else if (_playingAmbience && Time.time >= _initFadeOutTime)
            {
                StopAmbience(true);
            }
        }

        private void InitAmbience()
        {
            enabled = true;
            _ambienceInterval = 90f;
            _fadeDuration = _ambienceInterval / 5;
            _initAmbienceTime = Time.time + _ambienceInterval;
        }

        private void StartNextAmbience()
        {
            OWAudioSource musicSource = _audioSources.playerMusic;
            // avoid picking same track twice in a row
            int rIdx = Random.Range(0, s_ambiences.Length - 1);
            _currentAmbience = s_ambiences[rIdx];
            s_ambiences[rIdx] = s_ambiences[s_ambiences.Length - 1];
            s_ambiences[s_ambiences.Length - 1] = _currentAmbience;

            _fadeDuration = Mathf.Min(20f, musicSource.clip.length / 4);
            musicSource.AssignAudioLibraryClip(_currentAmbience);
            musicSource.FadeIn(_fadeDuration, true, targetVolume: AmbienceVolume);
            // prepare for the next clip
            _ambienceInterval = 5 * _fadeDuration + 5 * Random.Range(0, _fadeDuration);
            float endTime = Time.time + musicSource.clip.length - musicSource.time;
            _initFadeOutTime = endTime - _fadeDuration;
            _initAmbienceTime = endTime + _ambienceInterval;

            _playingAmbience = true;
        }

        private void PlayGameOver()
        {
            AudioType gameOverSound = _gameController.PlayerWon() ? AudioType.SecretKorok : AudioType.Ghost_Laugh;
            PlayOneShot(_audioSources.playerAudio, gameOverSound);
        }

        private void StopAmbience(bool continueLoop = false)
        {
            OWAudioSource musicSource = _audioSources.playerMusic;
            if (musicSource == null) return;

            float fadeTime = continueLoop ? _fadeDuration : 5f;
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
            enabled = continueLoop;
        }
        private void OnSitDown(ChessGame chess)
        {
            InitAmbience();
        }

        private void OnStoodUp(ChessGame chess)
        {
            StopAmbience();
        }

        private void OnSetupGame()
        {
            bool shortcutSkip = InhabitantChess.Shortcut != null && InhabitantChess.Shortcut.UsedShortcut;
            PlayFurnitureSounds(true, shortcutSkip);
        }

        private void OnCleanupGame()
        {
            PlayFurnitureSounds(false);
        }
    }
}
