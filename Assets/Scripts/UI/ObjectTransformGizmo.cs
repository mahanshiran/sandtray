using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.UI
{
    public enum ObjectTransformTool { None, Move, Rotate, Resize }

    // Screen-space drawing of world-space handles. Only the handles receive UI
    // raycasts; the rest of this full-screen graphic leaves the tray interactive.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ObjectTransformGizmo : MaskableGraphic, IPointerDownHandler,
        IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private struct Stroke
        {
            public Vector2 A, B;
            public Color Color;
            public int Handle;
            public Stroke(Vector2 a, Vector2 b, Color color, int handle)
            { A = a; B = b; Color = color; Handle = handle; }
        }
        private readonly List<Stroke> _strokes = new List<Stroke>(240);
        private static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };
        private static readonly Color[] AxisColors = {
            new Color(1f, .28f, .25f), new Color(.3f, 1f, .46f), new Color(.3f, .65f, 1f) };
        private static ObjectTransformGizmo _active;
        private ObjectPlacer _placer;
        private PlacedObject _target;
        private UnityEngine.Camera _camera;
        private ObjectTransformTool _tool;
        private int _handle = -1, _pointerId;
        private Vector2 _press, _screenAxis, _rotationTangent, _scaleDirection;
        private Vector3 _pivot, _axis, _lastRadial;
        private float _degrees, _rotationPixelsPerDegree;
        private bool _planeRotation, _changed;
        private TMPro.TMP_Text[] _axisLabels;
        private bool Editable => _target != null && _placer != null &&
            (GameManager.Instance == null || !GameManager.Instance.IsSpectator);
        public bool IsDragging => _handle >= 0;
        public static bool HasCapturedPointer => _active != null && _active.isActiveAndEnabled && _active.IsDragging;
        private bool TouchTargets => Application.isMobilePlatform || Input.touchCount > 0;
        private float HitRadius => TouchTargets ? Mathf.Max(24f, 22f / InputHelper.GetTouchDpiScale() / ScreenScale) : 14f;
        public ObjectTransformTool Tool => _tool;
        public static bool BlocksWorldInput => _active != null && _active.isActiveAndEnabled &&
            (_active.IsDragging || _active.HitScreen(InputHelper.GetPointerPosition()) >= 0);
        private float ScreenScale => canvas != null ? canvas.scaleFactor : 1f;
        private UnityEngine.Camera UICamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        public void Initialize(UnityEngine.Camera camera, TMPro.TMP_FontAsset font)
        {
            _camera = camera;
            raycastTarget = true;
            _axisLabels = new TMPro.TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Axis" + "XYZ"[i], typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var text = go.AddComponent<TMPro.TextMeshProUGUI>();
                text.font = font; text.text = "XYZ"[i].ToString(); text.fontSize = 14;
                text.fontStyle = TMPro.FontStyles.Bold; text.alignment = TMPro.TextAlignmentOptions.Center;
                text.color = AxisColors[i]; text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(24, 24);
                go.AddComponent<Shadow>().effectColor = Color.black;
                _axisLabels[i] = text;
            }
        }

        public void SetTarget(PlacedObject target)
        {
            Cancel(); _target = target; _tool = ObjectTransformTool.None;
            if (_placer == null) _placer = FindAnyObjectByType<ObjectPlacer>();
            gameObject.SetActive(target != null);
            _active = target != null ? this : null;
            Rebuild();
        }
        public void SetTool(ObjectTransformTool tool)
        { Cancel(); _tool = tool; Rebuild(); }
        public void Cancel()
        {
            if (IsDragging && _placer != null) _placer.CancelGroupTransform();
            _handle = -1; _changed = false;
        }
        protected override void OnDisable()
        { Cancel(); if (_active == this) _active = null; base.OnDisable(); }
        protected override void OnEnable()
        { base.OnEnable(); _active = this; }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        private void LateUpdate()
        {
            if (!Editable || InputHelper.IsInputBlocked || InputHelper.IsTextInputFocused ||
                Input.GetKeyDown(KeyCode.Escape))
                Cancel();
            // Finish when a release is missed outside the window; touch cancellation
            // rolls back instead of committing an accidental edit.
            if (IsDragging && _pointerId >= 0)
            {
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != _pointerId) continue;
                    found = true;
                    ProcessCapturedTouch(touch.fingerId, touch.phase, touch.position);
                    break;
                }
                // A vanished touch cannot become a different finger's gesture.
                if (!found) Cancel();
            }
            else if (IsDragging && !InputHelper.GetPointerHeld()) Finish();
            Rebuild();
        }
        private void ProcessCapturedTouch(int fingerId, TouchPhase phase, Vector2 position)
        {
            if (!IsDragging || fingerId != _pointerId) return;
            if (phase == TouchPhase.Canceled) { Cancel(); return; }
            DragTo(position);
            if (phase == TouchPhase.Ended) Finish();
        }

        private Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, UICamera, out var point);
            return point;
        }
        private Vector2 Project(Vector3 world) => Local(_camera.WorldToScreenPoint(world));
        private void Line(Vector2 a, Vector2 b, Color color, int handle = -1) => _strokes.Add(new Stroke(a,b,color,handle));
        private void Square(Vector2 p, float radius, Color color, int handle)
        {
            Line(p + new Vector2(-radius,-radius), p + new Vector2(radius,-radius), color, handle);
            Line(p + new Vector2(radius,-radius), p + new Vector2(radius,radius), color, handle);
            Line(p + new Vector2(radius,radius), p + new Vector2(-radius,radius), color, handle);
            Line(p + new Vector2(-radius,radius), p + new Vector2(-radius,-radius), color, handle);
        }
        private void Rebuild()
        {
            _strokes.Clear();
            if (_axisLabels != null) foreach (var label in _axisLabels) if (label != null) label.gameObject.SetActive(false);
            if (!Editable || _camera == null || InputHelper.IsInputBlocked) { SetVerticesDirty(); return; }
            var bounds = _placer.SelectionBounds();
            // Draw at the current selection; _pivot stays fixed only for drag math.
            var pivot = bounds.center;
            var screen = _camera.WorldToScreenPoint(pivot);
            if (screen.z <= _camera.nearClipPlane) { SetVerticesDirty(); return; }
            var center = Local(screen);
            // Corner brackets give a clear selection without changing model materials.
            var min = new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            bool validBounds = true;
            for (int i=0;i<8;i++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                if (_camera.WorldToScreenPoint(point).z <= _camera.nearClipPlane) { validBounds=false; break; }
                var projected = Project(point); min=Vector2.Min(min,projected); max=Vector2.Max(max,projected);
            }
            if (validBounds)
            {
                min -= Vector2.one * 6; max += Vector2.one * 6;
                var selectionColor = new Color(.35f,.9f,1f,.85f);
                float corner = Mathf.Min(14, Mathf.Min(max.x-min.x,max.y-min.y)*.3f);
                for(int i=0;i<4;i++)
                {
                    var p = new Vector2((i&1)==0?min.x:max.x,(i&2)==0?min.y:max.y);
                    Line(p,p+new Vector2((i&1)==0?corner:-corner,0),selectionColor);
                    Line(p,p+new Vector2(0,(i&2)==0?corner:-corner),selectionColor);
                }
            }
            float handleSize = _tool == ObjectTransformTool.Move ? 64f : 80f;
            float radius = Vector3.Distance(_camera.ScreenToWorldPoint(screen),
                _camera.ScreenToWorldPoint(screen + Vector3.right * (handleSize * ScreenScale)));
            if (_tool == ObjectTransformTool.Move)
            {
                for(int i=0;i<3;i++)
                {
                    var tip = Project(pivot + Axes[i]*radius);
                    var delta = tip-center;
                    if(delta.magnitude<16) continue;
                    var direction=delta.normalized;
                    if (validBounds)
                    {
                        // Extend along the projected axis until its arrow clears the
                        // selection's screen bounds. Drag math still uses the world axis.
                        float edgeX = Mathf.Abs(direction.x) > .0001f
                            ? ((direction.x > 0 ? max.x : min.x) - center.x) / direction.x
                            : float.PositiveInfinity;
                        float edgeY = Mathf.Abs(direction.y) > .0001f
                            ? ((direction.y > 0 ? max.y : min.y) - center.y) / direction.y
                            : float.PositiveInfinity;
                        float length = Mathf.Max(delta.magnitude, Mathf.Min(edgeX, edgeY) + 16f);
                        tip = center + direction * length;
                    }
                    var side=new Vector2(-direction.y,direction.x);
                    Line(center,tip,AxisColors[i],i);
                    Line(tip,tip-direction*12+side*6,AxisColors[i],i);
                    Line(tip,tip-direction*12-side*6,AxisColors[i],i);
                    LabelAxis(i,tip+direction*15);
                }
            }
            else if (_tool == ObjectTransformTool.Rotate)
            {
                for(int i=0;i<3;i++)
                {
                    Vector3 u=Axes[(i+1)%3], v=Axes[(i+2)%3];
                    for(int j=0;j<64;j++)
                    {
                        float a=j*Mathf.PI*2/64, b=(j+1)*Mathf.PI*2/64;
                        Line(Project(pivot+radius*(u*Mathf.Cos(a)+v*Mathf.Sin(a))),
                            Project(pivot+radius*(u*Mathf.Cos(b)+v*Mathf.Sin(b))),AxisColors[i],i);
                    }
                    LabelAxis(i,Project(pivot+radius*(u*.7071f+v*.7071f))+new Vector2(8,8));
                }
            }
            else if (_tool == ObjectTransformTool.Resize)
            {
                var extent = validBounds ? (max-min)*.5f : Vector2.one*65;
                extent = new Vector2(Mathf.Clamp(extent.x,45,110),Mathf.Clamp(extent.y,45,110));
                for(int i=0;i<2;i++)
                {
                    var tip=center+extent*(i==0?1:-1);
                    Line(center,tip,new Color(.45f,1f,.65f),i);
                    Square(tip,TouchTargets ? 11 : 7,new Color(.45f,1f,.65f),i);
                }
            }
            SetVerticesDirty();
        }
        private void LabelAxis(int axis, Vector2 point)
        {
            if (_axisLabels == null || _axisLabels[axis] == null) return;
            _axisLabels[axis].gameObject.SetActive(true);
            _axisLabels[axis].rectTransform.localPosition = point;
        }
        public static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {
            var d=b-a;
            float t=d.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);
            return Vector2.Distance(p,a+d*t);
        }
        private int HitScreen(Vector2 screen)
        {
            if (!Editable || _tool == ObjectTransformTool.None || InputHelper.IsInputBlocked) return -1;
            var p=Local(screen); float nearest=HitRadius; int handle=-1;
            foreach(var line in _strokes)
            {
                if(line.Handle<0) continue;
                float distance=SegmentDistance(p,line.A,line.B);
                if(distance<nearest) { nearest=distance; handle=line.Handle; }
            }
            return handle;
        }
        public override bool Raycast(Vector2 sp, UnityEngine.Camera eventCamera) => HitScreen(sp)>=0;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach(var line in _strokes)
            {
                bool active=IsDragging && line.Handle==_handle;
                StrokeQuad(vh,line.A,line.B,active?7:5,new Color(0,0,0,.7f));
                StrokeQuad(vh,line.A,line.B,active?4:2.5f,active?Color.white:line.Color);
            }
        }
        private static void StrokeQuad(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            var direction=(b-a).normalized;
            var n=new Vector2(-direction.y,direction.x)*(width*.5f);
            int index=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero); vh.AddVert(a+n,color,Vector2.zero);
            vh.AddVert(b+n,color,Vector2.zero); vh.AddVert(b-n,color,Vector2.zero);
            vh.AddTriangle(index,index+1,index+2); vh.AddTriangle(index,index+2,index+3);
        }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold=false; }
        public void OnPointerDown(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left || IsDragging || !Editable || InputHelper.IsTextInputFocused || Input.touchCount > 1) return;
            int hit=HitScreen(e.position); if(hit<0) return;
            _handle=hit; _pointerId=e.pointerId; _press=e.position; _changed=false; _degrees=0;
            _pivot=_placer.SelectionBounds().center; _axis=Axes[Mathf.Min(hit,2)];
            var screen=_camera.WorldToScreenPoint(_pivot);
            _screenAxis=(Vector2)(_camera.WorldToScreenPoint(_pivot+_axis)-screen);
            _scaleDirection=(e.position-(Vector2)screen).normalized;
            _planeRotation=Mathf.Abs(Vector3.Dot(_camera.transform.forward,_axis))>.15f && TryRadial(e.position,out _lastRadial);
            float nearest=float.PositiveInfinity;
            foreach(var line in _strokes)
            {
                if(line.Handle!=hit) continue;
                float distance=SegmentDistance(Local(e.position),line.A,line.B);
                if(distance>=nearest) continue;
                nearest=distance; _rotationTangent=(line.B-line.A).normalized;
                _rotationPixelsPerDegree=Mathf.Max(.1f,(line.B-line.A).magnitude*ScreenScale/(360f/64));
            }
            _placer.BeginGroupTransform();
        }
        private bool TryRadial(Vector2 point,out Vector3 radial)
        {
            var ray=_camera.ScreenPointToRay(point);
            if(new Plane(_axis,_pivot).Raycast(ray,out float distance))
            { radial=ray.GetPoint(distance)-_pivot; return radial.sqrMagnitude>.00001f; }
            radial=Vector3.zero; return false;
        }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e)
        {
            if(!IsDragging || e.pointerId!=_pointerId) return;
            DragTo(e.position);
        }
        private void DragTo(Vector2 position)
        {
            if(!IsDragging) return;
            if(!Editable || InputHelper.IsInputBlocked) { Cancel(); return; }
            var delta=position-_press;
            if(!_changed && delta.magnitude<4*ScreenScale) return;
            _changed=true;
            if(_tool==ObjectTransformTool.Move)
            {
                float amount=_screenAxis.sqrMagnitude<.001f?0:Vector2.Dot(delta,_screenAxis)/_screenAxis.sqrMagnitude;
                if(_handle==1) _placer.RaiseGroup(amount);
                else _placer.TranslateGroup(_axis*amount);
            }
            else if(_tool==ObjectTransformTool.Rotate)
            {
                if(_planeRotation && TryRadial(position,out var radial))
                { _degrees+=Vector3.SignedAngle(_lastRadial,radial,_axis); _lastRadial=radial; }
                else if(!_planeRotation) _degrees=Vector2.Dot(delta,_rotationTangent)/_rotationPixelsPerDegree;
                _placer.RotateGroup(_degrees,_axis);
            }
            else if(_tool==ObjectTransformTool.Resize)
                _placer.ResizeGroup(Mathf.Exp(Mathf.Clamp(Vector2.Dot(delta,_scaleDirection)/(110*ScreenScale),-5,5)));
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (!IsDragging || e.pointerId != _pointerId) return;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).fingerId == _pointerId && Input.GetTouch(i).phase == TouchPhase.Canceled)
                { Cancel(); return; }
            OnDrag(e); Finish();
        }
        public void OnEndDrag(PointerEventData e) { if(IsDragging && e.pointerId==_pointerId) Finish(); }
        private void Finish()
        {
            if(!IsDragging) return;
            bool preserveBurial=_tool==ObjectTransformTool.Move && _handle==1;
            _handle=-1;
            if(_changed) _placer.EndGroupTransform(preserveBurial); else _placer.CancelGroupTransform();
            _changed=false;
        }
    }
}
