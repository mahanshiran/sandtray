using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;
using Sandplay.Objects;

namespace Sandplay.UI
{
    public partial class ObjectActionPanel
    {
        private const float LinearToolsHeight = 72f;
        private bool WaitingForLinearConfirmation
        {
            get
            {
                if (SmartLinearObject.AwaitingConfirmation(_target)) return true;
                if (_groupPlacer != null)
                    foreach (var placed in _groupPlacer.Selection)
                        if (SmartLinearObject.AwaitingConfirmation(placed)) return true;
                return false;
            }
        }
        private TMP_Text _linearLeftMarker, _linearRightMarker;
        private float _linearMessageUntil;
        private void HideLinearMarkers()
        {
            if (_linearLeftMarker != null) _linearLeftMarker.gameObject.SetActive(false);
            if (_linearRightMarker != null) _linearRightMarker.gameObject.SetActive(false);
        }
        private void RefreshLinearMarkers(SmartLinearObject smart)
        {
            var canvas=GetComponentInParent<Canvas>();
            if(canvas==null)return;
            if(_linearLeftMarker==null)
            {
                TMP_Text Make(string name, Color color)
                {
                    var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(canvas.transform,false);
                    var text=go.GetComponent<TextMeshProUGUI>();text.font=_linearHeading.font;text.fontSize=14;text.color=color;
                    text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
                    var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,0);rect.sizeDelta=new Vector2(100,24);
                    return text;
                }
                _linearLeftMarker=Make("LinearLeftEnd",new Color(.35f,.88f,1));
                _linearRightMarker=Make("LinearRightEnd",new Color(1,.82f,.35f));
            }
            void Position(TMP_Text label,float x,string text)
            {
                var screen=_cam.WorldToScreenPoint(smart.transform.TransformPoint(new Vector3(x,SmartLinearObject.LocalBounds(smart.State).max.y+.03f,0)));
                label.gameObject.SetActive(screen.z>0 && !IsActionActive);label.text=text;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,screen,
                    canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var local);
                ((RectTransform)label.transform).anchoredPosition=local;
            }
            Position(_linearLeftMarker,smart.State.FirstSegment*SmartLinearObject.SectionLength,Localization.Text("Left end","左端"));
            Position(_linearRightMarker,(smart.State.FirstSegment+smart.State.SegmentCount)*SmartLinearObject.SectionLength,Localization.Text("Right end","右端"));
        }
        private RectTransform _linearTools;
        private TMP_InputField _linearCount;
        private PlacedObject _linearEditingTarget;
        private TMP_Text _linearHeading;
        private Button[] _linearButtons;
        private TMP_Text[] _linearLabels;
        private readonly Vector3[] _linearCorners=new Vector3[4];
        private void RefreshLinearTools()
        {
            var smart = _target != null ? _target.GetComponent<SmartLinearObject>() : null;
            bool show=smart!=null && !HasGroup && (GameManager.Instance==null || !GameManager.Instance.IsSpectator);
            if(!show) { if(_linearTools!=null)_linearTools.gameObject.SetActive(false); HideLinearMarkers(); return; }
            if(_linearTools==null) CreateLinearTools();
            _linearTools.gameObject.SetActive(true);
            bool editable=!IsActionActive && !InputHelper.IsInputBlocked &&
                (NetworkBootstrapper.Instance==null || !NetworkBootstrapper.Instance.IsOnline || _target.NetworkId!=0);
            _linearCount.interactable=editable;
            if(!_linearCount.isFocused) _linearCount.SetTextWithoutNotify(smart.State.SegmentCount.ToString());
            _linearHeading.text=WaitingForLinearConfirmation ? Localization.Text("Waiting for session…", "等待会话确认…") :
                Time.realtimeSinceStartup < _linearMessageUntil ? Localization.Text("Outside tray or section limit", "超出沙盘边界或节数限制") :
                !editable && !IsActionActive ? Localization.Text("Waiting for session…", "等待会话确认…") :
                Localization.Text("Sections · left / right ends", "节数 · 左端 / 右端");
            string[] labels={Localization.Text("+ Left","左+"),Localization.Text("− Left","左−"),Localization.Text("− Right","右−"),Localization.Text("+ Right","右+")};
            for(int i=0;i<4;i++)
            {
                _linearLabels[i].text=labels[i];
                _linearButtons[i].interactable=editable && (i==1 || i==2 ? smart.State.SegmentCount>1 : smart.State.SegmentCount<128);
            }
            _linearTools.anchoredPosition=new Vector2(0,8);
            _linearTools.GetWorldCorners(_linearCorners);
            float shift=Mathf.Max(0,8-_linearCorners[0].x)-Mathf.Max(0,_linearCorners[2].x-(Screen.width-8));
            _linearTools.anchoredPosition+=new Vector2(shift/Mathf.Max(.001f,transform.lossyScale.x),0);
            RefreshLinearMarkers(smart);
        }
        private void CreateLinearTools()
        {
            var existing=GetComponentInChildren<TMP_Text>(true);
            var font=existing!=null?existing.font:TMP_Settings.defaultFontAsset;
            var root=new GameObject("LinearSectionControls",typeof(RectTransform),typeof(Image),typeof(LayoutElement));
            root.transform.SetParent(transform,false);root.GetComponent<LayoutElement>().ignoreLayout=true;
            _linearTools=(RectTransform)root.transform;
            _linearTools.anchorMin=_linearTools.anchorMax=new Vector2(.5f,1);_linearTools.pivot=new Vector2(.5f,0);
            _linearTools.sizeDelta=new Vector2(310,LinearToolsHeight);
            root.GetComponent<Image>().color=new Color(.08f,.10f,.14f,.97f);
            TMP_Text Text(Transform parent,string name,float x,float y,float width,float height)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
                var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=Vector2.zero;rt.pivot=Vector2.zero;
                rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(width,height);
                var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=14;text.color=Color.white;
                text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.enableWordWrapping=false;
                return text;
            }
            _linearHeading=Text(root.transform,"SectionHeading",4,54,302,14);_linearHeading.fontSize=12;
            _linearButtons=new Button[4];_linearLabels=new TMP_Text[4];
            float[] positions={4,65,187,248};
            for(int i=0;i<4;i++)
            {
                int index=i;
                var go=new GameObject("SectionButton"+i,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(root.transform,false);
                var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=Vector2.zero;rt.pivot=Vector2.zero;
                rt.anchoredPosition=new Vector2(positions[i],4);rt.sizeDelta=new Vector2(58,46);
                go.GetComponent<Image>().color=new Color(.18f,.30f,.36f);
                var button=go.GetComponent<Button>();button.onClick.AddListener(()=>EditLinearEnd(index));
                _linearButtons[i]=button;_linearLabels[i]=Text(go.transform,"Label",0,0,58,46);
            }
            var input=new GameObject("SectionCount",typeof(RectTransform),typeof(Image),typeof(TMP_InputField));input.transform.SetParent(root.transform,false);
            var ir=(RectTransform)input.transform;ir.anchorMin=ir.anchorMax=Vector2.zero;ir.pivot=Vector2.zero;ir.anchoredPosition=new Vector2(126,4);ir.sizeDelta=new Vector2(58,46);
            input.GetComponent<Image>().color=new Color(.10f,.16f,.20f);
            _linearCount=input.GetComponent<TMP_InputField>();_linearCount.textViewport=ir;
            _linearCount.textComponent=(TextMeshProUGUI)Text(input.transform,"Count",4,0,50,46);
            _linearCount.contentType=TMP_InputField.ContentType.IntegerNumber;_linearCount.characterLimit=3;
            _linearCount.onSelect.AddListener(_ => _linearEditingTarget=_target);
            _linearCount.onEndEdit.AddListener(value=>
            {
                var smart=_target!=null?_target.GetComponent<SmartLinearObject>():null;
                if(smart!=null && _target==_linearEditingTarget && !IsActionActive && int.TryParse(value,out int count) && count>=1 && count<=128)
                    if(!smart.Change(0,count-smart.State.SegmentCount)) ShowLinearLimit();
                _linearEditingTarget=null;
                RefreshLinearTools();
            });
        }
        private void EditLinearEnd(int index)
        {
            var smart=_target!=null?_target.GetComponent<SmartLinearObject>():null;
            if(smart==null || IsActionActive || InputHelper.IsInputBlocked)return;
            bool changed=index==0?smart.Change(1,0):index==1?smart.Change(-1,0):index==2?smart.Change(0,-1):smart.Change(0,1);
            if(!changed) ShowLinearLimit();
            RefreshLinearTools();
        }
        private void ShowLinearLimit()
        {
            _linearMessageUntil=Time.realtimeSinceStartup+2f;
        }
    }
}
