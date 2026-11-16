using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandplay.UI
{
    // Resize existing rows instead of rebuilding thumbnails or interrupting an object drag.
    public sealed class CatalogShelfResponsiveLayout : MonoBehaviour
    {
        public const float DefaultPaneWidth=260f,MinimumPaneWidth=140f,MaximumPaneWidth=520f;
        private sealed class Group
        {
            public RectTransform Header;
            public readonly List<RectTransform> Items=new();
            public readonly List<RectTransform> Shelves=new();
        }
        private float width=-1;
        private int childCount=-1;
        public static int ColumnsForWidth(float availableWidth)=>Mathf.Clamp(Mathf.FloorToInt((availableWidth+5)/105f),1,4);
        private void LateUpdate()
        {
            if(transform is RectTransform root&&(Mathf.Abs(width-root.rect.width)>.5f||childCount!=root.childCount))RefreshLayout();
        }
        public void RefreshLayout()
        {
            if(!(transform is RectTransform root)||root.rect.width<=1)return;
            var groups=new List<Group>();Group group=null;
            foreach(Transform child in transform)
            {
                if(child.name.StartsWith("Cat_",StringComparison.Ordinal))
                {group=new Group{Header=(RectTransform)child};groups.Add(group);}
                else if(group!=null&&child.name.StartsWith("Item_",StringComparison.Ordinal))group.Items.Add((RectTransform)child);
                else if(group!=null&&child.name=="DisplayShelf")group.Shelves.Add((RectTransform)child);
            }
            if(groups.Count==0){width=root.rect.width;childCount=root.childCount;return;}
            float available=Mathf.Max(1,root.rect.width-4),y=0;
            int columns=ColumnsForWidth(available);
            float cardWidth=(available-5*(columns-1))/columns;
            foreach(var current in groups)
            {
                current.Header.anchoredPosition=new Vector2(4,y);y-=26;
                int rows=(current.Items.Count+columns-1)/columns;
                while(current.Shelves.Count<rows)
                {
                    var shelf=new GameObject("DisplayShelf",typeof(RectTransform),typeof(CanvasRenderer),typeof(CatalogShelfGraphic));
                    shelf.transform.SetParent(transform,false);
                    shelf.transform.SetSiblingIndex(current.Header.GetSiblingIndex()+1);
                    shelf.GetComponent<CatalogShelfGraphic>().raycastTarget=false;
                    current.Shelves.Add((RectTransform)shelf.transform);
                }
                for(int i=0;i<current.Shelves.Count;i++)
                {
                    var shelf=current.Shelves[i];shelf.gameObject.SetActive(i<rows);
                    shelf.anchorMin=shelf.anchorMax=new Vector2(0,1);shelf.pivot=new Vector2(0,1);
                    shelf.anchoredPosition=new Vector2(0,y-i*94);shelf.sizeDelta=new Vector2(available,94);
                }
                for(int i=0;i<current.Items.Count;i++)
                {
                    var item=current.Items[i];item.anchorMin=item.anchorMax=new Vector2(0,1);item.pivot=new Vector2(0,1);
                    item.anchoredPosition=new Vector2((i%columns)*(cardWidth+5),y-(i/columns)*94);
                    item.sizeDelta=new Vector2(cardWidth,84);
                }
                y-=rows*94+6;
            }
            root.sizeDelta=new Vector2(root.sizeDelta.x,Mathf.Abs(y)+8);
            width=root.rect.width;childCount=root.childCount;
        }
    }
}
