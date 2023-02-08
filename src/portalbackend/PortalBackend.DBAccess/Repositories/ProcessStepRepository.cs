/********************************************************************************
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

using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;

public class ProcessStepRepository : IProcessStepRepository
{
    private readonly PortalDbContext _context;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="portalDbContext">PortalDb context.</param>
    public ProcessStepRepository(PortalDbContext portalDbContext)
    {
        _context = portalDbContext;
    }

    public ProcessStep CreateProcessStep(ProcessStepTypeId processStepTypeId, ProcessStepStatusId processStepStatusId) =>
        _context.Add(new ProcessStep(Guid.NewGuid(), processStepTypeId, processStepStatusId, DateTimeOffset.UtcNow)).Entity;

    public void AttachAndModifyProcessStep(Guid processStepId, Action<ProcessStep>? initialize, Action<ProcessStep> modify)
    {
        var step = new ProcessStep(processStepId, default, default, default);
        initialize?.Invoke(step);
        _context.Attach(step);
        step.DateLastChanged = DateTimeOffset.UtcNow;
        modify(step);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<(ProcessStepTypeId ProcessStepTypeId, bool IsToDo)> GetProcessStepByApplicationIdInStatusTodo(Guid applicationId, ProcessStepTypeId[] processSteps) =>
        _context.ProcessSteps
            .Where(x => 
                x.ApplicationAssignedProcessStep!.CompanyApplication!.ApplicationStatusId == CompanyApplicationStatusId.SUBMITTED &&
                x.ApplicationAssignedProcessStep!.CompanyApplicationId == applicationId &&
                processSteps.Contains(x.ProcessStepTypeId))
            .GroupBy(x => x.ProcessStepTypeId)
            .Select(x => new ValueTuple<ProcessStepTypeId, bool>(x.Key, x.OrderByDescending(x => x.DateLastChanged).Last().ProcessStepStatusId == ProcessStepStatusId.TODO))
            .ToAsyncEnumerable();
}
