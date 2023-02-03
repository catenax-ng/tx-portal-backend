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

using System.Net;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
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

    private readonly ChecklistService _service;

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
        var entryStatusIds = _fixture.CreateMany<ApplicationChecklistEntryStatusId>(Enum.GetValues<ApplicationChecklistEntryStatusId>().Count()-1).ToImmutableArray();
        var processStepTypeId = _fixture.Create<ProcessStepTypeId>();
        var entryTypeIds = _fixture.CreateMany<ApplicationChecklistEntryTypeId>(Enum.GetValues<ApplicationChecklistEntryTypeId>().Count()-2).ToImmutableArray();
        var processStepTypeIds = _fixture.CreateMany<ProcessStepTypeId>(Enum.GetValues<ProcessStepTypeId>().Count()-2).ToImmutableArray();

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

    private IEnumerable<ApplicationChecklistEntryStatusId> ProduceEntryStatusIds(IEnumerable<ApplicationChecklistEntryStatusId> statusIds)
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
