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

using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Notifications.Library;

/// <summary>
/// Provides methods to create notifications
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IPortalRepositories _portalRepositories;

    /// <summary>
    /// Creates a new instance of <see cref="NotificationService"/>
    /// </summary>
    /// <param name="portalRepositories">Access to the application's repositories</param>
    public NotificationService(IPortalRepositories portalRepositories)
    {
        _portalRepositories = portalRepositories;
    }
    
    /// <inheritdoc />
    public async Task CreateNotifications(
        IDictionary<string, IEnumerable<string>> receiverUserRoles,
        Guid? creatorId,
        IEnumerable<(string? content, NotificationTypeId notificationTypeId)> notifications,
        Guid companyId)
    {
        var roleData = await ValidateRoleData(receiverUserRoles);
        var notificationRepository = _portalRepositories.GetInstance<INotificationRepository>();
        await foreach (var receiver in _portalRepositories.GetInstance<IUserRepository>().GetCompanyUserWithRoleIdForCompany(roleData, companyId))
        {
            CreateNotification(receiver, creatorId, notifications, notificationRepository);
        }
    }

    /// <inheritdoc />
    public async Task CreateNotifications(
        IDictionary<string, IEnumerable<string>> receiverUserRoles,
        Guid? creatorId,
        IEnumerable<(string? content, NotificationTypeId notificationTypeId)> notifications)
    {
        var roleData = await ValidateRoleData(receiverUserRoles);
        var notificationRepository = _portalRepositories.GetInstance<INotificationRepository>();
        await foreach (var receiver in _portalRepositories.GetInstance<IUserRepository>().GetCompanyUserWithRoleId(roleData))
        {
            CreateNotification(receiver, creatorId, notifications, notificationRepository);
        }
    }

    /// <inheritdoc />
    public async Task SetNotificationsForOfferToDone(IDictionary<string, IEnumerable<string>> roles, IEnumerable<NotificationTypeId> notificationTypeIds, Guid offerId)
    {
        var roleData = await ValidateRoleData(roles);
        var notificationRepository = _portalRepositories.GetInstance<INotificationRepository>();
        foreach (var notificationId in await notificationRepository.GetUpdateData(roleData, notificationTypeIds, offerId).ToListAsync())
        {
            notificationRepository.AttachAndModifyNotification(notificationId, not =>
            {
                not.Done = true;
            });
        }
    }

    private async Task<IEnumerable<Guid>> ValidateRoleData(IDictionary<string, IEnumerable<string>> receiverUserRoles)
    {
        var userRolesRepository = _portalRepositories.GetInstance<IUserRolesRepository>();
        var roleData = await userRolesRepository
            .GetUserRoleIdsUntrackedAsync(receiverUserRoles)
            .ToListAsync()
            .ConfigureAwait(false);
        if (roleData.Count < receiverUserRoles.Sum(clientRoles => clientRoles.Value.Count()))
        {
            throw new ConfigurationException($"invalid configuration, at least one of the configured roles does not exist in the database: {string.Join(", ", receiverUserRoles.Select(clientRoles => $"client: {clientRoles.Key}, roles: [{string.Join(", ", clientRoles.Value)}]"))}");
        }
        return roleData;
    }

    private static void CreateNotification(Guid receiver, Guid? creatorId, IEnumerable<(string? content, NotificationTypeId notificationTypeId)> notifications, INotificationRepository notificationRepository)
    {
        var notificationList = notifications.ToList();
        foreach (var notificationData in notificationList)
        {
            notificationRepository.CreateNotification(
                receiver,
                notificationData.notificationTypeId,
                false,
                notification =>
                {
                    notification.CreatorUserId = creatorId;
                    notification.Content = notificationData.content;
                });
        }
    }
}
