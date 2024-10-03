using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Motoplay.Scripts
{
    /*
     * This class hold Methods Extensions for others Classes
    */

    public static class ClassExtensions
    {
        //Extension methods

        public static void Shuffle<T>(this IList<T> list)
        {
            RandomNumberGenerator rngProvider = RandomNumberGenerator.Create();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do rngProvider.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            rngProvider.Dispose();
        }
    }
}
