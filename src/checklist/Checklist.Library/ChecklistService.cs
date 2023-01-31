/********************************************************************************
 * Copyright (c) 2021, 2023 Microsoft and BMW Group AG
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

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;

public class ChecklistService : IChecklistService
{
    private readonly IPortalRepositories _portalRepositories;

    public ChecklistService(
        IPortalRepositories portalRepositories)
    {
        _portalRepositories = portalRepositories;
    }

    public async Task<Guid> VerifyChecklistEntryAndProcessSteps(Guid applicationId, ApplicationChecklistEntryTypeId entryTypeId, ProcessStepTypeId processStepTypeId, IEnumerable<ProcessStepTypeId> nextProcessStepTypeIdsToCheck)
    {
        var checklistData = await _portalRepositories.GetInstance<IApplicationChecklistRepository>()
            .GetChecklistProcessStepData(applicationId, nextProcessStepTypeIdsToCheck.Append(processStepTypeId)).ConfigureAwait(false);

        if (!checklistData.IsValidApplicationId)
        {
            throw new NotFoundException($"application {applicationId} does not exist");
        }
        if (!checklistData.IsSubmitted)
        {
            throw new ConflictException($"application {applicationId} is not in status SUBMITTED");
        }
        if (checklistData.Checklist == null || checklistData.ProcessSteps == null)
        {
            throw new UnexpectedConditionException("checklist or processSteps should never be null here");
        }
        if (!checklistData.Checklist.Any(entry => entry.TypeId == entryTypeId && entry.StatusId == ApplicationChecklistEntryStatusId.TO_DO))
        {
            throw new ConflictException($"application {applicationId} does not have a checklist entry for {entryTypeId} in status {ApplicationChecklistEntryStatusId.TO_DO}");
        }
        var processStep = checklistData.ProcessSteps.SingleOrDefault(step => step.ProcessStepTypeId == processStepTypeId && step.ProcessStepStatusId == ProcessStepStatusId.TODO);
        if (processStep is null)
        {
            throw new ConflictException($"application {applicationId} checklist entry {entryTypeId}, process step {ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH} is not eligible to run");
        }
        var forbiddenSteps = checklistData.ProcessSteps.IntersectBy(nextProcessStepTypeIdsToCheck, step => step.ProcessStepTypeId).Where(step => step.ProcessStepStatusId == ProcessStepStatusId.TODO);
        if (forbiddenSteps.Any())
        {
            throw new ConflictException($"application {applicationId} checklist entry {entryTypeId}, process steps [{string.Join(", ", forbiddenSteps.Select(step => step.ProcessStepTypeId))}] are already scheduled to run");
        }
        return processStep.Id;
    }

    public void FinalizeChecklistEntryAndProcessSteps(Guid applicationId, ApplicationChecklistEntryTypeId entryTypeId, ApplicationChecklistEntryStatusId entryStatusId, Guid processStepId, IEnumerable<ProcessStepTypeId> nextProcessStepTypeIds)
    {
        _portalRepositories.GetInstance<IApplicationChecklistRepository>()
            .AttachAndModifyApplicationChecklist(applicationId, entryTypeId, checklist =>
            {
                checklist.ApplicationChecklistEntryStatusId = entryStatusId;
            });
        _portalRepositories.GetInstance<IProcessStepRepository>().AttachAndModifyProcessStep(processStepId, null, step => step.ProcessStepStatusId = ProcessStepStatusId.DONE);
        foreach (var processStepTypeId in nextProcessStepTypeIds)
        {
            var step = _portalRepositories.GetInstance<IProcessStepRepository>().CreateProcessStep(processStepTypeId, ProcessStepStatusId.TODO);
            _portalRepositories.GetInstance<IApplicationChecklistRepository>().CreateApplicationAssignedProcessStep(applicationId, step.Id);
        }
    }

    public IEnumerable<ProcessStep> ScheduleProcessSteps(Guid applicationId, IEnumerable<ProcessStep> processSteps, params ProcessStepTypeId[] processStepTypeIds)
    {
        foreach (var processStepTypeId in processStepTypeIds)
        {
            if (!processSteps.Any(step => step.ProcessStepTypeId == processStepTypeId))
            {
                var step = _portalRepositories.GetInstance<IProcessStepRepository>().CreateProcessStep(processStepTypeId, ProcessStepStatusId.TODO);
                _portalRepositories.GetInstance<IApplicationChecklistRepository>().CreateApplicationAssignedProcessStep(applicationId, step.Id);
                yield return step;
            }
        }
    }
}
