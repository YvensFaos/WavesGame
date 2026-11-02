/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the MIT License.
 * To view a copy of this license, visit https://opensource.org/licenses/MIT
 * or see the LICENSE file in the root directory of this repository.
 */

using System;

namespace Grid
{
    public enum GridMoveType
    {
        Cross,
        Area,
        Diagonal,
        Up,
        Down,
        Left,
        Right

        //TODO add Self
    }

    public static class GridMoveTypeExtensions
    {
        public static string GridMovementSymbol(GridMoveType type)
        {
            return type switch
            {
                GridMoveType.Cross => "~+",
                GridMoveType.Area => "~*",
                GridMoveType.Diagonal => "~X",
                GridMoveType.Up => "~^",
                GridMoveType.Down => "~V",
                GridMoveType.Left => "~<",
                GridMoveType.Right => "~>",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public static string GridMovementSimplifiedSymbol(GridMoveType type)
        {
            return type switch
            {
                GridMoveType.Cross => "+",
                GridMoveType.Area => "*",
                GridMoveType.Diagonal => "x",
                GridMoveType.Up => "^",
                GridMoveType.Down => "V",
                GridMoveType.Left => "<",
                GridMoveType.Right => ">",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}