/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UUtils;

namespace Core
{
    public class PlayerController : WeakSingleton<PlayerController>
    {
        [SerializeField] private PlayerInput input;

        public Action<Vector2> onMoveAction;
        public Action onInteract;
        public Action onCancel;

        public void OnMove(InputAction.CallbackContext context)
        {
            var readValue = context.ReadValue<Vector2>();
            onMoveAction?.Invoke(readValue);
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            DebugUtils.DebugLogMsg(
                $"On Interact called. S[{context.started}] P[{context.performed}] C[{context.canceled}]",
                DebugUtils.DebugType.Verbose);
            if (!context.performed) return;
            onInteract?.Invoke();
        }

        public void OnNavigate(InputAction.CallbackContext context)
        {
            // var readValue = context.ReadValue<Vector2>();
            // onMoveAction?.Invoke(readValue);
        }

        public void OnCancel(InputAction.CallbackContext context)
        {
            DebugUtils.DebugLogMsg(
                $"On Cancel called. S[{context.started}] P[{context.performed}] C[{context.canceled}]",
                DebugUtils.DebugType.Verbose);
            if (!context.performed) return;
            onCancel?.Invoke();
        }

        public void OnResetLevel(InputAction.CallbackContext context)
        {
            DebugUtils.DebugLogMsg(
                $"On Reset Level called. S[{context.started}] P[{context.performed}] C[{context.canceled}]",
                DebugUtils.DebugType.Verbose);
            if (!context.performed) return;
            if (GameController.TryToGetSingleton(out var gameController))
            {
                gameController.ResetController();
            }
            else
            {
                ApplicationHelper.RestartLevel();
            }
        }

        public void OnForceResetCursor(InputAction.CallbackContext context)
        {
            DebugUtils.DebugLogMsg(
                $"On OnForceResetCursor Level called. S[{context.started}] P[{context.performed}] C[{context.canceled}]",
                DebugUtils.DebugType.Verbose);
            if (!context.performed) return;
            if (CursorController.TryToGetSingleton(out var cursorController))
            {
                cursorController.ForceResetCursor();
            }
        }
    }
}