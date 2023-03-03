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
using System.Security.Cryptography;

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
    private readonly IOfferSetupService _offerSetupService;
    private readonly IMailingService _mailingService;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="portalRepositories">Factory to access the repositories</param>
    /// <param name="offerSubscriptionService">OfferSubscription Service.</param>
    /// <param name="offerService">Offer service</param>
    /// <param name="offerSetupService">Offer Setup Service</param>
    /// <param name="settings">Settings</param>
    /// <param name="mailingService">Mailing service</param>
    public AppsBusinessLogic(
        IPortalRepositories portalRepositories,
        IOfferSubscriptionService offerSubscriptionService,
        IOfferService offerService,
        IOfferSetupService offerSetupService,
        IOptions<AppsSettings> settings,
        IMailingService mailingService)
    {
        _portalRepositories = portalRepositories;
        _offerSubscriptionService = offerSubscriptionService;
        _offerService = offerService;
        _offerSetupService = offerSetupService;
        _mailingService = mailingService;
        _settings = settings.Value;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AppData> GetAllActiveAppsAsync(string? languageShortName = null) =>
        _portalRepositories.GetInstance<IOfferRepository>().GetAllActiveAppsAsync(languageShortName)
            .Select(app => new AppData(
                    app.Id,
                    app.Name ?? Constants.ErrorString,
                    app.ShortDescription ?? Constants.ErrorString,
                    app.VendorCompanyName,
                    app.LicenseText ?? Constants.ErrorString,
                    app.LeadPictureId,
                    app.UseCaseNames));

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

        var userName = string.Join(" ", requesterData.Firstname, requesterData.Lastname); 

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

        var licenseId = appRepository.CreateOfferLicenses(appInputModel.Price).Id;
        appRepository.CreateOfferAssignedLicense(appId, licenseId);
        appRepository.AddAppAssignedUseCases(appInputModel.UseCaseIds.Select(uc =>
            new ValueTuple<Guid, Guid>(appId, uc)));
        appRepository.AddOfferDescriptions(appInputModel.Descriptions.Select(d =>
            new ValueTuple<Guid, string, string, string>(appId, d.LanguageCode, d.LongDescription, d.ShortDescription)));
        appRepository.AddAppLanguages(appInputModel.SupportedLanguageCodes.Select(c =>
            new ValueTuple<Guid, string>(appId, c)));

        await _portalRepositories.SaveAsync().ConfigureAwait(false);

        return appId;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AllOfferData> GetCompanyProvidedAppsDataForUserAsync(string userId) =>
        _portalRepositories.GetInstance<IOfferRepository>().GetProvidedOffersData(OfferTypeId.APP, userId);
    
    /// <inheritdoc />
    public Task<OfferAutoSetupResponseData> AutoSetupAppAsync(OfferAutoSetupData data, string iamUserId) =>
        _offerSetupService.AutoSetupOfferAsync(data, _settings.ServiceAccountRoles, _settings.ITAdminRoles, iamUserId, OfferTypeId.APP, _settings.UserManagementAddress);

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
        if (document.IsInactive)
        {
            throw new ConflictException($"Document {documentId} is in status INACTIVE");
        }
        if (document.Content == null)
        {
            throw new UnexpectedConditionException($"document content should never be null");
        }
        return (document.Content, document.FileName.MapToContentType(), document.FileName);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OfferDescriptionData>> GetAppUpdateDescritionByIdAsync(Guid appId, string iamUserId)
    {        
        var offerRepository = _portalRepositories.GetInstance<IOfferRepository>();
        return await ValidateAndGetAppDescription(appId, iamUserId, offerRepository);        
    }

    /// <inheritdoc />
    public async Task CreateOrUpdateAppDescriptionByIdAsync(Guid appId, string iamUserId, IEnumerable<LocalizedDescription> offerDescriptionDatas)
    {
        var offerRepository = _portalRepositories.GetInstance<IOfferRepository>();

        offerRepository.CreateUpdateDeleteOfferDescriptions(appId,
            await ValidateAndGetAppDescription(appId, iamUserId, offerRepository),
            offerDescriptionDatas.Select(od => new ValueTuple<string,string, string>(od.LanguageCode,od.LongDescription,od.ShortDescription)));

        await _portalRepositories.SaveAsync().ConfigureAwait(false);
    }

    private static async Task<IEnumerable<OfferDescriptionData>> ValidateAndGetAppDescription(Guid appId, string iamUserId, IOfferRepository offerRepository)
    {
        var result = await offerRepository.GetActiveOfferDescriptionDataByIdAsync(appId, OfferTypeId.APP, iamUserId).ConfigureAwait(false);
        if(result == default)
        {
            throw new NotFoundException($"App {appId} does not exist.");
        }

        if(!result.IsStatusActive)
        {
            throw new ConflictException($"App {appId} is in InCorrect Status");
        }

        if(!result.IsProviderCompanyUser)
        {
            throw new ForbiddenException($"user {iamUserId} is not a member of the providercompany of App {appId}");
        }

        if (result.OfferDescriptionDatas == null)
        {
            throw new UnexpectedConditionException("offerDescriptionDatas should never be null here");
        }
        return result.OfferDescriptionDatas;
    }

    /// <inheritdoc />
    public async Task CreatOfferAssignedAppLeadImageDocumentByIdAsync(Guid appId, string iamUserId, IFormFile document, CancellationToken cancellationToken)
    {
        var appLeadImageContentTypes = new []{ "image/jpeg","image/png" };
        if (!appLeadImageContentTypes.Contains(document.ContentType))
        {
            throw new UnsupportedMediaTypeException($"Document type not supported. File with contentType :{string.Join(",", appLeadImageContentTypes)} are allowed.");
        }

        var offerRepository = _portalRepositories.GetInstance<IOfferRepository>();
        var result = await offerRepository.GetOfferAssignedAppLeadImageDocumentsByIdAsync(appId, iamUserId, OfferTypeId.APP).ConfigureAwait(false);

        if(result == default)
        {
            throw new NotFoundException($"App {appId} does not exist.");
        }
        if (!result.IsStatusActive)
        {
            throw new ConflictException("offerStatus is in Incorrect State");
        }
        var companyUserId = result.CompanyUserId;
        if (companyUserId == Guid.Empty)
        {
            throw new ForbiddenException($"user {iamUserId} is not a member of the providercompany of Apps {appId}");
        }

        var documentRepository = _portalRepositories.GetInstance<IDocumentRepository>();
        if(result.documentStatusDatas.Any())
        {
            foreach(var documentData in result.documentStatusDatas)
            {
                documentRepository.AttachAndModifyDocument(documentData.DocumentId,
                    a => { a.DocumentStatusId = documentData.StatusId; },
                    a => { a.DocumentStatusId = DocumentStatusId.INACTIVE; }
                );
            }
            await _portalRepositories.SaveAsync().ConfigureAwait(false);
            _portalRepositories.Clear();
        }
        var documentName = document.FileName;
        using var sha512Hash = SHA512.Create();
        using var ms = new MemoryStream((int)document.Length);

        await document.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var hash = await sha512Hash.ComputeHashAsync(ms);
        var documentContent = ms.GetBuffer();
        if (ms.Length != document.Length || documentContent.Length != document.Length)
        {
            throw new ControllerArgumentException($"document {document.FileName} transmitted length {document.Length} doesn't match actual length {ms.Length}.");
        }
        var doc = documentRepository.CreateDocument(documentName, documentContent, hash, DocumentTypeId.APP_LEADIMAGE, x =>
        {
            x.CompanyUserId = companyUserId;
            x.DocumentStatusId = DocumentStatusId.LOCKED;
        });
        _portalRepositories.GetInstance<IOfferRepository>().CreateOfferAssignedDocument(appId, doc.Id);

        foreach(var docId in result.documentStatusDatas.Select(x => x.DocumentId))
        {
            offerRepository.RemoveOfferAssignedDocument(appId, docId);
            documentRepository.RemoveDocument(docId);
        }
        await _portalRepositories.SaveAsync().ConfigureAwait(false);
    }
}
