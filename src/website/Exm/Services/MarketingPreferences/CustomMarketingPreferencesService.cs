﻿using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore;
using Sitecore.EmailCampaign.Core.Services;
using Sitecore.EmailCampaign.Model.XConnect;
using Sitecore.EmailCampaign.Model.XConnect.Facets;
using Sitecore.EmailCampaign.XConnect.Web;
using Sitecore.Framework.Conditions;

namespace Feature.SitecoreForms.MarketingCategoriesSubscription.Exm.Services.MarketingPreferences
{
    // ToDo: Remove this class if getting the Marketing preferences works correct and use the Sitecore MarketingPreferenceService!
    internal sealed class CustomMarketingPreferencesService : ICustomMarketingPreferencesService
    {
        private readonly ICurrentDateProvider _currentDateProvider;
        private readonly XConnectRetry _xConnectRetry;

        public CustomMarketingPreferencesService(
            ICurrentDateProvider currentDateProvider,
            XConnectRetry xConnectRetry)
        {
            Condition.Requires(currentDateProvider, nameof(currentDateProvider)).IsNotNull();
            Condition.Requires(xConnectRetry, nameof(xConnectRetry)).IsNotNull();
            _currentDateProvider = currentDateProvider;
            _xConnectRetry = xConnectRetry;
        }

        public double Delay { get; set; }

        public int RetryCount { get; set; }

        public List<MarketingPreference> GetPreferences(
            Sitecore.XConnect.Contact contact,
            Guid managerRootId)
        {
            Condition.Requires(contact, nameof(contact)).IsNotNull();
            var keyBehaviorCache = contact.ExmKeyBehaviorCache();
            if (keyBehaviorCache == null)
            {
                return null;
            }

            if (keyBehaviorCache.MarketingPreferences == null || !keyBehaviorCache.MarketingPreferences.Any() || managerRootId == Guid.Empty)
            {
                return keyBehaviorCache.MarketingPreferences;
            }

            return keyBehaviorCache.MarketingPreferences.Where(x => x.ManagerRootId == managerRootId).ToList();
        }

        public List<MarketingPreference> SavePreferences(
            Sitecore.XConnect.Contact contact,
            List<MarketingPreference> preferences)
        {
            Condition.Requires(contact, nameof(contact)).IsNotNull();
            Condition.Requires(preferences, nameof(preferences)).IsNotNull();
            var facet = contact.ExmKeyBehaviorCache();
            if (facet == null)
            {
                facet = new ExmKeyBehaviorCache
                {
                    MarketingPreferences = new List<MarketingPreference>()
                };
            }
            else
            {
                facet.MarketingPreferences = facet.MarketingPreferences ?? new List<MarketingPreference>();
            }

            facet.MarketingPreferences = Merge(facet.MarketingPreferences, preferences);
            _xConnectRetry.RequestWithRetry(
                client =>
                {
                    client.SetExmKeyBehaviorCache(contact, facet);
                    client.SubmitAsync();
                },
                Delay,
                RetryCount);
            return facet.MarketingPreferences;
        }

        private List<MarketingPreference> Merge(
            List<MarketingPreference> oldPreferences,
            List<MarketingPreference> newPreferences)
        {
            var source = oldPreferences;
            foreach (var newPreference1 in newPreferences)
            {
                var newPreference = newPreference1;
                var marketingPreference = source.FirstOrDefault(
                    x =>
                    {
                        if (x.MarketingCategoryId == newPreference.MarketingCategoryId)
                        {
                            return x.ManagerRootId == newPreference.ManagerRootId;
                        }

                        return false;
                    });
                if (marketingPreference == null)
                {
                    newPreference.DateTime = _currentDateProvider.UtcNow();
                    source.Add(newPreference);
                }
                else if (!marketingPreference.Preference.Equals(newPreference.Preference))
                {
                    marketingPreference.Preference = newPreference.Preference;
                    marketingPreference.DateTime = _currentDateProvider.UtcNow();
                }
            }

            return source;
        }
    }
}
