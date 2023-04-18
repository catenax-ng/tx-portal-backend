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
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Tests.Setup;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Xunit;
using Xunit.Extensions.AssemblyFixture;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Tests;

/// <summary>
/// Tests the functionality of the <see cref="ServiceAccountRepository"/>
/// </summary>
public class TechnicalUserProfileRepositoryTests : IAssemblyFixture<TestDbFixture>
{
    private readonly TestDbFixture _dbTestDbFixture;
    private const string IamUserId = "502dabcf-01c7-47d9-a88e-0be4279097b5";
    private readonly Guid _validOfferId = new("ac1cf001-7fbc-1f2f-817f-bce0000c0001");
    private readonly Guid _validTechnicalUserProfile = new("8a0cd2e0-ceb6-43db-8753-84f1b4238f00");
    
    public TechnicalUserProfileRepositoryTests(TestDbFixture testDbFixture)
    {
        var fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));

        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        _dbTestDbFixture = testDbFixture;
    }

    #region GetOfferProfileData

    [Fact]
    public async Task GetOfferProfileData_ReturnsExpectedResult()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOfferProfileData(_validOfferId, IamUserId).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.IsProvidingCompanyUser.Should().BeTrue();
        result.ProfileData.Should().HaveCount(2);
        result.ServiceTypeIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOfferProfileData_WithUnknownUser_ReturnsExpectedResult()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOfferProfileData(_validOfferId, Guid.NewGuid().ToString()).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.IsProvidingCompanyUser.Should().BeFalse();
        result.ProfileData.Should().HaveCount(2);
        result.ServiceTypeIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOfferProfileData_WithoutExistingProfile_ReturnsNull()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOfferProfileData(Guid.NewGuid(), IamUserId).ConfigureAwait(false);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CreateTechnicalUserProfiles

    [Fact]
    public async Task CreateTechnicalUserProfiles_ReturnsExpectedResult()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var (sut, context) = await CreateSutWithContext().ConfigureAwait(false);

        // Act
        var result = sut.CreateTechnicalUserProfiles(profileId, _validOfferId);

        // Assert
        var changeTracker = context.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        result.OfferId.Should().Be(_validOfferId);
        result.Id.Should().Be(profileId);
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(1);
        changedEntries.Single().Entity.Should().BeOfType<TechnicalUserProfile>().Which.Id.Should().Be(profileId);
    }

    #endregion

    #region CreateDeleteTechnicalUserProfileAssignedRoles

    [Fact]
    public async Task CreateDeleteTechnicalUserProfileAssignedRoles_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, context) = await CreateSutWithContext().ConfigureAwait(false);

        // Act
        sut.CreateDeleteTechnicalUserProfileAssignedRoles(_validTechnicalUserProfile, new []{new Guid("aabcdfeb-6669-4c74-89f0-19cda090873e"), new Guid ("aabcdfeb-6669-4c74-89f0-19cda090873f")}, new []{new Guid("efc20368-9e82-46ff-b88f-6495b9810253"), new Guid ("aabcdfeb-6669-4c74-89f0-19cda090873f")});

        // Assert
        var changeTracker = context.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(2);
        var addedEntries = changedEntries.Where(x => x.State == EntityState.Added);
        var removedEntries = changedEntries.Where(x => x.State == EntityState.Deleted);
        addedEntries.Should().ContainSingle();
        removedEntries.Should().ContainSingle();
        addedEntries.Single().Entity.Should().BeOfType<TechnicalUserProfileAssignedUserRole>().Which.UserRoleId.Should()
            .Be(new Guid("efc20368-9e82-46ff-b88f-6495b9810253"));
        removedEntries.Single().Entity.Should().BeOfType<TechnicalUserProfileAssignedUserRole>().Which.UserRoleId.Should()
            .Be(new Guid("aabcdfeb-6669-4c74-89f0-19cda090873e"));
    }

    #endregion

    #region RemoveTechnicalUserProfilesWithAssignedRoles

    [Fact]
    public async Task RemoveTechnicalUserProfilesWithAssignedRoles_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, context) = await CreateSutWithContext().ConfigureAwait(false);

        // Act
        sut.RemoveTechnicalUserProfilesWithAssignedRoles(new []{
            new ValueTuple<Guid, IEnumerable<Guid>>(_validTechnicalUserProfile, new []{new Guid("aabcdfeb-6669-4c74-89f0-19cda090873e"), new Guid ("aabcdfeb-6669-4c74-89f0-19cda090873f")})
        });

        // Assert
        var changeTracker = context.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(3);
        var removedEntries = changedEntries.Where(x => x.State == EntityState.Deleted);
        removedEntries.Should().HaveCount(3);
        removedEntries.Where(x => x.Entity.GetType() == typeof(TechnicalUserProfile)).Should().ContainSingle();
        removedEntries.Where(x => x.Entity.GetType() == typeof(TechnicalUserProfileAssignedUserRole)).Should().HaveCount(2);
    }

    #endregion

    #region RemoveTechnicalUserProfilesForOffer

    [Fact]
    public async Task RemoveTechnicalUserProfilesForOffer_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, context) = await CreateSutWithContext().ConfigureAwait(false);

        // Act
        sut.RemoveTechnicalUserProfilesForOffer(_validOfferId);

        // Assert
        var changeTracker = context.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(5);
        var removedEntries = changedEntries.Where(x => x.State == EntityState.Deleted);
        removedEntries.Should().HaveCount(5);
        removedEntries.Where(x => x.Entity.GetType() == typeof(TechnicalUserProfile)).Should().HaveCount(2);
        removedEntries.Where(x => x.Entity.GetType() == typeof(TechnicalUserProfileAssignedUserRole)).Should().HaveCount(3);
    }

    #endregion

    #region GetOfferProfileData

    [Fact]
    public async Task GetTechnicalUserProfileInformation_ReturnsExpectedResult()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTechnicalUserProfileInformation(_validOfferId, IamUserId, OfferTypeId.SERVICE).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.IsUserOfProvidingCompany.Should().BeTrue();
        result.Information.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTechnicalUserProfileInformation_WithUnknownUser_ReturnsExpectedResult()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTechnicalUserProfileInformation(_validOfferId, Guid.NewGuid().ToString(), OfferTypeId.SERVICE).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.IsUserOfProvidingCompany.Should().BeFalse();
    }

    [Fact]
    public async Task GetTechnicalUserProfileInformation_WithoutExistingProfile_ReturnsNull()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTechnicalUserProfileInformation(Guid.NewGuid(), IamUserId, OfferTypeId.SERVICE).ConfigureAwait(false);

        // Assert
        result.Should().Be(default);
    }

    [Fact]
    public async Task GetTechnicalUserProfileInformation_WithWrongType_ReturnsNull()
    {
        // Arrange
        var sut = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTechnicalUserProfileInformation(_validOfferId, IamUserId, OfferTypeId.APP).ConfigureAwait(false);

        // Assert
        result.Should().Be(default);
    }

    #endregion

    #region Setup
    
    private async Task<(TechnicalUserProfileRepository, PortalDbContext)> CreateSutWithContext()
    {
        var context = await _dbTestDbFixture.GetPortalDbContext().ConfigureAwait(false);
        var sut = new TechnicalUserProfileRepository(context);
        return (sut, context);
    }

    private async Task<TechnicalUserProfileRepository> CreateSut()
    {
        var context = await _dbTestDbFixture.GetPortalDbContext().ConfigureAwait(false);
        var sut = new TechnicalUserProfileRepository(context);
        return sut;
    }

    #endregion
}
