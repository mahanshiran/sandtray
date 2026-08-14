# Catalog Object Upload System

## Overview
This system allows users to upload custom 3D objects (GLB files) and thumbnail images to the sandplay catalog from within Unity.

## Unity Components Created

### 1. CatalogUploadUI.cs
**Location:** `Assets/Scripts/UI/CatalogUploadUI.cs`

A UI panel that provides an interface for uploading catalog objects. Features:
- File picker for selecting GLB model files
- File picker for thumbnail images (PNG/JPG)
- Input fields for:
  - Display name (required)
  - Category dropdown (People, Animals, Buildings, Nature, Vehicles, Abstract, Custom)
  - Tags (comma or semicolon separated)
  - Description (optional)
- Upload button with loading state
- Status messages for user feedback

**Usage:**
```csharp
// In your catalog management UI
CatalogUploadUI uploadUI = GetComponent<CatalogUploadUI>();
uploadUI.Show(catalogId); // Pass the catalog UUID
```

### 2. BackendClient Extensions
**Location:** `Assets/Scripts/Core/BackendClient.cs`

Added two new methods:

#### UploadFile
```csharp
FileUploadResult UploadFile(byte[] fileData, string fileName, string mimeType)
```
Uploads a file to the backend and returns the public URL. Use in a coroutine:
```csharp
var result = BackendClient.Instance.UploadFile(glbData, "model.glb", "model/gltf-binary");
yield return result;
if (result.Success)
{
    string modelUrl = result.FileUrl;
}
```

#### CreateCatalogObject
```csharp
CatalogObjectResult CreateCatalogObject(string catalogId, CatalogObjectCreateRequest objectData)
```
Creates a new catalog object with the provided metadata and file URLs.

## Backend Requirements

⚠️ **IMPORTANT**: The following backend endpoint must be implemented for this feature to work:

### File Upload Endpoint
**Endpoint:** `POST /api/catalogs/upload/`  
**Authentication:** Bearer token required  
**Content-Type:** `multipart/form-data`

**Request:**
```
POST /api/catalogs/upload/
Authorization: Bearer <access_token>
Content-Type: multipart/form-data

form-data:
  file: <binary file data>
```

**Success Response (201):**
```json
{
  "url": "https://your-oss-bucket.example.com/uploads/abc123.glb"
}
```

**Implementation Notes:**
1. Upload the file to your OSS/S3 bucket
2. Return the public URL that Unity can use to reference the file
3. The URL should be stable and accessible without authentication
4. Suggested path structure: `uploads/{year}/{month}/{uuid}.{ext}`

### Backend Implementation Example

Add to `api_backend/catalogs/views.py`:

```python
from rest_framework.decorators import api_view
from rest_framework.response import Response
from rest_framework import status
import boto3  # or oss2 for Alibaba Cloud
import uuid

@api_view(['POST'])
@permission_classes([permissions.IsAuthenticated])
def upload_file(request):
    """
    Upload a file to OSS/S3 and return the public URL.
    """
    if 'file' not in request.FILES:
        return Response({'error': 'No file provided'}, status=400)
    
    file_obj = request.FILES['file']
    file_extension = file_obj.name.split('.')[-1]
    unique_filename = f"{uuid.uuid4()}.{file_extension}"
    
    # Example for Alibaba Cloud OSS:
    # bucket.put_object(f'uploads/{unique_filename}', file_obj)
    # file_url = f'https://your-bucket.oss-region.aliyuncs.com/uploads/{unique_filename}'
    
    # Example for AWS S3:
    # s3_client.upload_fileobj(file_obj, 'your-bucket', f'uploads/{unique_filename}')
    # file_url = f'https://your-bucket.s3.amazonaws.com/uploads/{unique_filename}'
    
    return Response({'url': file_url}, status=201)
```

Add to `api_backend/catalogs/urls.py`:
```python
urlpatterns = [
    path('upload/', views.upload_file, name='catalog-upload'),
    # ... existing paths
]
```

## UI Setup

### Creating the Upload Panel in Unity

1. **Create the UI Panel:**
   - In your Canvas, create a new Panel GameObject
   - Name it "CatalogUploadPanel"
   - Add the `CatalogUploadUI` component

2. **Design the UI:**
   ```
   CatalogUploadPanel
   ├── Background (Image - semi-transparent)
   ├── ContentPanel (Vertical Layout Group)
       ├── TitleText ("Upload Custom Object")
       ├── DisplayNameInput (TMP_InputField)
       ├── CategoryDropdown (TMP_Dropdown)
       ├── TagsInput (TMP_InputField - placeholder: "tag1, tag2, tag3")
       ├── DescriptionInput (TMP_InputField - multiline)
       ├── GLBFileRow
       │   ├── PickGLBButton (Button - "Pick GLB Model")
       │   └── GLBFilePathText (TMP_Text - "No file selected")
       ├── ThumbnailFileRow
       │   ├── PickThumbnailButton (Button - "Pick Thumbnail")
       │   └── ThumbnailFilePathText (TMP_Text - "No file selected")
       ├── StatusText (TMP_Text - color: yellow/red)
       ├── LoadingIndicator (GameObject with rotating icon)
       ├── ButtonRow
           ├── UploadButton (Button - "Upload")
           └── CancelButton (Button - "Cancel")
   ```

3. **Assign References:**
   - Drag all UI elements to the corresponding SerializeField references in the Inspector

