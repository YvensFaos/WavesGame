/*
 * Copyright (c) 2025 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;
using UUtils;

namespace UI
{
    public class FillBar : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer fillBar;
        [SerializeField, ReadOnly] private Material fillBarMaterial;
        [SerializeField] private string materialProperty = "_FillFactor";
        private Tweener _animateMaterial;

        private void Awake()
        {
            AssessUtils.CheckRequirement(ref fillBar, this);
            fillBarMaterial = fillBar.material;
        }

        public void SetFillFactor(float factor, float speedFactor)
        {
            _animateMaterial?.Kill();
            _animateMaterial = AnimateMaterialProperty.AnimateProperty(fillBarMaterial, materialProperty, factor, 1.0f * speedFactor);
        }
        
        public void SetFillBarColor(Color color)
        {
            fillBar.color = color;
        }
    }
}