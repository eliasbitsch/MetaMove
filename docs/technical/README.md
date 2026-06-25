# MetaMove — Technical Documentation

Source for the MetaMove technical documentation, built with **Sphinx** + **MyST**
(Markdown). The same Markdown sources render on GitHub and build to HTML and PDF.

## Contents

- [`index.md`](index.md) — overview, system architecture, repo layout
- [`installation.md`](installation.md) — hardware/software requirements & bring-up
- [`reproduce.md`](reproduce.md) — **full build-it-yourself guide** (sim path needs no hardware)
- [`01_distance_speed_scaling.md`](01_distance_speed_scaling.md) — distance-based speed scaling
- [`02_pinch_move_teleop.md`](02_pinch_move_teleop.md) — pinch-and-move end-effector control
- [`03_dashboard_hmi.md`](03_dashboard_hmi.md) — dashboard and HMI
- [`reference.md`](reference.md) — ROS topics, parameters, ports, glossary

## Build

```bash
pip install -r requirements.txt

# Browsable HTML
sphinx-build -b html . _build/html

# PDF — pure-Python via rinohtype, no LaTeX toolchain required
sphinx-build -b rinoh . _build/pdf
# -> _build/pdf/MetaMove-Technical-Documentation.pdf
```

On Windows you can also run `python -m sphinx -b rinoh . _build/pdf`.
