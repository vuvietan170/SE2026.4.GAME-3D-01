using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("HUSEscape.Core.EditModeTests")]
[assembly: InternalsVisibleTo("HUSEscape.Core.PlayModeTests")]

namespace HUSEscape.Core
{
    /// <summary>Owns application state and enforces the Core transition matrix on the main thread.</summary>
    public sealed class GameManager
    {
        private bool isNotifying;

        internal sealed class SceneLoadReservation
        {
            internal GameState PreviousState { get; }

            internal SceneLoadReservation(GameState previousState)
            {
                PreviousState = previousState;
            }
        }

        internal SceneLoadReservation ActiveReservation { get; private set; }
        internal bool IsNotifying => isNotifying;

        /// <summary>Raised after a committed transition, with the previous and current states.</summary>
        /// <remarks>Reentrant transitions are rejected. Subscriber exceptions are logged independently.</remarks>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>Gets the current state. New managers start in Initializing.</summary>
        public GameState CurrentState { get; private set; } = GameState.Initializing;

        /// <summary>Gets whether initialization has completed.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>Initializes this manager once and publishes the Initializing-to-Ready transition.</summary>
        /// <returns>True on the first successful initialization; false on repeat or reentrant calls.</returns>
        public bool Initialize()
        {
            if (IsInitialized || isNotifying)
            {
                return false;
            }

            IsInitialized = true;
            CommitState(GameState.Ready);
            return true;
        }

        /// <summary>Checks whether a requested transition is currently permitted.</summary>
        /// <remarks>Actual scene loads reserve state changes for SceneLoader until completion.</remarks>
        /// <param name="nextState">The requested destination state.</param>
        /// <returns>False before initialization, during notification or loading, or for an invalid edge.</returns>
        public bool CanTransitionTo(GameState nextState)
        {
            return IsInitialized && !isNotifying && ActiveReservation == null
                && IsValidTransition(CurrentState, nextState);
        }

        /// <summary>Attempts a state-only transition without loading scenes or changing time scale.</summary>
        /// <param name="nextState">The requested destination state.</param>
        /// <returns>True if committed; false leaves state unchanged and publishes no event.</returns>
        public bool TryChangeState(GameState nextState)
        {
            if (!CanTransitionTo(nextState))
            {
                return false;
            }

            CommitState(nextState);
            return true;
        }

        internal bool TryBeginSceneLoad(out SceneLoadReservation reservation)
        {
            reservation = null;
            if (!CanTransitionTo(GameState.Loading))
            {
                return false;
            }

            // Reserve ownership before notifying listeners, preventing reentrant requests.
            reservation = new SceneLoadReservation(CurrentState);
            ActiveReservation = reservation;
            CommitState(GameState.Loading);
            return true;
        }

        internal bool OwnsReservation(SceneLoadReservation reservation)
        {
            return reservation != null && ReferenceEquals(ActiveReservation, reservation);
        }

        internal bool TryCompleteSceneLoad(SceneLoadReservation reservation, GameState nextState)
        {
            if (!OwnsReservation(reservation) || isNotifying || !IsValidTransition(CurrentState, nextState))
            {
                return false;
            }

            ActiveReservation = null;
            CommitState(nextState);
            return true;
        }

        internal bool TryRecoverSceneLoad(SceneLoadReservation reservation)
        {
            // A stale operation must never release a newer operation's reservation.
            return OwnsReservation(reservation)
                && TryCompleteSceneLoad(reservation, reservation.PreviousState);
        }

        private static bool IsValidTransition(GameState currentState, GameState nextState)
        {
            switch (currentState)
            {
                case GameState.Ready:
                    return nextState == GameState.Loading || nextState == GameState.Playing;
                case GameState.Loading:
                    return nextState == GameState.Ready || nextState == GameState.Playing
                        || nextState == GameState.Paused;
                case GameState.Playing:
                    return nextState == GameState.Loading || nextState == GameState.Paused;
                case GameState.Paused:
                    return nextState == GameState.Loading || nextState == GameState.Playing;
                default:
                    return false;
            }
        }

        private void CommitState(GameState nextState)
        {
            GameState previousState = CurrentState;
            CurrentState = nextState;
            Action<GameState, GameState> listeners = StateChanged;
            if (listeners == null)
            {
                return;
            }

            isNotifying = true;
            try
            {
                foreach (Action<GameState, GameState> listener in listeners.GetInvocationList())
                {
                    try
                    {
                        listener(previousState, nextState);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                isNotifying = false;
            }
        }
    }
}
