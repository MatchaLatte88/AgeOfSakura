## Grafikstil: 3D-Toon (gebacken zu Sprites)

**Status:** ENTSCHIEDEN (durch Fred). Stilversion: 1.0.0
**Referenz-Implementierung:** `haus_techniken.html` (Vergleich), `haus_baeume.html` (Haus + Bäume), `src/toon.js`, `src/trees.js`, `src/util.js`

> Der YAML-Block ist die einzige Quelle für Zahlen und Farben. Der Fließtext verweist darauf und wiederholt keine Werte. Änderungen an Werten mit Status `decided` nur mit Freds Freigabe (siehe `style-manager.md`).

### Kurzbeschreibung
Weiche, gerundete Low-Poly-Formen mit Toon-Schattierung (3 Stufen), dunkelbrauner Kontur, warmer Sonne und kräftigen, leicht pastelligen Grüntönen. Die Welt wird in 3D-Software/Code modelliert, aus fester Isometrie-Kamera gerendert und für das Spiel zu Sprites gebacken.

### Maßgebliche Werte (Single Source of Truth)

```yaml
style:
  name: 3D-Toon
  version: 1.0.0
  status: decided
  render:
    renderer: three.js r186 (nur Referenz/Bake-Werkzeug)
    material: MeshToonMaterial
    gradient_steps: 3
    outline:
      effect: OutlineEffect
      thickness: 0.0058
      color_rgb01: [0.16, 0.09, 0.04]   # dunkles Braun, nie reines Schwarz
      alpha: 1
      exclude_via: material.userData.outlineParameters.visible=false
    shadows: PCFSoft
  camera:
    type: OrthographicCamera
    elevation_deg: 30        # E = pi/6
    azimuth_deg: 45
    half_extent: 66
    center: [40, 15, 40]
    rotation: fixed          # keine Kamera-Drehung
  light:
    day:
      hemisphere: {sky: "#ffffff", ground: "#8aa86a", intensity: 1.7}
      sun: {color: "#fff0d0", intensity: 2.2}
    night:
      hemisphere: {sky: "#3a4c9a", ground: "#1c2440", intensity: 1.25}
      sun: {color: "#7f95ea", intensity: 1.0}
    sun_dir_for_baked_foliage: [-0.35, 0.72, 0.6]
    point_lights_night: {intensity: 200, chimney_or_index3: 260}
    window_glow: {day: "#ffe6a4", night: "#ffd35e"}
  palette:
    leaf_greens: ["#c4f083", "#86d24e", "#5cb547"]
    blossom_pinks: ["#ffd0e0", "#f8a3c1", "#ffbfd6"]
    moss: "#66b040"
    wood: "#a86f38"
    wood_dark: "#7c4c26"
    beam: "#8b5a2f"
    thatch_dark: "#c9953c"
    stone_plain: "#aeb2bb"
    cob: ["#dfcda6", "#cdb98f", "#e8d9b2"]
  materials:
    textures: canvas-gemalt, prozedural (Rinde, Putz, Stroh, Stein, Gras, Erde, Tür, Ziegel)
    corners: RoundedBoxGeometry, abgerundet (nie harte Würfelkanten)
  vegetation:
    foliage_geometry: Icosahedron detail 9 + mergeVertices (glatt, nicht facettiert)
    foliage_shading: vertex-color baked, Rampe [0, 0.38, 0.72, 1]
    leaf_cards: {alphaTest: 0.45, double_sided: true, aligned_to_camera: true}
    species: [oak, cherry, spruce, birch, old]
    ground_detail: [tufts, mushrooms, decals]
  animation:
    build_in_seconds: 4.4
    build_order: [ground, body, roof, trees, props]
    build_easing: bounce (drop-in)
    build_windows: {ground: [0, 0.22], body: [0.2, 0.5], roof: [0.42, 0.72], trees: [0.5, 0.8], props: [0.66, 1]}
    wind: layered sway per tree (Slider 0..n), Rauch, Blütenblätter
    approach: statischer Körper-Sprite + kleine animierte Layer
  pipeline:            # status: proposed
    bake: Sprite pro Asset/Tag/Nacht aus fester Kamera
    atlas: TexturePacker o.ä.
    budgets: offen bis Engine-Entscheidung
```

### Do
- Runde Formen, sichtbare Holzbalken, Strohdach, warme Fenster bei Nacht.
- Kontur immer dunkelbraun, gleichbleibende Stärke.
- Vegetation mit weichen, Baum-Kronen aus mehreren Klumpen und Kartenblättern.
- Jedes Asset in Tag- und Nachtvariante denkbar (Licht-Werte oben).

### Don't
- Kein reines Schwarz, keine harten Facetten, keine Fotorealismus-Texturen.
- Keine Kamera-Drehung (Blattkarten und gebackene Sprites setzen feste Kamera voraus).
- Keine zusätzlichen Lichtquellen/Schatteneinstellungen ohne Freigabe.

### Review-Checkliste (neues Asset)
1. Kontur, Kamera, Licht wie im YAML?
2. Palette aus `palette` oder begründete Ableitung?
3. Ecken gerundet, Foliage glatt?
4. Tag- und Nachtansicht geprüft?
5. Animation als Layer statt Vollbild-Neuberechnung?

### Offene Fragen
- **Engine** (Godot / Unity / Web) – noch nicht entschieden.
- Regeln für Gebäude Level 2/3 (Größe, Detailgrad).
- Stil für Figuren/Tiere und UI.
- Sprite-Pipeline: Auflösung, Atlas-Format, Speicherbudget (Vorschlag steht aus).
- Schatten-Details (Map-Größe) sind im Code gesetzt, aber nicht als Norm festgelegt.

### Changelog
- 1.0.0 – Erstfassung: Stil 3D-Toon festgelegt, Werte aus `src/toon.js` und `src/trees.js` übernommen.
