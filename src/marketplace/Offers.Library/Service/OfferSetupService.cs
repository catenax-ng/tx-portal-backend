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

using Microsoft.AspNetCore.Mvc.Routing;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.HttpClientExtensions;
using Org.Eclipse.TractusX.Portal.Backend.Framework.IO;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.Mailing.SendMail;
using Org.Eclipse.TractusX.Portal.Backend.Notifications.Library;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Service;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Flurl.Util;

namespace Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Service;
   
public class OfferSetupService : IOfferSetupService
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IProvisioningManager _provisioningManager;
    private readonly IServiceAccountCreation _serviceAccountCreation;
    private readonly INotificationService _notificationService;
    private readonly IMailingService _mailingService;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="portalRepositories">Factory to access the repositories</param>
    /// <param name="provisioningManager">Access to the provisioning manager</param>
    /// <param name="serviceAccountCreation">Access to the service account creation</param>
    /// <param name="notificationService">Creates notifications for the user</param>
    /// <param name="mailingService">Mailing service to send mails to the user</param>
    /// <param name="httpClientFactory">Creates the http client</param>
    public OfferSetupService(IPortalRepositories portalRepositories,
        IProvisioningManager provisioningManager,
        IServiceAccountCreation serviceAccountCreation,
        INotificationService notificationService,
        IMailingService mailingService,
        IHttpClientFactory httpClientFactory)
    {
        _portalRepositories = portalRepositories;
        _provisioningManager = provisioningManager;
        _serviceAccountCreation = serviceAccountCreation;
        _notificationService = notificationService;
        _mailingService = mailingService;
        _httpClientFactory = httpClientFactory;
    }
    
    /// <inheritdoc />
    public async Task AutoSetupOfferSubscription(OfferThirdPartyAutoSetupData autoSetupData, string accessToken, string autoSetupUrl)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(OfferSetupService));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await httpClient.PostAsJsonAsync(autoSetupUrl, autoSetupData)
            .CatchingIntoServiceExceptionFor("autosetup-offer-subscription")
            .ConfigureAwait(false);
    }
    
    public async Task<OfferAutoSetupResponseData> AutoSetupOfferAsync(OfferAutoSetupData data, IDictionary<string,IEnumerable<string>> serviceAccountRoles, IDictionary<string,IEnumerable<string>> itAdminRoles, string iamUserId, OfferTypeId offerTypeId, string basePortalAddress)
    {
        var offerSubscriptionsRepository = _portalRepositories.GetInstance<IOfferSubscriptionsRepository>();
        var offerDetails = await GetAndValidateOfferDetails(data.RequestId, iamUserId, offerTypeId, offerSubscriptionsRepository).ConfigureAwait(false);

        offerSubscriptionsRepository.AttachAndModifyOfferSubscription(data.RequestId, subscription =>
        {
            subscription.OfferSubscriptionStatusId = OfferSubscriptionStatusId.ACTIVE;
            subscription.LastEditorId = offerDetails.CompanyUserId;
        });

        if (offerDetails.InstanceData.IsSingleInstance)
        {
            _portalRepositories.GetInstance<IAppSubscriptionDetailRepository>()
                .CreateAppSubscriptionDetail(data.RequestId, appSubscriptionDetail =>
                {
                    appSubscriptionDetail.AppInstanceId = offerDetails.AppInstanceIds.Single();
                    appSubscriptionDetail.AppSubscriptionUrl = offerDetails.InstanceData.InstanceUrl;
                });
            await CreateNotifications(itAdminRoles, offerTypeId, offerDetails);
            await _portalRepositories.SaveAsync().ConfigureAwait(false);
            return new OfferAutoSetupResponseData(null, null);
        }

        var userRolesRepository = _portalRepositories.GetInstance<IUserRolesRepository>();
        ClientInfoData? clientInfoData = null;
        if (offerTypeId == OfferTypeId.APP)
        {
            var (clientId, iamClientId) = await CreateClient(data.OfferUrl, offerDetails.OfferId, true, userRolesRepository);
            clientInfoData = new ClientInfoData(clientId);
            CreateAppInstance(data, offerDetails, iamClientId);
        }

        TechnicalUserInfoData? technicalUserInfoData = null;
        if (offerDetails.IsTechnicalUserNeeded)
        {
            var technicalUserClientId = clientInfoData?.ClientId ?? $"{offerDetails.OfferName}-{offerDetails.CompanyName}";
            var createTechnicalUserData = new CreateTechnicalUserData(offerDetails.CompanyId, offerDetails.OfferName, offerDetails.Bpn);
            var (technicalClientId, serviceAccountData, serviceAccountId) = await CreateTechnicalUser(serviceAccountRoles, userRolesRepository, createTechnicalUserData, technicalUserClientId, offerTypeId == OfferTypeId.APP, data.RequestId)
                .ConfigureAwait(false);
            technicalUserInfoData = new TechnicalUserInfoData(serviceAccountId, serviceAccountData.AuthData.Secret, technicalClientId);
        }

        await CreateNotifications(itAdminRoles, offerTypeId, offerDetails);
        await _portalRepositories.SaveAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(offerDetails.RequesterEmail))
        {
            return new OfferAutoSetupResponseData(technicalUserInfoData, clientInfoData);
        }

        await SendMail(basePortalAddress, $"{offerDetails.RequesterFirstname} {offerDetails.RequesterLastname}", offerDetails.RequesterEmail, offerDetails.OfferName).ConfigureAwait(false);
        return new OfferAutoSetupResponseData(
            technicalUserInfoData,
            clientInfoData);
    }

    /// <inheritdoc />
    public async Task SetupSingleInstance(Guid offerId, string instanceUrl)
    {
        var userRolesRepository = _portalRepositories.GetInstance<IUserRolesRepository>();
        var (_, iamClientId) = await CreateClient(instanceUrl, offerId, false, userRolesRepository);
        _portalRepositories.GetInstance<IAppInstanceRepository>().CreateAppInstance(offerId, iamClientId);
    }

    /// <inheritdoc />
    public async Task DeleteSingleInstance(Guid appInstanceId, Guid clientId, string clientClientId)
    {
        await _provisioningManager.DeleteCentralClientAsync(clientClientId)
            .ConfigureAwait(false);
        _portalRepositories.GetInstance<IClientRepository>().RemoveClient(clientId);
        _portalRepositories.GetInstance<IAppInstanceRepository>().RemoveAppInstance(appInstanceId);
    }

    public async Task<string> ActivateSingleInstanceAppAsync(Guid offerId, IDictionary<string, IEnumerable<string>> serviceAccountRoles)
    {
        var data = await _portalRepositories.GetInstance<IOfferRepository>().GetSingleInstanceOfferData(offerId, OfferTypeId.APP).ConfigureAwait(false);
        if (data == null)
        {
            throw new ConflictException($"App {offerId} does not exist.");
        }

        if (data.InternalClientIds.Count() != 1 || string.IsNullOrWhiteSpace(data.InternalClientIds.Single()))
        {
            throw new ConflictException($"There must exactly be one clientId set for offer {offerId}");
        }

        if (data.InstanceIds.Count() != 1 || data.InstanceIds.Single() == Guid.Empty)
        {
            throw new ConflictException($"There must exactly be one instance set for offer {offerId}");
        }

        var internalClientId = data.InternalClientIds.Single();
        await _provisioningManager.EnableClient(internalClientId).ConfigureAwait(false);

        var technicalUserData = new CreateTechnicalUserData(data.CompanyId, data.OfferName, data.Bpn);
        var (technicalClientId, _, serviceAccountId) = await CreateTechnicalUser(serviceAccountRoles, _portalRepositories.GetInstance<IUserRolesRepository>(), technicalUserData, internalClientId, true, null)
            .ConfigureAwait(false);

        _portalRepositories.GetInstance<IOfferRepository>().AttachAndModifyAppInstance(data.InstanceIds.Single(), offerId, a =>
        {
            a.ServiceAccountId = serviceAccountId;
        });
        
        return technicalClientId;
    }

    /// <inheritdoc />
    public Task UpdateSingleInstance(string clientClientId, string instanceUrl) =>
        _provisioningManager.UpdateClient(clientClientId, instanceUrl, instanceUrl.AppendToPathEncoded("*"));

    private static async Task<OfferSubscriptionTransferData> GetAndValidateOfferDetails(Guid requestId, string iamUserId, OfferTypeId offerTypeId, IOfferSubscriptionsRepository offerSubscriptionsRepository)
    {
        var offerDetails = await offerSubscriptionsRepository
            .GetOfferDetailsAndCheckUser(requestId, iamUserId, offerTypeId)
            .ConfigureAwait(false);
        if (offerDetails == null)
        {
            throw new NotFoundException($"OfferSubscription {requestId} does not exist");
        }

        if (offerDetails.Status is not OfferSubscriptionStatusId.PENDING)
        {
            throw new ConflictException("Status of the offer subscription must be pending");
        }

        if (offerDetails.CompanyUserId == Guid.Empty && offerDetails.TechnicalUserId == Guid.Empty)
        {
            throw new ForbiddenException("Only the providing company can setup the service");
        }

        if (offerDetails.InstanceData.IsSingleInstance && offerDetails.AppInstanceIds.Count() != 1)
        {
            throw new ConflictException("There must only be one app instance for single instance apps");
        }

        return offerDetails;
    }

    private async Task<(string clientId, Guid iamClientId)> CreateClient(string offerUrl, Guid offerId, bool enabled, IUserRolesRepository userRolesRepository)
    {
        var userRoles = await userRolesRepository.GetUserRolesForOfferIdAsync(offerId).ConfigureAwait(false);
        offerUrl.EnsureValidHttpUrl(() => nameof(offerUrl));
        var redirectUrl = offerUrl.AppendToPathEncoded("*");

        var clientId = await _provisioningManager.SetupClientAsync(redirectUrl, offerUrl, userRoles, enabled)
            .ConfigureAwait(false);
        var iamClient = _portalRepositories.GetInstance<IClientRepository>().CreateClient(clientId);
        return (clientId, iamClient.Id);
    }

    private void CreateAppInstance(OfferAutoSetupData data, OfferSubscriptionTransferData offerDetails, Guid iamClientId)
    {
        var appInstance = _portalRepositories.GetInstance<IAppInstanceRepository>()
            .CreateAppInstance(offerDetails.OfferId, iamClientId);
        _portalRepositories.GetInstance<IAppSubscriptionDetailRepository>()
            .CreateAppSubscriptionDetail(data.RequestId, appSubscriptionDetail =>
            {
                appSubscriptionDetail.AppInstanceId = appInstance.Id;
                appSubscriptionDetail.AppSubscriptionUrl = data.OfferUrl;
            });
    }

    private async Task<(string technicalClientId, ServiceAccountData serviceAccountData, Guid serviceAccountId)> CreateTechnicalUser(
        IDictionary<string, IEnumerable<string>> serviceAccountRoles,
        IUserRolesRepository userRolesRepository,
        CreateTechnicalUserData data,
        string technicalUserName,
        bool enhanceTechnicalUserName,
        Guid? offerSubscriptionId)
    {
        var serviceAccountUserRoles = await userRolesRepository
            .GetUserRoleDataUntrackedAsync(serviceAccountRoles)
            .ToListAsync()
            .ConfigureAwait(false);

        var description = $"Technical User for app {data.OfferName} - {string.Join(",", serviceAccountUserRoles.Select(x => x.UserRoleText))}";
        var serviceAccountCreationData = new ServiceAccountCreationInfo(
            technicalUserName,
            description,
            IamClientAuthMethod.SECRET,
            serviceAccountUserRoles.Select(x => x.UserRoleId));
        var (technicalClientId, serviceAccountData, serviceAccountId, _) = await _serviceAccountCreation
            .CreateServiceAccountAsync(
                serviceAccountCreationData,
                data.CompanyId,
                data.Bpn == null ? Enumerable.Empty<string>() : Enumerable.Repeat(data.Bpn, 1),
                CompanyServiceAccountTypeId.MANAGED,
                enhanceTechnicalUserName,
                sa => { sa.OfferSubscriptionId = offerSubscriptionId; })
            .ConfigureAwait(false);
        return (technicalClientId, serviceAccountData, serviceAccountId);
    }

    private async Task CreateNotifications(
        IDictionary<string, IEnumerable<string>> itAdminRoles,
        OfferTypeId offerTypeId,
        OfferSubscriptionTransferData offerDetails)
    {
        var appSubscriptionActivation = offerTypeId == OfferTypeId.APP
            ? NotificationTypeId.APP_SUBSCRIPTION_ACTIVATION
            : NotificationTypeId.SERVICE_ACTIVATION;
        var notificationContent = JsonSerializer.Serialize(new
        {
            offerDetails.OfferId,
            offerDetails.CompanyName,
            offerDetails.OfferName
        });
        var notifications = new List<(string?, NotificationTypeId)>
        {
            (notificationContent, appSubscriptionActivation)
        };

        if (!offerDetails.InstanceData.IsSingleInstance)
        {
            notifications.Add((null, NotificationTypeId.TECHNICAL_USER_CREATION));
        }

        await _notificationService.CreateNotifications(
            itAdminRoles,
            offerDetails.CompanyUserId != Guid.Empty ? offerDetails.CompanyUserId : null,
            notifications,
            offerDetails.CompanyId).ConfigureAwait(false);

        _portalRepositories.GetInstance<INotificationRepository>().CreateNotification(offerDetails.RequesterId, appSubscriptionActivation, false, notification =>
        {
            notification.Content = notificationContent;
        });
    }

    private async Task SendMail(string basePortalAddress, string userName, string requesterEmail, string? offerName)
    {
        var mailParams = new Dictionary<string, string>
        {
            {"offerCustomerName", !string.IsNullOrWhiteSpace(userName) ? userName : "User"},
            {"offerName", offerName ?? "unnamed Offer"},
            {"url", basePortalAddress},
        };
        await _mailingService
            .SendMails(requesterEmail, mailParams, new List<string> {"subscription-activation"})
            .ConfigureAwait(false);
    }

    internal record CreateTechnicalUserData(Guid CompanyId, string? OfferName, string? Bpn);
}
