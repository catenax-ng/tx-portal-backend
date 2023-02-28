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

using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Apps.Service.ViewModels;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Models;
using Org.Eclipse.TractusX.Portal.Backend.Notifications.Library;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Offers.Library.Service;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared;
using Xunit;

namespace Org.Eclipse.TractusX.Portal.Backend.Apps.Service.BusinessLogic.Tests;

public class AppChangeBusinessLogicTest
{
    private const string ClientId = "catenax-portal";
    private readonly Guid _companyUserId = Guid.NewGuid();
    private readonly string _iamUserId = Guid.NewGuid().ToString();

    private readonly IFixture _fixture;
    private readonly IProvisioningManager _provisioningManager;
    private readonly IPortalRepositories _portalRepositories;
    private readonly IOfferRepository _offerRepository;
    private readonly IUserRolesRepository _userRolesRepository;
    private readonly INotificationService _notificationService;
    
    private readonly AppChangeBusinessLogic _sut;

    public AppChangeBusinessLogicTest()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _provisioningManager = A.Fake<IProvisioningManager>();
        _portalRepositories = A.Fake<IPortalRepositories>();
        _offerRepository = A.Fake<IOfferRepository>();
        _userRolesRepository = A.Fake<IUserRolesRepository>();
        _notificationService = A.Fake<INotificationService>();
        
        var settings = new AppsSettings
        {
            ActiveAppNotificationTypeIds = new []
            {
                NotificationTypeId.APP_ROLE_ADDED
            },
            ActiveAppCompanyAdminRoles = new Dictionary<string, IEnumerable<string>>
            {
                { ClientId, new [] { "Company Admin" } }
            }
        };
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>()).Returns(_offerRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IUserRolesRepository>()).Returns(_userRolesRepository);
        _sut = new AppChangeBusinessLogic(_portalRepositories, _notificationService, _provisioningManager, Options.Create(settings));
    }

    #region  AddActiveAppUserRole

    [Fact]
    public async Task AddActiveAppUserRoleAsync_ExecutesSuccessfully()
    {
        //Arrange
        const string roleName = "Legal Admin";
        var appId = _fixture.Create<Guid>();
        var companyId = _fixture.Create<Guid>();
        var appName = _fixture.Create<string>();

        var appUserRoleDescription = new AppUserRoleDescription[] {
            new("de","this is test1"),
            new("en","this is test2"),
        };
        var appAssignedRoleDesc = new AppUserRole[] { new(roleName, appUserRoleDescription) };
        var clientIds = new[] {"client"};
        A.CallTo(() => _portalRepositories.GetInstance<IOfferRepository>().GetInsertActiveAppUserRoleDataAsync(appId, _iamUserId, OfferTypeId.APP))
            .ReturnsLazily(() => (true, appName, _companyUserId, companyId, clientIds));

        //Act
        var result = await _sut.AddActiveAppUserRoleAsync(appId, appAssignedRoleDesc, _iamUserId).ConfigureAwait(false);

        //Assert
        A.CallTo(() => _offerRepository.GetInsertActiveAppUserRoleDataAsync(appId, _iamUserId, OfferTypeId.APP)).MustHaveHappened();
        foreach(var item in appAssignedRoleDesc)
        {
            A.CallTo(() => _userRolesRepository.CreateAppUserRole(A<Guid>._, A<string>.That.IsEqualTo(item.role))).MustHaveHappened();
            foreach (var indexItem in item.descriptions)
            {
                A.CallTo(() => _userRolesRepository.CreateAppUserRoleDescription(A<Guid>._, A<string>.That.IsEqualTo(indexItem.languageCode), A<string>.That.IsEqualTo(indexItem.description))).MustHaveHappened();
            }
        }
        A.CallTo(() => _notificationService.CreateNotifications(A<IDictionary<string, IEnumerable<string>>>._, A<Guid>._, A<IEnumerable<(string? content, NotificationTypeId notificationTypeId)>>._, A<Guid>._)).MustHaveHappened();
        A.CallTo(() => _provisioningManager.AddRolesToClientAsync("client", A<IEnumerable<string>>.That.Matches(ur => ur.Count() == 1 && ur.Contains(roleName))))
            .MustHaveHappenedOnceExactly();
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IEnumerable<AppRoleData>>(result);
    }

    [Fact]
    public async Task AddActiveAppUserRoleAsync_WithCompanyUserIdNotSet_ThrowsForbiddenException()
    {
        //Arrange
        var appId = _fixture.Create<Guid>();
        var appName = _fixture.Create<string>();

        var appUserRoleDescription = new AppUserRoleDescription[] {
            new("de","this is test1"),
            new("en","this is test2"),
        };
        var appAssignedRoleDesc = new AppUserRole[] { new("Legal Admin", appUserRoleDescription) };
        var clientIds = new[] {"client"};
        A.CallTo(() => _offerRepository.GetInsertActiveAppUserRoleDataAsync(appId, _iamUserId, OfferTypeId.APP))
            .ReturnsLazily(() => (true, appName, Guid.Empty, null, clientIds));

        //Act
        async Task Act() => await _sut.AddActiveAppUserRoleAsync(appId, appAssignedRoleDesc, _iamUserId).ConfigureAwait(false);

        //Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(Act);
        ex.Message.Should().Be($"user {_iamUserId} is not a member of the provider company of app {appId}");
    }

    [Fact]
    public async Task AddActiveAppUserRoleAsync_WithProviderCompanyNotSet_ThrowsConflictException()
    {
        //Arrange
        const string appName = "app name";
        var appId = _fixture.Create<Guid>();

        var appUserRoleDescription = new AppUserRoleDescription[] {
            new("de","this is test1"),
            new("en","this is test2"),
        };
        var appAssignedRoleDesc = new AppUserRole[] { new("Legal Admin", appUserRoleDescription) };
        var clientIds = new[] {"client"};
        A.CallTo(() => _offerRepository.GetInsertActiveAppUserRoleDataAsync(appId, _iamUserId, OfferTypeId.APP))
            .ReturnsLazily(() => (true, appName, _companyUserId, null, clientIds));

        //Act
        async Task Act() => await _sut.AddActiveAppUserRoleAsync(appId, appAssignedRoleDesc, _iamUserId).ConfigureAwait(false);

        //Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"App {appId} providing company is not yet set.");
    }

    #endregion
}
