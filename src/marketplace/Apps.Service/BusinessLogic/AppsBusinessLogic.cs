/********************************************************************************
 * Copyright (c) 2021, 2023 BMW Group AG
 * Copyright (c) 2021, 2023 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.ViewModels;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Web;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Service;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using System.Text.Json;
using Org.Eclipse.TractusX.Portal.Backend.Mailing.SendMail;

namespace Org.Eclipse.TractusX.Portal.Backend.Apps.Service.BusinessLogic;

/// <summary>
/// Implementation of <see cref="IAppsBusinessLogic"/>.
/// </summary>
public class AppsBusinessLogic : IAppsBusinessLogic
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IOfferSubscriptionService _offerSubscriptionService;
    private readonly AppsSettings _settings;
    private readonly IOfferService _offerService;
    private readonly IMailingService _mailingService;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="portalRepositories">Factory to access the repositories</param>
    /// <param name="offerSubscriptionService">OfferSubscription Service.</param>
    /// <param name="offerService">Offer service</param>
    /// <param name="settings">Settings</param>
    /// <param name="mailingService">Mailing service</param>
    public AppsBusinessLogic(IPortalRepositories portalRepositories, IOfferSubscriptionService offerSubscriptionService, IOfferService offerService, IOptions<AppsSettings> settings, IMailingService mailingService)
    {
        _portalRepositories = portalRepositories;
        _offerSubscriptionService = offerSubscriptionService;
        _offerService = offerService;
        _mailingService = mailingService;
        _settings = settings.Value;
    }

    /// <inheritdoc/>
    public async Task<List<AppData>> GetAllActiveAppsAsync(string iamUserId, string? languageShortName = null) =>
        await _portalRepositories.GetInstance<IOfferRepository>().GetAllActiveAppsAsync(iamUserId,languageShortName);

    /// <inheritdoc/>
    public async Task<List<SponsoredAppData>> GetAllSponsoredAppsAsync(string? languageShortName = null) =>
      await _portalRepositories.GetInstance<IOfferRepository>().GetAllSponsoredAppsAsync(languageShortName);

    /// <inheritdoc/>
    public async Task<AppFeaturesResponse> GetAppFeaturesByIdAsync(Guid appId) {
        var appRepository = _portalRepositories.GetInstance<IOfferRepository>();
        if (!await appRepository.CheckAppExistsById(appId).ConfigureAwait(false))
        {
            throw new NotFoundException($"app {appId} does not found");
        }

        return await _portalRepositories.GetInstance<IOfferRepository>().GetAppFeaturesByIdAsync(appId);
    }

    /// <inheritdoc/>
    public async Task<AppPricingResponse> GetAppPricingByIdAsync(Guid appId){

        var appRepository = _portalRepositories.GetInstance<IOfferRepository>();
        if (!await appRepository.CheckAppExistsById(appId).ConfigureAwait(false))
        {
            throw new NotFoundException($"app {appId} does not found");
        }
        return await _portalRepositories.GetInstance<IOfferRepository>().GetAppPricingByIdAsync(appId);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<BusinessAppData> GetAllUserUserBusinessAppsAsync(string userId) =>
        _portalRepositories.GetInstance<IOfferSubscriptionsRepository>()
            .GetAllBusinessAppDataForUserIdAsync(userId)
            .Select(x =>
                new BusinessAppData(
                    x.SubscriptionId,
                    x.OfferName ?? Constants.ErrorString,
                    x.SubscriptionUrl,
                    x.LeadPictureId,
                    x.Provider));

    /// <inheritdoc/>
    public async Task<AppDetailResponse> GetAppDetailsByIdAsync(Guid appId, string iamUserId, string? languageShortName = null)
    {
        var result = await _portalRepositories.GetInstance<IOfferRepository>()
            .GetOfferDetailsByIdAsync(appId, iamUserId, languageShortName, Constants.DefaultLanguage, OfferTypeId.APP).ConfigureAwait(false);
        if (result == null)
        {
            throw new NotFoundException($"appId {appId} does not exist");
        }

        return new AppDetailResponse(
            result.Id,
            result.Title ?? Constants.ErrorString,
            result.LeadPictureId,
            result.Images,
            result.ProviderUri ?? Constants.ErrorString,
            result.Provider,
            result.ContactEmail,
            result.ContactNumber,
            result.UseCases,
            result.LongDescription ?? Constants.ErrorString,
            result.Price ?? Constants.ErrorString,
            result.Tags,
            result.IsSubscribed == default ? null : result.IsSubscribed,
            result.Languages,
            result.Documents.GroupBy(d => d.documentTypeId).ToDictionary(g => g.Key, g => g.Select(d => new DocumentData(d.documentId, d.documentName))),
            result.PrivacyPolicies
        );
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<Guid> GetAllFavouriteAppsForUserAsync(string userId) =>
        _portalRepositories
            .GetInstance<IUserRepository>()
            .GetAllFavouriteAppsForUserUntrackedAsync(userId);

    /// <inheritdoc/>
    public async Task RemoveFavouriteAppForUserAsync(Guid appId, string userId)
    {
        try
        {
            var companyUserId = await _portalRepositories.GetInstance<IUserRepository>().GetCompanyUserIdForIamUserUntrackedAsync(userId).ConfigureAwait(false);
            _portalRepositories.Remove(new CompanyUserAssignedAppFavourite(appId, companyUserId));
            await _portalRepositories.SaveAsync().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ArgumentException($"Parameters are invalid or favourite does not exist.");
        }
    }

    /// <inheritdoc/>
    public async Task AddFavouriteAppForUserAsync(Guid appId, string userId)
    {
        try
        {
            var companyUserId = await _portalRepositories.GetInstance<IUserRepository>().GetCompanyUserIdForIamUserUntrackedAsync(userId).ConfigureAwait(false);
            _portalRepositories.GetInstance<IOfferRepository>().CreateAppFavourite(appId, companyUserId);
            await _portalRepositories.SaveAsync().ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException($"Parameters are invalid or app is already favourited.");
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AppWithSubscriptionStatus> GetCompanySubscribedAppSubscriptionStatusesForUserAsync(string iamUserId) =>
        _portalRepositories.GetInstance<IOfferSubscriptionsRepository>()
            .GetOwnCompanySubscribedAppSubscriptionStatusesUntrackedAsync(iamUserId);

    /// <inheritdoc/>
    public Task<Pagination.Response<OfferCompanySubscriptionStatusData>> GetCompanyProvidedAppSubscriptionStatusesForUserAsync(int page, int size, string iamUserId, SubscriptionStatusSorting? sorting, OfferSubscriptionStatusId? statusId) =>
        Pagination.CreateResponseAsync(page, size, _settings.ApplicationsMaxPageSize, _portalRepositories.GetInstance<IOfferSubscriptionsRepository>()
            .GetOwnCompanyProvidedOfferSubscriptionStatusesUntrackedAsync(iamUserId, OfferTypeId.APP, sorting, statusId ?? OfferSubscriptionStatusId.ACTIVE));

    /// <inheritdoc/>
    public Task<Guid> AddOwnCompanyAppSubscriptionAsync(Guid appId, IEnumerable<OfferAgreementConsentData> offerAgreementConsentData, string iamUserId, string accessToken) =>
        _offerSubscriptionService.AddOfferSubscriptionAsync(appId, offerAgreementConsentData, iamUserId, accessToken, _settings.ServiceManagerRoles, OfferTypeId.APP, _settings.BasePortalAddress);

    /// <inheritdoc/>
    public async Task ActivateOwnCompanyProvidedAppSubscriptionAsync(Guid appId, Guid subscribingCompanyId, string iamUserId)
    {
        var offerSubscriptionRepository = _portalRepositories.GetInstance<IOfferSubscriptionsRepository>();
        var assignedAppData = await offerSubscriptionRepository.GetCompanyAssignedAppDataForProvidingCompanyUserAsync(appId, subscribingCompanyId, iamUserId).ConfigureAwait(false);
        if(assignedAppData == default)
        {
            throw new NotFoundException($"App {appId} does not exist.");
        }

        var (subscriptionId, subscriptionStatusId, requesterId, appName, companyUserId, requesterData) = assignedAppData;
        if(companyUserId == Guid.Empty)
        {
            throw new ControllerArgumentException("Missing permission: The user's company does not provide the requested app so they cannot activate it.");
        }

        if (subscriptionId == Guid.Empty)
        {
            throw new ControllerArgumentException($"subscription for app {appId}, company {subscribingCompanyId} has not been created yet");
        }

        if (subscriptionStatusId != OfferSubscriptionStatusId.PENDING )
        {
            throw new ControllerArgumentException($"subscription for app {appId}, company {subscribingCompanyId} is not in status PENDING");
        }

        if (appName is null)
        {
            throw new ConflictException("App Name is not yet set.");
        }

        offerSubscriptionRepository.AttachAndModifyOfferSubscription(subscriptionId, subscription => subscription.OfferSubscriptionStatusId = OfferSubscriptionStatusId.ACTIVE);

        _portalRepositories.GetInstance<INotificationRepository>().CreateNotification(requesterId,
            NotificationTypeId.APP_SUBSCRIPTION_ACTIVATION, false,
            notification =>
            {
                notification.CreatorUserId = companyUserId;
                notification.Content = JsonSerializer.Serialize(new
                {
                    AppId = appId,
                    AppName = appName
                });
            });

        var userName = string.Join(" ", new[] { requesterData.Firstname, requesterData.Lastname });

        if (!string.IsNullOrWhiteSpace(requesterData.Email))
        {
            var mailParams = new Dictionary<string, string>
            {
                { "offerCustomerName", !string.IsNullOrWhiteSpace(userName) ? userName : "App Owner" },
                { "offerName", appName },
                { "url", _settings.BasePortalAddress },
            };
            await _mailingService.SendMails(requesterData.Email, mailParams, new List<string> { "subscription-activation" }).ConfigureAwait(false);
        }
        await _portalRepositories.SaveAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UnsubscribeOwnCompanyAppSubscriptionAsync(Guid appId, string iamUserId)
    {
        var assignedAppData = await _portalRepositories.GetInstance<IOfferSubscriptionsRepository>().GetCompanyAssignedAppDataForCompanyUserAsync(appId, iamUserId).ConfigureAwait(false);

        if(assignedAppData == default)
        {
            throw new NotFoundException($"App {appId} does not exist.");
        }

        var (subscription, _) = assignedAppData;

        if (subscription == null)
        {
            throw new ArgumentException($"There is no active subscription for user '{iamUserId}' and app '{appId}'");
        }
        subscription.OfferSubscriptionStatusId = OfferSubscriptionStatusId.INACTIVE;
        await _portalRepositories.SaveAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Guid> CreateAppAsync(AppInputModel appInputModel)
    {
        // Add app to db
        var appRepository = _portalRepositories.GetInstance<IOfferRepository>();

        var appId = appRepository.CreateOffer(appInputModel.Provider, OfferTypeId.APP, app =>
        {
            app.Name = appInputModel.Title;
            app.MarketingUrl = appInputModel.ProviderUri;
            app.ContactEmail = appInputModel.ContactEmail;
            app.ContactNumber = appInputModel.ContactNumber;
            app.ProviderCompanyId = appInputModel.ProviderCompanyId;
            app.OfferStatusId = OfferStatusId.CREATED;
            app.SalesManagerId = appInputModel.SalesManagerId;

        }).Id;

        appRepository.AddAppAssignedUseCases(appInputModel.UseCaseIds.Select(uc =>
            new ValueTuple<Guid, Guid>(appId, uc)));
        appRepository.AddOfferDescriptions(appInputModel.Descriptions.Select(d =>
            new ValueTuple<Guid, string, string, string>(appId, d.LanguageCode, d.LongDescription, d.ShortDescription)));
        appRepository.AddAppLanguages(appInputModel.SupportedLanguageCodes.Select(c =>
            new ValueTuple<Guid, string>(appId, c)));

        //To Save keywords for the App
        appRepository.AddOfferKeyWords(appId, appInputModel.TagNames);
        //To Save LeadPicture For the App
        //appRepository.AddLeadPicture(appId, appInputModel.LeadPictureUri);

        //To Save Features and Vedio,Featureimages
        var featureId = new Guid();
        appRepository.AddAppFeaturesByIdAsync(appInputModel.FeatureSummary,appInputModel.videoLink, appId);

        var KeyfeatureId = new Guid();
        appRepository.AddAppKeyFeaturesByIdAsync(appInputModel.KeyFeatures.Select(f =>
            new ValueTuple<Guid,string,string,int,Guid>(KeyfeatureId, f.Title, f.ShortDescription, f.Sequence, featureId)));

        var licenseId = appRepository.CreateOfferLicenses(appInputModel.Price).Id;
        appRepository.CreateOfferAssignedLicense(appId, licenseId);
                            await _portalRepositories.SaveAsync().ConfigureAwait(false);
         return appId;
    }

    /// <inheritdoc/>
    public async Task<Guid> CreateAppCardAsync(AppCardInputModel appCardInputModel)
    {
        // Add app card to db
        var appRepository = _portalRepositories.GetInstance<IOfferRepository>();

        var appId = appRepository.CreateOffer(appCardInputModel.Provider, OfferTypeId.APP, app =>
        {
            app.Name = appCardInputModel.Title;
            app.ProviderCompanyId = appCardInputModel.ProviderCompanyId;
            app.OfferStatusId = OfferStatusId.CREATED;
            app.SalesManagerId = appCardInputModel.SalesManagerId;

        }).Id;

        appRepository.AddAppAssignedUseCases(appCardInputModel.UseCaseIds.Select(uc =>
            new ValueTuple<Guid, Guid>(appId, uc)));
        appRepository.AddOfferDescriptions(appCardInputModel.Descriptions.Select(d =>
            new ValueTuple<Guid, string, string, string>(appId, d.LanguageCode, d.LongDescription, d.ShortDescription)));
        appRepository.AddAppLanguages(appCardInputModel.SupportedLanguageCodes.Select(c =>
            new ValueTuple<Guid, string>(appId, c)));

        //To Save keywords for the App
        appRepository.AddOfferKeyWords(appId, appCardInputModel.Tags);
        //To Save LeadPicture For the App
        //appRepository.AddLeadPicture(appId, appInputModel.LeadPictureUri);       

        try
        {
            await _portalRepositories.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        { }


        return appId;
    }

    /// <inheritdoc/>
    public async Task CreateAppCardDetailsAsync(AppCardDeatilsInputModel appCardDeatilsInputModel,Guid appid)
    {
        // Add app details to db
        var appRepository = _portalRepositories.GetInstance<IOfferRepository>();

        //To Save Features and Vedio,Featureimages
        var feature = appRepository.AddAppFeaturesByIdAsync(appCardDeatilsInputModel.FeatureSummary, appCardDeatilsInputModel.videoLink, appid);

        IEnumerable<(Guid, string, string, int, Guid)> keyfeatureTypes = appCardDeatilsInputModel.KeyFeatures.Select(f =>
                           new ValueTuple<Guid, string, string, int, Guid>(Guid.NewGuid(), f.Title, f.ShortDescription, f.Sequence, feature.Id));
        appRepository.AddAppKeyFeaturesByIdAsync(keyfeatureTypes);


        try
        {
            await _portalRepositories.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        { }

    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AllOfferData> GetCompanyProvidedAppsDataForUserAsync(string userId) =>
        _portalRepositories.GetInstance<IOfferRepository>().GetProvidedOffersData(OfferTypeId.APP, userId);

    /// <inheritdoc />
    public Task<OfferAutoSetupResponseData> AutoSetupAppAsync(OfferAutoSetupData data, string iamUserId) =>
        _offerService.AutoSetupServiceAsync(data, _settings.ServiceAccountRoles, _settings.ITAdminRoles, iamUserId, OfferTypeId.APP, _settings.UserManagementAddress);

    /// <inheritdoc />
    public IAsyncEnumerable<AgreementData> GetAppAgreement(Guid appId) =>
        _offerService.GetOfferAgreementsAsync(appId, OfferTypeId.APP);

    /// <inheritdoc />
    public Task DeactivateOfferbyAppIdAsync(Guid appId, string iamUserId) =>
        _offerService.DeactivateOfferIdAsync(appId, iamUserId, OfferTypeId.APP);

    /// <inheritdoc />
    public async Task<(byte[] Content, string ContentType, string FileName)> GetAppImageDocumentContentAsync(Guid appId, Guid documentId, CancellationToken cancellationToken)
    {
        var documentRepository = _portalRepositories.GetInstance<IDocumentRepository>();
        var document = await documentRepository.GetOfferImageDocumentContentAsync(appId, documentId, _settings.AppImageDocumentTypeIds, OfferTypeId.APP, cancellationToken).ConfigureAwait(false);
        if (!document.IsDocumentExisting)
        {
            throw new NotFoundException($"document {documentId} does not exist");
        }
        if (!document.IsValidDocumentType)
        {
            throw new ControllerArgumentException($"Document {documentId} can not get retrieved. Document type not supported.");
        }
        if (!document.IsValidOfferType)
        {
            throw new ControllerArgumentException($"offer {appId} is not an app");
        }
        if (!document.IsDocumentLinkedToOffer)
        {
            throw new ControllerArgumentException($"Document {documentId} and app id {appId} do not match.");
        }
        if (document.Content == null)
        {
            throw new UnexpectedConditionException($"document content should never be null");
        }
        return (document.Content, document.FileName.MapToContentType(), document.FileName);
    }

    public Task UpdateCardAppAsync(Guid appId, AppRequestModel appRequestModel, string userId)
    {
        if (appId == Guid.Empty)
        {
            throw new ControllerArgumentException($"AppId must not be empty");
        }        

        return EditAppCardAsync(appId, appRequestModel, userId);
    }

    private async Task EditAppCardAsync(Guid appId, AppRequestModel appRequestModel, string userId)
    {
        var appData = await _portalRepositories.GetInstance<IOfferRepository>()
            .GetAppCardUpdateData(
                appId,
                userId,
                appRequestModel.SupportedLanguageCodes)
            .ConfigureAwait(false);
        if (appData is null)
        {
            throw new NotFoundException($"app {appId} does not exists");
        }

        if (appData.OfferState != OfferStatusId.CREATED)
        {
            throw new ConflictException($"Apps in State {appData.OfferState} can't be updated");
        }
        var newSupportedLanguages = appRequestModel.SupportedLanguageCodes.Except(appData.Languages.Where(x => x.IsMatch).Select(x => x.Shortname));
        var existingLanguageCodes = await _portalRepositories.GetInstance<ILanguageRepository>().GetLanguageCodesUntrackedAsync(newSupportedLanguages).ToListAsync().ConfigureAwait(false);
        if (newSupportedLanguages.Except(existingLanguageCodes).Any())
        {
            throw new ControllerArgumentException($"The language(s) {string.Join(",", newSupportedLanguages.Except(existingLanguageCodes))} do not exist in the database.",
                nameof(appRequestModel.SupportedLanguageCodes));
        }
        var appRepository = _portalRepositories.GetInstance<IOfferRepository>();
        appRepository.AttachAndModifyOffer(
        appId,
        app =>
        {
            app.Name = appRequestModel.Title;
            app.OfferStatusId = OfferStatusId.CREATED;
            app.Provider = appRequestModel.Provider;
            app.SalesManagerId = appRequestModel.SalesManagerId;
        },
        app =>
        {
            app.SalesManagerId = appData.SalesManagerId;
        });

        _offerService.UpsertRemoveOfferDescription(appId, appRequestModel.Descriptions.Select(x => new Localization(x.LanguageCode, x.LongDescription, x.ShortDescription)), appData.OfferDescriptions);
        UpdateAppSupportedLanguages(appId, newSupportedLanguages, appData.Languages.Where(x => !x.IsMatch).Select(x => x.Shortname), appRepository);

        appRepository.CreateDeleteAppAssignedUseCases(appId, appData.MatchingUseCases, appRequestModel.UseCaseIds);

        // appRepository.CreateDeleteAppAssignedPrivacyPolicies(appId, appData.MatchingPrivacyPolicies, appRequestModel.PrivacyPolicies);

        //_offerService.CreateOrUpdateOfferLicense(appId, appRequestModel.Provider, appData.OfferLicense);

        await _portalRepositories.SaveAsync().ConfigureAwait(false);
    }

    private static void UpdateAppSupportedLanguages(Guid appId, IEnumerable<string> newSupportedLanguages, IEnumerable<string> languagesToRemove, IOfferRepository appRepository)
    {
        appRepository.AddAppLanguages(newSupportedLanguages.Select(language => (appId, language)));
        appRepository.RemoveAppLanguages(languagesToRemove.Select(language => (appId, language)));
    }


    public Task UpdateCardDetailsAppAsync(Guid appId, EditAppCardDetails updateModel)
    {
        if (appId == Guid.Empty)
        {
            throw new ControllerArgumentException($"AppId must not be empty");
        }

        return EditAppCardDetailsAsync(appId, updateModel);
    }

    private async Task EditAppCardDetailsAsync(Guid appId, EditAppCardDetails updateModel)
    {
        var appRepository = _portalRepositories.GetInstance<IOfferRepository>();
        var appResult = await appRepository.GetAppFeaturesByIdAsync(appId).ConfigureAwait(false);
        if (appResult == default)
        {
            throw new NotFoundException($"app {appId} does not exist");
        }
        appRepository.AttachAndModifyFeature(appResult.Id, app =>
        {
            if (appResult.summary != updateModel.FeatureSummary)
            {
                app.Summary= updateModel.FeatureSummary;
            }
            if (appResult.videoLink != updateModel.Videolink)
            {
                app.VideoLink = updateModel.Videolink;
            }

        });
        _offerService.UpsertRemoveKeyFeatures(appResult.Id, updateModel.KeyFeature,appResult.Features,appId);

        await _portalRepositories.SaveAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task CreateAppDocumentAsync(Guid appId, DocumentTypeId documentTypeId, IFormFile document, string iamUserId, CancellationToken cancellationToken) =>
        UploadAppDoc(appId, documentTypeId, document, iamUserId, OfferTypeId.SERVICE, cancellationToken);

    private async Task UploadAppDoc(Guid appId, DocumentTypeId documentTypeId, IFormFile document, string iamUserId, OfferTypeId offerTypeId, CancellationToken cancellationToken) =>
        await _offerService.UploadDocumentForAppAsync(appId, documentTypeId, document, iamUserId, offerTypeId, _settings.DocumentTypeIds, _settings.ContentTypeSettings, cancellationToken).ConfigureAwait(false);

}
