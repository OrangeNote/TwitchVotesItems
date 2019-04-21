using UnityEngine;
using RoR2.UI;
using System;
using RoR2;
using System.Reflection;
using UnityEngine.UI;
using System.Collections.Generic;

namespace TwitchVotesItems
{
    class VoteNotification : MonoBehaviour
    {
        private GameObject go;
        public GenericNotification notification;

        private List<Icon> icons = new List<Icon>();

        private void Awake()
        {
            go = Instantiate(Resources.Load<GameObject>("Prefabs/NotificationPanel2"));
            notification = go.GetComponent<GenericNotification>();
            notification.transform.SetParent(RoR2Application.instance.mainCanvas.transform);
            notification.iconImage.enabled = false;
            notification.fadeTime = 0.5f;
            notification.duration = 20f;
        }

        private void Update()
        {
            if(notification == null)
            {
                Destroy(this);
                return;
            }
            typeof(LanguageTextMeshController).GetField("resolvedString", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this.notification.titleText, "Twitch choose one of these items!");
            typeof(LanguageTextMeshController).GetMethod("UpdateLabel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this.notification.titleText, new object[0]);
        }

        public void SetItems(List<PickupIndex> items, float duration)
        {
            notification.duration = duration;

            for (var i = 0; i < items.Count; i++)
            {
                icons.Add(new Icon(ItemCatalog.GetItemDef(items[i].itemIndex), this.notification, i));
            }
        }

        public void SetPosition(Vector3 position)
        {
            go.transform.position = position;
        }
        public void SetSize(Vector2 size)
        {
            notification.GetComponent<RectTransform>().sizeDelta = size;
        }

        public class Icon
        {
            private GameObject image;

            public Icon(ItemDef itemDef, GenericNotification genericNotification, int index)
            {
                GameObject gameObject = new GameObject("VoteNotification_Icon");

                gameObject.AddComponent<Image>().sprite = Resources.Load<Sprite>(itemDef.pickupIconPath);

                if (gameObject.GetComponent<CanvasRenderer>() == null)
                    gameObject.AddComponent<CanvasRenderer>();

                if (gameObject.GetComponent<RectTransform>() == null)
                    gameObject.AddComponent<RectTransform>();

                gameObject.transform.SetParent(genericNotification.transform);

                gameObject.transform.position = new Vector3(Screen.width / 2, Screen.height / 2, 0) + new Vector3(-128 + index * 128, 128 + 32, 0);

                gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(64, 64);

                this.image = gameObject;
                // this.ItemDef = itemDef;
                // gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
            }
        }
    }
}