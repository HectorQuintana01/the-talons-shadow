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
- **Day 4** — enemies + melee: talon-strike lunge, `Health`, a chasing Stalker
  (+ faster Shade variant) and a ranged Sentry; perched crow **distracts**
  nearby enemies — scout → send → dash in → strike.
- **Day 5** — **content lock**: three encounter pockets, a win gate, tuning to
  new-player difficulty. Boss (Talon-Warden) only if the loop already feels
  good — it's a stretch goal, cut without guilt.
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
| Send crow | E | RB |
| Peek (hold) | Right mouse | LT |
| Recall crow | Q | LB |
