﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Feature.SitecoreForms.MarketingCategoriesSubscription.Contract.Services;
using Feature.SitecoreForms.MarketingCategoriesSubscription.xConnect.Services;
using Microsoft.Extensions.DependencyInjection;
using Sitecore;
using Sitecore.Analytics;
using Sitecore.Data.Items;
using Sitecore.DependencyInjection;
using Sitecore.EmailCampaign.Model.XConnect.Facets;
using Sitecore.ExM.Framework.Diagnostics;
using Sitecore.ExperienceForms.Mvc.Models;
using Sitecore.ExperienceForms.Mvc.Models.Fields;
using Sitecore.Framework.Conditions;
using Sitecore.Modules.EmailCampaign;
using Sitecore.Modules.EmailCampaign.Services;
using Sitecore.SecurityModel;

namespace Feature.SitecoreForms.MarketingCategoriesSubscription.Forms.FieldTypes
{
    [Serializable]
    public class MarketingPreferencesViewModel : CheckBoxListViewModel
    {
        [NonSerialized]
        private readonly IXConnectContactService _xConnectContactService;
        [NonSerialized]
        private readonly IManagerRootService _managerRootService;
        [NonSerialized]
        private readonly IMarketingPreferencesService _marketingPreferencesService;
        [NonSerialized]
        private readonly ILogger _logger;

        public MarketingPreferencesViewModel() : this(
            ServiceLocator.ServiceProvider.GetService<IXConnectContactService>(),
            ServiceLocator.ServiceProvider.GetService<IManagerRootService>(),
            ServiceLocator.ServiceProvider.GetService<IMarketingPreferencesService>(),
            ServiceLocator.ServiceProvider.GetService<ILogger>())
        {
        }

        public MarketingPreferencesViewModel(
            IXConnectContactService xConnectContactService,
            IManagerRootService managerRootService,
            IMarketingPreferencesService marketingPreferencesService,
            ILogger logger)
        {
            Condition.Requires(xConnectContactService, nameof(xConnectContactService)).IsNotNull();
            Condition.Requires(managerRootService, nameof(managerRootService)).IsNotNull();
            Condition.Requires(marketingPreferencesService, nameof(marketingPreferencesService)).IsNotNull();
            Condition.Requires(logger, nameof(logger)).IsNotNull();

            _xConnectContactService = xConnectContactService;
            _managerRootService = managerRootService;
            _marketingPreferencesService = marketingPreferencesService;
            _logger = logger;
        }

        public string ContactListId { get; set; }
        public string ManagerRootId { get; set; }

        protected override void InitItemProperties(Item item)
        {
            base.InitItemProperties(item);
            ContactListId = StringUtil.GetString(item.Fields["Contact List Id"]);
            ManagerRootId = StringUtil.GetString(item.Fields["Manager Root Id"]);
            RenderListItems(item, ManagerRootId);
        }

        protected override void UpdateItemFields(Item item)
        {
            base.UpdateItemFields(item);
            item.Fields["Contact List Id"]?.SetValue(ContactListId, true);
            item.Fields["Manager Root Id"]?.SetValue(ManagerRootId, true);
            RenderListItems(item, ManagerRootId);
        }

        private void RenderListItems(Item item, string selectedManagerRootId)
        {
            var database = Context.ContentDatabase ?? Context.Database;
            // ReSharper disable once UnusedVariable
            var isValid = Guid.TryParse(selectedManagerRootId, out var guidOutput);
            if (!isValid)
            {
                _logger.LogError($"No Valid Manager Root: {selectedManagerRootId}!");
                return;
            }

            var managerRoot = _managerRootService.GetManagerRoot(new Guid(selectedManagerRootId));

            if (managerRoot == null)
            {
                _logger.LogError($"You have to select a valid Manager Root: {selectedManagerRootId}!");
                return;
            }

            var marketingPreferences = GetMarketingPreferences(managerRoot);
            var marketingCategoryGroups = managerRoot.Settings.MarketingCategoryGroups.Select(database.GetItem).ToList();

            if (!marketingCategoryGroups.Any())
            {
                _logger.LogWarn("no marketing groups are associated to the manager root!");
                return;
            }

            if(Items.Count == 0)
            {
                foreach (var marketingCategoryGroup in marketingCategoryGroups)
                {
                    var marketingCategories = marketingCategoryGroup.Children;
                    foreach (Item marketingCategory in marketingCategories)
                    {
                        var categoryListItem = new ListFieldItem();
                        categoryListItem.ItemId = categoryListItem.Value = marketingCategory.ID.ToString();
                        categoryListItem.Text = marketingCategory.DisplayName;
                        categoryListItem.Selected = IsSelected(marketingPreferences, marketingCategory);
                        Items.Add(categoryListItem);
                    }
                }
            }

            try
            {
                using (new SecurityDisabler())
                {
                    base.UpdateDataSourceSettings(item);
                }
            }
            catch (Exception)
            {

            }
        }

        private List<MarketingPreference> GetMarketingPreferences(ManagerRoot managerRoot)
        {
            var marketingPreferences = new List<MarketingPreference>();
            if (Tracker.Current == null || Tracker.Current.Contact == null || Tracker.Current.Contact.IsNew)
            {
                return marketingPreferences;
            }

            var knownContact = _xConnectContactService.GetXConnectContactByEmailAddress();
            if (knownContact != null)
            {
                marketingPreferences = _marketingPreferencesService.GetPreferences(knownContact, managerRoot.Id);
            }

            return marketingPreferences;
        }

        private static bool IsSelected(IEnumerable<MarketingPreference> contactMarketingPreferences, Item marketingCategory)
        {
            return contactMarketingPreferences != null && contactMarketingPreferences.Any(contactMarketingPreference => contactMarketingPreference.MarketingCategoryId == marketingCategory.ID.Guid && contactMarketingPreference.Preference == true);
        }
    }
}
