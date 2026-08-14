# Backend Implementation - File Upload Endpoint

## Complete Implementation (Copy-Paste Ready)

### Option 1: Using Alibaba Cloud OSS (Recommended for China)

#### Step 1: Install OSS SDK
```bash
cd api_backend
source venv/bin/activate  # or venv\Scripts\activate on Windows
pip install oss2
pip freeze > requirements.txt
```

#### Step 2: Add to views.py

**File:** `api_backend/catalogs/views.py`

Add these imports at the top:
```python
import uuid
import oss2
from django.conf import settings
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import IsAuthenticated
from rest_framework.response import Response
```

Add this function (paste at the end of the file, before any other functions):
```python
@api_view(['POST'])
@permission_classes([IsAuthenticated])
def upload_file(request):
    """
    Upload a file (GLB model or image) to Alibaba Cloud OSS.
    Returns the public URL for the uploaded file.
    
    Expected request: multipart/form-data with 'file' field
    Returns: {"url": "https://..."}
    """
    if 'file' not in request.FILES:
        return Response({'error': 'No file provided'}, status=400)
    
    uploaded_file = request.FILES['file']
    file_extension = uploaded_file.name.split('.')[-1].lower()
    
    # Validate file type
    allowed_extensions = ['glb', 'gltf', 'png', 'jpg', 'jpeg']
    if file_extension not in allowed_extensions:
        return Response({'error': f'File type .{file_extension} not allowed'}, status=400)
    
    # Validate file size (10MB limit)
    max_size = 10 * 1024 * 1024  # 10MB in bytes
    if uploaded_file.size > max_size:
        return Response({'error': 'File too large (max 10MB)'}, status=400)
    
    try:
        # Generate unique filename
        unique_filename = f"{uuid.uuid4()}.{file_extension}"
        object_key = f"catalog-objects/{unique_filename}"
        
        # Initialize OSS client
        auth = oss2.Auth(
            settings.OSS_ACCESS_KEY_ID,
            settings.OSS_ACCESS_KEY_SECRET
        )
        bucket = oss2.Bucket(
            auth,
            settings.OSS_ENDPOINT,
            settings.OSS_BUCKET_NAME
        )
        
        # Upload file
        bucket.put_object(object_key, uploaded_file.read())
        
        # Generate public URL
        file_url = f"https://{settings.OSS_BUCKET_NAME}.{settings.OSS_ENDPOINT.replace('http://', '').replace('https://', '')}/{object_key}"
        
        return Response({'url': file_url}, status=201)
        
    except Exception as e:
        return Response({'error': str(e)}, status=500)
```

#### Step 3: Add to urls.py

**File:** `api_backend/catalogs/urls.py`

Add this import if not already present:
```python
from . import views
```

Add this to `urlpatterns`:
```python
urlpatterns = [
    path('upload/', views.upload_file, name='catalog-upload'),  # ADD THIS LINE
    path('public/', views.public_catalog, name='catalog-public'),
    path('', include(router.urls)),
    # ... rest of existing paths
]
```

#### Step 4: Configure OSS Settings

**File:** `api_backend/sandtray_api/settings.py`

Add these settings at the end:
```python
# ── Alibaba Cloud OSS Configuration ──────────────────────────────────

OSS_ACCESS_KEY_ID = os.environ.get('OSS_ACCESS_KEY_ID', '')
OSS_ACCESS_KEY_SECRET = os.environ.get('OSS_ACCESS_KEY_SECRET', '')
OSS_ENDPOINT = os.environ.get('OSS_ENDPOINT', 'oss-cn-hangzhou.aliyuncs.com')
OSS_BUCKET_NAME = os.environ.get('OSS_BUCKET_NAME', '')
```

#### Step 5: Set Environment Variables

**Local Development (.env file):**
```bash
# Add to api_backend/.env
OSS_ACCESS_KEY_ID=your_oss_access_key
OSS_ACCESS_KEY_SECRET=your_oss_secret_key
OSS_ENDPOINT=oss-cn-hangzhou.aliyuncs.com
OSS_BUCKET_NAME=your-bucket-name
```

**Production Server:**
```bash
ssh root@43.99.51.164
cd /root/sandplay/api_backend

# Edit .env.production
nano .env.production

# Add these lines:
export OSS_ACCESS_KEY_ID="your_oss_access_key"
export OSS_ACCESS_KEY_SECRET="your_oss_secret_key"
export OSS_ENDPOINT="oss-cn-hangzhou.aliyuncs.com"
export OSS_BUCKET_NAME="your-bucket-name"

# Save and exit (Ctrl+X, Y, Enter)

# Restart the service
systemctl restart sandplay-api
```

