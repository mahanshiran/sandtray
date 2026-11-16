using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sandplay.UI
{
    /// <summary>A non-modal side pane which reserves a real camera viewport beside it.</summary>
    public sealed class ResizableReportDock : MonoBehaviour
    {
        // Start with equal space for the table and report.
        public const float DefaultReportFraction = .5f;
        public Action LayoutChanged;
        public Action<float> Resized;
        public Action Closed;
        private RectTransform surface, pane, card;
        private UnityEngine.Camera sceneCamera, backgroundCamera;
        private Rect originalViewport;
        private bool initialized;
        private float reportFraction=DefaultReportFraction;
        private float heightFraction=.92f,defaultFraction=DefaultReportFraction;
        private float minimumPaneWidth=280f,defaultPaneWidth,maximumPaneWidth=float.PositiveInfinity;
        private Rect lastSurfaceScreenRect;
        private Vector2 lastScreenSize;
        private readonly Vector3[] corners=new Vector3[4];
        public float ReportFraction => reportFraction;

        public void Initialize(RectTransform availableSurface, RectTransform reportCard, UnityEngine.Camera camera, float fraction,
            float topFraction=.92f,float resetFraction=DefaultReportFraction,float minimumWidth=280f,float resetWidth=0f,
            float maximumWidth=float.PositiveInfinity)
        {
            surface=availableSurface;pane=(RectTransform)transform;card=reportCard;sceneCamera=camera;
            if(!initialized&&sceneCamera!=null)originalViewport=sceneCamera.rect;
            heightFraction=Mathf.Clamp01(topFraction);defaultFraction=resetFraction;
            minimumPaneWidth=minimumWidth;defaultPaneWidth=resetWidth;maximumPaneWidth=maximumWidth;
            reportFraction=fraction;initialized=true;
            Apply();
        }
        public static float ClampReportFraction(float fraction,float width,float minimumWidth=280f,float maximumWidth=float.PositiveInfinity)
        {
            if(width<=0)return DefaultReportFraction;
            float minReport=Mathf.Min(minimumWidth,width*.55f);
            float minTable=Mathf.Min(320f,width*.45f);
            return Mathf.Clamp(fraction,minReport/width,Mathf.Min(1f-minTable/width,maximumWidth/width));
        }
        public void ResizeFromScreen(Vector2 position,UnityEngine.Camera eventCamera)
        {
            if(!initialized||surface==null)return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,position,eventCamera,out var point))
            {
                reportFraction=1f-(point.x-surface.rect.xMin)/Mathf.Max(1f,surface.rect.width);
                Apply();Resized?.Invoke(reportFraction);
            }
        }
        public void ResetSplit()
        {
            reportFraction=defaultPaneWidth>0&&surface!=null?defaultPaneWidth/Mathf.Max(1,surface.rect.width):defaultFraction;
            Apply();Resized?.Invoke(reportFraction);
        }
        private void LateUpdate()
        {
            if(initialized&&surface!=null&&(lastSurfaceScreenRect!=SurfaceScreenRect()||lastScreenSize!=new Vector2(Screen.width,Screen.height)))Apply();
        }
        private Rect SurfaceScreenRect()
        {
            surface.GetWorldCorners(corners);
            var canvas=surface.GetComponentInParent<Canvas>();
            var uiCamera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var lower=RectTransformUtility.WorldToScreenPoint(uiCamera,corners[0]);
            var upper=RectTransformUtility.WorldToScreenPoint(uiCamera,corners[2]);
            return Rect.MinMaxRect(lower.x,lower.y,upper.x,upper.y);
        }
        public static Rect SceneViewport(Rect safePixels,Vector2 screen,float split,float height,Rect original)
        {
            if(screen.x<=0||screen.y<=0)return original;
            float right=Mathf.Lerp(safePixels.xMin,safePixels.xMax,split)-3f;
            // Safe areas constrain controls, not the world behind the notch and
            // home indicator. Never leave a second, stale view around the game.
            float top=height>=.999f?original.yMax:Mathf.Lerp(safePixels.yMin,safePixels.yMax,height)/screen.y;
            return Rect.MinMaxRect(original.xMin,original.yMin,
                Mathf.Clamp(right/screen.x,original.xMin,original.xMax),Mathf.Clamp(top,original.yMin,original.yMax));
        }
        private void EnsureBackgroundClear()
        {
            if(backgroundCamera==null)
            {
                var go=new GameObject("SplitViewBackgroundClear");
                go.transform.SetParent(transform,false);
                backgroundCamera=go.AddComponent<UnityEngine.Camera>();
                backgroundCamera.cullingMask=0;
                backgroundCamera.clearFlags=CameraClearFlags.SolidColor;
                backgroundCamera.backgroundColor=new Color(.045f,.075f,.08f,1);
                backgroundCamera.allowHDR=false;backgroundCamera.allowMSAA=false;
            }
            // A partial viewport does not clear the rest of the render target.
            // Clear first on every frame; this also covers rotation / resizing.
            backgroundCamera.rect=new Rect(0,0,1,1);
            backgroundCamera.targetTexture=sceneCamera.targetTexture;
            backgroundCamera.targetDisplay=sceneCamera.targetDisplay;
            backgroundCamera.depth=sceneCamera.depth-1;
            backgroundCamera.enabled=sceneCamera.enabled;
        }
        private void Apply()
        {
            if(!initialized||surface==null)return;
            reportFraction=ClampReportFraction(reportFraction,surface.rect.width,minimumPaneWidth,maximumPaneWidth);
            float split=1f-reportFraction;
            pane.anchorMin=new Vector2(split,0);pane.anchorMax=new Vector2(1,heightFraction);
            pane.offsetMin=pane.offsetMax=Vector2.zero;
            if(card!=null&&card!=pane)
            {card.anchorMin=Vector2.zero;card.anchorMax=Vector2.one;card.offsetMin=card.offsetMax=Vector2.zero;}
            if(sceneCamera!=null&&Screen.width>0&&Screen.height>0)
            {
                Canvas.ForceUpdateCanvases();
                sceneCamera.rect=SceneViewport(SurfaceScreenRect(),new Vector2(Screen.width,Screen.height),split,heightFraction,originalViewport);
                EnsureBackgroundClear();
            }
            lastSurfaceScreenRect=SurfaceScreenRect();lastScreenSize=new Vector2(Screen.width,Screen.height);
            Canvas.ForceUpdateCanvases();LayoutChanged?.Invoke();
        }
        private void OnDisable()
        {
            if(!initialized)return;
            initialized=false;
            if(backgroundCamera!=null)
            {
                backgroundCamera.enabled=false;
                Destroy(backgroundCamera.gameObject);backgroundCamera=null;
            }
            if(sceneCamera!=null)sceneCamera.rect=originalViewport;
            Closed?.Invoke();
        }
    }

    public sealed class ReportDockDivider : MonoBehaviour, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public ResizableReportDock Dock;
        public Image Line;
        public Color Idle,Active;
        private bool dragging;
        public void OnInitializePotentialDrag(PointerEventData data){data.useDragThreshold=false;}
        public void OnBeginDrag(PointerEventData data){dragging=true;if(Line!=null)Line.color=Active;}
        public void OnDrag(PointerEventData data){Dock?.ResizeFromScreen(data.position,data.pressEventCamera);}
        public void OnEndDrag(PointerEventData data){dragging=false;if(Line!=null)Line.color=Idle;}
        public void OnPointerEnter(PointerEventData data){if(Line!=null)Line.color=Active;}
        public void OnPointerExit(PointerEventData data){if(!dragging&&Line!=null)Line.color=Idle;}
        public void OnPointerClick(PointerEventData data){if(data.clickCount==2)Dock?.ResetSplit();}
    }
}
