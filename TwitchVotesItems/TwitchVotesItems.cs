using BepInEx;
using BepInEx.Configuration;
using EntityStates;
using RoR2;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TwitchIntegration;
using UnityEngine;
using UnityEngine.Networking;
using static TwitchIntegration.TwitchIntegration;

namespace TwitchVotesItems
{
    [BepInDependency("dev.orangenote.twitchintegration")]
    [BepInPlugin("dev.orangenote.twitchvotesitems", "TwitchVotesItems", "1.1.0")]
    class TwitchVotesItems : BaseUnityPlugin
    {
        private float VoteDuration;
        private bool DropItems;

        public void Awake()
        {
            On.RoR2.ChestBehavior.RollItem += ChestBehavior_RollItem;
            On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop;
            On.RoR2.ChestBehavior.Open += ChestBehavior_Open;

            VoteDuration = ParseVoteDuration(Config.Wrap("Twitch", "VoteDuration", "Time your chat has to vote in milliseconds.", "20000").Value);

            DropItems = Config.Wrap(
                "Twitch",
                "DropItems",
                "If disabled, items will be automatically added to the inventory (experimental, might not work with multiplayer + other mods).",
                true
            ).Value;
        }

        private float ParseVoteDuration(string configline)
        {
            if (float.TryParse(configline, out float value))
                return value;
            return 20000f;
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

            var chestName = self.gameObject.name.ToLower();

            if (chestName.StartsWith("lunarchest"))
            {
                orig(self);
                return;
            }

            FieldInfo dropPickup = self.GetType().GetField("dropPickup", BindingFlags.Instance | BindingFlags.NonPublic);
            var dropPickupValue = (PickupIndex) dropPickup.GetValue(self);

            if (dropPickupValue != PickupIndex.none)
            {
                if (!DropItems && (chestName.StartsWith("chest") || chestName.StartsWith("goldchest")))
                {
                    var pickupController = gameObject.GetComponent<GenericPickupController>();

                    if (pickupController == null)
                        pickupController = this.gameObject.AddComponent<GenericPickupController>();

                    var characterBody = LocalUserManager.GetFirstLocalUser().cachedBody;

                    characterBody.inventory.GiveItem(dropPickupValue.itemIndex, 1);

                    pickupController.GetType().GetMethod("SendPickupMessage", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(pickupController, new object[] { characterBody.inventory.GetComponent<CharacterMaster>(), dropPickupValue });

                    dropPickup.SetValue(self, PickupIndex.none);
                    return;
                }

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

            // Create a new vote, by passing valid poll options and vote duration
            var vote = new Vote(new List<string>(new string[] { "1", "2", "3" }), VoteDuration);

            vote.StartEventHandler += (_, __) =>
            {
                var characterBody = LocalUserManager.GetFirstLocalUser().cachedBody;

                var notificationQueue = characterBody.gameObject.GetComponent<VoteNotificationQueue>();

                if (notificationQueue == null)
                    notificationQueue = characterBody.gameObject.AddComponent<VoteNotificationQueue>();

                notificationQueue.OnVoteStart(randomItemList, VoteDuration);

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
