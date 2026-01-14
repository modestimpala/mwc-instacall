# InstaCall (MSCLoader / My Winter Car)

Small MSCLoader mod that speeds up the **PlayMaker** phone calling FSMs (no Harmony).

## What it does

When a `PlayMakerFSM` with `FsmName == "Calling"` becomes active, the mod patches it to:

- Make the ringing loop fast.
- Make the call/conversation finish quickly.

It also disables the specific `GetFsmFloat` action that would normally overwrite the `CallerCallLenght` variable right before the call, so the shortened call length sticks.

## How it works (FSM extension approach)

- Scans `PlayMakerFSM.FsmList` periodically (throttled).
- Patches up to **3** different `Calling` FSM instances (the 3 phones).
- Stops scanning once all 3 have been patched.

## Tuning

Open `InstaCall.cs` and change:

- `FastWaitSeconds` (default `0.1f`) to control how fast the ringing + conversation are.
- `ScanIntervalSeconds` to control how often the mod scans for `Calling` FSMs.

## Notes

- This mod keeps the vanilla call flow intact (including order spawning), it just shortens waits.
- If you make waits *too* small (e.g. 0), PlayMaker FSMs can hit the max loop count and break; use a small positive value instead.
