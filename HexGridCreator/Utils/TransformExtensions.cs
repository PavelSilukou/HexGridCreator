using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexGridCreator.Utils
{
    internal static class TransformExtensions
    {
        public static List<Transform> GetAllChildrenTransform(this Transform root)
        {
            return root.Cast<Transform>().ToList();
        }
    }
}