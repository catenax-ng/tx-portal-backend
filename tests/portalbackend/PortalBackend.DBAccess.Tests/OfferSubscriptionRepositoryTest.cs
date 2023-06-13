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
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Tests.Setup;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Xunit.Extensions.AssemblyFixture;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Tests;

public class OfferSubscriptionRepositoryTest : IAssemblyFixture<TestDbFixture>
{
    private readonly TestDbFixture _dbTestDbFixture;
    private readonly Guid _userCompanyId = new("3390c2d7-75c1-4169-aa27-6ce00e1f3cdd");

    public OfferSubscriptionRepositoryTest(TestDbFixture testDbFixture)
    {
        var fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));

        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        _dbTestDbFixture = testDbFixture;
    }

    #region AttachAndModifyOfferSubscription

    [Fact]
    public async Task AttachAndModifyOfferSubscription_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, context) = await CreateSut().ConfigureAwait(false);

        var offerSubscriptionId = new Guid("eb98bdf5-14e1-4feb-a954-453eac0b93cd");
        var modifiedName = "Modified Name";

        // Act
        sut.AttachAndModifyOfferSubscription(offerSubscriptionId,
            sub =>
            {
                sub.OfferSubscriptionStatusId = OfferSubscriptionStatusId.PENDING;
                sub.DisplayName = modifiedName;
            });

        // Assert
        var changeTracker = context.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(1);
        changedEntries.Single().Entity.Should().BeOfType<OfferSubscription>().Which.Should().Match<OfferSubscription>(os =>
            os.Id == offerSubscriptionId &&
            os.OfferSubscriptionStatusId == OfferSubscriptionStatusId.PENDING &&
            os.DisplayName == modifiedName);
    }

    #endregion

    #region GetOfferSubscriptionStateForCompany

    [Fact]
    public async Task GetOfferSubscriptionStateForCompanyAsync_WithExistingData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOfferSubscriptionStateForCompanyAsync(
            new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfea"),
            new Guid("2dc4249f-b5ca-4d42-bef1-7a7a950a4f87"),
            OfferTypeId.SERVICE).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBe(default);
        result.OfferSubscriptionStatusId.Should().Be(OfferSubscriptionStatusId.ACTIVE);
        result.OfferSubscriptionId.Should().Be(new Guid("3DE6A31F-A5D1-4F60-AA3A-4B1A769BECBF"));
    }

    [Fact]
    public async Task GetOfferSubscriptionStateForCompanyAsync_WithWrongType_ReturnsDefault()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOfferSubscriptionStateForCompanyAsync(
            new Guid("99C5FD12-8085-4DE2-ABFD-215E1EE4BAA4"),
            new Guid("2dc4249f-b5ca-4d42-bef1-7a7a950a4f87"),
            OfferTypeId.SERVICE).ConfigureAwait(false);

        // Assert
        result.Should().Be(default);
    }

    #endregion

    #region GetAllBusinessAppDataForUserId

    [Fact]
    public async Task GetAllBusinessAppDataForUserIdAsync_WithValidUser_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetAllBusinessAppDataForUserIdAsync(new("ac1cf001-7fbc-1f2f-817f-bce058020006")).ToListAsync().ConfigureAwait(false);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveCount(1);
        result.First().SubscriptionUrl.Should().Be("https://ec-qas.d13fe27.kyma.ondemand.com");
        result.First().OfferId.Should().Be(new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfea"));
    }

    #endregion

    #region GetOwnCompanyProvidedOfferSubscriptionStatusesUntracked

    [Theory]
    [InlineData(SubscriptionStatusSorting.OfferIdAsc, null, 2, true)]
    [InlineData(SubscriptionStatusSorting.OfferIdDesc, null, 2, false)]
    [InlineData(SubscriptionStatusSorting.CompanyNameAsc, null, 2, true)]
    [InlineData(SubscriptionStatusSorting.CompanyNameDesc, null, 2, true)]
    [InlineData(SubscriptionStatusSorting.OfferIdAsc, "a16e73b9-5277-4b69-9f8d-3b227495dfea", 1, false)]
    [InlineData(SubscriptionStatusSorting.OfferIdAsc, "a16e73b9-5277-4b69-9f8d-3b227495dfae", 1, true)]
    [InlineData(SubscriptionStatusSorting.OfferIdAsc, "deadbeef-dead-beef-dead-beefdeadbeef", 0, false)]
    public async Task GetOwnCompanyProvidedOfferSubscriptionStatusesUntrackedAsync_ReturnsExpectedNotificationDetailData(SubscriptionStatusSorting sorting, string? offerIdTxt, int count, bool technicalUser)
    {
        // Arrange
        Guid? offerId = offerIdTxt == null ? null : new Guid(offerIdTxt);
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var results = await sut.GetOwnCompanyProvidedOfferSubscriptionStatusesUntrackedAsync(_userCompanyId, OfferTypeId.SERVICE, sorting, OfferSubscriptionStatusId.ACTIVE, offerId)(0, 15).ConfigureAwait(false);

        // Assert
        if (count > 0)
        {
            results.Should().NotBeNull();
            results!.Count.Should().Be(count);
            results.Data.Should().HaveCount(count);
            results.Data.Should().AllBeOfType<OfferCompanySubscriptionStatusData>().Which.First().CompanySubscriptionStatuses.Should().HaveCount(1);
            results.Data.Should().AllBeOfType<OfferCompanySubscriptionStatusData>().Which.First().CompanySubscriptionStatuses.Should().Match(x => x.Count() == 1 && x.First().TechnicalUser == technicalUser);
        }
        else
        {
            results.Should().BeNull();
        }
    }

    #endregion

    #region GetOfferDetailsAndCheckUser

    [Fact]
    public async Task GetOfferDetailsAndCheckUser_WithValidUserandSubscriptionId_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOfferDetailsAndCheckUser(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba"), new("2dc4249f-b5ca-4d42-bef1-7a7a950a4f87"), OfferTypeId.APP).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBe(default);
        result!.OfferId.Should().Be(new Guid("ac1cf001-7fbc-1f2f-817f-bce0572c0007"));
        result.Status.Should().Be(OfferSubscriptionStatusId.ACTIVE);
        result.CompanyId.Should().Be(new Guid("2dc4249f-b5ca-4d42-bef1-7a7a950a4f87"));
        result.CompanyName.Should().Be("Catena-X");
        result.IsUserOfProvider.Should().BeTrue();
        result.Bpn.Should().Be("BPNL00000003CRHK");
        result.OfferName.Should().Be("Trace-X");
    }

    #endregion

    #region GetSubscriptionDetailForProviderAsync

    [Fact]
    public async Task GetSubscriptionDetailForProviderAsync_ForProvider_ReturnsExpected()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetSubscriptionDetailsAsync(new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfea"), new Guid("3DE6A31F-A5D1-4F60-AA3A-4B1A769BECBF"), _userCompanyId, OfferTypeId.SERVICE, new[] { new Guid("58f897ec-0aad-4588-8ffa-5f45d6638632") }, true).ConfigureAwait(false);

        // Assert
        result.Exists.Should().BeTrue();
        result.IsUserOfCompany.Should().BeTrue();
        result.Details.Name.Should().Be("SDE with EDC");
        result.Details.CompanyName.Should().Be("Catena-X");
        result.Details.Contact.Should().ContainSingle().And.Subject.Should().ContainSingle("tobeadded@cx.com");
        result.Details.OfferSubscriptionStatus.Should().Be(OfferSubscriptionStatusId.ACTIVE);
    }

    [Fact]
    public async Task GetSubscriptionDetailForProviderAsync_ForSubscriber_ReturnsExpected()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetSubscriptionDetailsAsync(new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfea"), new Guid("3DE6A31F-A5D1-4F60-AA3A-4B1A769BECBF"), new("2dc4249f-b5ca-4d42-bef1-7a7a950a4f87"), OfferTypeId.SERVICE, new[] { new Guid("58f897ec-0aad-4588-8ffa-5f45d6638632") }, false).ConfigureAwait(false);

        // Assert
        result.Exists.Should().BeTrue();
        result.IsUserOfCompany.Should().BeTrue();
        result.Details.Name.Should().Be("SDE with EDC");
        result.Details.CompanyName.Should().Be("Service Provider");
        result.Details.Contact.Should().ContainSingle().And.Subject.Should().ContainSingle("tobeadded@cx.com");
        result.Details.OfferSubscriptionStatus.Should().Be(OfferSubscriptionStatusId.ACTIVE);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSubscriptionDetailForProviderAsync_WithNotExistingId_ReturnsExpected(bool forProvider)
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetSubscriptionDetailsAsync(Guid.NewGuid(), new Guid("3DE6A31F-A5D1-4F60-AA3A-4B1A769BECBF"), _userCompanyId, OfferTypeId.SERVICE, new List<Guid>(), forProvider).ConfigureAwait(false);

        // Assert
        result.Exists.Should().BeFalse();
        result.IsUserOfCompany.Should().BeFalse();
        result.Details.Should().Be(default!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSubscriptionDetailForProviderAsync_WithWrongUser_ReturnsExpected(bool forProvider)
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetSubscriptionDetailsAsync(new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfea"), new Guid("3DE6A31F-A5D1-4F60-AA3A-4B1A769BECBF"), Guid.NewGuid(), OfferTypeId.SERVICE, new List<Guid>(), forProvider).ConfigureAwait(false);

        // Assert
        result.Exists.Should().BeTrue();
        result.IsUserOfCompany.Should().BeFalse();
        result.Details.Name.Should().Be("SDE with EDC");
    }

    #endregion

    #region GetOfferSubscriptionDataForProcessIdAsync

    [Fact]
    public async Task GetOfferSubscriptionDataForProcessIdAsync_WithValidData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOfferSubscriptionDataForProcessIdAsync(new Guid("0cc208c3-bdf6-456c-af81-6c3ebe14fe06")).ConfigureAwait(false);

        // Assert
        result.Should().NotBe(Guid.Empty);
        result.Should().Be(new Guid("e8886159-9258-44a5-88d8-f5735a197a09"));
    }

    [Fact]
    public async Task GetOfferSubscriptionDataForProcessIdAsync_WithNotExistingId_ReturnsNull()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOfferSubscriptionDataForProcessIdAsync(Guid.NewGuid()).ConfigureAwait(false);

        // Assert
        result.Should().Be(Guid.Empty);
    }

    #endregion

    #region GetOfferSubscriptionDataForProcessIdAsync

    [Fact]
    public async Task GetTriggerProviderInformation_WithValidData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTriggerProviderInformation(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba")).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.OfferName.Should().Be("Trace-X");
        result.IsSingleInstance.Should().BeTrue();
    }

    [Fact]
    public async Task GetTriggerProviderInformation_WithNotExistingId_ReturnsNull()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTriggerProviderInformation(Guid.NewGuid()).ConfigureAwait(false);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetSubscriptionActivationDataByIdAsync

    [Fact]
    public async Task GetSubscriptionActivationDataByIdAsync_WithValidData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetSubscriptionActivationDataByIdAsync(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba")).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.OfferName.Should().Be("Trace-X");
        result.InstanceData.Should().Be((true, "https://test.com"));
        result.Status.Should().Be(OfferSubscriptionStatusId.ACTIVE);
    }

    [Fact]
    public async Task GetSubscriptionActivationDataByIdAsync_WithNotExistingId_ReturnsNull()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetSubscriptionActivationDataByIdAsync(Guid.NewGuid()).ConfigureAwait(false);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetProcessStepData

    [Fact]
    public async Task GetProcessStepData_WithValidData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetProcessStepData(new Guid("e8886159-9258-44a5-88d8-f5735a197a09"), new[]
        {
            ProcessStepTypeId.START_AUTOSETUP
        }).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.ProcessSteps.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProcessStepData_WithNotExistingId_ReturnsNull()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetProcessStepData(Guid.NewGuid(), new[] { ProcessStepTypeId.START_AUTOSETUP }).ConfigureAwait(false);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IsActiveOfferSubscription

    [Fact]
    public async Task IsActiveOfferSubscription_WithValidData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.IsActiveOfferSubscription(new Guid("e8886159-9258-44a5-88d8-f5735a197a09")).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.IsValidSubscriptionId.Should().BeTrue();
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task IsActiveOfferSubscription_WithActive_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.IsActiveOfferSubscription(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba")).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.IsValidSubscriptionId.Should().BeTrue();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsActiveOfferSubscription_WithNotExistingId_ReturnsNull()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.IsActiveOfferSubscription(Guid.NewGuid()).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.IsValidSubscriptionId.Should().BeFalse();
    }

    #endregion

    #region GetClientCreationData

    [Fact]
    public async Task GetClientCreationData_WithValidData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetClientCreationData(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba")).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.OfferType.Should().Be(OfferTypeId.APP);
        result.IsTechnicalUserNeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetClientCreationData_WithNotExistingId_ReturnsNull()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetClientCreationData(Guid.NewGuid()).ConfigureAwait(false);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetTechnicalUserCreationData

    [Fact]
    public async Task GetTechnicalUserCreationData_WithValidData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTechnicalUserCreationData(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba")).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.Bpn.Should().Be("BPNL00000003CRHK");
        result.OfferName.Should().Be("Trace-X");
        result.IsTechnicalUserNeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetTechnicalUserCreationData_WithNotExistingId_ReturnsNull()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTechnicalUserCreationData(Guid.NewGuid()).ConfigureAwait(false);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetTriggerProviderCallbackInformation

    [Fact]
    public async Task GetTriggerProviderCallbackInformation_WithValidData_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTriggerProviderCallbackInformation(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba")).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(OfferSubscriptionStatusId.ACTIVE);
    }

    [Fact]
    public async Task GetTriggerProviderCallbackInformation_WithNotExistingId_ReturnsNull()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetTriggerProviderCallbackInformation(Guid.NewGuid()).ConfigureAwait(false);

        // Assert
        result.Should().Be(default);
    }

    #endregion

    #region Create Notification

    [Fact]
    public async Task CreateNotification_ReturnsExpectedResult()
    {
        // Arrange
        var (sut, context) = await CreateSut().ConfigureAwait(false);

        // Act
        var results = sut.CreateOfferSubscriptionProcessData(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba"), "https://www.test.de");

        // Assert
        var changeTracker = context.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        results.OfferUrl.Should().Be("https://www.test.de");
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(1);
        changedEntries.Single().Entity.Should().BeOfType<OfferSubscriptionProcessData>().Which.OfferUrl.Should().Be("https://www.test.de");
    }

    #endregion

    #region RemoveOfferSubscriptionProcessData

    [Fact]
    public async Task RemoveOfferSubscriptionProcessData_WithExisting_RemovesOfferSubscriptionProcessData()
    {
        // Arrange
        var (sut, dbContext) = await CreateSut().ConfigureAwait(false);

        // Act
        sut.RemoveOfferSubscriptionProcessData(new Guid("ed4de48d-fd4b-4384-a72f-ecae3c6cc5ba"));

        // Assert
        var changeTracker = dbContext.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().NotBeEmpty();
        changedEntries.Should().HaveCount(1);
        var changedEntity = changedEntries.Single();
        changedEntity.State.Should().Be(EntityState.Deleted);
    }

    #endregion

    #region GetUpdateUrlDataAsync

    [Fact]
    public async Task GetUpdateUrlDataAsync_WithValidData_ReturnsExpected()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetUpdateUrlDataAsync(new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfea"), new Guid("3DE6A31F-A5D1-4F60-AA3A-4B1A769BECBF"), _userCompanyId).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.IsUserOfCompany.Should().BeTrue();
    }

    [Fact]
    public async Task GetUpdateUrlDataAsync_WithNotExistingId_ReturnsExpected()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetUpdateUrlDataAsync(Guid.NewGuid(), new Guid("3DE6A31F-A5D1-4F60-AA3A-4B1A769BECBF"), _userCompanyId).ConfigureAwait(false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUpdateUrlDataAsync_WithWrongUser_ReturnsExpected()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetUpdateUrlDataAsync(new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfea"), new Guid("3DE6A31F-A5D1-4F60-AA3A-4B1A769BECBF"), Guid.NewGuid()).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result!.IsUserOfCompany.Should().BeFalse();
        result.OfferName.Should().Be("SDE with EDC");
    }

    #endregion

    #region AttachAndModifyAppSubscriptionDetail

    [Theory]
    [InlineData("https://www.new-url.com")]
    [InlineData(null)]
    public async Task AttachAndModifyAppSubscriptionDetail_ReturnsExpectedResult(string? modifiedUrl)
    {
        // Arrange
        var (sut, context) = await CreateSut().ConfigureAwait(false);

        var detailId = new Guid("eb98bdf5-14e1-4feb-a954-453eac0b93ca");
        var offerSubscriptionId = new Guid("eb98bdf5-14e1-4feb-a954-453eac0b93cd");

        // Act
        sut.AttachAndModifyAppSubscriptionDetail(detailId, offerSubscriptionId,
            os =>
            {
                os.AppSubscriptionUrl = "https://test.com";
            },
            sub =>
            {
                sub.AppSubscriptionUrl = modifiedUrl;
            });

        // Assert
        var changeTracker = context.ChangeTracker;
        var changedEntries = changeTracker.Entries().ToList();
        changeTracker.HasChanges().Should().BeTrue();
        changedEntries.Should().ContainSingle()
            .Which.Entity.Should().BeOfType<AppSubscriptionDetail>()
            .Which.AppSubscriptionUrl.Should().Be(modifiedUrl);
    }

    [Theory]
    [InlineData("https://www.new-url.com")]
    [InlineData(null)]
    public async Task AttachAndModifyAppSubscriptionDetail__WithUnchangedUrl_DoesntUpdate(string? modifiedUrl)
    {
        // Arrange
        var (sut, context) = await CreateSut().ConfigureAwait(false);

        var detailId = new Guid("eb98bdf5-14e1-4feb-a954-453eac0b93ca");
        var offerSubscriptionId = new Guid("eb98bdf5-14e1-4feb-a954-453eac0b93cd");

        // Act
        sut.AttachAndModifyAppSubscriptionDetail(detailId, offerSubscriptionId,
            os =>
            {
                os.AppSubscriptionUrl = modifiedUrl;
            },
            sub =>
            {
                sub.AppSubscriptionUrl = modifiedUrl;
            });

        // Assert
        var changeTracker = context.ChangeTracker;
        changeTracker.HasChanges().Should().BeFalse();
    }

    #endregion

    #region  GetOwnCompanySubscribedOfferSubscriptionStatuse

    [Theory]
    [InlineData("2dc4249f-b5ca-4d42-bef1-7a7a950a4f87", OfferTypeId.APP, DocumentTypeId.APP_LEADIMAGE)]
    [InlineData("2dc4249f-b5ca-4d42-bef1-7a7a950a4f87", OfferTypeId.SERVICE, DocumentTypeId.SERVICE_LEADIMAGE)]
    [InlineData("2dc4249f-b5ca-4d42-bef1-7a7a950a4f87", OfferTypeId.CORE_COMPONENT, DocumentTypeId.SERVICE_LEADIMAGE)]
    public async Task GetOwnCompanySubscribedOfferSubscriptionStatusesUntrackedAsync_ReturnsExpected(Guid companyId, OfferTypeId offerTypeId, DocumentTypeId documentTypeId)
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetOwnCompanySubscribedOfferSubscriptionStatusesUntrackedAsync(companyId, offerTypeId, documentTypeId)(0, 15).ConfigureAwait(false);

        // Assert
        switch (offerTypeId)
        {
            case OfferTypeId.APP:
                result.Should().NotBeNull();
                result!.Data.Should().HaveCount(2).And.Satisfy(
                    x => x.OfferId == new Guid("ac1cf001-7fbc-1f2f-817f-bce0572c0007") &&
                        x.OfferSubscriptionStatusId == OfferSubscriptionStatusId.ACTIVE &&
                        x.OfferName == "Trace-X" &&
                        x.Provider == "Catena-X" &&
                        x.DocumentId == new Guid("e020787d-1e04-4c0b-9c06-bd1cd44724b1"),
                    x => x.OfferId == new Guid("ac1cf001-7fbc-1f2f-817f-bce0572c0007") &&
                        x.OfferSubscriptionStatusId == OfferSubscriptionStatusId.PENDING &&
                        x.OfferName == "Trace-X" &&
                        x.Provider == "Catena-X" &&
                        x.DocumentId == new Guid("e020787d-1e04-4c0b-9c06-bd1cd44724b1")
                );
                break;

            case OfferTypeId.SERVICE:
                result.Should().NotBeNull();
                result!.Data.Should().HaveCount(2).And.Satisfy(
                    x => x.OfferId == new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfea") &&
                        x.OfferSubscriptionStatusId == OfferSubscriptionStatusId.ACTIVE &&
                        x.OfferName == "SDE with EDC" &&
                        x.Provider == "Service Provider" &&
                        x.DocumentId == Guid.Empty,
                    x => x.OfferId == new Guid("a16e73b9-5277-4b69-9f8d-3b227495dfae") &&
                        x.OfferSubscriptionStatusId == OfferSubscriptionStatusId.ACTIVE &&
                        x.OfferName == "Service Test 123" &&
                        x.Provider == "Service Provider" &&
                        x.DocumentId == Guid.Empty
                );
                break;

            case OfferTypeId.CORE_COMPONENT:
                result.Should().BeNull();
                break;
        }
    }

    #endregion

    #region GetProcessStepsForSubscription

    [Fact]
    public async Task GetProcessStepsForSubscription_WithExisting_ReturnsExpected()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetProcessStepsForSubscription(new Guid("e8886159-9258-44a5-88d8-f5735a197a09")).ToListAsync().ConfigureAwait(false);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProcessStepsForSubscription_WithoutExisting_ReturnsExpected()
    {
        // Arrange
        var (sut, _) = await CreateSut().ConfigureAwait(false);

        // Act
        var result = await sut.GetProcessStepsForSubscription(Guid.NewGuid()).ToListAsync().ConfigureAwait(false);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Setup

    private async Task<(IOfferSubscriptionsRepository, PortalDbContext)> CreateSut()
    {
        var context = await _dbTestDbFixture.GetPortalDbContext().ConfigureAwait(false);
        var sut = new OfferSubscriptionsRepository(context);
        return (sut, context);
    }

    #endregion
}
