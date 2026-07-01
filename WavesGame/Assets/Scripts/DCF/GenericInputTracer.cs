/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UUtils;

namespace DCF
{
    public abstract class GenericInputTracer : WeakSingleton<GenericInputTracer>
    {
        [SerializeField] private InputActionAsset inputActionAsset;
        [SerializeField] private List<string> trackingMaps;
        private InputActionTrace _tracer;
        private bool _tracing;

        private void Start()
        {
            _tracer = new InputActionTrace();

            if (inputActionAsset == null)
            {
                _tracer.SubscribeToAll();
            }
            else
            {
                if (trackingMaps.Count == 0)
                {
                    foreach (var inputActionMap in inputActionAsset.actionMaps)
                    {
                        _tracer.SubscribeTo(inputActionMap);
                    }
                }
                else
                {
                    trackingMaps.ForEach(trackingMap =>
                    {
                        foreach (var inputActionMap in inputActionAsset.actionMaps.Where(
                                     t => t.name.Equals(trackingMap)))
                        {
                            _tracer.SubscribeTo(inputActionMap);
                        }
                    });
                }
            }

            _tracing = true;

            StartCoroutine(Tracing());
        }

        private IEnumerator Tracing()
        {
            while (_tracing)
            {
                yield return new WaitUntil(() => _tracer.count > 0);
                DebugUtils.DebugLogMsg($"Tracer detected {_tracer.count} inputs.", DebugUtils.DebugType.Temporary);
                var inputEnumerator = _tracer.GetEnumerator();
                while (inputEnumerator.MoveNext())
                {
                    var input = inputEnumerator.Current;
                    ProcessActionEventPtr(input);
                }

                inputEnumerator.Dispose();
                _tracer.Clear();
            }

            yield return null;
        }

        protected abstract void ProcessActionEventPtr(InputActionTrace.ActionEventPtr actionEventPtr);

        [Button("Load Maps")]
        public void LoadMaps()
        {
            trackingMaps.Clear();
            foreach (var inputActionMap in inputActionAsset.actionMaps)
            {
                trackingMaps.Add(inputActionMap.name);
            }
        }
    }
}