#### Step 6: Create OSS Bucket (if not exists)

1. Go to Alibaba Cloud OSS Console: https://oss.console.aliyun.com/
2. Create a new bucket:
   - Name: `sandplay-assets` (or your choice)
   - Region: Same as your server (e.g., Hangzhou)
   - ACL: **Public Read** (important for Unity to download files)
3. Note down:
   - Bucket name
   - Endpoint URL
   - Access Key ID and Secret

---

### Option 2: Using AWS S3

#### Step 1: Install Boto3
```bash
cd api_backend
source venv/bin/activate
pip install boto3
pip freeze > requirements.txt
```

#### Step 2: Add to views.py

**File:** `api_backend/catalogs/views.py`

```python
import uuid
import boto3
from django.conf import settings
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import IsAuthenticated
from rest_framework.response import Response

@api_view(['POST'])
@permission_classes([IsAuthenticated])
def upload_file(request):
    """
    Upload a file to AWS S3.
    Returns the public URL for the uploaded file.
    """
    if 'file' not in request.FILES:
        return Response({'error': 'No file provided'}, status=400)
    
    uploaded_file = request.FILES['file']
    file_extension = uploaded_file.name.split('.')[-1].lower()
    
    # Validate
    allowed_extensions = ['glb', 'gltf', 'png', 'jpg', 'jpeg']
    if file_extension not in allowed_extensions:
        return Response({'error': f'File type .{file_extension} not allowed'}, status=400)
    
    if uploaded_file.size > 10 * 1024 * 1024:
        return Response({'error': 'File too large (max 10MB)'}, status=400)
    
    try:
        unique_filename = f"{uuid.uuid4()}.{file_extension}"
        object_key = f"catalog-objects/{unique_filename}"
        
        # Upload to S3
        s3_client = boto3.client(
            's3',
            aws_access_key_id=settings.AWS_ACCESS_KEY_ID,
            aws_secret_access_key=settings.AWS_SECRET_ACCESS_KEY,
            region_name=settings.AWS_S3_REGION
        )
        
        s3_client.upload_fileobj(
            uploaded_file,
            settings.AWS_STORAGE_BUCKET_NAME,
            object_key,
            ExtraArgs={'ACL': 'public-read'}
        )
        
        file_url = f"https://{settings.AWS_STORAGE_BUCKET_NAME}.s3.{settings.AWS_S3_REGION}.amazonaws.com/{object_key}"
        
        return Response({'url': file_url}, status=201)
        
    except Exception as e:
        return Response({'error': str(e)}, status=500)
```

#### Step 3: Configure S3 Settings

**File:** `api_backend/sandtray_api/settings.py`

```python
# ── AWS S3 Configuration ──────────────────────────────────────────────

AWS_ACCESS_KEY_ID = os.environ.get('AWS_ACCESS_KEY_ID', '')
AWS_SECRET_ACCESS_KEY = os.environ.get('AWS_SECRET_ACCESS_KEY', '')
AWS_STORAGE_BUCKET_NAME = os.environ.get('AWS_STORAGE_BUCKET_NAME', '')
AWS_S3_REGION = os.environ.get('AWS_S3_REGION', 'us-east-1')
```

---

### Option 3: Simple Local Storage (Development Only)

⚠️ **Not recommended for production** - files will be lost on server restart

#### Step 1: Add to views.py

```python
import os
import uuid
from django.conf import settings
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import IsAuthenticated
from rest_framework.response import Response

@api_view(['POST'])
@permission_classes([IsAuthenticated])
def upload_file(request):
    """
    Upload file to local media folder (development only).
    """
    if 'file' not in request.FILES:
        return Response({'error': 'No file provided'}, status=400)
    
    uploaded_file = request.FILES['file']
    file_extension = uploaded_file.name.split('.')[-1].lower()
    
    # Validate
    allowed_extensions = ['glb', 'gltf', 'png', 'jpg', 'jpeg']
    if file_extension not in allowed_extensions:
        return Response({'error': f'File type not allowed'}, status=400)
    
    if uploaded_file.size > 10 * 1024 * 1024:
        return Response({'error': 'File too large (max 10MB)'}, status=400)
    
    try:
        # Create uploads directory
        upload_dir = os.path.join(settings.MEDIA_ROOT, 'catalog-objects')
        os.makedirs(upload_dir, exist_ok=True)
        
        # Generate unique filename
        unique_filename = f"{uuid.uuid4()}.{file_extension}"
        file_path = os.path.join(upload_dir, unique_filename)
        
        # Save file
        with open(file_path, 'wb+') as destination:
            for chunk in uploaded_file.chunks():
                destination.write(chunk)
        
        # Generate URL
        file_url = f"{settings.MEDIA_URL}catalog-objects/{unique_filename}"
        # Convert to absolute URL
        if not file_url.startswith('http'):
            file_url = request.build_absolute_uri(file_url)
        
        return Response({'url': file_url}, status=201)
        
    except Exception as e:
        return Response({'error': str(e)}, status=500)
```

