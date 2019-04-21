using BepInEx;
using EntityStates;
using RoR2;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TwitchIntegration;
using static TwitchIntegration.TwitchIntegration;
using UnityEngine;
using UnityEngine.Networking;

namespace TwitchVotesItems
{
    [BepInDependency("dev.orangenote.twitchintegration")]
    [BepInPlugin("dev.orangenote.twitchvotesitems", "TwitchVotesItems", "1.0.0")]
    class TwitchVotesItems : BaseUnityPlugin
    {
        public void Awake()
        {
            On.RoR2.ChestBehavior.RollItem += ChestBehavior_RollItem;
            On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop;
            On.RoR2.ChestBehavior.Open += ChestBehavior_Open;
        }

        private void ChestBehavior_Open(On.RoR2.ChestBehavior.orig_Open orig, ChestBehavior self)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Server] function 'System.Void RoR2.ChestBehavior::Open()' called on client");
                return;
            }

            if (self.gameObject.name.StartsWith("LunarChest"))
            {
                orig(self);
                return;
            }

            FieldInfo dropPickup = self.GetType().GetField("dropPickup", BindingFlags.Instance | BindingFlags.NonPublic);
            var dropPickupValue = (PickupIndex) dropPickup.GetValue(self);

            if (dropPickupValue == PickupIndex.none)
            {
                self.ItemDrop();
            }
            else
                orig(self);
        }

        private void ChestBehavior_ItemDrop(On.RoR2.ChestBehavior.orig_ItemDrop orig, ChestBehavior self)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Server] function 'System.Void RoR2.ChestBehavior::ItemDrop()' called on client");
                return;
            }

            if (self.gameObject.name.StartsWith("LunarChest"))
            {
                orig(self);
                return;
            }

            FieldInfo dropPickup = self.GetType().GetField("dropPickup", BindingFlags.Instance | BindingFlags.NonPublic);
            var dropPickupValue = (PickupIndex) dropPickup.GetValue(self);

            if (dropPickupValue != PickupIndex.none)
            {
                orig(self);
                return;
            }

            // Steal opening sound from timed chest without changing to a problematic state
            EntityStateMachine component = self.GetComponent<EntityStateMachine>();
            if (component)
            {
                component.SetNextState(EntityState.Instantiate(new SerializableEntityStateType(typeof(EntityStates.TimedChest.Opening))));
            }

            // Generate list of 3 random item indexes
            var randomItemList = new List<PickupIndex>();
            randomItemList.Add(RollVoteItem(self));
            randomItemList.Add(RollVoteItem(self));
            randomItemList.Add(RollVoteItem(self));

            var voteDuration = 5000f;

            // Create a new vote, by passing valid poll options and vote duration
            var vote = new Vote(new List<string>(new string[] { "1", "2", "3" }), voteDuration);

            vote.StartEventHandler += (_, __) =>
            {
                var characterBody = LocalUserManager.GetFirstLocalUser().cachedBody;

                var notificationQueue = characterBody.gameObject.GetComponent<VoteNotificationQueue>();

                if (notificationQueue == null)
                    notificationQueue = characterBody.gameObject.AddComponent<VoteNotificationQueue>();

                notificationQueue.OnVoteStart(randomItemList, voteDuration);

                // Notify the event in chat
                SendChatMessage(
                    string.Format("Vote for the next item! (1) {0} | (2) {1} | (3) {2}",
                        Language.GetString(randomItemList[0].GetPickupNameToken()),
                        Language.GetString(randomItemList[1].GetPickupNameToken()),
                        Language.GetString(randomItemList[2].GetPickupNameToken())
                    )
                );
            };

            vote.EndEventHandler += (_, e) =>
            {
                var votedItemName = Language.GetString(randomItemList[e.VotedIndex].GetPickupNameToken());

                float totalVotesCount = 1f;
                int winnerCount = 1;

                if (e.VoteCounter.Keys.Count > 0)
                {
                    totalVotesCount = e.VoteCounter.Sum(el => el.Value);
                    winnerCount = e.VoteCounter[(e.VotedIndex + 1).ToString()];
                }

                SendChatMessage(
                    string.Format(
                        "({0}) {1} won! ({2})",
                        e.VotedIndex + 1,
                        votedItemName,
                        (winnerCount / totalVotesCount).ToString("P1", CultureInfo.InvariantCulture).Replace(" %", "%")
                    )
                );

                var votedItem = randomItemList[e.VotedIndex];

                dropPickup.SetValue(self, votedItem);
                self.Open();
            };

            VoteManager.AddVote(vote);
        }

        private PickupIndex RollVoteItem(ChestBehavior self)
        {
            WeightedSelection<List<PickupIndex>> weightedSelection = new WeightedSelection<List<PickupIndex>>(8);
            weightedSelection.AddChoice(Run.instance.availableTier1DropList, self.tier1Chance);
            weightedSelection.AddChoice(Run.instance.availableTier2DropList, self.tier2Chance);
            weightedSelection.AddChoice(Run.instance.availableTier3DropList, self.tier3Chance);
            weightedSelection.AddChoice(Run.instance.availableLunarDropList, self.lunarChance);
            List<PickupIndex> dropList = weightedSelection.Evaluate(Run.instance.treasureRng.nextNormalizedFloat);

            return dropList[Run.instance.treasureRng.RangeInt(0, dropList.Count)];
        }

        private void ChestBehavior_RollItem(On.RoR2.ChestBehavior.orig_RollItem orig, ChestBehavior self)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Server] function 'System.Void RoR2.ChestBehavior::RollItem()' called on client");
                return;
            }
            WeightedSelection<List<PickupIndex>> weightedSelection = new WeightedSelection<List<PickupIndex>>(8);
            weightedSelection.AddChoice(Run.instance.availableTier1DropList, self.tier1Chance);
            weightedSelection.AddChoice(Run.instance.availableTier2DropList, self.tier2Chance);
            weightedSelection.AddChoice(Run.instance.availableTier3DropList, self.tier3Chance);
            weightedSelection.AddChoice(Run.instance.availableLunarDropList, self.lunarChance);
            List<PickupIndex> dropList = weightedSelection.Evaluate(Run.instance.treasureRng.nextNormalizedFloat);

            if(self.gameObject.name.StartsWith("LunarChest"))
                self.GetType().GetMethod("PickFromList", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(self, new object[] { dropList });
        }
    }
}
