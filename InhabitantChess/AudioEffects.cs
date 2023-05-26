using InhabitantChess.BoardGame;
using InhabitantChess.Util;
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

            _board.OnBoardInitialized += AddPieceSources;
            _board.OnPieceFinishedMoving += PlayPieceMoved;
            _instance.BoardGame.GetComponent<BoardGameController>().OnPieceRemoved += PlayPieceRemoved;
            _instance.PrisonerSequence.OnSpotlightTorch += PlayTorchSpotlight;
            _instance.PrisonerSequence.OnPrisonerCurious += PlayPrisonerCurious;
            _instance.PrisonerSequence.OnSetupGame += PlaySetup;
        }

        private void OnDestroy()
        {
            _board.OnBoardInitialized -= AddPieceSources;
            _board.OnPieceFinishedMoving -= PlayPieceMoved;
            _instance.BoardGame.GetComponent<BoardGameController>().OnPieceRemoved -= PlayPieceRemoved;
            _instance.PrisonerSequence.OnSpotlightTorch -= PlayTorchSpotlight;
            _instance.PrisonerSequence.OnPrisonerCurious -= PlayPrisonerCurious;
            _instance.PrisonerSequence.OnSetupGame -= PlaySetup;
        }

        private void AddPieceSources()
        {
            _pieceSources = new();
            foreach (var piece in _board.Pieces)
            {
                _pieceSources.Add(piece.g.AddComponent<OWAudioSource>());
            }
        }

        // for props, music etc.
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

        // for board pieces
        private void PlayOneShot(int idx, AudioType audio)
        {
            var source = _pieceSources[idx];
            if (source != null)
            {
                source.AssignAudioLibraryClip(audio);
                source.PlayOneShot(source._audioLibraryClip, 1f);
                Logger.Log($"Played piece oneshot {source._audioLibraryClip}");
            }
            else
            {
                Logger.LogError($"Couldn't find audio source for piece {idx}!");
            }
        }

        private void PlayTorchSpotlight()
        {
            PlayOneshot("torchAudio", AudioType.ShipCockpitHeadlightsOn);
        }

        private void PlaySetup()
        {
            // TODO all kindsa timed audio events or w/e crashing and banging around
            PlayOneshot("lanternAudio", AudioType.Artifact_Unconceal);
        }

        private void PlayPrisonerCurious()
        {
            _prisonerFX.PlayVoiceAudioNear(AudioType.Ghost_Identify_Curious);
        }

        private void PlayPieceMoved(int idx)
        {
            PlayOneShot(idx, AudioType.Artifact_Drop);
        }

        private void PlayPieceRemoved(int idx)
        {
            PlayOneShot(idx, AudioType.Artifact_Extinguish);
        }
    }
}
