using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandplay.UI;
namespace Sandplay.Core
{
    public partial class SceneBootstrapper
    {
        private void CreateSessionAvatars(Transform row)
        {
            var group = new GameObject("Session avatars", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            group.transform.SetParent(row, false);
            var layout=group.GetComponent<HorizontalLayoutGroup>();
            layout.spacing=6; layout.childControlWidth=layout.childControlHeight=true;
            layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            var avatars=group.AddComponent<SessionAvatars>();
            avatars.Font=GetUIFont(); avatars.OpenProfile=OpenSessionProfile;
        }
        internal static List<KeyValuePair<string,string>> SessionProfileFields(SessionProfile profile)
        {
            var result=new List<KeyValuePair<string,string>>();
            void Add(string label,string value) { if(!string.IsNullOrWhiteSpace(value) && value.Trim()!="null") result.Add(new KeyValuePair<string,string>(label,value.Trim())); }
            string Values(string[] values) => values == null ? "" : string.Join(", ",Array.FindAll(values,v=>!string.IsNullOrWhiteSpace(v) && v!="null"));
            Add("Name",profile.name);
            if(profile.user_type!="psychologist" || profile.therapist==null) return result;
            var p=profile.therapist;
            Add("Professional display name",p.display_name); Add("Professional title",p.professional_title);
            Add("About your practice",p.bio); Add("Practice name",p.practice_name);
            Add("Professional email",p.professional_email); Add("Professional phone (include country code)",p.professional_phone);
            Add("Website (https://…)",p.website); Add("Country code (for example CN, US, GB)",p.country);
            Add("State / province / region",p.region); Add("City",p.city); Add("Time zone (for example Asia/Shanghai)",p.timezone);
            Add("Languages (comma separated)",Values(p.languages)); Add("Areas of practice (comma separated)",Values(p.specialties));
            Add("Therapeutic approaches (comma separated)",Values(p.approaches)); Add("Age groups (comma separated)",Values(p.age_groups));
            if(p.session_formats!=null) foreach(var format in p.session_formats)
                if(format=="in_person" || format=="online" || format=="phone") Add("Session format",Localization.Get(format=="in_person"?"In person":format=="online"?"Online":"Phone"));
            Add("Accessibility and accommodations",p.accessibility); Add("Fees, currency and payment information",p.fees_information);
            if(p.qualifications!=null) foreach(var q in p.qualifications)
            {
                if(q==null) continue;
                Add("Qualification / licence / registration",q.title);Add("Training institution",q.institution);
                Add("Issuing or regulatory body",q.issuing_body);Add("Registration / membership number",q.registration_number);
                Add("Credential country code",q.country);Add("Credential state / province / region",q.region);
                Add("Valid from (YYYY-MM-DD)",q.valid_from);Add("Valid until (YYYY-MM-DD)",q.valid_until);
            }
            return result;
        }
        private void OpenSessionProfile(SessionProfile profile)
        {
            var net=NetworkBootstrapper.Instance;
            if(net==null || !net.IsOnline || !Array.Exists(net.LiveProfiles,p=>p.token==profile.token)) return;
            ShowUserProfile(profile);
            var dialog=_clientDialog;
            StartCoroutine(WatchSessionProfile());
            IEnumerator WatchSessionProfile()
            {
                while(dialog!=null && _clientDialog==dialog)
                {
                    if(net!=NetworkBootstrapper.Instance || !net.IsOnline || !Array.Exists(net.LiveProfiles,p=>p.token==profile.token))
                    { Destroy(dialog); if(_clientDialog==dialog) _clientDialog=null; yield break; }
                    yield return new WaitForSeconds(.5f);
                }
            }
        }
        private void ShowUserProfile(SessionProfile profile)
        {
            var box=ClientDialog(Localization.Get(profile.user_type=="psychologist"?"Therapist profile":"Profile"),650,760);
            var content=ClientScroll(box,"Session profile details",.04f,.04f,.92f,.80f);
            var hero=ClientRow(content,"Profile photo",112);
            var portrait=new GameObject("Avatar",typeof(RectTransform),typeof(Image),typeof(Mask));
            portrait.transform.SetParent(hero,false);
            var portraitRT=(RectTransform)portrait.transform;
            portraitRT.anchorMin=portraitRT.anchorMax=new Vector2(.5f,.5f);portraitRT.sizeDelta=new Vector2(96,96);
            portrait.GetComponent<Image>().sprite=SessionAvatars.Circle();
            portrait.GetComponent<Image>().color=new Color(.22f,.32f,.43f);
            var initials=ClientText(portrait.transform,string.IsNullOrWhiteSpace(profile.name)?"?":System.Globalization.StringInfo.GetNextTextElement(profile.name.Trim()),28,0,0,1,1,Color.white);
            initials.richText=false;initials.alignment=TextAlignmentOptions.Center;
            StartCoroutine(SessionAvatars.Photo(portrait.transform,initials,profile));
            if(profile.user_type=="psychologist")
            {
                var notice=ClientText(ClientRow(content,"Credential status",60),Localization.Get("Credentials are self-reported and have not been verified."),13,.03f,.05f,.94f,.90f,HomeMuted);
                notice.richText=false;
            }
            if(profile.therapist?.certificates!=null)
                foreach(var certificate in profile.therapist.certificates) CertificateLink(content,certificate);
            foreach(var field in SessionProfileFields(profile))
            {
                var row=ClientRow(content,field.Key,80);
                string caption=Localization.Get(field.Key);
                int hint=caption.IndexOfAny(new[]{'(','（'});
                if(hint>0) caption=caption.Substring(0,hint).Trim();
                var label=ClientText(row,caption,12,.03f,.65f,.94f,.30f,HomeMuted);label.richText=false;
                var value=ClientText(row,field.Value,16,.03f,.04f,.94f,.58f,HomeText);value.richText=false;
                // Measure long text against the actual dialog width; never truncate a bio.
                float width=Mathf.Max(180,((RectTransform)box).rect.width*.82f);
                float height=Mathf.Max(80,value.GetPreferredValues(field.Value,width,0).y+48);
                row.GetComponent<LayoutElement>().preferredHeight=height;
                var valueRT=value.rectTransform;valueRT.anchorMax=new Vector2(.97f,1);valueRT.offsetMax=new Vector2(0,-32);
                var labelRT=label.rectTransform;labelRT.anchorMin=new Vector2(.03f,1);labelRT.anchorMax=new Vector2(.97f,1);labelRT.offsetMin=new Vector2(0,-30);labelRT.offsetMax=Vector2.zero;
            }
        }
    }
}
