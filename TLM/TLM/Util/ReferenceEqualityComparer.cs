using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace TrafficManager.Util {
    public sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> {
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
        private ReferenceEqualityComparer() { }
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}