/********************************************************************************
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
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Models;
using Xunit;

namespace Org.Eclipse.TractusX.Portal.Backend.SyncCompanUserIdpAssignment.Executor.Tests;

public class SyncCompanyUserIdpAssigmentProcessTypeExecutorTests
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProviderRepository _identityProviderRepository;
    private readonly IProvisioningManager _provisioningManager;
    private readonly IFixture _fixture;
    private readonly SyncCompanyUserIdpAssigmentProcessTypeExecutor _executor;

    public SyncCompanyUserIdpAssigmentProcessTypeExecutorTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var portalRepositories = A.Fake<IPortalRepositories>();
        _provisioningManager = A.Fake<IProvisioningManager>();
        _userRepository = A.Fake<IUserRepository>();
        _identityProviderRepository = A.Fake<IIdentityProviderRepository>();

        A.CallTo(() => portalRepositories.GetInstance<IUserRepository>())
            .Returns(_userRepository);
        A.CallTo(() => portalRepositories.GetInstance<IIdentityProviderRepository>())
            .Returns(_identityProviderRepository);

        _executor = new SyncCompanyUserIdpAssigmentProcessTypeExecutor(portalRepositories, _provisioningManager);
    }

    #region InitializeProcess

    [Fact]
    public async Task InitializeProcess_ValidProcessId_ReturnsExpected()
    {
        // Arrange
        var result = await _executor.InitializeProcess(Guid.NewGuid(), _fixture.CreateMany<ProcessStepTypeId>()).ConfigureAwait(false);

        // Assert
        result.Modified.Should().BeFalse();
        result.ScheduleStepTypeIds.Should().BeNull();
    }

    #endregion

    #region ExecuteProcessStep

    [Fact]
    public async Task ExecuteProcessStep_WithUnrecoverableServiceException_Throws()
    {
        // Arrange
        var processStepTypeIds = _fixture.CreateMany<ProcessStepTypeId>();
        A.CallTo(() => _provisioningManager.GetUserByUserName(A<string>._))
            .ThrowsAsync(new ServiceException("test"));
        A.CallTo(() => _userRepository.GetNextCompanyUserInfoForIdpLinDataSync())
            .Returns(Enumerable.Repeat(new ValueTuple<Guid, string?>(Guid.NewGuid(), null), 1).ToAsyncEnumerable());

        // Act
        var result = await _executor.ExecuteProcessStep(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, processStepTypeIds, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Modified.Should().BeTrue();
        result.ProcessStepStatusId.Should().Be(ProcessStepStatusId.FAILED);
        result.ProcessMessage.Should().Be("test");
    }

    [Fact]
    public async Task ExecuteProcessStep_WithConflictException_Throws()
    {
        // Arrange
        var processStepTypeIds = _fixture.CreateMany<ProcessStepTypeId>();
        A.CallTo(() => _provisioningManager.GetUserByUserName(A<string>._))
            .ThrowsAsync(new ConflictException("test"));
        A.CallTo(() => _userRepository.GetNextCompanyUserInfoForIdpLinDataSync())
            .Returns(Enumerable.Repeat(new ValueTuple<Guid, string?>(Guid.NewGuid(), null), 1).ToAsyncEnumerable());

        // Act
        var result = await _executor.ExecuteProcessStep(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, processStepTypeIds, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Modified.Should().BeTrue();
        result.ProcessStepStatusId.Should().Be(ProcessStepStatusId.FAILED);
        result.ProcessMessage.Should().Be("test");
    }

    [Fact]
    public async Task ExecuteProcessStep_WithRecoverableServiceException_Throws()
    {
        // Arrange
        var processStepTypeIds = _fixture.CreateMany<ProcessStepTypeId>();
        A.CallTo(() => _provisioningManager.GetUserByUserName(A<string>._))
            .ThrowsAsync(new ServiceException("test", true));
        A.CallTo(() => _userRepository.GetNextCompanyUserInfoForIdpLinDataSync())
            .Returns(Enumerable.Repeat(new ValueTuple<Guid, string?>(Guid.NewGuid(), null), 1).ToAsyncEnumerable());

        // Act
        var result = await _executor.ExecuteProcessStep(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, processStepTypeIds, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Modified.Should().BeTrue();
        result.ProcessStepStatusId.Should().Be(ProcessStepStatusId.TODO);
    }

    [Fact]
    public async Task ExecuteProcessStep_WithNoIdpForAlias_Throws()
    {
        // Arrange
        var identity = new Identity(Guid.NewGuid(), DateTimeOffset.Now, Guid.NewGuid(), UserStatusId.ACTIVE, IdentityTypeId.COMPANY_USER);
        var userEntityId = Guid.NewGuid().ToString();
        A.CallTo(() => _userRepository.GetNextCompanyUserInfoForIdpLinDataSync())
            .Returns(new[]
            {
                (identity.Id, (string?)userEntityId)
            }.ToAsyncEnumerable());
        A.CallTo(() => _provisioningManager.GetUserByUserName(A<string>._))
            .Returns(userEntityId);
        A.CallTo(() => _provisioningManager.GetProviderUserLinkDataForCentralUserIdAsync(userEntityId))
            .Returns(new[]
            {
                new IdentityProviderLink("a1", "uId1", "testUser"),
            }.ToAsyncEnumerable());
        A.CallTo(() => _identityProviderRepository.GetIdpIdByAlias("a1")).Returns(Guid.Empty);

        // Act
        var result = await _executor.ExecuteProcessStep(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, Enumerable.Repeat(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, 1), CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Modified.Should().BeTrue();
        result.ProcessStepStatusId.Should().Be(ProcessStepStatusId.FAILED);
        result.ProcessMessage.Should().Be("No Idp found for alias a1");
    }

    [Fact]
    public async Task ExecuteProcessStep_WithNoCompanyUser_ExecutesExpected()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetNextCompanyUserInfoForIdpLinDataSync())
            .Returns(Enumerable.Empty<(Guid, string?)>().ToAsyncEnumerable());

        // Act
        var result = await _executor.ExecuteProcessStep(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, new[] { ProcessStepTypeId.SYNC_COMPANY_USER_IDP }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Modified.Should().BeFalse();
        result.ScheduleStepTypeIds.Should().BeNull();
        result.ProcessStepStatusId.Should().Be(ProcessStepStatusId.DONE);
        result.ProcessMessage.Should().Be("no companyUsers to add idp link data found");

        A.CallTo(() => _identityProviderRepository.GetIdpIdByAlias(A<string>._))
            .MustNotHaveHappened();
        A.CallTo(() => _provisioningManager.GetUserByUserName(A<string>._))
            .MustNotHaveHappened();
        A.CallTo(() => _userRepository.AddCompanyUserAssignedIdentityProvider(A<Guid>._, A<Guid>._, A<string>._, A<string>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteProcessStep_WithSingleIdentity_ExecutesExpected(bool hasUserEntityId)
    {
        // Arrange
        var identity = new Identity(Guid.NewGuid(), DateTimeOffset.Now, Guid.NewGuid(), UserStatusId.ACTIVE, IdentityTypeId.COMPANY_USER);
        var userEntityId = Guid.NewGuid().ToString();
        var idp1Id = Guid.NewGuid();
        var idp2Id = Guid.NewGuid();
        if (hasUserEntityId)
        {
            identity.UserEntityId = userEntityId;
        }

        A.CallTo(() => _userRepository.GetNextCompanyUserInfoForIdpLinDataSync())
            .Returns(new[]
            {
                (identity.Id, identity.UserEntityId)
            }.ToAsyncEnumerable());
        A.CallTo(() => _provisioningManager.GetUserByUserName(A<string>._))
            .Returns(userEntityId);
        A.CallTo(() => _provisioningManager.GetProviderUserLinkDataForCentralUserIdAsync(userEntityId))
            .Returns(new[]
            {
                new IdentityProviderLink("a1", "uId1", "testUser"),
                new IdentityProviderLink("a2", "uId2", "test2")
            }.ToAsyncEnumerable());
        A.CallTo(() => _identityProviderRepository.GetIdpIdByAlias("a1")).Returns(idp1Id);
        A.CallTo(() => _identityProviderRepository.GetIdpIdByAlias("a2")).Returns(idp2Id);

        // Act
        var result = await _executor.ExecuteProcessStep(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, new[] { ProcessStepTypeId.SYNC_COMPANY_USER_IDP }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Modified.Should().BeTrue();
        result.ScheduleStepTypeIds.Should().BeNull();
        result.ProcessStepStatusId.Should().Be(ProcessStepStatusId.DONE);
        result.ProcessMessage.Should().Be($"added idp link data to companyUser {identity.Id}");

        if (hasUserEntityId)
        {
            A.CallTo(() => _provisioningManager.GetUserByUserName(identity.Id.ToString()))
                .MustNotHaveHappened();
        }
        else
        {
            A.CallTo(() => _provisioningManager.GetUserByUserName(identity.Id.ToString()))
                .MustHaveHappenedOnceExactly();
        }

        A.CallTo(() => _userRepository.AddCompanyUserAssignedIdentityProvider(identity.Id, idp1Id, "uId1", "testUser"))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userRepository.AddCompanyUserAssignedIdentityProvider(identity.Id, idp2Id, "uId2", "test2"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteProcessStep_WithMultipleIdentities_ExecutesExpected()
    {
        // Arrange
        var identity = new Identity(Guid.NewGuid(), DateTimeOffset.Now, Guid.NewGuid(), UserStatusId.ACTIVE, IdentityTypeId.COMPANY_SERVICE_ACCOUNT);
        var userEntityId = Guid.NewGuid().ToString();
        var identity2Id = Guid.NewGuid();
        var idpId = Guid.NewGuid();
        A.CallTo(() => _userRepository.GetNextCompanyUserInfoForIdpLinDataSync())
            .Returns(new[]
            {
                (identity.Id, (string?)null),
                (identity2Id, (string?)null)
            }.ToAsyncEnumerable());
        A.CallTo(() => _provisioningManager.GetUserByUserName(A<string>._))
            .Returns(userEntityId);
        A.CallTo(() => _provisioningManager.GetProviderUserLinkDataForCentralUserIdAsync(userEntityId))
            .Returns(new[]
            {
                new IdentityProviderLink("a1", "uId1", "testUser")
            }.ToAsyncEnumerable());
        A.CallTo(() => _identityProviderRepository.GetIdpIdByAlias("a1")).Returns(idpId);

        // Act
        var result = await _executor.ExecuteProcessStep(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, new[] { ProcessStepTypeId.SYNC_COMPANY_USER_IDP }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Modified.Should().BeTrue();
        result.ScheduleStepTypeIds.Should().ContainSingle(x => x == ProcessStepTypeId.SYNC_COMPANY_USER_IDP);
        result.ProcessStepStatusId.Should().Be(ProcessStepStatusId.DONE);
        result.ProcessMessage.Should().Be($"added idp link data to companyUser {identity.Id}");

        A.CallTo(() => _userRepository.GetNextCompanyUserInfoForIdpLinDataSync())
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _provisioningManager.GetUserByUserName(identity.Id.ToString()))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _provisioningManager.GetServiceAccountUserId(identity2Id.ToString()))
            .MustNotHaveHappened();
        A.CallTo(() => _userRepository.AddCompanyUserAssignedIdentityProvider(identity.Id, idpId, "uId1", "testUser"))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userRepository.AttachAndModifyIdentity(identity2Id, A<Action<Identity>>._, A<Action<Identity>>._))
           .MustNotHaveHappened();
    }

    #endregion

    #region GetProcessTypeId

    [Fact]
    public void GetProcessTypeId_ReturnsExpected()
    {
        // Act
        var result = _executor.GetProcessTypeId();

        // Assert
        result.Should().Be(ProcessTypeId.SYNC_COMPANY_USER_IDP);
    }

    #endregion

    #region GetProcessTypeId

    [Fact]
    public async Task IsLockRequested_ReturnsExpected()
    {
        // Act
        var result = await _executor.IsLockRequested(_fixture.Create<ProcessStepTypeId>()).ConfigureAwait(false);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsExecutableStepTypeId

    [Theory]
    [InlineData(ProcessStepTypeId.SYNC_COMPANY_USER_IDP, true)]
    [InlineData(ProcessStepTypeId.OFFERSUBSCRIPTION_CLIENT_CREATION, false)]
    [InlineData(ProcessStepTypeId.OFFERSUBSCRIPTION_TECHNICALUSER_CREATION, false)]
    [InlineData(ProcessStepTypeId.ACTIVATE_SUBSCRIPTION, false)]
    [InlineData(ProcessStepTypeId.TRIGGER_PROVIDER_CALLBACK, false)]
    [InlineData(ProcessStepTypeId.SINGLE_INSTANCE_SUBSCRIPTION_DETAILS_CREATION, false)]
    [InlineData(ProcessStepTypeId.START_AUTOSETUP, false)]
    [InlineData(ProcessStepTypeId.END_CLEARING_HOUSE, false)]
    [InlineData(ProcessStepTypeId.START_CLEARING_HOUSE, false)]
    [InlineData(ProcessStepTypeId.DECLINE_APPLICATION, false)]
    [InlineData(ProcessStepTypeId.CREATE_IDENTITY_WALLET, false)]
    [InlineData(ProcessStepTypeId.TRIGGER_ACTIVATE_SUBSCRIPTION, false)]
    public void IsExecutableProcessStep_ReturnsExpected(ProcessStepTypeId processStepTypeId, bool expectedResult)
    {
        // Act
        var result = _executor.IsExecutableStepTypeId(processStepTypeId);

        // Assert
        result.Should().Be(expectedResult);
    }

    #endregion

    #region GetExecutableStepTypeIds

    [Fact]
    public void GetExecutableStepTypeIds_ReturnsExpected()
    {
        //Act
        var result = _executor.GetExecutableStepTypeIds();

        // Assert
        result.Should().HaveCount(1).And.Satisfy(x => x == ProcessStepTypeId.SYNC_COMPANY_USER_IDP);
    }

    #endregion
}
