using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandplay.Core;

namespace Sandplay.UI
{
    public partial class LoginScreen
    {
        private sealed class AccountTypeChoice
        {
            public string Value;
            public Button Button;
            public Image Background;
            public Image Border;
            public TextMeshProUGUI Marker;
        }

        private GameObject _accountTypeOverlay;

        private void ShowAccountTypeOnboarding(string userName)
        {
            if (_accountTypeOverlay != null) return;
            if (_authCard != null) _authCard.gameObject.SetActive(false);

            var font = GetFont();
            var overlay = MakePanel("AccountTypeOnboarding", transform, Vector2.zero, Vector2.one, Surface);
            _accountTypeOverlay = overlay;

            var card = MakePanel("AccountTypeCard", overlay.transform,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Surface);
            _accountTypeCard = card.GetComponent<RectTransform>();
            _accountTypeCard.sizeDelta = new Vector2(760f, 700f);

            var title = MakeText("Title", card.transform, font,
                Copy("Choose your account type", "选择账户类型"), 30, FontStyles.Bold, Ink,
                new Vector2(.055f, .91f), new Vector2(.945f, .975f));
            title.alignment = TextAlignmentOptions.Left;

            var help = MakeText("Help", card.transform, font,
                Copy("Required to finish setting up your account.", "必须选择后才能完成账户设置。"),
                14, FontStyles.Normal, Muted,
                new Vector2(.055f, .855f), new Vector2(.945f, .905f));
            help.alignment = TextAlignmentOptions.Left;

            var choices = new List<AccountTypeChoice>();
            string selectedType = null;
            Button continueButton = null;
            TextMeshProUGUI continueLabel = null;

            AccountTypeChoice AddChoice(string objectName, string value, string heading,
                string description, string limitation, float minY, float maxY)
            {
                var row = MakePanel(objectName, card.transform,
                    new Vector2(.055f, minY), new Vector2(.945f, maxY), Field);
                AddRoundedBorder(row, Border);
                var choice = new AccountTypeChoice
                {
                    Value = value,
                    Background = row.GetComponent<Image>(),
                    Border = row.transform.Find("RoundedBorder").GetComponent<Image>(),
                };
                choice.Button = row.AddComponent<Button>();
                choice.Button.targetGraphic = choice.Background;

                var headingText = MakeText("Heading", row.transform, font, heading, 19,
                    FontStyles.Bold, Ink, new Vector2(.035f, .60f), new Vector2(.88f, .91f));
                headingText.alignment = TextAlignmentOptions.Left;
                var descriptionText = MakeText("Description", row.transform, font, description, 13,
                    FontStyles.Normal, Ink, new Vector2(.035f, .31f), new Vector2(.91f, .61f));
                descriptionText.alignment = TextAlignmentOptions.Left;
                descriptionText.enableWordWrapping = true;
                var limitationText = MakeText("Limitation", row.transform, font, limitation, 11,
                    FontStyles.Normal, Muted, new Vector2(.035f, .065f), new Vector2(.91f, .30f));
                limitationText.alignment = TextAlignmentOptions.Left;
                limitationText.enableWordWrapping = true;
                choice.Marker = MakeText("Selection", row.transform, font, "○", 23,
                    FontStyles.Bold, Muted, new Vector2(.91f, .34f), new Vector2(.975f, .72f));

                choice.Button.onClick.AddListener(() =>
                {
                    selectedType = value;
                    foreach (var item in choices)
                    {
                        bool isSelected = item.Value == selectedType;
                        Color fill = isSelected
                            ? (IsLightTheme ? new Color(.88f, .97f, .96f, 1f) : new Color(.04f, .23f, .23f, 1f))
                            : Field;
                        ApplyRoundedImage(item.Background, fill);
                        item.Border.color = isSelected ? Primary : Border;
                        item.Marker.text = isSelected ? "●" : "○";
                        item.Marker.color = isSelected ? Primary : Muted;
                    }
                    if (continueButton != null)
                    {
                        continueButton.interactable = true;
                        continueLabel.color = Color.white;
                    }
                });
                choices.Add(choice);
                return choice;
            }

            AddChoice("Personal", "normal", Copy("Personal", "个人"),
                Copy("Create private boards, join sessions, and connect with friends.",
                    "创建私人沙盘、加入会话并与好友联系。"),
                Copy("Does not include client management or organization administration.",
                    "不包含客户管理或机构管理功能。"), .655f, .825f);
            AddChoice("Therapist", "psychologist", Copy("Therapist", "治疗师"),
                Copy("Manage clients, host and schedule sessions, and create reports independently.",
                    "独立管理客户、主持和安排会话，并创建报告。"),
                Copy("Does not include organization administration.", "不包含机构管理功能。"),
                .445f, .615f);
            AddChoice("Organization", "organization", Copy("Organization", "机构"),
                Copy("Manage organization therapists, clients, usage allocations, branding, and activity.",
                    "管理机构治疗师、客户、用量分配、品牌和活动。"),
                Copy("For organization administration; it is not a therapist account.",
                    "用于机构管理；它不是治疗师账户。"), .235f, .405f);

            var status = MakeText("Status", card.transform, font, "", 12, FontStyles.Normal, Muted,
                new Vector2(.055f, .17f), new Vector2(.945f, .215f));
            status.alignment = TextAlignmentOptions.Left;

            continueButton = MakeButton("Continue", card.transform,
                Copy("Continue", "继续"), font, Primary,
                new Vector2(.055f, .075f), new Vector2(.945f, .155f));
            continueLabel = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            continueLabel.fontStyle = FontStyles.Bold;
            continueLabel.color = Muted;
            continueButton.interactable = false;
            continueButton.onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(selectedType) || BackendClient.Instance.IsUpdatingUserType) return;
                foreach (var item in choices) item.Button.interactable = false;
                continueButton.interactable = false;
                continueLabel.text = Copy("Saving…", "正在保存…");
                status.text = "";
                BackendClient.Instance.UpdateUserType(selectedType, () =>
                {
                    if (this == null) return;
                    status.color = Primary;
                    status.text = Copy("Account setup complete.", "账户设置已完成。");
                    CompleteLoginSuccess(userName);
                }, error =>
                {
                    if (this == null) return;
                    foreach (var item in choices) item.Button.interactable = true;
                    continueButton.interactable = true;
                    continueLabel.text = Copy("Continue", "继续");
                    continueLabel.color = Color.white;
                    status.color = new Color(1f, .3f, .24f, 1f);
                    status.text = error;
                });
            });
        }

        private static string Copy(string english, string chinese)
        {
            return FriendsClient.Text(english, chinese);
        }
    }
}
