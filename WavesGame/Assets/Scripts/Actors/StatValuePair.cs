/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;
using UUtils;

namespace Actors
{
    [Serializable]
    public class StatValuePair : Pair<StatSO, int>
    {
        public StatValuePair(StatSO one, int two) : base(one, two)
        {
        }

        public override string ToString()
        {
            return $"{One.statName}: {Two}";
        }
    }
}