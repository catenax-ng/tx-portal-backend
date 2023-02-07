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
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using System.Collections.Immutable;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Library.Tests;

public class ChecklistServiceTests
{
    private readonly IFixture _fixture;
    
    private readonly IApplicationChecklistRepository _applicationChecklistRepository;
    private readonly IProcessStepRepository _processStepRepository;
    private readonly IPortalRepositories _portalRepositories;

    private readonly IChecklistService _service;

    public ChecklistServiceTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization {ConfigureMembers = true});
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _applicationChecklistRepository = A.Fake<IApplicationChecklistRepository>();
        _processStepRepository = A.Fake<IProcessStepRepository>();
        _portalRepositories = A.Fake<IPortalRepositories>();

        A.CallTo(() => _portalRepositories.GetInstance<IApplicationChecklistRepository>()).Returns(_applicationChecklistRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IProcessStepRepository>()).Returns(_processStepRepository);

        _service = new ChecklistService(_portalRepositories);
    }
    
    #region VerifyChecklistEntryAndProcessSteps
    
    [Fact]
    public async Task VerifyChecklistEntryAndProcessSteps()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var entryTypeId = _fixture.Create<ApplicationChecklistEntryTypeId>();
        var entryStatusIds = _fixture.CreateMany<ApplicationChecklistEntryStatusId>(Enum.GetValues<ApplicationChecklistEntryStatusId>().Length-1).ToImmutableArray();
        var processStepTypeId = _fixture.Create<ProcessStepTypeId>();
        var entryTypeIds = _fixture.CreateMany<ApplicationChecklistEntryTypeId>(Enum.GetValues<ApplicationChecklistEntryTypeId>().Length-2).ToImmutableArray();
        var processStepTypeIds = _fixture.CreateMany<ProcessStepTypeId>(Enum.GetValues<ProcessStepTypeId>().Length-2).ToImmutableArray();

        IEnumerable<(ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId)>? checklistData = null;
        IEnumerable<ProcessStep>? processSteps = null;

        A.CallTo(() => _applicationChecklistRepository.GetChecklistProcessStepData(A<Guid>._, A<IEnumerable<ApplicationChecklistEntryTypeId>>._, A<IEnumerable<ProcessStepTypeId>>._))
            .ReturnsLazily((Guid appId, IEnumerable<ApplicationChecklistEntryTypeId> entryTypes, IEnumerable<ProcessStepTypeId> processStepTypes) => {
                checklistData = entryTypes.Append(entryTypeId).Distinct().Zip(ProduceEntryStatusIds(entryStatusIds), (typeId, statusId) => (typeId,statusId)).ToImmutableArray();
                processSteps = processStepTypes.Select(typeId => new ProcessStep(Guid.NewGuid(), typeId, ProcessStepStatusId.TODO)).ToImmutableArray();
                return (
                    applicationId == appId,
                    true,
                    checklistData,
                    processSteps);
            });

        // Act
        var result = await _service.VerifyChecklistEntryAndProcessSteps(applicationId, entryTypeId, entryStatusIds, processStepTypeId, entryTypeIds, processStepTypeIds).ConfigureAwait(false);
        // Assert
        result.Should().NotBeNull();

        result.ApplicationId.Should().Be(applicationId);
        result.ProcessStepId.Should().NotBeEmpty();
        var processStep = processSteps?.SingleOrDefault(step => step.Id == result.ProcessStepId);
        processStep.Should().NotBeNull();
        processStep!.ProcessStepTypeId.Should().Be(processStepTypeId);
        processStep.ProcessStepStatusId.Should().Be(ProcessStepStatusId.TODO);
        result.EntryTypeId.Should().Be(entryTypeId);
        result.Checklist.Should().ContainKey(entryTypeId);
        result.Checklist.Should().ContainKeys(entryTypeIds);
        result.ProcessSteps.Select(step => step.ProcessStepTypeId).Should().Contain(processStepTypeIds);
        result.ProcessSteps.Select(step => step.ProcessStepStatusId).Should().AllSatisfy(statusId => statusId.Should().Be(ProcessStepStatusId.TODO));
    }

    [Fact]
    public async Task VerifyChecklistEntry_InvalidApplicationId_Throws()
    {
        // Arrange
        // Arrange
        var applicationId = Guid.NewGuid();
        var entryTypeId = _fixture.Create<ApplicationChecklistEntryTypeId>();
        var entryStatusIds = _fixture.CreateMany<ApplicationChecklistEntryStatusId>(Enum.GetValues<ApplicationChecklistEntryStatusId>().Length-1).ToImmutableArray();
        var processStepTypeId = _fixture.Create<ProcessStepTypeId>();
        var entryTypeIds = _fixture.CreateMany<ApplicationChecklistEntryTypeId>(Enum.GetValues<ApplicationChecklistEntryTypeId>().Length-2).ToImmutableArray();
        var processStepTypeIds = _fixture.CreateMany<ProcessStepTypeId>(Enum.GetValues<ProcessStepTypeId>().Length-2).ToImmutableArray();

        // (bool IsValidApplicationId, bool IsSubmitted, IEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId)
        A.CallTo(() => _applicationChecklistRepository.GetChecklistProcessStepData(A<Guid>._, A<IEnumerable<ApplicationChecklistEntryTypeId>>._, A<IEnumerable<ProcessStepTypeId>>._))
            .Returns((false, false, null, null));

        var Act = () => _service.VerifyChecklistEntryAndProcessSteps(applicationId, entryTypeId, entryStatusIds, processStepTypeId, entryTypeIds, processStepTypeIds);

        // Act
        var result = await Assert.ThrowsAsync<NotFoundException>(Act).ConfigureAwait(false);;

        // Assert
        result.Message.Should().Be($"application {applicationId} does not exist");
    }

    [Fact]
    public async Task VerifyChecklistEntry_InvalidApplicationStatus_Throws()
    {
        // Arrange
        // Arrange
        var applicationId = Guid.NewGuid();
        var entryTypeId = _fixture.Create<ApplicationChecklistEntryTypeId>();
        var entryStatusIds = _fixture.CreateMany<ApplicationChecklistEntryStatusId>(Enum.GetValues<ApplicationChecklistEntryStatusId>().Length-1).ToImmutableArray();
        var processStepTypeId = _fixture.Create<ProcessStepTypeId>();
        var entryTypeIds = _fixture.CreateMany<ApplicationChecklistEntryTypeId>(Enum.GetValues<ApplicationChecklistEntryTypeId>().Length-2).ToImmutableArray();
        var processStepTypeIds = _fixture.CreateMany<ProcessStepTypeId>(Enum.GetValues<ProcessStepTypeId>().Length-2).ToImmutableArray();

        // (bool IsValidApplicationId, bool IsSubmitted, IEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId)
        A.CallTo(() => _applicationChecklistRepository.GetChecklistProcessStepData(A<Guid>._, A<IEnumerable<ApplicationChecklistEntryTypeId>>._, A<IEnumerable<ProcessStepTypeId>>._))
            .Returns((true, false, null, null));

        var Act = () => _service.VerifyChecklistEntryAndProcessSteps(applicationId, entryTypeId, entryStatusIds, processStepTypeId, entryTypeIds, processStepTypeIds);

        // Act
        var result = await Assert.ThrowsAsync<ConflictException>(Act).ConfigureAwait(false);;

        // Assert
        result.Message.Should().Be($"application {applicationId} is not in status SUBMITTED");
    }

    [Fact]
    public async Task VerifyChecklistEntry_UnexpectedEntryData_Throws()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var entryTypeId = _fixture.Create<ApplicationChecklistEntryTypeId>();
        var entryStatusIds = _fixture.CreateMany<ApplicationChecklistEntryStatusId>(1).ToImmutableArray();
        var processStepTypeId = _fixture.Create<ProcessStepTypeId>();
        IEnumerable<ApplicationChecklistEntryTypeId>? entryTypeIds = null;
        IEnumerable<ProcessStepTypeId>? processStepTypeIds = null;

        var entryData = Enum.GetValues<ApplicationChecklistEntryTypeId>().Except(new [] { entryTypeId }).Select(entryTypeId => (entryTypeId, entryStatusIds.First())).ToImmutableArray();
        var processSteps = new ProcessStep [] { new (Guid.NewGuid(), processStepTypeId, ProcessStepStatusId.TODO) };

        A.CallTo(() => _applicationChecklistRepository.GetChecklistProcessStepData(A<Guid>._, A<IEnumerable<ApplicationChecklistEntryTypeId>>._, A<IEnumerable<ProcessStepTypeId>>._))
            .Returns((true, true, entryData, processSteps));

        var Act = () => _service.VerifyChecklistEntryAndProcessSteps(applicationId, entryTypeId, entryStatusIds, processStepTypeId, entryTypeIds, processStepTypeIds);

        // Act
        var result = await Assert.ThrowsAsync<ConflictException>(Act).ConfigureAwait(false);;

        // Assert
        result.Message.Should().Be($"application {applicationId} does not have a checklist entry for {entryTypeId} in status {string.Join(", ",entryStatusIds)}");
    }

    [Fact]
    public async Task VerifyChecklistEntry_UnexpectedEntryStatusData_Throws()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var entryTypeId = _fixture.Create<ApplicationChecklistEntryTypeId>();
        var entryStatusIds = _fixture.CreateMany<ApplicationChecklistEntryStatusId>(1).ToImmutableArray();
        var processStepTypeId = _fixture.Create<ProcessStepTypeId>();
        IEnumerable<ApplicationChecklistEntryTypeId>? entryTypeIds = null;
        IEnumerable<ProcessStepTypeId>? processStepTypeIds = null;

        var entryData = new [] { (entryTypeId, Enum.GetValues<ApplicationChecklistEntryStatusId>().Except(entryStatusIds).First()) };
        var processSteps = new ProcessStep [] { new (Guid.NewGuid(), processStepTypeId, ProcessStepStatusId.TODO) };

        A.CallTo(() => _applicationChecklistRepository.GetChecklistProcessStepData(A<Guid>._, A<IEnumerable<ApplicationChecklistEntryTypeId>>._, A<IEnumerable<ProcessStepTypeId>>._))
            .Returns((true, true, entryData, processSteps));

        var Act = () => _service.VerifyChecklistEntryAndProcessSteps(applicationId, entryTypeId, entryStatusIds, processStepTypeId, entryTypeIds, processStepTypeIds);

        // Act
        var result = await Assert.ThrowsAsync<ConflictException>(Act).ConfigureAwait(false);;

        // Assert
        result.Message.Should().Be($"application {applicationId} does not have a checklist entry for {entryTypeId} in status {string.Join(", ",entryStatusIds)}");
    }

    [Fact]
    public async Task VerifyChecklistEntry_UnexpectedProcessStepData_Throws()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var entryTypeId = _fixture.Create<ApplicationChecklistEntryTypeId>();
        var entryStatusIds = _fixture.CreateMany<ApplicationChecklistEntryStatusId>(1).ToImmutableArray();
        var processStepTypeId = _fixture.Create<ProcessStepTypeId>();
        IEnumerable<ApplicationChecklistEntryTypeId>? entryTypeIds = null;
        IEnumerable<ProcessStepTypeId>? processStepTypeIds = null;

        var entryData = new [] { (entryTypeId, entryStatusIds.First()) };
        var processSteps = new ProcessStep [] { new (Guid.NewGuid(), Enum.GetValues<ProcessStepTypeId>().Except( new [] { processStepTypeId } ).First(), ProcessStepStatusId.TODO) };

        A.CallTo(() => _applicationChecklistRepository.GetChecklistProcessStepData(A<Guid>._, A<IEnumerable<ApplicationChecklistEntryTypeId>>._, A<IEnumerable<ProcessStepTypeId>>._))
            .Returns((true, true, entryData, processSteps));

        var Act = () => _service.VerifyChecklistEntryAndProcessSteps(applicationId, entryTypeId, entryStatusIds, processStepTypeId, entryTypeIds, processStepTypeIds);

        // Act
        var result = await Assert.ThrowsAsync<ConflictException>(Act).ConfigureAwait(false);;

        // Assert
        result.Message.Should().Be($"application {applicationId} checklist entry {entryTypeId}, process step {processStepTypeId} is not eligible to run");
    }

    [Fact]
    public async Task VerifyChecklistEntry_UnexpectedProcessStepStatus_Throws()
    {
        // Arrange
        var applicationId = Guid.NewGuid();
        var entryTypeId = _fixture.Create<ApplicationChecklistEntryTypeId>();
        var entryStatusIds = _fixture.CreateMany<ApplicationChecklistEntryStatusId>(1).ToImmutableArray();
        var processStepTypeId = _fixture.Create<ProcessStepTypeId>();
        IEnumerable<ApplicationChecklistEntryTypeId>? entryTypeIds = null;
        IEnumerable<ProcessStepTypeId>? processStepTypeIds = null;

        var entryData = new [] { (entryTypeId, entryStatusIds.First()) };
        var processSteps = new ProcessStep [] { new (Guid.NewGuid(), processStepTypeId, ProcessStepStatusId.SKIPPED) };

        A.CallTo(() => _applicationChecklistRepository.GetChecklistProcessStepData(A<Guid>._, A<IEnumerable<ApplicationChecklistEntryTypeId>>._, A<IEnumerable<ProcessStepTypeId>>._))
            .Returns((true, true, entryData, processSteps));

        var Act = () => _service.VerifyChecklistEntryAndProcessSteps(applicationId, entryTypeId, entryStatusIds, processStepTypeId, entryTypeIds, processStepTypeIds);

        // Act
        var result = await Assert.ThrowsAsync<UnexpectedConditionException>(Act).ConfigureAwait(false);;

        // Assert
        result.Message.Should().Be($"processSteps should never have other status then TODO here");
    }

    private static IEnumerable<ApplicationChecklistEntryStatusId> ProduceEntryStatusIds(IEnumerable<ApplicationChecklistEntryStatusId> statusIds)
    {
        while (true)
        {
            foreach (var statusId in statusIds)
            {
                yield return statusId;
            }
        }
    }
    #endregion
}
