using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.UI;

namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private GameObject _activeGameSidePane,_gameAnalysisPanel;
        private string _activeGameSidePaneKey;
        private bool _openingReportSidePane;
        private readonly Dictionary<string,float> _gameSidePaneFractions=new();

        private bool IsGameReportBoard(string board)=>!IsHomeMenuActive&&_sandboxRoot!=null&&_sandboxRoot.activeInHierarchy&&board==_currentBoardName;
        private void RequestGamePaneNavigation(Action next)
        {
            if(_requestTestReportNavigation!=null&&_clientDialog!=null&&_clientDialog.GetComponent<ResizableReportDock>()!=null)
            {_requestTestReportNavigation(next);return;}
            var ai=_gameAnalysisPanel!=null&&_gameAnalysisPanel.activeInHierarchy?_gameAnalysisPanel.GetComponent<AnalysisUI>():null;
            if(ai!=null&&!ai.SavePendingEdits())return;
            next();
        }
        private void RequestGameSidePane(string key,Action open)
        {
            if(_activeGameSidePane!=null&&_activeGameSidePane.activeInHierarchy&&_activeGameSidePaneKey==key)return;
            RequestGamePaneNavigation(()=>{CloseGameSidePanes();open();});
        }
        private void EnsureGameViewHud()
        {
            if(_sandboxUI==null||_catalogGameHud!=null)return;
            _catalogGameHud=ClientRect(_sandboxUI.transform,"GameViewUI",0,0,1,1);
            for(int i=_sandboxUI.transform.childCount-1;i>=0;i--)
            {
                var child=_sandboxUI.transform.GetChild(i);
                if(child!=_catalogGameHud&&child.gameObject!=_catalogPanel&&child.gameObject!=_settingsPanel&&child.gameObject!=_gameAnalysisPanel)
                    child.SetParent(_catalogGameHud,false);
            }
        }
        private void RegisterGameSidePane(GameObject panel,string key)
        {
            _activeGameSidePane=panel;_activeGameSidePaneKey=key;
            if(key!="catalog"&&_catalogButton!=null&&_catalogGameHud!=null)
                _catalogButton.transform.SetParent(_catalogGameHud,false);
        }
        private void ReleaseGameSidePane(GameObject panel)
        {
            if(_activeGameSidePane!=panel)return;
            _activeGameSidePane=null;_activeGameSidePaneKey=null;
            if(_catalogGameHud!=null)_catalogGameHud.anchorMax=Vector2.one;
        }
        private void ResizeGameViewHud(GameObject panel,float fraction)
        {
            if(_activeGameSidePane==panel&&_catalogGameHud!=null)_catalogGameHud.anchorMax=new Vector2(1-fraction,1);
        }
        private void CloseGameSidePanes()
        {
            CloseCatalogPanel();
            if(_settingsPanel!=null)_settingsPanel.SetActive(false);
            if(_gameAnalysisPanel!=null)_gameAnalysisPanel.SetActive(false);
            if(_clientDialog!=null&&_clientDialog.GetComponent<ResizableReportDock>()!=null)CloseClientDialog();
            _activeGameSidePane=null;_activeGameSidePaneKey=null;
            if(_catalogGameHud!=null)_catalogGameHud.anchorMax=Vector2.one;
        }
        private ResizableReportDock DockGameSidePane(GameObject panel,string key,RectTransform card,float defaultWidth,float top=1)
        {
            EnsureGameViewHud();
            // A pane must be a sibling of the game HUD, never nested inside its cropped area.
            if(panel.transform.IsChildOf(_sandboxUI.transform))panel.transform.SetParent(_sandboxUI.transform,false);
            RegisterGameSidePane(panel,key);
            var dock=panel.GetComponent<ResizableReportDock>()??panel.AddComponent<ResizableReportDock>();
            dock.Resized=value=>_gameSidePaneFractions[key]=value;
            dock.LayoutChanged=()=>ResizeGameViewHud(panel,dock.ReportFraction);
            dock.Closed=()=>ReleaseGameSidePane(panel);
            if(panel.transform.Find("SidePaneDivider")==null)
            {
                var divider=ClientRect(panel.transform,"SidePaneDivider",0,0,0,1);
                divider.pivot=new Vector2(.5f,.5f);divider.sizeDelta=new Vector2(12,0);
                divider.gameObject.AddComponent<Image>().color=HomeCard;
                var line=ClientRect(divider,"Line",.42f,0,.16f,1).gameObject.AddComponent<Image>();
                line.color=HomeCardBorder;line.raycastTarget=false;
                var grip=ClientRect(divider,"Grip",.25f,.47f,.5f,.06f).gameObject.AddComponent<Image>();
                grip.color=HomeMuted;grip.raycastTarget=false;ApplyHomeRoundedCorners(grip,3);
                var drag=divider.gameObject.AddComponent<ReportDockDivider>();drag.Dock=dock;drag.Line=line;drag.Idle=HomeCardBorder;drag.Active=HomePrimary;
            }
            var surface=(RectTransform)_safeArea.transform;
            float initial=defaultWidth/Mathf.Max(1,surface.rect.width);
            float fraction=_gameSidePaneFractions.TryGetValue(key,out var remembered)?remembered:initial;
            var camera=FindAnyObjectByType<Sandplay.Camera.SandboxCamera>()?.GetComponent<UnityEngine.Camera>();
            dock.Initialize(surface,card,camera,fraction,top,initial,280,defaultWidth);
            panel.transform.SetAsLastSibling();
            return dock;
        }
    }
}
