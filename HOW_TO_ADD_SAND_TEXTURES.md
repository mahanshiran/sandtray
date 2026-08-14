# How to Add Custom Sand Textures (e.g., Yughues Free Sand Materials)

## Step 1: Import the Asset from Unity Asset Store

1. In Unity Editor: **Window** → **Package Manager**
2. Top-left dropdown → Select **My Assets**
3. If you haven't added the asset yet:
   - Click on any asset in the list or search bar
   - This opens the Asset Store view within Package Manager
   - Search for "Yughues Free Sand Materials": https://assetstore.unity.com/packages/2d/textures-materials/nature/yughues-free-sand-materials-12964
   - Click **Add to My Assets** (it's free!)
4. Back in **My Assets** view, find "Yughues Free Sand Materials"
5. Click **Download**
6. After download completes → Click **Import**
7. In the import dialog, you can uncheck materials if you only want textures
8. Click **Import** to finish

## Step 2: Assign Textures to SceneBootstrapper

After importing, the textures will be in your Assets folder (typically `Assets/Yughues Free Sand Materials/`).

1. In Unity Hierarchy, find your **SceneBootstrapper** GameObject
2. Select it to view in Inspector
3. Look for the **"Optional: Custom Sand Textures"** section
4. Drag and drop textures from the Yughues folder:
   - **Custom Sand Texture**: Drag one of the sand diffuse/albedo textures (e.g., `Sand_001_COLOR.jpg`)
   - **Custom Sand Normal Map**: Drag the corresponding normal map (e.g., `Sand_001_NORM.jpg`)

## Step 3: Enter Play Mode

That's it! When you enter Play Mode:
- The custom textures will be used for the sand surface
- The console will show: `"Using custom sand texture: Sand_001_COLOR"`
- If fields are empty, it falls back to procedurally generated textures

## Tips

- **Texture Scale**: The textures use a 6×6 tiling (defined in code). You can adjust this in [SceneBootstrapper.cs](Assets/Scripts/Core/SceneBootstrapper.cs) if the pattern looks too large or small.
- **Try Different Sands**: The Yughues pack has multiple sand variations - experiment to find your favorite!
- **Performance**: Custom textures are more efficient than procedural generation.
- **Normal Maps**: Normal maps add surface bumps for realistic lighting. They're optional but recommended.

## Other Asset Store Packages (Same Process)

This works for ANY Unity Asset Store package:
1. Add to My Assets on Asset Store
2. Download via Package Manager → My Assets
3. Import
4. Assign in Inspector or reference in code

For packages with code/scripts, they'll be available in your project after import.
