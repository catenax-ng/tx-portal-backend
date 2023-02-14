/********************************************************************************
 * Copyright (c) 2021,2023 Microsoft and BMW Group AG
 * Copyright (c) 2021,2023 Contributors to the Eclipse Foundation
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
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Worker.Library;

public class ProcessExecutor : IProcessExecutor
{
    private readonly IProcessStepRepository _processStepRepository;
    private readonly IProcessTypeExecutorFactory _executorFactory;

    ProcessExecutor(IPortalRepositories portalRepositories, IProcessTypeExecutorFactory executorFactory)
    {
        _processStepRepository = portalRepositories.GetInstance<IProcessStepRepository>();
        _executorFactory = executorFactory;
    }

    public async IAsyncEnumerable<bool> ExecuteProcess(Guid processId)
    {
        var processStepData = await _processStepRepository.GetProcessStepData(processId).ConfigureAwait(false);
        if (processStepData == default)
        {
            throw new ConflictException($"process {processId} does not exist");
        }

        var executor = _executorFactory.GetInstance(processStepData.ProcessTypeId);
        var groupedSteps = processStepData.ProcessSteps.GroupBy(step => step.ProcessStepTypeId).ToList();

        var context = new ProcessContext(
            processId,
            groupedSteps.ToDictionary(g => g.Key, g => g.Select(x => x.ProcessStepId)),
            new Queue<ProcessStepTypeId>(groupedSteps.Select(g => g.Key).Where(x => executor.IsExecutableStepTypeId(x))),
            executor);

        var (modified, initialStepTypeIds) = await executor.InitializeProcess(processId).ConfigureAwait(false);

        modified |= ScheduleProcessStepTypeIds(initialStepTypeIds, context);

        if (modified)
        {
            yield return true;
            modified = false;
        }

        while (context.ExecutableStepTypeIds.TryDequeue(out var stepTypeId))
        {
            ProcessStepStatusId resultStepStatusId;
            IEnumerable<ProcessStepTypeId>? scheduleStepTypeIds;
            IEnumerable<ProcessStepTypeId>? skipStepTypeIds;
            try
            {
                (modified, resultStepStatusId, scheduleStepTypeIds, skipStepTypeIds) = await executor.ExecuteProcessStep(stepTypeId, context.AllSteps.Keys).ConfigureAwait(false);
            }
            catch(Exception e) when (e is not SystemException)
            {
                resultStepStatusId = ProcessStepStatusId.FAILED;
                scheduleStepTypeIds = null;
                skipStepTypeIds = null;
            }
            modified |= SetProcessStepStatus(stepTypeId, resultStepStatusId, context);
            modified |= SkipProcessStepTypeIds(skipStepTypeIds, context);
            modified |= ScheduleProcessStepTypeIds(scheduleStepTypeIds, context);
            if (modified)
            {
                yield return true;
                modified = false;
            }
        }
    }

    private bool ScheduleProcessStepTypeIds(IEnumerable<ProcessStepTypeId>? scheduleStepTypeIds, ProcessContext context)
    {
        if (scheduleStepTypeIds == null)
        {
            return false;
        }

        var newStepTypeIds = scheduleStepTypeIds.Except(context.AllSteps.Keys).ToList();
        if (!newStepTypeIds.Any())
        {
            return false;
        }
        foreach (var newStep in _processStepRepository.CreateProcessStepRange(context.ProcessId, newStepTypeIds))
        {
            context.AllSteps.Add(newStep.ProcessStepTypeId, new [] { newStep.Id });
            if (context.Executor.IsExecutableStepTypeId(newStep.ProcessStepTypeId))
            {
                context.ExecutableStepTypeIds.Enqueue(newStep.ProcessStepTypeId);
            }
        }
        return true;
    }

    private bool SkipProcessStepTypeIds(IEnumerable<ProcessStepTypeId>? skipStepTypeIds, ProcessContext context)
    {
        if (skipStepTypeIds == null)
        {
            return false;
        }
        var modified = false;
        foreach (var stepTypeId in skipStepTypeIds)
        {
            modified |= SetProcessStepStatus(stepTypeId, ProcessStepStatusId.SKIPPED, context);
        }
        return modified;
    }

    private bool SetProcessStepStatus(ProcessStepTypeId stepTypeId, ProcessStepStatusId stepStatusId, ProcessContext context)
    {
        if (stepStatusId == ProcessStepStatusId.TODO || !context.AllSteps.TryGetValue(stepTypeId, out var stepIds))
        {
            return false;
        }
        bool isFirst = true;
        foreach (var stepId in stepIds)
        {
            _processStepRepository.AttachAndModifyProcessStep(stepId, null, step => { step.ProcessStepStatusId = isFirst ? stepStatusId : ProcessStepStatusId.DUPLICATE; });
        }
        context.AllSteps.Remove(stepTypeId);
        return true;
    }

    private record ProcessContext(
        Guid ProcessId,
        IDictionary<ProcessStepTypeId,IEnumerable<Guid>> AllSteps,
        Queue<ProcessStepTypeId> ExecutableStepTypeIds,
        IProcessTypeExecutor Executor
    );
}
