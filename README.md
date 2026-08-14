# Sandplay Hakoniwa Therapy Game - Quick Start

## ▶️ How to Run (Simplest Method)

1. **Open Unity 2022.3 LTS** with this project
2. Wait for scripts to compile (watch bottom-right status bar)
3. **Press Play ▶️** — that's it!

The game will auto-bootstrap itself when you press Play. You'll see a 3D sandbox with tools and UI.

---

## 🎮 Controls

### Camera
- **Right-click + drag** — Orbit around sandbox
- **Scroll wheel** — Zoom in/out  
- **Middle-click + drag** — Pan camera
- **Touch (mobile):** Pinch to zoom, two-finger drag to pan

### Tools (Click toolbar buttons)
- **Select** — Click objects to select/deselect
- **Raise** — Drag on sand to raise it
- **Dig** — Drag on sand to dig/lower it
- **Smooth** — Blend sand heights smoothly
- **Flatten** — Flatten sand to base height
- **Move** — Drag selected object
- **Rotate** — Rotate selected object with mouse drag

### Actions (Top bar)
- **Catalog** — Open object library to place items
- **Save** — Save current session (QuickSave)
- **Load** — Load saved session
- **Photo** — Take screenshot (saved to persistent storage)
- **Analyze** — View spatial/terrain analysis (configure AI in AIAnalysisManager for full interpretation)
- **Delete** — Remove selected object
- **Undo/Redo** — Ctrl+Z / Ctrl+Shift+Z

---

## 🏗️ What's Available

### Objects (15 prototypes)
- **People:** Adult, Child  
- **Animals:** Dog, Cat, Bird
- **Buildings:** House, Tower
- **Nature:** Tree, Rock, Bush, Flower
- **Vehicles:** Car, Boat
- **Abstract:** Sphere, Cube, Cylinder

All objects are placeholder primitives. Replace with real models via the Object Catalog system.

---

## 🔧 Configuration

### AI Analysis (Optional)
Edit `AIAnalysisManager` in scene (after first Play):
1. Set `API Endpoint` (e.g., `https://api.openai.com/v1/chat/completions`)
2. Set `API Key` (your OpenAI/Claude key)
3. Set `Model` (e.g., `gpt-4` or `claude-3-opus`)

Without API config, you'll get local analysis (spatial stats, clustering, terrain metrics).

### Game Settings
After first Play, find `DefaultGameConfig` in Project:
- Sandbox size, sand height limits
- Brush radius/strength ranges  
- Camera orbit/zoom speeds
- Max undo steps

---

## 📁 Project Structure

```
Assets/
├── Scripts/
│   ├── Core/         — EventBus, GameManager, InputHelper, AutoBootstrap
│   ├── Camera/       — Orbit/pan/zoom controller
│   ├── Sand/         — Deformable heightmap mesh + tools
│   ├── Objects/      — Catalog system, placement, interaction
│   ├── Data/         — Save/load, undo/redo, screenshots
│   ├── AI/           — Analysis extraction & API integration
│   └── UI/           — HUD, catalog panel, analysis view
├── Materials/        — Sand.shader
└── Scenes/           — SampleScene.unity
```

---

## 🚀 Next Steps

### Add Real 3D Models
1. Import `.fbx`/`.obj` models to `Assets/Models/`
2. Create prefabs in `Assets/Prefabs/Objects/`
3. Create `SandplayObject` ScriptableObjects for each  
   (Right-click → Create → Sandplay → Sandplay Object)
4. Assign to `ObjectCatalog` (will be auto-generated after first Play)

### Backend Integration (Future)
- All state changes route through `EventBus` → add network layer
- `SessionData` JSON is ready for cloud sync
- `ObjectCatalog` can load from API via `ICatalogProvider` pattern
- Undo/Redo `ICommand` pattern = network messages

### Build Targets
- **PC/Mac:** Works out of the box
- **WebGL:** Test with simplified sand resolution (64x64)
- **Mobile:** Enable touch controls (already supported), test performance

---

## ⚠️ Troubleshooting

**"Nothing happens when I press Play"**
- Make sure Unity finished compiling (no spinner in bottom-right)
- Check Console for errors (Ctrl+Shift+C)
- Verify `AutoBootstrap.cs` is in the project

**"Performance is slow"**
- Reduce `HeightmapResolution` in GameConfig (128 → 64)
- Reduce brush radius range
- Disable shadows on Directional Light

**"Objects won't place"**
- Click **Catalog** button first to open object library
- Select an object from the list
- Click on the sand surface to place

**"AI Analysis says 'not configured'"**
- Expected! Configure API key in `AIAnalysisManager` component
- Local analysis (spatial/terrain stats) works without API

---

## 📝 Credits

Built with Unity 2022.3 LTS  
Architecture: Event-driven, command pattern, online-ready  
Analysis: Jungian sandplay therapy framework (Dora Kalff method)

---

**Ready to start!** Just press Play ▶️
# sandtray
