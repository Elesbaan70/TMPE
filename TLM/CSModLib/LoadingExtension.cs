using HarmonyLib;
using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CSModLib {
    internal sealed class LoadingExtension : ILoadingExtension {

        private const string harmonyId = "org.CitiesSkylinesMods.CSModLib";

        public void OnCreated(ILoading loading) {
        }

        public void OnLevelLoaded(LoadMode mode) {
            new Harmony(harmonyId).PatchAll(Assembly.GetExecutingAssembly());
        }

        public void OnLevelUnloading() {
            new Harmony(harmonyId).UnpatchAll();
        }

        public void OnReleased() {
        }
    }
}
