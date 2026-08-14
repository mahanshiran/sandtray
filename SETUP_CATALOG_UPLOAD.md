# Quick Setup Guide - Catalog Upload Feature

## ✅ What's Already Done (Unity Code)

The following Unity components have been created and are ready to use:

1. **CatalogUploadUI.cs** - Main upload panel UI
2. **CatalogUploadButton.cs** - Helper component for adding upload buttons
3. **BackendClient extensions** - File upload and catalog object creation methods

## ⚠️ What's Needed (Backend)

You need to add ONE endpoint to your Django backend:

### File Upload Endpoint

**File:** `api_backend/catalogs/views.py`

Add this function:

```python
@api_view(['POST'])
@permission_classes([permissions.IsAuthenticated])
def upload_file(request):
    """Upload a file to OSS/S3 and return the public URL."""
    import uuid
    import oss2  # or boto3 for AWS S3
    
    if 'file' not in request.FILES:
        return Response({'error': 'No file provided'}, status=400)
    
    file_obj = request.FILES['file']
    file_extension = file_obj.name.split('.')[-1]
    unique_filename = f"{uuid.uuid4()}.{file_extension}"
    
    # TODO: Configure your OSS/S3 credentials
    # Example for Alibaba Cloud OSS:
    # auth = oss2.Auth('your-access-key', 'your-secret-key')
    # bucket = oss2.Bucket(auth, 'oss-endpoint', 'bucket-name')
    # bucket.put_object(f'uploads/{unique_filename}', file_obj)
    # file_url = f'https://your-bucket.oss-region.aliyuncs.com/uploads/{unique_filename}'
    
    return Response({'url': file_url}, status=201)
```

**File:** `api_backend/catalogs/urls.py`

Add this to urlpatterns:
```python
path('upload/', views.upload_file, name='catalog-upload'),
```

That's it for the backend! The catalog object creation endpoint already exists.

## 🎨 Unity Setup (5 minutes)

### Step 1: Create the Upload Panel

1. **In your Unity scene's Canvas:**
   - Right-click → UI → Panel
   - Name it "CatalogUploadPanel"
   - Set it inactive by default (uncheck at top of Inspector)

2. **Add the script:**
   - Add Component → `CatalogUploadUI`

3. **Create child UI elements:**

   Use this hierarchy:
   ```
   CatalogUploadPanel (with CatalogUploadUI script)
   ├── ContentPanel (Vertical Layout Group)
       ├── TitleText: "Upload Custom Object"
       ├── DisplayNameInput (TMP Input Field)
       ├── CategoryDropdown (TMP Dropdown)
       ├── TagsInput (TMP Input Field)
       │   └── Placeholder: "tag1, tag2, tag3"
       ├── DescriptionInput (TMP Input Field - multiline)
       ├── GLBRow (Horizontal Layout Group)
       │   ├── PickGLBButton: "Pick GLB Model"
       │   └── GLBPathText: "No file selected"
       ├── ThumbnailRow (Horizontal Layout Group)
       │   ├── PickThumbnailButton: "Pick Thumbnail"
       │   └── ThumbnailPathText: "No file selected"
       ├── StatusText (TMP Text - yellow color)
       ├── LoadingSpinner (optional rotating image)
       └── ButtonRow (Horizontal Layout Group)
           ├── UploadButton: "Upload"
           └── CancelButton: "Cancel"
   ```

4. **Wire up the script:**
   - In CatalogUploadUI Inspector, drag all UI elements to their corresponding fields
   - Assign the panel GameObject to the `_panel` field

### Step 2: Add Upload Button to Your Catalog UI

**Option A: Quick Method (using helper component)**

