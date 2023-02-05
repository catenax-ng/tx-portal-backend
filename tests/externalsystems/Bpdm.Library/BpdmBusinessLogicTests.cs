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

using System.Collections.Immutable;
using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.Tests;

public class BpdmBusinessLogicTests
{
    #region Initialization

    private static readonly Guid IdWithBpn = new ("c244f79a-7faf-4c59-bb85-fbfdf72ce46f");
    private static readonly Guid IdWithStateCreated = new ("bda6d1b5-042e-493a-894c-11f3a89c12b1");
    private static readonly Guid IdWithoutZipCode = new ("beaa6de5-d411-4da8-850e-06047d3170be");
    private static readonly Guid ValidCompanyId = new ("abf990f8-0c27-43dc-bbd0-b1bce964d8f4");
    private static readonly string IamUserId = new Guid("4C1A6851-D4E7-4E10-A011-3732CD045E8A").ToString();
    private const string ValidCompanyName = "valid company";

    private readonly IFixture _fixture;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IBpdmService _bpdmService;
    private readonly BpdmBusinessLogic _logic;

    public BpdmBusinessLogicTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization {ConfigureMembers = true});
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var portalRepository = A.Fake<IPortalRepositories>();
        _applicationRepository = A.Fake<IApplicationRepository>();
        _bpdmService = A.Fake<IBpdmService>();

        A.CallTo(() => portalRepository.GetInstance<IApplicationRepository>()).Returns(_applicationRepository);

        _logic = new BpdmBusinessLogic(portalRepository, _bpdmService);
    }

    #endregion

    #region Trigger PushLegalEntity

    [Fact]
    public async Task PushLegalEntity_WithoutExistingApplication_ThrowsNotFoundException()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.TO_DO},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(applicationId, checklist, Enumerable.Empty<ProcessStepTypeId>());
        SetupFakesForTrigger();

        // Act
        async Task Act() => await _logic.PushLegalEntity(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(Act);
        ex.Message.Should().Be($"Application {applicationId} does not exists.");
    }

    [Fact]
    public async Task PushLegalEntity_WithBpn_ThrowsConflictException()
    {
        // Arrange
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.TO_DO},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(IdWithoutZipCode, checklist, Enumerable.Empty<ProcessStepTypeId>());
        A.CallTo(() => _applicationRepository.GetBpdmDataForApplicationAsync(A<Guid>.That.Matches(x => x == IdWithoutZipCode)))
            .ReturnsLazily(() => (ValidCompanyId, new BpdmData(ValidCompanyName, null!, "Test", null!, null!, "Test", "test", null!, null!, new List<(BpdmIdentifierId UniqueIdentifierId, string Value)>())));

        // Act
        async Task Act() => await _logic.PushLegalEntity(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("BusinessPartnerNumber is already set");
    }

    [Fact]
    public async Task PushLegalEntity_WithoutAlpha2Code_ThrowsConflictException()
    {
        // Arrange
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.TO_DO},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(IdWithoutZipCode, checklist, Enumerable.Empty<ProcessStepTypeId>());
        A.CallTo(() => _applicationRepository.GetBpdmDataForApplicationAsync(A<Guid>.That.Matches(x => x == IdWithoutZipCode)))
            .ReturnsLazily(() => (ValidCompanyId, new BpdmData(ValidCompanyName, null!, null!, null!, null!, "Test", "test", null!, null!, new List<(BpdmIdentifierId UniqueIdentifierId, string Value)>())));

        // Act
        async Task Act() => await _logic.PushLegalEntity(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("Alpha2Code must not be empty");
    }

    [Fact]
    public async Task PushLegalEntity_WithoutCity_ThrowsConflictException()
    {
        // Arrange
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.TO_DO},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(IdWithoutZipCode, checklist, Enumerable.Empty<ProcessStepTypeId>());
        A.CallTo(() => _applicationRepository.GetBpdmDataForApplicationAsync(A<Guid>.That.Matches(x => x == IdWithoutZipCode)))
            .ReturnsLazily(() => (ValidCompanyId, new BpdmData(ValidCompanyName, null!, null!, "DE", null!, null, "test", null!, null!, new List<(BpdmIdentifierId UniqueIdentifierId, string Value)>())));

        // Act
        async Task Act() => await _logic.PushLegalEntity(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("City must not be empty");
    }

    [Fact]
    public async Task PushLegalEntity_WithoutStreetName_ThrowsConflictException()
    {
        // Arrange
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.TO_DO},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(IdWithoutZipCode, checklist, Enumerable.Empty<ProcessStepTypeId>());
        A.CallTo(() => _applicationRepository.GetBpdmDataForApplicationAsync(A<Guid>.That.Matches(x => x == IdWithoutZipCode)))
            .ReturnsLazily(() => (ValidCompanyId, new BpdmData(ValidCompanyName, null!, null!, "DE", null!, "TEST", null, null!, null!, new List<(BpdmIdentifierId UniqueIdentifierId, string Value)>())));

        // Act
        async Task Act() => await _logic.PushLegalEntity(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("StreetName must not be empty");
    }

    [Fact]
    public async Task PushLegalEntity_WithValidData_CallsExpected()
    {
        // Arrange
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.TO_DO},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(IdWithBpn, checklist, Enumerable.Empty<ProcessStepTypeId>());
        SetupFakesForTrigger();

        // Act
        var result = await _logic.PushLegalEntity(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Item3.Should().BeTrue();
        A.CallTo(() => _bpdmService.PutInputLegalEntity(
                A<BpdmTransferData>.That.Matches(x => x.ZipCode == "50668" && x.CompanyName == ValidCompanyName),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    #endregion

    #region Setup
    
    private void SetupFakesForTrigger()
    {
        var validData = _fixture.Build<BpdmData>()
            .With(x => x.ZipCode, "50668")
            .With(x => x.CompanyName, ValidCompanyName)
            .With(x => x.Alpha2Code, "DE")
            .With(x => x.City, "Test")
            .With(x => x.StreetName, "test")
            .With(x => x.BusinessPartnerNumber, (string?)null)
            .Create();

        A.CallTo(() => _applicationRepository.GetBpdmDataForApplicationAsync(A<Guid>.That.Matches(x => x == IdWithBpn)))
            .ReturnsLazily(() => (ValidCompanyId, validData));
        A.CallTo(() => _applicationRepository.GetBpdmDataForApplicationAsync(A<Guid>.That.Matches(x => x == IdWithStateCreated)))
            .ReturnsLazily(() => (ValidCompanyId, new BpdmData(ValidCompanyName, null!, null!, "DE", null!, "Test", "test", null!, null!, new List<(BpdmIdentifierId UniqueIdentifierId, string Value)>())));
        A.CallTo(() => _applicationRepository.GetBpdmDataForApplicationAsync(A<Guid>.That.Matches(x => x == IdWithoutZipCode)))
            .ReturnsLazily(() => (ValidCompanyId, new BpdmData(ValidCompanyName, null!, null!, null!, null!, "Test", "test", null!, null!, new List<(BpdmIdentifierId UniqueIdentifierId, string Value)>())));
        A.CallTo(() => _applicationRepository.GetBpdmDataForApplicationAsync(A<Guid>.That.Not.Matches(x => x == IdWithStateCreated || x == IdWithBpn || x == IdWithoutZipCode)))
            .ReturnsLazily(() => new ValueTuple<Guid, BpdmData>());

        A.CallTo(() => _bpdmService.PutInputLegalEntity(
                A<BpdmTransferData>.That.Matches(x => x.CompanyName == ValidCompanyName && x.ZipCode == "50668"),
                A<CancellationToken>._))
            .ReturnsLazily(() => true);
    }

    #endregion
}
