# RetroQB

RetroQB is a retro 2D American football offense-only game where you play the QB. Run or pass using a simple ASCII-like presentation in a Raylib window.

## How to run

```bash
dotnet run
```

## Controls

- Move: WASD or Arrow keys
- Sprint: Left Shift
- Aim pass: Hold Space
- Throw: Release Space
- Cycle receiver: Tab
- Restart drive: R
- Pause: Esc
- Snap play (PreSnap): Space
- Select play (PreSnap): 1/2/3

## Passing mechanic

Hold Space to enter aim mode. An aim line shows the direction (based on mouse position) and a power meter fills up over about one second. Release Space to throw; the ball speed is based on the current power. Receivers can catch if the ball comes within catch radius; defenders can intercept if they are close to the catch point.