1. Find your catalog UI (wherever you show the object catalog)
2. Add a button called "Add Custom Object"
3. Add Component → `CatalogUploadButton`
4. In Inspector:
   - Assign the `CatalogUploadPanel` to Upload UI field
   - Set Catalog ID (your catalog's UUID)
   - Optional: Check "Requires Subscription" if you want only VIP users to upload

Done! The button will automatically show/hide based on login state.

**Option B: Manual Integration**

In your existing catalog UI script:
```csharp
using Sandplay.UI;

public class YourCatalogUI : MonoBehaviour
{
    [SerializeField] private CatalogUploadUI _uploadUI;
    [SerializeField] private Button _addObjectButton;
    
    void Start()
    {
        _addObjectButton.onClick.AddListener(() =>
        {
            string catalogId = "your-catalog-uuid"; // Get this from backend
            _uploadUI.Show(catalogId);
        });
    }
}
```

### Step 3: File Picker (Editor/Standalone Only)

The current implementation uses Unity Editor file dialogs. This works for:
- ✅ Unity Editor (testing)
- ✅ Windows/Mac/Linux standalone builds

For production builds on other platforms, you'll need a file picker plugin:
- **Recommended:** [SimpleFileBrowser](https://github.com/yasirkula/UnitySimpleFileBrowser)
- Install from Asset Store or GitHub
- Replace the `#if UNITY_EDITOR` blocks in CatalogUploadUI.cs

## 🧪 Testing

### 1. Test Backend Endpoint First
```bash
# Upload a test file
curl -X POST http://43.99.51.164:8000/api/catalogs/upload/ \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -F "file=@test.glb"

# Expected response:
# {"url":"https://your-oss-bucket.example.com/uploads/abc123.glb"}
```

### 2. Test in Unity Editor

1. Press Play in Unity Editor
2. Login to your account
3. Click "Add Custom Object" button
4. Pick a GLB file from your computer
5. Pick a thumbnail image
6. Fill in the form:
   - Display Name: "My Custom Object"
   - Category: Select from dropdown
   - Tags: "custom, test"
7. Click Upload
8. Watch the console for progress
9. Check status text in UI

### 3. Verify Success

Check Django admin:
```
http://43.99.51.164:8000/admin/catalogs/catalogobject/
```
Your new object should appear with the model_url and thumbnail_url filled in.

## 📝 Summary of Changes

### New Files Created:
1. `/Assets/Scripts/UI/CatalogUploadUI.cs` - Upload panel UI logic
2. `/Assets/Scripts/UI/CatalogUploadButton.cs` - Helper for easy integration
3. `/CATALOG_UPLOAD_GUIDE.md` - Detailed documentation

### Modified Files:
1. `/Assets/Scripts/Core/BackendClient.cs`:
   - Added `UploadFile()` method
   - Added `CreateCatalogObject()` method
   - Added `FileUploadResult` and `CatalogObjectResult` helper classes

## 🔒 Security Notes

- Upload button only shows when user is logged in
- Backend validates authentication
- Optional: Require subscription for uploads
- Backend should validate file types and sizes
- Files are uploaded to OSS/S3, not stored in Django

## 🎯 Next Steps

1. ✅ Unity code is complete
2. ⏳ Implement backend upload endpoint (10 minutes)
3. ⏳ Configure OSS/S3 credentials
4. ⏳ Create the Unity UI panel (5 minutes)
5. ⏳ Test the feature
6. 🚀 Ship it!

## 💡 Tips

- Start with a simple test catalog with just your user
- Test with small GLB files first (< 1MB)
- Use 256x256 PNG thumbnails for best performance
- Consider adding file size limits in backend (e.g., 10MB max)

## 🐛 Troubleshooting

**Problem:** "Not logged in" error  
**Solution:** Ensure user is logged in via BackendClient.Login()

**Problem:** "No catalog ID set"  
**Solution:** Set the catalog ID in CatalogUploadButton Inspector or pass it to Show()

**Problem:** "Upload succeeded but no URL returned"  
**Solution:** Backend must return `{"url": "..."}` format

**Problem:** File picker doesn't work  
**Solution:** Install SimpleFileBrowser plugin for non-editor builds

**Problem:** Compilation errors with "using UnityEditor"  
**Solution:** Wrap editor-only code in `#if UNITY_EDITOR` blocks

Need help? Check the full documentation in CATALOG_UPLOAD_GUIDE.md
