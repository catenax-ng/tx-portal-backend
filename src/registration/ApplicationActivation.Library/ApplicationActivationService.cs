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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.ApplicationActivation.Library.DependencyInjection;
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Framework.DateTimeProvider;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Mailing.SendMail;
using Org.Eclipse.TractusX.Portal.Backend.Notifications.Library;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.ApplicationActivation.Library;

public class ApplicationActivationService : IApplicationActivationService
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly INotificationService _notificationService;
    private readonly IProvisioningManager _provisioningManager;
    private readonly IMailingService _mailingService;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<ApplicationActivationService> _logger;
    private readonly ApplicationActivationSettings _settings;

    public ApplicationActivationService(
        IPortalRepositories portalRepositories,
        INotificationService notificationService,
        IProvisioningManager provisioningManager,
        IMailingService mailingService,
        IDateTimeProvider dateTime,
        IOptions<ApplicationActivationSettings> options,
        ILogger<ApplicationActivationService> logger)
    {
        _portalRepositories = portalRepositories;
        _notificationService = notificationService;
        _provisioningManager = provisioningManager;
        _mailingService = mailingService;
        _dateTime = dateTime;
        _logger = logger;
        _settings = options.Value;
    }

    public Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStepTypeId>?,bool)> HandleApplicationActivation(IChecklistService.WorkerChecklistProcessStepData context, CancellationToken cancellationToken)
    {
        if (!InProcessingTime())
        {
            return Task.FromResult<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStepTypeId>?,bool)>((null,null,false));
        }
        var prerequisiteEntries = context.Checklist.Where(entry => entry.Key != ApplicationChecklistEntryTypeId.APPLICATION_ACTIVATION);
        if (prerequisiteEntries.Any(entry => entry.Value != ApplicationChecklistEntryStatusId.DONE))
        {
            throw new ConflictException($"cannot activate application {context.ApplicationId}. Checklist entries that are not in status DONE: {string.Join(",",prerequisiteEntries)}");
        }
        return HandleApplicationActivationInternal(context);
    }

    private async Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStepTypeId>?,bool)> HandleApplicationActivationInternal(IChecklistService.WorkerChecklistProcessStepData context)
    {
        _logger.LogDebug("Handle ApplicationActivation for {ApplicationId}", context.ApplicationId);
        var applicationRepository = _portalRepositories.GetInstance<IApplicationRepository>();
        var result = await applicationRepository.GetCompanyAndApplicationDetailsForApprovalAsync(context.ApplicationId).ConfigureAwait(false);
        if (result == default)
        {
            throw new ConflictException($"CompanyApplication {context.ApplicationId} is not in status SUBMITTED");
        }
        var (companyId, businessPartnerNumber) = result;

        if (string.IsNullOrWhiteSpace(businessPartnerNumber))
        {
            throw new ConflictException($"BusinessPartnerNumber (bpn) for CompanyApplications {context.ApplicationId} company {companyId} is empty");
        }

        var userRolesRepository = _portalRepositories.GetInstance<IUserRolesRepository>();
        var assignedRoles = await AssignRolesAndBpn(context.ApplicationId, userRolesRepository, applicationRepository, businessPartnerNumber).ConfigureAwait(false);
        await RemoveRegistrationRoles(context.ApplicationId, userRolesRepository).ConfigureAwait(false);

        _logger.LogDebug("Update Application {ApplicationId}", context.ApplicationId);
        applicationRepository.AttachAndModifyCompanyApplication(context.ApplicationId, ca =>
        {
            ca.ApplicationStatusId = CompanyApplicationStatusId.CONFIRMED;
            ca.DateLastChanged = DateTimeOffset.UtcNow;    
        });
        _logger.LogDebug("Updated Application {ApplicationId}", context.ApplicationId);

        _logger.LogDebug("Update Company {CompanyId}", companyId);
        _portalRepositories.GetInstance<ICompanyRepository>().AttachAndModifyCompany(companyId, null, c =>
        {
            c.CompanyStatusId = CompanyStatusId.ACTIVE;
        });
        _logger.LogDebug("Updated Company {CompanyId}", companyId);

        _logger.LogDebug("Create notifications");
        var notifications = _settings.WelcomeNotificationTypeIds.Select(x => (default(string), x));
        await _notificationService.CreateNotifications(_settings.CompanyAdminRoles, null, notifications, companyId).ConfigureAwait(false);
        _logger.LogDebug("Created notifications");

        _logger.LogDebug("Send mail");
        await PostRegistrationWelcomeEmailAsync(userRolesRepository, applicationRepository, context.ApplicationId).ConfigureAwait(false);
        _logger.LogDebug("Mail send");

        if (assignedRoles != null)
        {
            var unassignedClientRoles = _settings.ApplicationApprovalInitialRoles
                .Select(initialClientRoles => (
                    client: initialClientRoles.Key,
                    roles: initialClientRoles.Value.Except(assignedRoles[initialClientRoles.Key])))
                .Where(clientRoles => clientRoles.roles.Any())
                .ToList();

            if (unassignedClientRoles.Any())
            {
                throw new UnexpectedConditionException($"inconsistent data, roles not assigned in keycloak: {string.Join(", ", unassignedClientRoles.Select(clientRoles => $"client: {clientRoles.client}, roles: [{string.Join(", ", clientRoles.roles)}]"))}");
            }
        }
        return (entry => entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE, null, true);
    }

    private bool InProcessingTime()
    {
        var startTime = _settings.StartTime;
        var endTime = _settings.EndTime;
        if (!startTime.HasValue || !endTime.HasValue)
        {
            return true;
        }

        var now = _dateTime.Now.TimeOfDay;
        return startTime > endTime ? 
            now >= startTime || now <= endTime :
            now >= startTime && now <= endTime;
    }

    private async Task<IDictionary<string, IEnumerable<string>>?> AssignRolesAndBpn(Guid applicationId, IUserRolesRepository userRolesRepository, IApplicationRepository applicationRepository, string businessPartnerNumber)
    {
        _logger.LogDebug("Assigne roles and bpn for {ApplicationId}", applicationId);
        var userBusinessPartnersRepository = _portalRepositories.GetInstance<IUserBusinessPartnerRepository>();

        var applicationApprovalInitialRoles = _settings.ApplicationApprovalInitialRoles;
        var initialRolesData = await GetRoleData(userRolesRepository, applicationApprovalInitialRoles).ConfigureAwait(false);

        _logger.LogDebug("initial roles {Roles}", string.Join(",", initialRolesData.Select(x => $"urt: {x.UserRoleText}; cci: {x.ClientClientId}; uri: {x.UserRoleId}")));
        IDictionary<string, IEnumerable<string>>? assignedRoles = null;
        var invitedUsersData = await applicationRepository
            .GetInvitedUsersDataByApplicationIdUntrackedAsync(applicationId)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var userData in invitedUsersData)
        {
            _logger.LogDebug("assign roles for user {Roles}", userData.UserEntityId);
            assignedRoles = await _provisioningManager
                .AssignClientRolesToCentralUserAsync(userData.UserEntityId, applicationApprovalInitialRoles)
                .ToDictionaryAsync(assigned => assigned.Client, assigned => assigned.Roles)
                .ConfigureAwait(false);

            var userRoleDatas = initialRolesData.Where(roleData => !userData.RoleIds.Contains(roleData.UserRoleId) &&
                                                                   assignedRoles[roleData.ClientClientId].Contains(roleData.UserRoleText));
            _logger.LogDebug("user role datas {Roles}", string.Join(",", userRoleDatas.Select(x => $"urt: {x.UserRoleText}; cci: {x.ClientClientId}; uri: {x.UserRoleId}")));
            foreach (var roleData in userRoleDatas)
            {
                userRolesRepository.CreateCompanyUserAssignedRole(userData.CompanyUserId, roleData.UserRoleId);
            }

            if (userData.BusinessPartnerNumbers.Contains(businessPartnerNumber)) continue;

            _logger.LogDebug("create company user assignd bpn {CompanyUserId}", userData.CompanyUserId);
            userBusinessPartnersRepository.CreateCompanyUserAssignedBusinessPartner(userData.CompanyUserId, businessPartnerNumber);
            await _provisioningManager
                .AddBpnAttributetoUserAsync(userData.UserEntityId, Enumerable.Repeat(businessPartnerNumber, 1))
                .ConfigureAwait(false);
            _logger.LogDebug("added attribute to user {UserEntityId}", userData.UserEntityId);
        }

        return assignedRoles;
    }

    private async Task RemoveRegistrationRoles(Guid applicationId, IUserRolesRepository userRolesRepository)
    {
        _logger.LogDebug("Remove Registration Roles {ApplicationId}", applicationId);
        var iamClientIds = _settings.ClientToRemoveRolesOnActivation;
        _logger.LogDebug("GetUserRolesByClientId {ClientIds}", string.Join(",", iamClientIds));
        var clientRoleData = await userRolesRepository
            .GetUserRolesByClientId(iamClientIds)
            .ToListAsync()
            .ConfigureAwait(false);
        _logger.LogDebug("clientRoleData {ClientIds}", string.Join(",", clientRoleData.Select(x => $"{x.ClientClientId}")));
        _logger.LogDebug("GetUserWithUserRolesForApplicationId");
        var invitedUsersData = await userRolesRepository
            .GetUserWithUserRolesForApplicationId(applicationId, clientRoleData.SelectMany(data => data.UserRoles).Select(role => role.UserRoleId)).ToListAsync().ConfigureAwait(false);

        var userRoles = clientRoleData.SelectMany(data => data.UserRoles.Select(role => (role.UserRoleId, data.ClientClientId, role.UserRoleText))).ToImmutableDictionary(x => x.UserRoleId, x => (x.ClientClientId, x.UserRoleText));

        _logger.LogDebug("UserRole Keys {UseRoles}", string.Join(",", userRoles.Keys));
        foreach (var userData in invitedUsersData)
        {
            if (!userData.UserRoleIds.Any()) {
                _logger.LogDebug("Error for user IU: {IamUser} Cu: {CompanyUserId}", userData.UserEntityId, userData.CompanyUserId);
                throw new UnexpectedConditionException("userRoleIds should never be empty here");
            }

            var roleNamesToDelete = userData.UserRoleIds
                .Select(roleId => userRoles[roleId])
                .GroupBy(clientRoleData => clientRoleData.ClientClientId)
                .ToImmutableDictionary(
                    clientRoleDataGroup => clientRoleDataGroup.Key,
                    clientRoleData => clientRoleData.Select(y => y.UserRoleText));

            _logger.LogDebug("Role Names to remove {UseRoles}", string.Join(",", roleNamesToDelete.Keys));
            await _provisioningManager.DeleteClientRolesFromCentralUserAsync(userData.UserEntityId, roleNamesToDelete)
                .ConfigureAwait(false);
            _logger.LogDebug("Deleted from Keycloak");
            _logger.LogDebug("Removing from db");
            userRolesRepository.DeleteCompanyUserAssignedRoles(userData.UserRoleIds.Select(roleId => (userData.CompanyUserId, roleId)));
            _logger.LogDebug("Removed from db");
        }
    }

    private async Task PostRegistrationWelcomeEmailAsync(IUserRolesRepository userRolesRepository, IApplicationRepository applicationRepository, Guid applicationId)
    {
        var failedUserNames = new List<string>();
        var initialRolesData = await GetRoleData(userRolesRepository, _settings.CompanyAdminRoles).ConfigureAwait(false);
        await foreach (var user in applicationRepository.GetWelcomeEmailDataUntrackedAsync(applicationId, initialRolesData.Select(x => x.UserRoleId)).ConfigureAwait(false))
        {
            var userName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(item => !string.IsNullOrWhiteSpace(item)));
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                failedUserNames.Add(userName);
                continue;
            }

            var mailParameters = new Dictionary<string, string>
            {
                { "userName", !string.IsNullOrWhiteSpace(userName) ?  userName : user.Email },
                { "companyName", user.CompanyName },
                { "url", _settings.BasePortalAddress }
            };

            await _mailingService.SendMails(user.Email, mailParameters, new List<string> { "EmailRegistrationWelcomeTemplate" }).ConfigureAwait(false);
        }

        if (failedUserNames.Any())
            throw new ArgumentException($"user(s) {string.Join(",", failedUserNames)} has no assigned email");
    }

    private static async Task<List<UserRoleData>> GetRoleData(IUserRolesRepository userRolesRepository, IDictionary<string, IEnumerable<string>> roles)
    {
        var roleData = await userRolesRepository
            .GetUserRoleDataUntrackedAsync(roles)
            .ToListAsync()
            .ConfigureAwait(false);
        if (roleData.Count < roles.Sum(clientRoles => clientRoles.Value.Count()))
        {
            throw new ConfigurationException($"invalid configuration, at least one of the configured roles does not exist in the database: {string.Join(", ", roles.Select(clientRoles => $"client: {clientRoles.Key}, roles: [{string.Join(", ", clientRoles.Value)}]"))}");
        }

        return roleData;
    }
}
