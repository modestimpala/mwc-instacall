using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using UnityEngine;

namespace InstaCall
{
    public class InstaCall : Mod
    {
        private static readonly HashSet<int> PatchedFsms = new HashSet<int>();
        private static readonly List<PlayMakerFSM> PatchedFsmList = new List<PlayMakerFSM>();

        private const int TargetCallingFsmsToPatch = 3;
        private const float ScanIntervalSeconds = 0.25f;
        private const float EnforceIntervalSeconds = 0.05f;
        private float _nextScanTime;
        private float _nextEnforceTime;
        private int _callingFsmsPatched;

        public override string ID => "Moddy-InstaCall"; // Your (unique) mod ID 
        public override string Name => "InstaCall"; // Your mod name
        public override string Author => "Moddy"; // Name of the Author (your name)
        public override string Version => "1.0"; // Version
        public override string Description => "Makes phone calls ring and complete near instatly."; // Description of the mod
        public override Game SupportedGames => Game.MyWinterCar;

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.OnGUI, Mod_OnGUI);
            SetupFunction(Setup.Update, Mod_Update);
        }

        private void Mod_OnLoad()
        {
            // Reset counters for a new load.
            _nextScanTime = 0f;
            _nextEnforceTime = 0f;
            _callingFsmsPatched = 0;
            PatchedFsms.Clear();
            PatchedFsmList.Clear();

            ModConsole.Log("[InstaCall] Loaded. Will patch up to 3 'Calling' FSM instances when they appear.");
        }

        private void Mod_OnGUI()
        {
            //  debug hints?
        }

        private void Mod_Update()
        {
            // Continuously enforce fast call length on already-patched FSMs
            if (Time.time >= _nextEnforceTime)
            {
                _nextEnforceTime = Time.time + EnforceIntervalSeconds;
                EnforceCallLengthOnPatchedFsms();
            }

            // After we've patched all known phone Calling FSMs, stop scanning.
            if (_callingFsmsPatched >= TargetCallingFsmsToPatch)
            {
                return;
            }

            // Throttle the scan; no need to walk all FSMs every frame.
            if (Time.time < _nextScanTime)
            {
                return;
            }
            _nextScanTime = Time.time + ScanIntervalSeconds;

            // Patch Calling FSMs as they become active.
            for (int i = 0; i < PlayMakerFSM.FsmList.Count; i++)
            {
                PlayMakerFSM fsm = PlayMakerFSM.FsmList[i];
                if (fsm == null)
                {
                    continue;
                }

                int id = fsm.GetInstanceID();
                if (PatchedFsms.Contains(id))
                {
                    continue;
                }

                if (fsm.FsmName == "Calling")
                {
                    PatchCallingFsmSkipRinging(fsm);
                    PatchedFsms.Add(id);
                    PatchedFsmList.Add(fsm);

                    _callingFsmsPatched++;
                    if (_callingFsmsPatched >= TargetCallingFsmsToPatch)
                    {
                        ModConsole.Log($"[InstaCall] Patched {_callingFsmsPatched} 'Calling' FSMs. Stopping scans.");
                        return;
                    }
                }
            }
        }

        private static void PatchCallingFsmSkipRinging(PlayMakerFSM fsm)
        {
            // From FSM: "Delay" -> "Ring" -> "Add ring" (loop)
            // and also "Ring 2" / "Add ring 2".
            // Fully zeroing these waits can create an ultra-tight loop (Ring <-> Add ring)
            // that hits PlayMaker's max loop count and can spam audio actions.
            // Instead, we make ringing effectively instant by setting only the Ring state's Wait
            // to a tiny duration, which yields at least one frame between loops.
            const float FastWaitSeconds = 0.1f;
            int changed = 0;

            // Some variants appear to drive the conversation duration from this FSM float instead of
            // hardcoding the Wait value in the state. If present, force it to our fast duration.
            FsmFloat callLength = fsm.FsmVariables.GetFsmFloat("CallerCallLenght");
            if (callLength != null)
            {
                callLength.Value = FastWaitSeconds;
            }

            // Prevent other FSMs (e.g. Data) from re-imposing a longer call length.
            // In the vanilla graph, "Fixed theme" uses GetFsmFloat to pull CallerCallLenght.
            // If we leave that enabled, it can overwrite our forced value right before Call.
            foreach (FsmState state in fsm.FsmStates)
            {
                if (state == null)
                {
                    continue;
                }

                FsmStateAction[] actions = state.Actions;
                for (int i = 0; i < actions.Length; i++)
                {
                    if (actions[i] is GetFsmFloat getFsmFloat && getFsmFloat.variableName != null && getFsmFloat.variableName.Value == "CallerCallLenght")
                    {
                        getFsmFloat.Enabled = false;
                    }
                }
            }

            foreach (FsmState state in fsm.FsmStates)
            {
                if (state == null)
                {
                    continue;
                }

                // More aggressive: shorten essentially all delays in this FSM, but don't break dialing.
                // "Set number" includes a Wait tied to keypress/number entry, so we leave it alone.
                if (state.Name == "Set number")
                {
                    continue;
                }

                FsmStateAction[] actions = state.Actions;
                for (int i = 0; i < actions.Length; i++)
                {
                    if (actions[i] is Wait wait)
                    {
                        wait.time.Value = FastWaitSeconds;
                        changed++;
                    }
                }
            }

            ModConsole.Log($"[InstaCall] Calling FSM patched on '{fsm.gameObject.name}': set {changed} Wait(s) to {FastWaitSeconds:0.0}s.");
        }

        private static void EnforceCallLengthOnPatchedFsms()
        {
            const float FastWaitSeconds = 0.1f;
            
            // Keep forcing CallerCallLenght to fast duration on all patched FSMs
            // This prevents other FSMs (like Data) from overwriting it during call setup
            for (int i = PatchedFsmList.Count - 1; i >= 0; i--)
            {
                PlayMakerFSM fsm = PatchedFsmList[i];
                if (fsm == null || fsm.FsmVariables == null)
                {
                    PatchedFsmList.RemoveAt(i);
                    continue;
                }

                FsmFloat callLength = fsm.FsmVariables.GetFsmFloat("CallerCallLenght");
                if (callLength != null && callLength.Value > FastWaitSeconds)
                {
                    callLength.Value = FastWaitSeconds;
                }
            }
        }
    }
}
