# The Talon's Shadow — Jam Roadmap (7 days)

A one-week solo jam: first Unity project, 3D, built with Claude Code driving the
Unity editor over MCP. The goal is a small finished game, shipped to itch, that
proves the pipeline and the concept.

**The concept:** a grounded third-person rogue whose *attention has a body* — a
crow companion. Extend your awareness out through the crow to read the space,
snap back, commit with the body. The pulse of out-and-in is the game.
Guardrail: the crow must never feel like a mode or a menu.

## Day log

- **Day 1 (done)** — pipeline smoke test: Unity MCP control, Meshy character
  (TalonRogue, rigged), WebGL → itch draft channel, end to end.
- **Day 2 (done)** — playable prototype: camera-relative third-person movement
  (keyboard + gamepad), **Talon Dash** (i-frame dodge), greybox arena. itch v2.
- **Day 3 (done)** — **the crow**: send it to a perch (E / RB), peek through its
  eyes (hold right-mouse / LT), recall (Q / LB). While extended, the body is
  rooted and vulnerable — awareness costs presence. itch v3.
- **Day 4 (done)** — enemies + melee: talon-strike lunge (left-click / X),
  shared `Health`, a chasing Stalker + faster Shade variant (NavMesh), and a
  ranged Sentry whose aim line heats up before it fires; a crow landing nearby
  **distracts** enemies for a window — scout → send → dash in → strike.
  Peek now also dilates time to 35%. itch v4.
- **Day 5 (done)** — **content lock**: three encounter pockets (lone Stalker →
  Sentry + cover → mixed pack), the Shadow Gate win condition + restart, and
  **Shadow Step** — dash while peeking from a perch to *become where your
  attention is* (the perch is consumed). Boss cut in its favor; kill-floor
  added; tuned toward new players. See DESIGN.md for where this is headed.
  itch v5.
- **Day 6** — juice + art: hitstop/shake/SFX, texture pass, real crow model,
  title/restart screen, outside playtest, fix the top confusions.
- **Day 7** — **release**: final build, footage captured from builds (never the
  editor), itch page public.

## Controls

| Action | Keyboard/Mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Camera | Mouse | Right stick |
| Talon Dash | Space / Left Shift | B |
| Talon Strike | Left mouse | X |
| Send crow | E | RB |
| Peek (hold) | Right mouse | LT |
| Shadow Step | Space (while peeking a perch) | B (while peeking a perch) |
| Recall crow | Q | LB |
| Restart (after winning) | R | Start |
