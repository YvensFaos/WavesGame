/*
 * Copyright (c) 2026 Yvens R Serpa [https://github.com/YvensFaos/]
 *
 * This work is licensed under the Creative Commons Attribution 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/
 * or see the LICENSE file in the root directory of this repository.
 */

using System.Collections.Generic;
using System.Linq;
using UUtils;

namespace Actors.AI
{
    public static class AINamer
    {
        private static readonly string[] AINames =
        {
            "Yvens","Juliana","Morgana","Mateo",
            "Rodrigo","Eduardo","Victor","Mauricio",
            "Caio","Ygor","Luana","Audo",
            "Maria","Maira","Leo","Milena",
            "Luigi","Randy","Max","Iain",
            "Hans","Bram","Mark","Louise",
            "David","Aliria","Daniel","Alejandro",
            "Marcello","Danny","Wouter","Jeroen",
            "George","Chris","Jesus","Jose",
            "Tidus","Yuna","Rikku","Kimahri",
            "Verso","Gustave","Sciel","Lune",
            "Lufia","Chrono","Marle","Lucca",
        };
        
        private static readonly string[] AISurnames =
        {
            "A.","von B.","S.","el O.",
            "D.","vaan G.","C.","da P.",
            "X.","Mc F.","R.","of J.",
            "X.","Mc F.","R.","of J.",
        };
        
        private static List<string> _aiNamesList;
        private static List<string> _aiSurnamesList;
        
        public static string GetRandomName()
        {
            _aiNamesList ??= AINames.ToList();
            return RandomHelper<string>.GetRandomFromList(_aiNamesList);
        }
        
        public static string GetRandomFullName() 
        {
            _aiNamesList ??= AINames.ToList();
            _aiSurnamesList ??= AISurnames.ToList();
            return $"{RandomHelper<string>.GetRandomFromList(_aiNamesList)} {RandomHelper<string>.GetRandomFromList(_aiSurnamesList)}";
        }
    }
}