#### Step 2: Configure Media Settings

**File:** `api_backend/sandtray_api/settings.py`

```python
import os

MEDIA_URL = '/media/'
MEDIA_ROOT = os.path.join(BASE_DIR, 'media')
```

**File:** `api_backend/sandtray_api/urls.py`

```python
from django.conf import settings
from django.conf.urls.static import static

urlpatterns = [
    # ... existing paths
] + static(settings.MEDIA_URL, document_root=settings.MEDIA_ROOT)
```

---

## Testing the Endpoint

### Test with cURL:

```bash
# First, login and get token
TOKEN=$(curl -X POST http://43.99.51.164:8000/api/auth/login/ \
  -H "Content-Type: application/json" \
  -d '{"email":"your@email.com","password":"yourpassword"}' \
  | python -m json.tool | grep access | cut -d'"' -f4)

echo "Token: $TOKEN"

# Upload a test file
curl -X POST http://43.99.51.164:8000/api/catalogs/upload/ \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@test_model.glb" \
  | python -m json.tool

# Expected output:
# {
#   "url": "https://your-bucket.oss-cn-hangzhou.aliyuncs.com/catalog-objects/abc-123.glb"
# }
```

### Test with Python:

```python
import requests

# Login
response = requests.post('http://43.99.51.164:8000/api/auth/login/', json={
    'email': 'your@email.com',
    'password': 'yourpassword'
})
token = response.json()['access']

# Upload file
with open('test_model.glb', 'rb') as f:
    response = requests.post(
        'http://43.99.51.164:8000/api/catalogs/upload/',
        headers={'Authorization': f'Bearer {token}'},
        files={'file': f}
    )
    print(response.json())
```

---

## Deployment Checklist

- [ ] Install oss2 or boto3 package
- [ ] Add upload_file view to catalogs/views.py
- [ ] Add 'upload/' path to catalogs/urls.py
- [ ] Add OSS/S3 settings to settings.py
- [ ] Set environment variables
- [ ] Create OSS bucket with public read access
- [ ] Restart Django server
- [ ] Test endpoint with cURL
- [ ] Test from Unity

---

## Security Best Practices

1. **File Type Validation:**
   - Only allow .glb, .gltf, .png, .jpg
   - Check file magic numbers, not just extensions

2. **File Size Limits:**
   - Enforce max 10MB per file
   - Consider separate limits for models vs images

3. **User Permissions:**
   - Require authentication
   - Optional: Require subscription for uploads
   - Rate limiting (Django REST Framework throttling)

4. **Malware Scanning:**
   - Integrate ClamAV or similar for production
   - Scan files before uploading to OSS

5. **Bucket Security:**
   - Set bucket ACL to public-read (not public-read-write!)
   - Use separate bucket for user uploads
   - Enable bucket logging

6. **API Rate Limiting:**

Add to `settings.py`:
```python
REST_FRAMEWORK = {
    'DEFAULT_THROTTLE_CLASSES': [
        'rest_framework.throttling.UserRateThrottle',
    ],
    'DEFAULT_THROTTLE_RATES': {
        'user': '100/hour',  # 100 uploads per hour per user
    }
}
```

---

## Troubleshooting

**Error: "OSS Access Denied"**
- Check access key and secret
- Verify bucket permissions (public read)
- Check bucket region matches endpoint

**Error: "File too large"**
- Increase max size in view
- Check OSS/S3 upload limits
- Check nginx/gunicorn upload size limits

**Error: "Connection refused"**
- Check OSS endpoint URL
- Verify firewall allows outbound connections
- Test bucket access from server: `curl https://your-bucket.oss-cn-hangzhou.aliyuncs.com`

**Files not accessible:**
- Ensure bucket has public-read ACL
- Test URL in browser
- Check CORS settings if accessing from browser

---

## Cost Estimates

### Alibaba Cloud OSS:
- Storage: ¥0.12/GB/month
- Outbound traffic: ¥0.5/GB (first 10TB)
- PUT requests: ¥0.01/10,000 requests
- **Estimated:** ~¥10-50/month for moderate usage

### AWS S3:
- Storage: $0.023/GB/month
- Outbound traffic: $0.09/GB (first 10TB)
- PUT requests: $0.005/1,000 requests
- **Estimated:** ~$5-30/month for moderate usage

---

Choose the option that fits your infrastructure and deploy! 🚀
