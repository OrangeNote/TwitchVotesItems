using RoR2;
using RoR2.UI;
using System.Collections.Generic;
using UnityEngine;

namespace TwitchVotesItems
{
    class VoteNotificationQueue : MonoBehaviour
    {
        private Queue<VoteNotificationInfo> notificationQueue = new Queue<VoteNotificationInfo>();
        private VoteNotification currentNotification;

        public void OnVoteStart(List<PickupIndex> items, float duration)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(items[i].itemIndex);
                if (itemDef == null || itemDef.hidden)
                {
                    return;
                }
            }

            notificationQueue.Enqueue(new VoteNotificationInfo
            {
                items = items,
                duration = duration / 1000f
            });
        }

        public void Update()
        {
            //Debug.Log(currentNotification);
            //Debug.Log(notificationQueue.Count);

            if (!currentNotification && notificationQueue.Count > 0)
            {
                var characterBody = LocalUserManager.GetFirstLocalUser().cachedBody;

                var info = notificationQueue.Dequeue();

                currentNotification = characterBody.gameObject.AddComponent<VoteNotification>();
                currentNotification.transform.SetParent(characterBody.gameObject.transform);
                currentNotification.SetPosition(new Vector3(Screen.width / 2, Screen.height / 2, 0) + new Vector3(0, 128, 0));
                currentNotification.SetItems(info.items, info.duration);
            }
        }

        private class VoteNotificationInfo
        {
            public List<PickupIndex> items;
            public float duration;
        }
    }
}
