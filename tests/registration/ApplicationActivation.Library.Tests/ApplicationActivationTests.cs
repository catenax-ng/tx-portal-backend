﻿/********************************************************************************
 * Copyright (c) 2021,2022 BMW Group AG
 * Copyright (c) 2021,2022 Contributors to the Eclipse Foundation
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

using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.ApplicationActivation.Library.DependencyInjection;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Mailing.SendMail;
using Org.Eclipse.TractusX.Portal.Backend.Notifications.Library;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library;

namespace Org.Eclipse.TractusX.Portal.Backend.ApplicationActivation.Library.Tests;

public class ApplicationActivationTests
{
    private const string BusinessPartnerNumber = "CAXLSHAREDIDPZZ";
    private const string ClientId = "catenax-portal";
    private const string CompanyName = "Shared Idp Test";
    private static readonly Guid Id = new("d90995fe-1241-4b8d-9f5c-f3909acc6383");
    private static readonly Guid IdWithoutBpn = new("d90995fe-1241-4b8d-9f5c-f3909acc6399");
    private static readonly Guid CompanyUserId1 = new("857b93b1-8fcb-4141-81b0-ae81950d489e");
    private static readonly Guid CompanyUserId2 = new("857b93b1-8fcb-4141-81b0-ae81950d489f");
    private static readonly Guid CompanyUserId3 = new("857b93b1-8fcb-4141-81b0-ae81950d48af");
    private static readonly Guid UserRoleId = new("607818be-4978-41f4-bf63-fa8d2de51154");
    private static readonly Guid CompanyUserRoleId = new("607818be-4978-41f4-bf63-fa8d2de51154");
    private static readonly Guid CentralUserId1 = new("6bc51706-9a30-4eb9-9e60-77fdd6d9cd6f");
    private static readonly Guid CentralUserId2 = new("6bc51706-9a30-4eb9-9e60-77fdd6d9cd70");
    private static readonly Guid CentralUserId3 = new("6bc51706-9a30-4eb9-9e60-77fdd6d9cd71");

    private readonly IFixture _fixture;
    private readonly IPortalRepositories _portalRepositories;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IUserBusinessPartnerRepository _businessPartnerRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUserRolesRepository _rolesRepository;
    private readonly List<Notification> _notifications = new();
    private readonly INotificationService _notificationService;
    private readonly IMailingService _mailingService;
    private readonly IProvisioningManager _provisioningManager;
    private readonly ApplicationActivationSettings _settings;
    private readonly ApplicationActivationService _sut;

    public ApplicationActivationTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());  

        _provisioningManager = A.Fake<IProvisioningManager>();
        _portalRepositories = A.Fake<IPortalRepositories>();
        _applicationRepository = A.Fake<IApplicationRepository>();
        _businessPartnerRepository = A.Fake<IUserBusinessPartnerRepository>();
        _companyRepository = A.Fake<ICompanyRepository>();
        _rolesRepository = A.Fake<IUserRolesRepository>();
        _settings = A.Fake<ApplicationActivationSettings>();

        var options = A.Fake<IOptions<ApplicationActivationSettings>>();
        _mailingService = A.Fake<IMailingService>();
        _notificationService = A.Fake<INotificationService>();

        _settings.WelcomeNotificationTypeIds = new List<NotificationTypeId>
        {
            NotificationTypeId.WELCOME,
            NotificationTypeId.WELCOME_USE_CASES,
            NotificationTypeId.WELCOME_APP_MARKETPLACE,
            NotificationTypeId.WELCOME_SERVICE_PROVIDER,
            NotificationTypeId.WELCOME_CONNECTOR_REGISTRATION
        };

        A.CallTo(() => _portalRepositories.GetInstance<IApplicationRepository>()).Returns(_applicationRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IUserBusinessPartnerRepository>()).Returns(_businessPartnerRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IUserRolesRepository>()).Returns(_rolesRepository);
        A.CallTo(() => _portalRepositories.GetInstance<ICompanyRepository>()).Returns(_companyRepository);
        A.CallTo(() => options.Value).Returns(_settings);

        _sut = new ApplicationActivationService(_portalRepositories, _notificationService, _provisioningManager, _mailingService, options);
    }
    
    #region HandleApplicationActivation

    [Fact]
    public async Task HandleApplicationActivation_OutsideConfiguredTime_DoesntActivateApplication()
    {
        //Arrange
        var companyUserAssignedRole = _fixture.Create<CompanyUserAssignedRole>();
        var companyUserAssignedBusinessPartner = _fixture.Create<CompanyUserAssignedBusinessPartner>();
        var now = DateTime.UtcNow.TimeOfDay;
        _settings.StartTime = now.Add(TimeSpan.FromHours(4));
        _settings.EndTime = now.Subtract(TimeSpan.FromHours(4));
        SetupFakes(new Dictionary<string, IEnumerable<string>>(), new List<UserRoleData>(), companyUserAssignedRole, companyUserAssignedBusinessPartner);
        
        //Act
        await _sut.HandleApplicationActivation(Id).ConfigureAwait(false);

        //Assert
        A.CallTo(() => _applicationRepository.GetCompanyAndApplicationDetailsForApprovalAsync(Id)).MustNotHaveHappened();
        A.CallTo(() => _applicationRepository.GetInvitedUsersDataByApplicationIdUntrackedAsync(Id)).MustNotHaveHappened();
        A.CallTo(() => _rolesRepository.CreateCompanyUserAssignedRole(CompanyUserId1, UserRoleId)).MustNotHaveHappened();
        A.CallTo(() => _businessPartnerRepository.CreateCompanyUserAssignedBusinessPartner(CompanyUserId1, BusinessPartnerNumber)).MustNotHaveHappened();
        A.CallTo(() => _rolesRepository.CreateCompanyUserAssignedRole(CompanyUserId2, UserRoleId)).MustNotHaveHappened();
        A.CallTo(() => _businessPartnerRepository.CreateCompanyUserAssignedBusinessPartner(CompanyUserId2, BusinessPartnerNumber)).MustNotHaveHappened();
        A.CallTo(() => _rolesRepository.CreateCompanyUserAssignedRole(CompanyUserId3, UserRoleId)).MustNotHaveHappened();
        A.CallTo(() => _businessPartnerRepository.CreateCompanyUserAssignedBusinessPartner(CompanyUserId3, BusinessPartnerNumber)).MustNotHaveHappened();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustNotHaveHappened();
        _notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleApplicationActivation_WithCompanyAdminUser_ApprovesRequestAndCreatesNotifications()
    {
        //Arrange
        var roles = new List<string> { "Company Admin" };
        var clientRoleNames = new Dictionary<string, IEnumerable<string>>
        {
            { ClientId, roles.AsEnumerable() }
        };
        var userRoleData = new List<UserRoleData> { new(UserRoleId, ClientId, "Company Admin") };

        var companyUserAssignedRole = _fixture.Create<CompanyUserAssignedRole>();
        var companyUserAssignedBusinessPartner = _fixture.Create<CompanyUserAssignedBusinessPartner>();
        var companyApplication = _fixture.Build<CompanyApplication>()
            .With(x => x.ApplicationStatusId, CompanyApplicationStatusId.SUBMITTED)
            .Create();
        var company = _fixture.Build<Company>()
            .With(x => x.CompanyStatusId, CompanyStatusId.PENDING)
            .Create();
        SetupFakes(clientRoleNames, userRoleData, companyUserAssignedRole, companyUserAssignedBusinessPartner);
        A.CallTo(() =>
                _applicationRepository.AttachAndModifyCompanyApplication(A<Guid>._, A<Action<CompanyApplication>>._))
            .Invokes((Guid id, Action<CompanyApplication> setOptionalParameters) =>
            {
                setOptionalParameters.Invoke(companyApplication);
            });
        A.CallTo(() => _companyRepository.AttachAndModifyCompany(A<Guid>._, null, A<Action<Company>>._))
            .Invokes((Guid _, Action<Company>? _, Action<Company> setOptionalParameters) =>
            {
                setOptionalParameters.Invoke(company);
            });

        //Act
        await _sut.HandleApplicationActivation(Id).ConfigureAwait(false);

        //Assert
        A.CallTo(() => _applicationRepository.GetCompanyAndApplicationDetailsForApprovalAsync(Id)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _applicationRepository.GetInvitedUsersDataByApplicationIdUntrackedAsync(Id)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _rolesRepository.CreateCompanyUserAssignedRole(CompanyUserId1, UserRoleId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _businessPartnerRepository.CreateCompanyUserAssignedBusinessPartner(CompanyUserId1, BusinessPartnerNumber)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _rolesRepository.CreateCompanyUserAssignedRole(CompanyUserId2, UserRoleId)).MustNotHaveHappened();
        A.CallTo(() => _businessPartnerRepository.CreateCompanyUserAssignedBusinessPartner(CompanyUserId2, BusinessPartnerNumber)).MustNotHaveHappened();
        A.CallTo(() => _rolesRepository.CreateCompanyUserAssignedRole(CompanyUserId3, UserRoleId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _businessPartnerRepository.CreateCompanyUserAssignedBusinessPartner(CompanyUserId3, BusinessPartnerNumber)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mailingService.SendMails(A<string>._, A<IDictionary<string, string>>._, A<IEnumerable<string>>._)).MustHaveHappened(3, Times.Exactly);
        _notifications.Should().HaveCount(5);
        companyApplication.ApplicationStatusId.Should().Be(CompanyApplicationStatusId.CONFIRMED);
        company.CompanyStatusId.Should().Be(CompanyStatusId.ACTIVE);
    }

    [Fact]
    public async Task HandleApplicationActivation_WithDefaultApplicationId_ThrowsConflictException()
    {
        //Act
        async Task Action() => await _sut.HandleApplicationActivation(Guid.Empty).ConfigureAwait(false);
        
        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Action);
        ex.Message.Should().Be($"BusinessPartnerNumber (bpn) for CompanyApplications {Guid.Empty} company {Guid.Empty} is empty");
    }

    [Fact]
    public async Task HandleApplicationActivation_WithoutCompanyApplication_ThrowsConflictException()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        A.CallTo(() => _applicationRepository.GetCompanyAndApplicationDetailsForApprovalAsync(applicationId))
            .ReturnsLazily(() => new ValueTuple<Guid, string?>());

        //Act
        async Task Action() => await _sut.HandleApplicationActivation(applicationId).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Action);
        ex.Message.Should().Be($"CompanyApplication {applicationId} is not in status SUBMITTED");
    }

    [Fact]
    public async Task HandleApplicationActivation_WithCompanyWithoutBPN_ThrowsConflictException()
    {
        //Act
        async Task Action() => await _sut.HandleApplicationActivation(IdWithoutBpn).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Action);
        ex.Message.Should().Be($"BusinessPartnerNumber (bpn) for CompanyApplications {IdWithoutBpn} company {Guid.Empty} is empty");
    }

    #endregion

    #region Setup
    
    private void SetupFakes(
        IDictionary<string, IEnumerable<string>> clientRoleNames,
        IEnumerable<UserRoleData> userRoleData,
        CompanyUserAssignedRole companyUserAssignedRole,
        CompanyUserAssignedBusinessPartner companyUserAssignedBusinessPartner)
    {
        var company = _fixture.Build<Company>()
            .With(u => u.BusinessPartnerNumber, BusinessPartnerNumber)
            .With(u => u.Name, CompanyName)
            .Create();

        var companyInvitedUsers = new List<CompanyInvitedUserData>
        {
            new(CompanyUserId1, CentralUserId1.ToString(), Enumerable.Empty<string>(), Enumerable.Empty<Guid>()),
            new(CompanyUserId2, CentralUserId2.ToString(), Enumerable.Repeat(BusinessPartnerNumber, 1), Enumerable.Repeat(UserRoleId, 1)),
            new(CompanyUserId3, CentralUserId3.ToString(), Enumerable.Empty<string>(), Enumerable.Empty<Guid>())
        }.ToAsyncEnumerable();
        var businessPartnerNumbers = new List<string> { BusinessPartnerNumber }.AsEnumerable();

        _settings.ApplicationApprovalInitialRoles = clientRoleNames;
        _settings.CompanyAdminRoles = new Dictionary<string, IEnumerable<string>>
        {
            { ClientId, new List<string> { "Company Admin" }.AsEnumerable() }
        };

        A.CallTo(() => _applicationRepository.GetCompanyAndApplicationDetailsForApprovalAsync(A<Guid>.That.Matches(x => x == Id)))
            .ReturnsLazily(() => new ValueTuple<Guid, string?>(company.Id, company.BusinessPartnerNumber!));
        A.CallTo(() => _applicationRepository.GetCompanyAndApplicationDetailsForApprovalAsync(A<Guid>.That.Matches(x => x == IdWithoutBpn)))
            .ReturnsLazily(() => new ValueTuple<Guid, string?>(IdWithoutBpn, null));

        A.CallTo(() => _applicationRepository.GetCompanyAndApplicationDetailsForCreateWalletAsync(A<Guid>.That.Matches(x => x == Id)))
            .ReturnsLazily(() => new ValueTuple<Guid, string, string?>(company.Id, company.Name, company.BusinessPartnerNumber!));
        A.CallTo(() => _applicationRepository.GetCompanyAndApplicationDetailsForCreateWalletAsync(A<Guid>.That.Matches(x => x == IdWithoutBpn)))
            .ReturnsLazily(() => new ValueTuple<Guid, string, string?>(IdWithoutBpn, company.Name, null));

        var welcomeEmailData = new List<WelcomeEmailData>();
        welcomeEmailData.AddRange(new WelcomeEmailData[]
        {
            new (CompanyUserId1, "Stan", "Lee", "stan@lee.com", company.Name),
            new (CompanyUserId2, "Tony", "Stark", "tony@stark.com", company.Name),
            new (CompanyUserId3, "Peter", "Parker", "peter@parker.com", company.Name)
        });
        A.CallTo(() => _applicationRepository.GetWelcomeEmailDataUntrackedAsync(Id, A<IEnumerable<Guid>>._))
            .Returns(welcomeEmailData.ToAsyncEnumerable());
        A.CallTo(() => _applicationRepository.GetWelcomeEmailDataUntrackedAsync(A<Guid>.That.Not.Matches(x => x == Id), A<IEnumerable<Guid>>._))
            .Returns(new List<WelcomeEmailData>().ToAsyncEnumerable());

        A.CallTo(() => _rolesRepository.GetUserRoleDataUntrackedAsync(A<IDictionary<string, IEnumerable<string>>>.That.Matches(x => x[ClientId].First() == clientRoleNames[ClientId].First())))
            .Returns(userRoleData.ToAsyncEnumerable());

        A.CallTo(() => _rolesRepository.GetUserRoleDataUntrackedAsync(A<IDictionary<string, IEnumerable<string>>>.That.Matches(x => x[ClientId].First() == _settings.CompanyAdminRoles[ClientId].First())))
            .Returns(new List<UserRoleData>() { new(UserRoleId, ClientId, "Company Admin") }.ToAsyncEnumerable());

        A.CallTo(() => _applicationRepository.GetInvitedUsersDataByApplicationIdUntrackedAsync(Id))
            .Returns(companyInvitedUsers);

        A.CallTo(() => _provisioningManager.AssignClientRolesToCentralUserAsync(CentralUserId1.ToString(), clientRoleNames))
            .Returns(clientRoleNames.Select(x => (Client: x.Key, Roles: x.Value)).ToAsyncEnumerable());
        A.CallTo(() => _provisioningManager.AssignClientRolesToCentralUserAsync(CentralUserId2.ToString(), clientRoleNames))
            .Returns(clientRoleNames.Select(x => (Client: x.Key, Roles: x.Value)).ToAsyncEnumerable());
        A.CallTo(() => _provisioningManager.AssignClientRolesToCentralUserAsync(CentralUserId3.ToString(), clientRoleNames))
            .Returns(clientRoleNames.Select(x => (Client: x.Key, Roles: x.Value)).ToAsyncEnumerable());

        A.CallTo(() => _provisioningManager.AddBpnAttributetoUserAsync(CentralUserId1.ToString(), businessPartnerNumbers))
            .Returns(Task.CompletedTask);
        A.CallTo(() => _provisioningManager.AddBpnAttributetoUserAsync(CentralUserId2.ToString(), businessPartnerNumbers))
            .Returns(Task.CompletedTask);
        A.CallTo(() => _provisioningManager.AddBpnAttributetoUserAsync(CentralUserId3.ToString(), businessPartnerNumbers))
            .Returns(Task.CompletedTask);

        A.CallTo(() => _rolesRepository.CreateCompanyUserAssignedRole(CompanyUserId1, CompanyUserRoleId))
            .Returns(companyUserAssignedRole);
        A.CallTo(() => _rolesRepository.CreateCompanyUserAssignedRole(CompanyUserId3, CompanyUserRoleId))
            .Returns(companyUserAssignedRole);

        A.CallTo(() =>
                _businessPartnerRepository.CreateCompanyUserAssignedBusinessPartner(CompanyUserId1, BusinessPartnerNumber))
            .Returns(companyUserAssignedBusinessPartner);
        A.CallTo(() =>
                _businessPartnerRepository.CreateCompanyUserAssignedBusinessPartner(CompanyUserId3, BusinessPartnerNumber))
            .Returns(companyUserAssignedBusinessPartner);

        A.CallTo(() => _portalRepositories.SaveAsync())
            .Returns(1);

        A.CallTo(() => _notificationService.CreateNotifications(A<IDictionary<string, IEnumerable<string>>>._, A<Guid?>._, A<IEnumerable<(string? content, NotificationTypeId notificationTypeId)>>._, A<Guid>._))
            .Invokes((
                IDictionary<string,IEnumerable<string>> _, 
                Guid? creatorId, 
                IEnumerable<(string? content, NotificationTypeId notificationTypeId)> notifications, 
                Guid _) =>
            {
                foreach (var notificationData in notifications)
                {
                    var notification = new Notification(Guid.NewGuid(), Guid.NewGuid(),
                        DateTimeOffset.UtcNow, notificationData.notificationTypeId, false)
                    {
                        CreatorUserId = creatorId,
                        Content = notificationData.content
                    };
                    _notifications.Add(notification);
                }
            });
    }

    #endregion
}