4. **Add Upload Button to Catalog UI:**
   ```csharp
   // In CatalogUI or your catalog management screen
   public CatalogUploadUI uploadUI;
   public Button addObjectButton;

   void Start()
   {
       addObjectButton.onClick.AddListener(() =>
       {
           // Get first user catalog or default catalog
           string catalogId = "your-catalog-uuid";
           uploadUI.Show(catalogId);
       });
   }
   ```

## File Picker Implementation

The current implementation uses Unity Editor file dialogs, which only work in the Editor and standalone builds. For production:

### Standalone (Windows/Mac/Linux)
The current `UnityEditor.EditorUtility.OpenFilePanel` works in Editor only. For standalone builds, use:
- **StandaloneFileBrowser** plugin ([GitHub](https://github.com/gkngkc/UnityStandaloneFileBrowser))
- **SimpleFileBrowser** plugin ([GitHub](https://github.com/yasirkula/UnitySimpleFileBrowser))

### Mobile (iOS/Android)
Replace file picker with:
- **NativeFilePicker** plugin ([GitHub](https://github.com/yasirkula/UnityNativeFilePicker))
- **NativeGallery** for images ([GitHub](https://github.com/yasirkula/UnityNativeGallery))

### Implementation Example with NativeFilePicker:
```csharp
private void OnPickGlbFile()
{
    NativeFilePicker.PickFile((path) =>
    {
        if (!string.IsNullOrEmpty(path))
        {
            _glbFilePath = path;
            if (_glbFilePathText) _glbFilePathText.text = Path.GetFileName(path);
        }
    }, new string[] { "model/gltf-binary", "application/octet-stream" });
}
```

## Workflow

### User Workflow:
1. User clicks "Add Custom Object" button in catalog UI
2. Upload panel opens
3. User picks a GLB file from their device
4. User picks a thumbnail image
5. User fills in object details (name, category, tags, description)
6. User clicks "Upload"
7. System uploads GLB → gets URL
8. System uploads thumbnail → gets URL
9. System creates catalog object with both URLs
10. Success message shown, UI refreshes catalog

### Technical Flow:
```
Unity Client                    Backend API                  OSS/S3
     |                               |                          |
     |--- Upload GLB --------------->|                          |
     |                               |--- Store GLB ----------->|
     |                               |<-- URL ------------------|
     |<-- GLB URL -------------------|                          |
     |                               |                          |
     |--- Upload Thumbnail --------->|                          |
     |                               |--- Store Image --------->|
     |                               |<-- URL ------------------|
     |<-- Thumbnail URL -------------|                          |
     |                               |                          |
     |--- Create Object (URLs) ----->|                          |
     |<-- Success (Object JSON) -----|                          |
```

## Testing

### 1. Test Upload Endpoint First
```bash
curl -X POST http://43.99.51.164:8000/api/catalogs/upload/ \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@test_model.glb"

# Expected response:
# {"url":"https://your-bucket.oss-region.aliyuncs.com/uploads/abc123.glb"}
```

### 2. Test in Unity Editor
- Open the scene with catalog UI
- Click the upload button
- Select a GLB file (any 3D model exported as GLB)
- Select a thumbnail image
- Fill in object details
- Click Upload
- Check console for upload progress and results

### 3. Verify Backend
- Check Django admin: `http://43.99.51.164:8000/admin/catalogs/catalogobject/`
- Confirm new object was created with correct URLs
- Test that URLs are accessible (open in browser)

## Catalog Object Categories

The system supports these categories (matching backend):
- `people` - Human figures
- `animals` - Animal figures
- `buildings` - Structures, houses, etc.
- `nature` - Trees, plants, natural elements
- `vehicles` - Cars, boats, planes, etc.
- `abstract` - Abstract shapes and concepts
- `mythological` - Mythological creatures and figures
- `religious` - Religious symbols and figures
- `custom` - User-defined custom objects

## Error Handling

The system handles these error cases:
- No file selected → Shows "Please select a valid GLB/image file"
- Not logged in → Shows "Not logged in"
- Upload failed → Shows backend error message
- Network timeout → Shows timeout error
- Invalid file format → Backend should validate and return error

## Future Enhancements

1. **Preview before upload:**
   - Load and display GLB preview in Unity
   - Show thumbnail preview

2. **File validation:**
   - Check file size limits (e.g., < 10MB for GLB)
   - Validate GLB format before upload
   - Auto-generate thumbnail from GLB if not provided

3. **Batch upload:**
   - Upload multiple objects at once
   - Progress bar for each file

4. **Catalog management:**
   - Edit existing objects
   - Delete objects
   - Manage user catalogs

5. **Advanced metadata:**
   - Age range (age_range_min, age_range_max)
   - Cultural tags
   - Usage statistics

## Security Considerations

1. **Backend should validate:**
   - File size limits
   - File type (only GLB and images allowed)
   - User permissions (only allow uploads to user's own catalogs)
   - Virus scanning for uploaded files

2. **Unity should validate:**
   - File extensions before upload
   - Form completeness
   - Token expiration

## Notes

- The backend model already supports all required fields
- Model URLs are automatically hashed (SHA-256) by the backend
- Unity clients cache GLB files by hash to avoid re-downloading
- The `NetworkCatalogRegistry` system will automatically incorporate new objects after catalog refresh
