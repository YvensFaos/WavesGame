/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using Unity.Cinemachine;
using UnityEngine;

namespace Core
{
    public class CameraCursorFinder : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera cinemachineCamera;

        private bool _found;

        private void Update()
        {
            if (_found) return;
            if (!CursorController.TryToGetSingleton(out var cursorController)) return;
            _found = true;
            cinemachineCamera.Follow = cursorController.transform;
            Destroy(this);
        }
    }
}