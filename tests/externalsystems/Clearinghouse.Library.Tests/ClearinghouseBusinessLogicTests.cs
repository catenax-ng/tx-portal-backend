﻿/********************************************************************************
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
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library.Custodian.Models;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.Tests;

public class ClearinghouseBusinessLogicTests
{
    private static readonly Guid IdWithoutBpn = new ("0a9bd7b1-e692-483e-8128-dbf52759c7a5");
    private static readonly Guid IdWithApplicationCreated = new ("7a8f5cb6-6ad2-4b88-a765-ff1888fcedbe");
    private static readonly Guid IdWithCustodianUnavailable = new ("beaa6de5-d411-4da8-850e-06047d3170be");

    private static readonly Guid IdWithBpn = new ("c244f79a-7faf-4c59-bb85-fbfdf72ce46f");
    private const string ValidBpn = "BPNL123698762345";
    private const string ValidDid = "thisisavaliddid";
    private const string FailingBpn = "FAILINGBPN";

    private readonly IFixture _fixture;
    
    private readonly IApplicationRepository _applicationRepository;
    private readonly IApplicationChecklistRepository _applicationChecklistRepository;
    private readonly IPortalRepositories _portalRepositories;
    
    private readonly ClearinghouseBusinessLogic _logic;
    private readonly IClearinghouseService _clearinghouseService;
    private readonly IChecklistService _checklistService;
    private readonly ICustodianBusinessLogic _custodianBusinessLogic;

    public ClearinghouseBusinessLogicTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization {ConfigureMembers = true});
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _applicationRepository = A.Fake<IApplicationRepository>();
        _applicationChecklistRepository = A.Fake<IApplicationChecklistRepository>();
        _portalRepositories = A.Fake<IPortalRepositories>();
        _clearinghouseService = A.Fake<IClearinghouseService>();
        _custodianBusinessLogic = A.Fake<ICustodianBusinessLogic>();
        _checklistService = A.Fake<IChecklistService>();
        

        A.CallTo(() => _portalRepositories.GetInstance<IApplicationRepository>()).Returns(_applicationRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IApplicationChecklistRepository>()).Returns(_applicationChecklistRepository);

        _logic = new ClearinghouseBusinessLogic(_portalRepositories, _clearinghouseService, _custodianBusinessLogic, _checklistService);
    }
    
    #region HandleStartClearingHouse

    [Fact]
    public async Task HandleStartClearingHouse_WithNotExistingApplication_ThrowsConflictException()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                { ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE },
                { ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.DONE },
                { ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE },
                { ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ApplicationChecklistEntryStatusId.TO_DO },
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(applicationId, checklist, Enumerable.Empty<ProcessStep>());
        SetupForHandleStartClearingHouse();

        // Act
        async Task Act() => await _logic.HandleStartClearingHouse(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"Decentralized Identifier for application {context.ApplicationId} is not set");
    }

    [Fact]
    public async Task HandleStartClearingHouse_WithCreatedApplication_ThrowsConflictException()
    {
        // Arrange
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ApplicationChecklistEntryStatusId.TO_DO},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(IdWithApplicationCreated, checklist, Enumerable.Empty<ProcessStep>());
        SetupForHandleStartClearingHouse();

        // Act
        async Task Act() => await _logic.HandleStartClearingHouse(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be($"CompanyApplication {IdWithApplicationCreated} is not in status SUBMITTED");
    }

    [Fact]
    public async Task HandleStartClearingHouse_WithBpnNull_ThrowsConflictException()
    {
        // Arrange
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ApplicationChecklistEntryStatusId.TO_DO},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(IdWithoutBpn, checklist, Enumerable.Empty<ProcessStep>());
        SetupForHandleStartClearingHouse();

        // Act
        async Task Act() => await _logic.HandleStartClearingHouse(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(Act);
        ex.Message.Should().Be("BusinessPartnerNumber is null");
    }

    [Fact]
    public async Task HandleStartClearingHouse_WithValidData_CallsExpected()
    {
        // Arrange
        var checklist = new Dictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>
            {
                {ApplicationChecklistEntryTypeId.REGISTRATION_VERIFICATION, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.DONE},
                {ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ApplicationChecklistEntryStatusId.TO_DO},
            }
            .ToImmutableDictionary();
        var context = new IChecklistService.WorkerChecklistProcessStepData(IdWithBpn, checklist, Enumerable.Empty<ProcessStep>());
        SetupForHandleStartClearingHouse();

        // Act
        var result = await _logic.HandleStartClearingHouse(context, CancellationToken.None).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _clearinghouseService.TriggerCompanyDataPost(A<ClearinghouseTransferData>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _checklistService.ScheduleProcessSteps(context, A<IEnumerable<ProcessStepTypeId>>.That.Matches(x => x.Single() == ProcessStepTypeId.END_CLEARING_HOUSE))).MustHaveHappenedOnceExactly();
        result.Item3.Should().BeTrue();
    }

    #endregion
    
    #region ProcessClearinghouseResponse

    [Fact]
    public async Task ProcessClearinghouseResponseAsync_WithConfirmation_UpdatesEntry()
    {
        // Arrange
        var entry = new ApplicationChecklistEntry(IdWithBpn, ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.TO_DO, DateTimeOffset.UtcNow);
        var data = _fixture.Build<ClearinghouseResponseData>()
            .With(x => x.Status, ClearinghouseResponseStatus.CONFIRM)
            .With(x => x.Message, (string?)null)
            .Create();
        SetupForProcessClearinghouseResponse(entry);

        // Act
        await _logic.ProcessEndClearinghouse(IdWithBpn, data, CancellationToken.None).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _checklistService.FinalizeChecklistEntryAndProcessSteps(A<IChecklistService.ManualChecklistProcessStepData>._, A<Action<ApplicationChecklistEntry>>._, A<IEnumerable<ProcessStepTypeId>>.That.Matches(x => x.Count(y => y == ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP) == 1))).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustNotHaveHappened();
        entry.Comment.Should().BeNull();
        entry.ApplicationChecklistEntryStatusId.Should().Be(ApplicationChecklistEntryStatusId.DONE);
    }

    [Fact]
    public async Task ProcessClearinghouseResponseAsync_WithDecline_UpdatesEntry()
    {
        // Arrange
        var entry = new ApplicationChecklistEntry(IdWithBpn, ApplicationChecklistEntryTypeId.IDENTITY_WALLET, ApplicationChecklistEntryStatusId.TO_DO, DateTimeOffset.UtcNow);
        var data = _fixture.Build<ClearinghouseResponseData>()
            .With(x => x.Status, ClearinghouseResponseStatus.DECLINE)
            .With(x => x.Message, "Comment about the error")
            .Create();
        SetupForProcessClearinghouseResponse(entry);

        // Act
        await _logic.ProcessEndClearinghouse(IdWithBpn, data, CancellationToken.None).ConfigureAwait(false);

        // Assert
        A.CallTo(() => _checklistService.FinalizeChecklistEntryAndProcessSteps(A<IChecklistService.ManualChecklistProcessStepData>._, A<Action<ApplicationChecklistEntry>>._, null)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _portalRepositories.SaveAsync()).MustNotHaveHappened();
        entry.Comment.Should().Be("Comment about the error");
        entry.ApplicationChecklistEntryStatusId.Should().Be(ApplicationChecklistEntryStatusId.FAILED);
    }

    #endregion
    
    #region Setup
    
    private void SetupForHandleStartClearingHouse()
    {
        A.CallTo(() => _custodianBusinessLogic.GetWalletByBpnAsync(A<Guid>.That.Matches(x => x == IdWithoutBpn || x == IdWithBpn || x == IdWithApplicationCreated), A<CancellationToken>._))
            .ReturnsLazily(() => new WalletData("Name", ValidBpn, ValidDid, DateTime.UtcNow, false, null));
        A.CallTo(() => _custodianBusinessLogic.GetWalletByBpnAsync(IdWithCustodianUnavailable, A<CancellationToken>._))
            .ReturnsLazily(() => (WalletData?)null);
        A.CallTo(() => _custodianBusinessLogic.GetWalletByBpnAsync(A<Guid>.That.Not.Matches(x => x == IdWithoutBpn || x == IdWithBpn || x == IdWithApplicationCreated || x== IdWithCustodianUnavailable), A<CancellationToken>._))
            .ReturnsLazily(() => new WalletData("Name", ValidBpn, null, DateTime.UtcNow, false, null));

        var participantDetailsWithoutBpn = _fixture.Build<ParticipantDetails>()
            .With(x => x.Bpn, (string?)null)
            .Create();
        var clearinghouseDataWithoutBpn = _fixture.Build<ClearinghouseData>()
            .With(x => x.ApplicationStatusId, CompanyApplicationStatusId.SUBMITTED)
            .With(x => x.ParticipantDetails, participantDetailsWithoutBpn)
            .Create();
        var participantDetails = _fixture.Build<ParticipantDetails>()
            .With(x => x.Bpn, ValidBpn)
            .Create();
        var clearinghouseData = _fixture.Build<ClearinghouseData>()
            .With(x => x.ApplicationStatusId, CompanyApplicationStatusId.SUBMITTED)
            .With(x => x.ParticipantDetails, participantDetails)
            .Create();
        var chDataWithApplicationCreated = _fixture.Build<ClearinghouseData>()
            .With(x => x.ApplicationStatusId, CompanyApplicationStatusId.CREATED)
            .Create();

        A.CallTo(() => _applicationRepository.GetClearinghouseDataForApplicationId(IdWithoutBpn))
            .ReturnsLazily(() => clearinghouseDataWithoutBpn);
        A.CallTo(() => _applicationRepository.GetClearinghouseDataForApplicationId(IdWithBpn))
            .ReturnsLazily(() => clearinghouseData);
        A.CallTo(() => _applicationRepository.GetClearinghouseDataForApplicationId(IdWithApplicationCreated))
            .ReturnsLazily(() => chDataWithApplicationCreated);
        A.CallTo(() => _applicationRepository.GetClearinghouseDataForApplicationId(A<Guid>.That.Not.Matches(x => x == IdWithoutBpn || x == IdWithBpn || x == IdWithApplicationCreated || x == IdWithCustodianUnavailable)))
            .ReturnsLazily(() => (ClearinghouseData?)null);
    }

    private void SetupForProcessClearinghouseResponse(ApplicationChecklistEntry? applicationChecklistEntry = null)
    {
        A.CallTo(() => _checklistService.FinalizeChecklistEntryAndProcessSteps(A<IChecklistService.ManualChecklistProcessStepData>._, A<Action<ApplicationChecklistEntry>>._, A<IEnumerable<ProcessStepTypeId>>._))
            .Invokes((IChecklistService.ManualChecklistProcessStepData _, Action<ApplicationChecklistEntry> modifyApplicationChecklistEntry, IEnumerable<ProcessStepTypeId> _) =>
            {
                applicationChecklistEntry.DateLastChanged = DateTimeOffset.UtcNow;
                modifyApplicationChecklistEntry.Invoke(applicationChecklistEntry);
            });

        A.CallTo(() => _checklistService.VerifyChecklistEntryAndProcessSteps(
                IdWithBpn, 
                ApplicationChecklistEntryTypeId.CLEARING_HOUSE, 
                A<IEnumerable<ApplicationChecklistEntryStatusId>>._, 
                ProcessStepTypeId.END_CLEARING_HOUSE, 
                A<IEnumerable<ApplicationChecklistEntryTypeId>?>._,
                A<IEnumerable<ProcessStepTypeId>?>._))
            .ReturnsLazily(() => new IChecklistService.ManualChecklistProcessStepData(Guid.Empty, Guid.Empty, ApplicationChecklistEntryTypeId.CLEARING_HOUSE, ImmutableDictionary<ApplicationChecklistEntryTypeId, ApplicationChecklistEntryStatusId>.Empty, new List<ProcessStep>()));
    }

    #endregion
}
