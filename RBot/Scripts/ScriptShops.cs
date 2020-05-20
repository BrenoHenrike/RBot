﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RBot.Flash;
using RBot.Shops;
using RBot.Items;
using System.Dynamic;
using Newtonsoft.Json;

namespace RBot
{
    public class ScriptShops : ScriptableObject
    {
        /// <summary>
        /// A list of items that were available in the last loaded shop.
        /// </summary>
        [ObjectBinding("world.shopinfo.items")]
        public List<ShopItem> ShopItems { get; }
        /// <summary>
        /// A boolean indicated whether a shop is currently loaded or not.
        /// </summary>
        public bool IsShopLoaded => !Bot.IsNull("world.shopinfo");
        /// <summary>
        /// Gets the currently (or last loaded) shop id.
        /// </summary>
        [ObjectBinding("world.shopinfo.ShopID")]
        public int ShopID { get; }
        /// <summary>
        /// Gets the currently (or last loaded) shop's name.
        /// </summary>
        [ObjectBinding("world.shopinfo.sName")]
        public string ShopName { get; }
        /// <summary>
        /// Gets a list of items from the currently loaded merge shop.
        /// </summary>
        [ObjectBinding("world.shopinfo.items")]
        public List<MergeItem> MergeItems { get; }

        /// <summary>
        /// Loads the specified shop in game.
        /// </summary>
        /// <param name="id">The id of the shop to be loaded.</param>
        /// <remarks>Loading invalid shop ids will get you kicked. Be sure to only use updated/recent lists.</remarks>
        public void Load(int id)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForActionCooldown(ScriptWait.GameActions.LoadShop);
            Bot.CallGameFunction("world.sendLoadShopRequest", id);
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForTrue(() => ShopID == id, 10);
        }

        /// <summary>
        /// Buys the specified item from the shop with the specified id.
        /// </summary>
        /// <param name="shopId">The shop to buy the item from.</param>
        /// <param name="name">The name of the item to buy.</param>
        /// <remarks>This loads the shop, waits until it is fully loaded, and then sends the buy item request.</remarks>
        public void BuyItem(int shopId, string name)
        {
            Load(shopId);
            BuyItem(name);
        }

        /// <summary>
        /// Buys the specified item from the currently loaded shop.
        /// </summary>
        /// <param name="name">The name of the item to buy.</param>
        public void BuyItem(string name)
        {
            int index;
            List<ShopItem> items = ShopItems;
            if (IsShopLoaded && (index = items.FindIndex(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) > -1)
            {
                if (Bot.Options.SafeTimings)
                {
                    Bot.Wait.ForActionCooldown(ScriptWait.GameActions.BuyItem);
                    Bot.Wait.ItemBuyEvent.Reset();
                }
                ExpandoObject item;
                using (FlashArray<ExpandoObject> fItems = FlashObject<ExpandoObject>.Create("world.shopinfo.items").ToArray())
                using (FlashObject<ExpandoObject> fItem = fItems.Get(index))
                    item = fItem.Value;
                Bot.CallGameFunction("world.sendBuyItemRequest", item);
                if (Bot.Options.SafeTimings)
                    Bot.Wait.ForItemBuy();
            }
        }

        /// <summary>
        /// Sells the specified item.
        /// </summary>
        /// <param name="name">The name of the item to sell.</param>
        public void SellItem(string name)
        {
            if (Bot.Inventory.TryGetItem(name, out InventoryItem item))
            {
                if (Bot.Options.SafeTimings)
                {
                    Bot.Wait.ForActionCooldown(ScriptWait.GameActions.SellItem);
                    Bot.Wait.ItemSellEvent.Reset();
                }
                Bot.SendPacket($"%xt%zm%sellItem%{Bot.Map.RoomID}%{item.ID}%{item.Quantity}%{item.CharItemID}%");
                if (Bot.Options.SafeTimings)
                    Bot.Wait.ForItemSell();
            }
        }

        /// <summary>
        /// Loads the specified hair shop in game.
        /// </summary>
        /// <param name="id">The id of the hair shop to be loaded.</param>
        [MethodCallBinding("world.sendLoadHairShopRequest", RunMethodPre = true, GameFunction = true)]
        public void LoadHairShop(int id)
        {
            if (Bot.Options.SafeTimings)
                Bot.Wait.ForActionCooldown(ScriptWait.GameActions.LoadHairShop);
        }

        /// <summary>
        /// Loads the armour customizer interface.
        /// </summary>
        [MethodCallBinding("openArmorCustomize", GameFunction = true)]
        public void LoadArmourCustomizer() { }
    }
}
