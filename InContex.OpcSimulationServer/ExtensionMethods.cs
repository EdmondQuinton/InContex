﻿using System;
using System.Collections.Generic;
using System.Linq;

using OfficeOpenXml;
using OfficeOpenXml.Table;
using System.Linq;

namespace InContex.OpcSimulationServer
{
    public static class ExtensionMethods
    {
        private static Random rng = new Random();

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
