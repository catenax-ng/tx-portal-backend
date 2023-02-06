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

using Microsoft.Extensions.Logging;
using Org.Eclipse.TractusX.Portal.Backend.ApplicationActivation.Library;
using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Checklist.Library;
using Org.Eclipse.TractusX.Portal.Backend.Clearinghouse.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Custodian.Library.BusinessLogic;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.SdFactory.Library.BusinessLogic;
using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;

namespace Org.Eclipse.TractusX.Portal.Backend.Checklist.Worker;

/// <inheritdoc />
public class ChecklistProcessor : IChecklistProcessor
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IBpdmBusinessLogic _bpdmBusinessLogic;
    private readonly ICustodianBusinessLogic _custodianBusinessLogic;
    private readonly IClearinghouseBusinessLogic _clearinghouseBusinessLogic;
    private readonly ISdFactoryBusinessLogic _sdFactoryBusinessLogic;
    private readonly IApplicationActivationService _applicationActivationService;
    private readonly ILogger<IChecklistProcessor> _logger;
    private readonly ImmutableDictionary<ProcessStepTypeId, StepExecution> _stepExecutions;

    /// <inheritdoc />
    public ChecklistProcessor(
        IPortalRepositories portalRepositories,
        IBpdmBusinessLogic bpdmBusinessLogic,
        ICustodianBusinessLogic custodianBusinessLogic,
        IClearinghouseBusinessLogic clearinghouseBusinessLogic,
        ISdFactoryBusinessLogic sdFactoryBusinessLogic,
        IApplicationActivationService applicationActivationService,
        ILogger<IChecklistProcessor> logger)
    {
        _portalRepositories = portalRepositories;
        _bpdmBusinessLogic = bpdmBusinessLogic;
        _custodianBusinessLogic = custodianBusinessLogic;
        _clearinghouseBusinessLogic = clearinghouseBusinessLogic;
        _sdFactoryBusinessLogic = sdFactoryBusinessLogic;
        _applicationActivationService = applicationActivationService;
        _logger = logger;

        _stepExecutions = new (ProcessStepTypeId ProcessStepTypeId, StepExecution StepExecution)[]
        {
            (ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PUSH, new (ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, (context, cancellationToken) => _bpdmBusinessLogic.PushLegalEntity(context, cancellationToken), null)),
            (ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_PULL, new (ApplicationChecklistEntryTypeId.BUSINESS_PARTNER_NUMBER, (context, cancellationToken) => _bpdmBusinessLogic.HandlePullLegalEntity(context, cancellationToken), null)),
            (ProcessStepTypeId.CREATE_IDENTITY_WALLET, new (ApplicationChecklistEntryTypeId.IDENTITY_WALLET, (context, cancellationToken) => _custodianBusinessLogic.CreateIdentityWalletAsync(context, cancellationToken), null)),
            (ProcessStepTypeId.START_CLEARING_HOUSE, new (ApplicationChecklistEntryTypeId.CLEARING_HOUSE, (context, cancellationToken) => _clearinghouseBusinessLogic.HandleStartClearingHouse(context, cancellationToken), null)),
            (ProcessStepTypeId.CREATE_SELF_DESCRIPTION_LP, new (ApplicationChecklistEntryTypeId.SELF_DESCRIPTION_LP, (context, cancellationToken) => _sdFactoryBusinessLogic.RegisterSelfDescription(context, cancellationToken), null)),
            (ProcessStepTypeId.ACTIVATE_APPLICATION, new (ApplicationChecklistEntryTypeId.APPLICATION_ACTIVATION, (context, cancellationToken) => _applicationActivationService.HandleApplicationActivation(context, cancellationToken), null)),
        }.ToImmutableDictionary(x => x.ProcessStepTypeId, x => x.StepExecution);
    }

    private static readonly IEnumerable<ProcessStepTypeId> _manuelProcessSteps = new [] {
        ProcessStepTypeId.CREATE_BUSINESS_PARTNER_NUMBER_MANUAL,
        ProcessStepTypeId.END_CLEARING_HOUSE,
        ProcessStepTypeId.VERIFY_REGISTRATION,
    };

    private sealed record ProcessingContext(
        Guid ApplicationId,
        IDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> Checklist,
        IDictionary<ProcessStepTypeId, IEnumerable<ProcessStep>> AllSteps,
        Queue<ProcessStepTypeId> WorkerStepTypeIds,
        IList<ProcessStepTypeId> ManualStepTypeIds,
        IApplicationChecklistRepository ChecklistRepository,
        IProcessStepRepository ProcessStepRepository);

    private sealed record StepExecution(
        ApplicationChecklistEntryTypeId EntryTypeId,
        Func<IChecklistService.WorkerChecklistProcessStepData,CancellationToken,Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStepTypeId>?,bool)>> ProcessFunc,
        Func<Exception,IChecklistService.WorkerChecklistProcessStepData,CancellationToken,Task<(Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStepTypeId>?,bool)>>? ErrorFunc
    );

    /// <inheritdoc />
    public async IAsyncEnumerable<(ApplicationChecklistEntryTypeId TypeId, ApplicationChecklistEntryStatusId StatusId)> ProcessChecklist(Guid applicationId, IEnumerable<(ApplicationChecklistEntryTypeId EntryTypeId, ApplicationChecklistEntryStatusId EntryStatusId)> checklistEntries, IEnumerable<ProcessStep> processSteps, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var allSteps = processSteps.GroupBy(step => step.ProcessStepTypeId).ToDictionary(group => group.Key, group => group.AsEnumerable());

        var context = new ProcessingContext(
            applicationId,
            checklistEntries.ToDictionary(entry => entry.EntryTypeId, entry => entry.EntryStatusId),
            allSteps,
            new Queue<ProcessStepTypeId>(allSteps.Select(step => step.Key).Except(_manuelProcessSteps)),
            allSteps.Select(step => step.Key).Intersect(_manuelProcessSteps).ToList(),
            _portalRepositories.GetInstance<IApplicationChecklistRepository>(),
            _portalRepositories.GetInstance<IProcessStepRepository>());

        _logger.LogInformation("Found {StepsCount} possible steps for application {ApplicationId}", context.WorkerStepTypeIds.Count, applicationId);

        while (context.WorkerStepTypeIds.TryDequeue(out var stepTypeId))
        {
            var (execution, entryStatusId) = GetExecution(stepTypeId, context.Checklist);
            var modified = false;
            var stepData = new IChecklistService.WorkerChecklistProcessStepData(
                applicationId,
                context.Checklist.ToImmutableDictionary(),
                context.WorkerStepTypeIds.Concat(context.ManualStepTypeIds));

            (Action<ApplicationChecklistEntry>?,IEnumerable<ProcessStepTypeId>?,bool) result;
            ProcessStepStatusId stepStatusId;
            try
            {
                result = await execution.ProcessFunc(
                    stepData,
                    cancellationToken).ConfigureAwait(false);
                stepStatusId = ProcessStepStatusId.DONE;
            }
            catch (Exception ex) when (ex is not SystemException)
            {
                if (execution.ErrorFunc == null)
                {
                    (stepStatusId, var modifyEntry) = ProcessError(ex);
                    result = (modifyEntry, null, true);
                }
                else
                {
                    result = await execution.ErrorFunc.Invoke(ex,stepData,cancellationToken).ConfigureAwait(false);
                    stepStatusId = ProcessStepStatusId.FAILED;
                }
            }
            (entryStatusId, modified) = ProcessResult(
                result,
                stepTypeId,
                stepStatusId,
                execution.EntryTypeId,
                entryStatusId,
                context);
            if (modified)
            {
                context.Checklist[execution.EntryTypeId] = entryStatusId;
                yield return (execution.EntryTypeId, entryStatusId);
            }
        }
    }

    private (StepExecution, ApplicationChecklistEntryStatusId EntryStatusId) GetExecution(
        ProcessStepTypeId stepTypeId,
        IDictionary<ApplicationChecklistEntryTypeId,ApplicationChecklistEntryStatusId> checklist)
    {
        if (!_stepExecutions.TryGetValue(stepTypeId, out var execution))
        {
            throw new ConflictException($"no execution defined for processStep {stepTypeId}");
        }
        if (!checklist.TryGetValue(execution.EntryTypeId, out var entryStatusId))
        {
            throw new ConflictException($"no checklist entry {execution.EntryTypeId} for {stepTypeId}");
        }
        return (execution, entryStatusId);
    }

    private static (ApplicationChecklistEntryStatusId EntryStatusId, bool Modified) ProcessResult(
        (Action<ApplicationChecklistEntry>? ModifyApplicationChecklistEntry,IEnumerable<ProcessStepTypeId>? NextSteps, bool Modified) executionResult,
        ProcessStepTypeId stepTypeId,
        ProcessStepStatusId stepStatusId,
        ApplicationChecklistEntryTypeId entryTypeId,
        ApplicationChecklistEntryStatusId entryStatusId,
        ProcessingContext context)
    {
        var modified = false;
        if (executionResult.Modified)
        {
            modified |= ModifyStep(stepTypeId, stepStatusId, context);
            modified |= ScheduleNextSteps(executionResult.NextSteps, context);
            if (executionResult.ModifyApplicationChecklistEntry != null)
            {
                var entry = context.ChecklistRepository
                    .AttachAndModifyApplicationChecklist(context.ApplicationId, entryTypeId,
                        executionResult.ModifyApplicationChecklistEntry);
                return (entry.ApplicationChecklistEntryStatusId, true);
            }
        }
        return (entryStatusId, modified);
    }

    private static (ProcessStepStatusId,Action<ApplicationChecklistEntry>?) ProcessError(Exception ex) =>
        (ex is not ServiceException { StatusCode: HttpStatusCode.ServiceUnavailable })
            ? ( ProcessStepStatusId.FAILED,
                item => {
                    item.ApplicationChecklistEntryStatusId = ApplicationChecklistEntryStatusId.FAILED;
                    item.Comment = ex.ToString();
                })
            : ( ProcessStepStatusId.TODO,
                item => {
                    item.Comment = ex.ToString();
                });

    private static bool ScheduleNextSteps(IEnumerable<ProcessStepTypeId>? nextSteps, ProcessingContext context)
    {
        bool modified = false;
        if (nextSteps != null)
        {
            foreach (var nextStepTypeId in nextSteps.Except(context.AllSteps.Keys))
            {
                context.AllSteps.Add(nextStepTypeId, new[] { context.ProcessStepRepository.CreateProcessStep(nextStepTypeId, ProcessStepStatusId.TODO) });
                if (_manuelProcessSteps.Contains(nextStepTypeId))
                {
                    context.ManualStepTypeIds.Add(nextStepTypeId);
                }
                else
                {
                    context.WorkerStepTypeIds.Enqueue(nextStepTypeId);
                }
                modified = true;
            }
        }
        return modified;
    }
    
    private static bool ModifyStep(ProcessStepTypeId stepTypeId, ProcessStepStatusId statusId, ProcessingContext context)
    {
        if (context.AllSteps.Remove(stepTypeId, out var currentSteps))
        {
            var firstModified = false;
            foreach (var processStep in currentSteps)
            {
                context.ProcessStepRepository.AttachAndModifyProcessStep(
                    processStep.Id,
                    null,
                    step => step.ProcessStepStatusId =
                        firstModified
                            ? ProcessStepStatusId.DUPLICATE
                            : statusId);
                firstModified = true;
            }
            return firstModified;
        }
        return false;
    }
}
