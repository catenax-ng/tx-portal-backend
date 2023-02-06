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
using Org.Eclipse.TractusX.Portal.Backend.ApplicationActivation.Library;
using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.SdFactory.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Tests.Shared;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Worker.Tests;

public class ChecklistProcessorTests
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IApplicationChecklistRepository _applicationChecklistRepository;
    private readonly IProcessStepRepository _processStepRepository;
    private readonly IBpdmBusinessLogic _bpdmBusinessLogic;
    private readonly ICustodianBusinessLogic _custodianBusinessLogic;
    private readonly IClearinghouseBusinessLogic _clearinghouseBusinessLogic;
    private readonly ISdFactoryBusinessLogic _sdFactoryBusinessLogic;
    private readonly IApplicationActivationService _applicationActivationService;
    private readonly IMockLogger<ChecklistProcessor> _mockLogger;
    private readonly ILogger<ChecklistProcessor> _logger;
    private readonly ChecklistProcessor _processor;
    private readonly IFixture _fixture;

    public ChecklistProcessorTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization {ConfigureMembers = true});
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b =>_fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _portalRepositories = A.Fake<IPortalRepositories>();
        _applicationChecklistRepository = A.Fake<IApplicationChecklistRepository>();
        _processStepRepository = A.Fake<IProcessStepRepository>();

        _bpdmBusinessLogic = A.Fake<IBpdmBusinessLogic>();
        _custodianBusinessLogic = A.Fake<ICustodianBusinessLogic>();
        _clearinghouseBusinessLogic = A.Fake<IClearinghouseBusinessLogic>();
        _sdFactoryBusinessLogic = A.Fake<ISdFactoryBusinessLogic>();
        _applicationActivationService = A.Fake<IApplicationActivationService>();

        _mockLogger = A.Fake<IMockLogger<ChecklistProcessor>>();
        _logger = new MockLogger<ChecklistProcessor>(_mockLogger);

        A.CallTo(() => _portalRepositories.GetInstance<IApplicationChecklistRepository>())
            .Returns(_applicationChecklistRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IProcessStepRepository>())
            .Returns(_processStepRepository);

        _processor = new ChecklistProcessor(
            _portalRepositories,
            _bpdmBusinessLogic,
            _custodianBusinessLogic,
            _clearinghouseBusinessLogic,
            _sdFactoryBusinessLogic,
            _applicationActivationService,
            _logger);
    }

    [Fact]
    public async Task ProcessChecklist_IgnoringDuplicates_Success()
    {
        // Arrange
        var checklist = Enum.GetValues<ApplicationChecklistEntryTypeId>()
            .Select(typeId => _fixture
                .Build<(ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId)>()
                .With(x => x.Item1, typeId)
                .Create())
            .ToImmutableArray();

        var processSteps = _fixture.CreateMany<int>(100)
            .Select(_ =>
                _fixture.Build<ProcessStep>()
                    .With(x => x.ProcessStepStatusId, ProcessStepStatusId.TODO)
                    .Create())
            .ToImmutableArray();

        A.CallTo(() => _bpdmBusinessLogic.PushLegalEntity(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                new [] {
                    ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH,
                    ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH
                },
                true))
            .Once()
            .Then
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                null,
                true));

        A.CallTo(() => _bpdmBusinessLogic.HandlePullLegalEntity(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                new [] {
                    ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL,
                    ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL
                },
                true))
            .Once()
            .Then
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                null,
                true));

        A.CallTo(() => _custodianBusinessLogic.CreateIdentityWalletAsync(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                new [] {
                    ProcessStepTypeId.CREATE_IDENTITY_WALLET,
                    ProcessStepTypeId.CREATE_IDENTITY_WALLET
                },
                true))
            .Once()
            .Then
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                null,
                true));

        A.CallTo(() => _clearinghouseBusinessLogic.HandleStartClearingHouse(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                new [] {
                    ProcessStepTypeId.START_CLEARING_HOUSE,
                    ProcessStepTypeId.START_CLEARING_HOUSE
                },
                true))
            .Once()
            .Then
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                null,
                true));

        A.CallTo(() => _sdFactoryBusinessLogic.RegisterSelfDescription(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                new [] {
                    ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP,
                    ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP
                },
                true))
            .Once()
            .Then
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                null,
                true));

        A.CallTo(() => _applicationActivationService.HandleApplicationActivation(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                new [] {
                    ProcessStepTypeId.ACTIVATE_APPLICATION,
                    ProcessStepTypeId.ACTIVATE_APPLICATION
                },
                true))
            .Once()
            .Then
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                null,
                true));

        var manualStepTypeIds = new [] {
                ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL,
                ProcessStepTypeId.END_CLEARING_HOUSE,
                ProcessStepTypeId.VERIFY_REGISTRATION,
            }.ToImmutableArray();

        var automaticStepTypeIds = Enum.GetValues<ProcessStepTypeId>().Except(manualStepTypeIds).ToImmutableArray();

        // Act
        var result = await _processor.ProcessChecklist(Guid.NewGuid(), checklist, processSteps, CancellationToken.None).ToListAsync().ConfigureAwait(false);

        // Assert
        result.Should().HaveCount(automaticStepTypeIds.Length * 2);

        A.CallTo(() => _bpdmBusinessLogic.PushLegalEntity(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        A.CallTo(() => _bpdmBusinessLogic.HandlePullLegalEntity(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        A.CallTo(() => _custodianBusinessLogic.CreateIdentityWalletAsync(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        A.CallTo(() => _clearinghouseBusinessLogic.HandleStartClearingHouse(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        A.CallTo(() => _sdFactoryBusinessLogic.RegisterSelfDescription(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        A.CallTo(() => _applicationActivationService.HandleApplicationActivation(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.VERIFY_REGISTRATION, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.END_CLEARING_HOUSE, ProcessStepStatusId.TODO)).MustNotHaveHappened();

        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_IDENTITY_WALLET, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.START_CLEARING_HOUSE, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.ACTIVATE_APPLICATION, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();

        A.CallTo(()=> _processStepRepository.AttachAndModifyProcessStep(A<Guid>._, A<Action<ProcessStep>>._, A<Action<ProcessStep>>._)).MustHaveHappened(processSteps.Where(step => automaticStepTypeIds.Contains(step.ProcessStepTypeId)).Count() + 6, Times.Exactly);
    }

    [Fact]
    public async Task ProcessChecklist_IgnoreManualSteps()
    {
        // Arrange
        var checklist = Enum.GetValues<ApplicationChecklistEntryTypeId>()
            .Select(typeId => _fixture
                .Build<(ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId)>()
                .With(x => x.Item1, typeId)
                .Create())
            .ToImmutableArray();

        var processSteps = new [] {
            ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL,
            ProcessStepTypeId.END_CLEARING_HOUSE,
            ProcessStepTypeId.VERIFY_REGISTRATION
        }.Select(steptTypeId => new ProcessStep(Guid.NewGuid(), steptTypeId, ProcessStepStatusId.TODO)).ToImmutableArray();

        // Act
        var result = await _processor.ProcessChecklist(Guid.NewGuid(), checklist, processSteps, CancellationToken.None).ToListAsync().ConfigureAwait(false);

        // Assert
        result.Should().BeEmpty();

        A.CallTo(() => _bpdmBusinessLogic.HandlePullLegalEntity(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _custodianBusinessLogic.CreateIdentityWalletAsync(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _clearinghouseBusinessLogic.HandleStartClearingHouse(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _sdFactoryBusinessLogic.RegisterSelfDescription(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _applicationActivationService.HandleApplicationActivation(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();
    
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.VERIFY_REGISTRATION, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.END_CLEARING_HOUSE, ProcessStepStatusId.TODO)).MustNotHaveHappened();

        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_IDENTITY_WALLET, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.START_CLEARING_HOUSE, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.ACTIVATE_APPLICATION, ProcessStepStatusId.TODO)).MustNotHaveHappened();
    
        A.CallTo(()=> _processStepRepository.AttachAndModifyProcessStep(A<Guid>._, A<Action<ProcessStep>>._, A<Action<ProcessStep>>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ProcessChecklist_ScheduleManualSteps_IgnoringDuplicates()
    {
        // Arrange
        var checklist = Enum.GetValues<ApplicationChecklistEntryTypeId>()
            .Select(typeId => _fixture
                .Build<(ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId)>()
                .With(x => x.Item1, typeId)
                .Create())
            .ToImmutableArray();

        var processSteps = new [] {
            ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL
        }.Select(steptTypeId => new ProcessStep(Guid.NewGuid(), steptTypeId, ProcessStepStatusId.TODO)).ToImmutableArray();

        A.CallTo(() => _bpdmBusinessLogic.HandlePullLegalEntity(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .Returns((
                (ApplicationChecklistEntry entry) => { entry.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.DONE; },
                new [] {
                    ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL,
                    ProcessStepTypeId.END_CLEARING_HOUSE,
                    ProcessStepTypeId.VERIFY_REGISTRATION,
                    ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL,
                    ProcessStepTypeId.END_CLEARING_HOUSE,
                    ProcessStepTypeId.VERIFY_REGISTRATION
                },
                true));

        // Act
        var result = await _processor.ProcessChecklist(Guid.NewGuid(), checklist, processSteps, CancellationToken.None).ToListAsync().ConfigureAwait(false);

        // Assert
        result.Should().HaveCount(1);

        A.CallTo(() => _bpdmBusinessLogic.HandlePullLegalEntity(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _custodianBusinessLogic.CreateIdentityWalletAsync(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _clearinghouseBusinessLogic.HandleStartClearingHouse(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _sdFactoryBusinessLogic.RegisterSelfDescription(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _applicationActivationService.HandleApplicationActivation(A<IChecklistService.WorkerChecklistProcessStepData>._,A<CancellationToken>._))
            .MustNotHaveHappened();

        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.VERIFY_REGISTRATION, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.END_CLEARING_HOUSE, ProcessStepStatusId.TODO)).MustHaveHappenedOnceExactly();

        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_IDENTITY_WALLET, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.START_CLEARING_HOUSE, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP, ProcessStepStatusId.TODO)).MustNotHaveHappened();
        A.CallTo(() => _processStepRepository.CreateProcessStep(ProcessStepTypeId.ACTIVATE_APPLICATION, ProcessStepStatusId.TODO)).MustNotHaveHappened();

        A.CallTo(()=> _processStepRepository.AttachAndModifyProcessStep(A<Guid>._, A<Action<ProcessStep>>._, A<Action<ProcessStep>>._)).MustHaveHappenedOnceExactly();
    }
}
