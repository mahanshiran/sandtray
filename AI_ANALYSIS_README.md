# AI Board Analysis System

## Overview
The Sandplay app includes AI-powered psychological analysis of sandbox scenes using Aliyun Bailian (DashScope) API. The system analyzes:
- Object placement and symbolism
- Spatial patterns (left/right, center/edge, clustered/spread)
- Sand terrain formations
- Color choices
- Psychological themes and insights

## Setup Instructions

### 1. Get Aliyun Bailian API Key
1. Sign up at https://www.aliyun.com/
2. Navigate to DashScope service (https://dashscope.console.aliyun.com/)
3. Create an API key
4. Copy the API key (format: `sk-...`)

### 2. Configure in Unity Editor
1. Open your scene in Unity
2. Find the `BailianClient` GameObject (created automatically by SceneBootstrapper)
3. In Inspector, paste your API key into the "Api Key" field
4. Choose model:
   - `qwen-plus` - Best for text-based analysis (faster, cheaper)
   - `qwen-vl-plus` - Vision model for image + text analysis (more detailed)

### 3. Configure AIAnalysisManager
1. Find `AIAnalysisManager` GameObject
2. Settings:
   - **Use Vision Model**: Check this to send screenshot to AI (recommended)
   - **Use Bailian**: Should be checked (uses Aliyun API)
   - If unchecked, will use OpenAI-compatible API (requires separate setup)

## How to Use

### In-Game Analysis
1. Create a sandbox scene with objects
2. Click the "AI Analysis" button in the left toolbar
3. In the Analysis Panel:
   - Click "🤖 AI Analysis" for full AI interpretation
   - Click "📊 Local Stats" for numerical summary only
4. Wait for results (usually 5-15 seconds)
5. Read the therapeutic insights provided

### Setting API Key at Runtime
```csharp
// If you want to let users enter their own API key
BailianClient.Instance.SetApiKey("sk-your-api-key-here");
```

### Programmatic Analysis
```csharp
// Trigger analysis from code
EventBus.Publish(new AnalysisRequestedEvent());
```

## API Costs (Approximate)
- **qwen-plus** (text): ~¥0.02 per analysis
- **qwen-vl-plus** (vision): ~¥0.08 per analysis

Pricing may vary. Check https://help.aliyun.com/zh/dashscope/developer-reference/tongyi-qianwen-metering-and-billing

## Technical Details

### Data Captured
The system sends to AI:
- **Screenshot** (optional, base64-encoded PNG)
- **Object List**: All placed objects with positions, types, rotations
- **Spatial Metrics**: Left/right balance, center/edge distribution, spread score
- **Terrain Data**: Average height, variance, mound/valley counts
- **Cluster Analysis**: Groups of nearby objects

### Psychology Prompt
The AI is instructed to analyze using:
- Jungian psychology principles
- Dora Kalff's sandplay therapy method
- Spatial symbolism (left=unconscious, right=conscious, etc.)
- Object archetypes and relationships

### Analysis Structure
AI response includes:
1. **Overview** - Brief scene description
2. **Spatial Analysis** - Placement pattern meanings
3. **Object Symbolism** - Key symbolic interpretations
4. **Terrain Analysis** - What sand formations suggest
5. **Cluster Analysis** - Relationship themes
6. **Therapeutic Insights** - Overall psychological themes
7. **Recommendations** - Follow-up questions for therapist

## Troubleshooting

### "Bailian API key not configured"
- Check that API key is set in BailianClient component
- Verify the key starts with "sk-"
- Try calling `BailianClient.Instance.SetApiKey("your-key")` at runtime

### "API Error: 401 Unauthorized"
- API key is invalid or expired
- Get a new key from DashScope console

### "API Error: 429 Rate Limit"
- You've exceeded your free tier or rate limit
- Wait a few minutes and try again
- Check your Aliyun billing/quota settings

### "Invalid response format"
- Model name might be incorrect
- Check endpoint is correct (auto-set for Bailian)
- Try switching between text and vision models

### Analysis shows local stats instead of AI results
- This is fallback behavior when AI fails
- Check console for error messages
- Verify internet connection

## Files Modified/Created
- `/Assets/Scripts/AI/BailianClient.cs` - Aliyun API client
- `/Assets/Scripts/AI/AIAnalysisManager.cs` - Analysis orchestrator
- `/Assets/Scripts/AI/AnalysisExtractor.cs` - Data capture (existing)
- `/Assets/Scripts/UI/AnalysisUI.cs` - Results display (existing)
- `/Assets/Scripts/Core/SceneBootstrapper.cs` - UI integration

## Security Notes
- **Never** commit API keys to source control
- Use Unity's inspector to set keys in editor builds
- For production apps, store keys securely (e.g., server-side API proxy)
- Consider implementing user-supplied keys for public releases

## Future Enhancements
- Save analysis history to disk
- Export reports as PDF
- Compare multiple sessions over time
- Sentiment tracking across sessions
- Multi-language support (Bailian supports Chinese natively)
