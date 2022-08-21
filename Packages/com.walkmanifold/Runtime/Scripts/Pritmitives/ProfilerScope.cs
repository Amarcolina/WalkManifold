using System;
using UnityEngine.Profiling;

namespace WalkManifold.Internals {

  public struct ProfilerScope : IDisposable {

    public ProfilerScope(string name) {
      Profiler.BeginSample(name);
    }

    public void Dispose() {
      Profiler.EndSample();
    }
  }
}
