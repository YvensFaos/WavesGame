/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using Actors;
using Core;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UUtils;

namespace UI
{
    public class InfoPanelUI : MonoBehaviour
    {
        [SerializeField, ReadOnly] private NavalActor currentActor;
        [SerializeField] private HealthDisplayUI healthDisplayUI;
        [SerializeField] private List<StatDisplayUI> statDisplayUIs;
        [SerializeField] private TextMeshProUGUI actorNameText;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            AssessUtils.CheckRequirement(ref healthDisplayUI, this);
        }

        private void Start()
        {
            statDisplayUIs.ForEach(statDisplayUI => statDisplayUI.Initialize(this));
        }

        private void OnEnable()
        {
            EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void OpenWith(NavalActor actor)
        {
            gameObject.SetActive(true);

            currentActor = actor;
            var actorName = actor.gameObject.name;
            actorNameText.text = actor.gameObject.name; //TODO to show the actual name of the actor
            DebugUtils.DebugLogMsg($"Set the Info Panel actor name to {actorName}.", DebugUtils.DebugType.Temporary);
            healthDisplayUI.Initialize(currentActor);

            switch (currentActor)
            {
                case NavalShip navalShip:
                {
                    var stats = navalShip.ShipData.stats;
                    UpdateDisplayFromStats(stats);
                    break;
                }
                case NavalTarget navalTarget:
                {
                    var stats = navalTarget.GetStats();
                    UpdateDisplayFromStats(stats);
                    break;
                }
                default:
                    //Case it is no type with a stat, then display zero for all stats.
                    statDisplayUIs.ForEach(statDisplayUI => statDisplayUI.SetStatText("0"));
                    break;
            }

            return;

            void UpdateDisplayFromStatPair(StatValuePair pair)
            {
                var display = statDisplayUIs.Find(statDis => statDis.IsStatOfType(pair.One));
                display?.SetInfoFromStatPair(pair);
                if (display == null) DebugUtils.DebugLogErrorMsg($"{pair.One.statName} not found in the InfoPanelUI.");
            }

            void UpdateDisplayFromStats(NavalActorStats stats)
            {
                UpdateDisplayFromStatPair(stats.strength);
                UpdateDisplayFromStatPair(stats.speed);
                UpdateDisplayFromStatPair(stats.stability);
                UpdateDisplayFromStatPair(stats.sight);
                UpdateDisplayFromStatPair(stats.sturdiness);
                UpdateDisplayFromStatPair(stats.spirit);
            }
        }

        // ReSharper disable once InconsistentNaming
        public void SetStatSOText(StatSO statSo)
        {
            infoText.text = statSo.statDescription;
        }

        public void Close(bool cancelActor = false)
        {
            gameObject.SetActive(false);
            if (cancelActor)
            {
                CursorController.GetSingleton().CancelSelectedActor();
            }
        }
    }
}