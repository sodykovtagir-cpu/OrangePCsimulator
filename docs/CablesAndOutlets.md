# Physical PC wiring (Unity Play Mode checklist)

## Shop and installation

- The **Cables & Power** page sells 2- and 3-socket wall outlets and three length variants of **each** cable: AC (1.5/3/5 m), VGA (1.5/3.5/5.5 m), HDMI (2/4/6 m).
- Move an outlet against a wall as you would a wall shelf. Its existing `Hook` fixes it in place; it only supplies power while mounted. Each numbered socket accepts one plug. Unmounting it cuts connected power.
- Grab either solid plug using the game's existing object-drag controls and bring it to a small matching rear-panel port. The wire body cannot be selected with the drag ray but is physically collidable; its joints and length limit remain active. Grab a connected plug to unplug it; snapping is temporarily suppressed while you pull it away.
- Supply AC from the outlet to **both** the PC (PSU rear or accessible case AC port; mini-PC adapter/AC port) **and** the monitor. VGA fits only the CRT. HDMI fits the other displays. Connect the video cable's second plug to the GPU video port (blue) or motherboard video port (green), including the corresponding rear-case ports.
- Both ends of a cable are interchangeable. Two devices or two outlets cannot be joined with a power cord; a video cord cannot connect two outputs or two inputs. Each port accepts one plug. Endpoints beyond 95% of the cable length are rejected.

## Expected behavior / smoke tests

1. Old saves also require new wiring: PC cannot start without an energized PSU/adapter cord. An unpowered monitor is black; powered monitor without valid video shows No Signal.
2. CRT rejects HDMI; other monitors reject VGA. A GPU cable outputs via an installed, working GPU. Motherboard video needs a working CPU with integrated graphics; without it the powered monitor shows No Signal. Only the selected GPU can create VRAM artifacts on that monitor.
3. Pulling either PC power plug or unmounting an outlet powers the PC off. Pulling only video/power from the display removes the picture without breaking the PC. Reinsert to restore it.
4. Save and reload: sockets, cable endpoint positions and their links to a case, monitor, PSU, GPU, motherboard and outlets should survive. Buying two identical cables at the same delivery point must not be deduplicated on load.
5. For collision, drag the middle cable around a table/wall and verify the cable capsule bodies cannot cross solid colliders. On an idle powered PC the rope bodies sleep: no per-frame mesh rebuilding or whole-room scan is performed.

The game generates connector markers once per device; a designer can override each location by adding a child with the listed connector name to its prefab. Power sockets do not require adding wiring into scene walls.

**Important:** Static source and prefab checks cannot substitute for Unity 2022.3 Play Mode, particularly joint stability and rear-panel position on every hardware model.
